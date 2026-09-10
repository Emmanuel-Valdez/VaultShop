using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;

namespace VaultShop.Web.Services.System;

public sealed record DeploymentVersion(string Commit, string BuildDate, bool IsUnknown);

public sealed record GitHubVersionResult(string? LatestCommit, string? Error);

public sealed class DeploymentVersionService
{
    private const string CacheKey = "github:main:sha";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IMemoryCache _cache;
    private readonly ILogger<DeploymentVersionService> _logger;

    public DeploymentVersionService(
        IConfiguration config,
        IHttpClientFactory httpFactory,
        IMemoryCache cache,
        ILogger<DeploymentVersionService> logger)
    {
        _config = config;
        _httpFactory = httpFactory;
        _cache = cache;
        _logger = logger;
    }

    public DeploymentVersion GetDeployed()
    {
        var commit = _config["APP_VERSION"]
            ?? Environment.GetEnvironmentVariable("APP_VERSION")
            ?? "unknown";
        var buildDate = _config["APP_BUILD_DATE"]
            ?? Environment.GetEnvironmentVariable("APP_BUILD_DATE")
            ?? "unknown";
        commit = commit.Trim();
        buildDate = buildDate.Trim();
        var isUnknown = string.IsNullOrWhiteSpace(commit) || commit == "unknown";
        if (isUnknown) commit = "unknown";
        if (string.IsNullOrWhiteSpace(buildDate)) buildDate = "unknown";
        return new DeploymentVersion(commit, buildDate, isUnknown);
    }

    public static bool IsUpToDate(string deployedCommit, string? latestCommit)
    {
        if (string.IsNullOrWhiteSpace(deployedCommit) || deployedCommit == "unknown") return false;
        if (string.IsNullOrWhiteSpace(latestCommit)) return false;
        // ponytail: prefix compare so short SHA matches full SHA from API
        latestCommit = latestCommit.Trim();
        deployedCommit = deployedCommit.Trim();
        if (latestCommit.Length >= deployedCommit.Length)
            return latestCommit.StartsWith(deployedCommit, StringComparison.OrdinalIgnoreCase);
        return deployedCommit.StartsWith(latestCommit, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<GitHubVersionResult> GetLatestAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue<string>(CacheKey, out var cached) && cached is not null)
            return new GitHubVersionResult(cached, null);

        try
        {
            var client = _httpFactory.CreateClient("GitHub");
            using var req = new HttpRequestMessage(HttpMethod.Get, "repos/Emmanuel-Valdez/VaultShop/git/ref/heads/main");
            req.Headers.UserAgent.ParseAdd("VaultShop-VersionCheck/1.0");
            using var res = await client.SendAsync(req, ct);
            if (!res.IsSuccessStatusCode)
            {
                _logger.LogWarning("GitHub version check failed: {Status}", (int)res.StatusCode);
                return new GitHubVersionResult(null, $"GitHub API {(int)res.StatusCode}");
            }

            var json = await res.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("object", out var obj) ||
                !obj.TryGetProperty("sha", out var shaEl))
                return new GitHubVersionResult(null, "Respuesta inesperada de GitHub");

            var sha = shaEl.GetString();
            if (string.IsNullOrWhiteSpace(sha))
                return new GitHubVersionResult(null, "Respuesta vacía de GitHub");

            _cache.Set(CacheKey, sha, CacheTtl);
            return new GitHubVersionResult(sha, null);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "GitHub version check error");
            return new GitHubVersionResult(null, ex.Message);
        }
    }
}
