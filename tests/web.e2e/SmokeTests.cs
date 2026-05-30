using System.Text.RegularExpressions;
using Microsoft.Playwright.NUnit;
using NUnit.Framework;

namespace web.e2e;

/// <summary>
/// Hollywood-style smoke tests: just call and see if it answers.
/// These run against the live deployed app — no local server required.
/// Set E2E_BASE_URL in CI secrets (or locally) to override the default.
/// </summary>
[Parallelizable(ParallelScope.Self)]
[TestFixture]
public class SmokeTests : PageTest
{
    private static string BaseUrl =>
        Environment.GetEnvironmentVariable("E2E_BASE_URL") is { Length: > 0 } url
            ? url
            : "https://longevity.eastus2.cloudapp.azure.com";

    [Test]
    public async Task AppIsOnlineAndRendersShell()
    {
        var response = await Page.GotoAsync(BaseUrl);

        Assert.That(response?.Status, Is.EqualTo(200), "Expected HTTP 200 from the app");

        await Expect(Page).ToHaveTitleAsync(new Regex("longevity", RegexOptions.IgnoreCase));

        // React SPA fetches /auth/me on mount; if no session, Layout renders the sign-in link.
        // Waiting for this implicitly waits for the JS bundle to load and the auth check to complete.
        await Expect(Page.GetByText("Sign in with Google")).ToBeVisibleAsync();
    }
}
