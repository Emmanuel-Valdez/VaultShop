using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using VaultShop.Utility;
using VaultShop.Web.Services.System;

namespace VaultShop.Web.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = SD.Role_Admin)]
public sealed class SystemController : Controller
{
    private readonly DeploymentVersionService _version;

    public SystemController(DeploymentVersionService version) => _version = version;

    public async Task<IActionResult> Version(CancellationToken ct)
    {
        var deployed = _version.GetDeployed();
        var latest = await _version.GetLatestAsync(ct);
        var upToDate = !deployed.IsUnknown && latest.LatestCommit is not null
            && DeploymentVersionService.IsUpToDate(deployed.Commit, latest.LatestCommit);
        ViewData["Deployed"] = deployed;
        ViewData["LatestCommit"] = latest.LatestCommit;
        ViewData["LatestError"] = latest.Error;
        ViewData["UpToDate"] = upToDate;
        return View();
    }
}
