using System.Net;
using System.Text.RegularExpressions;

namespace VaultShop.Web.Tests
{
    internal static class TestAuthHelper
    {
        private static readonly Regex TokenRegex = new(
            "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"|value=\"([^\"]+)\"[^>]*name=\"__RequestVerificationToken\"",
            RegexOptions.Compiled);

        public static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string url)
        {
            var html = await (await client.GetAsync(url)).Content.ReadAsStringAsync();
            var match = TokenRegex.Match(html);
            if (!match.Success)
            {
                throw new InvalidOperationException($"No antiforgery token found in the response body for '{url}'.");
            }
            return match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
        }

        public static async Task LoginAsync(HttpClient client, string email, string password)
        {
            var token = await GetAntiforgeryTokenAsync(client, "/Identity/Account/Login");
            var form = new Dictionary<string, string>
            {
                ["Input.Email"] = email,
                ["Input.Password"] = password,
                ["__RequestVerificationToken"] = token,
            };
            var response = await client.PostAsync("/Identity/Account/Login", new FormUrlEncodedContent(form));
            if (response.StatusCode != HttpStatusCode.Redirect)
            {
                throw new InvalidOperationException(
                    $"Login as '{email}' failed, expected a 302 redirect but got {(int)response.StatusCode}.");
            }
        }
    }
}
