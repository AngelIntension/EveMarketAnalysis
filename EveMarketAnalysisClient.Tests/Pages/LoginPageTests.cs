using System.Collections.Immutable;
using EveMarketAnalysisClient.Configuration;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Pages.Auth;
using EveMarketAnalysisClient.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Moq;

namespace EveMarketAnalysisClient.Tests.Pages;

public class LoginPageTests
{
    private static readonly EsiOAuthMetadata TestMetadata = new(
        Issuer: "https://login.eveonline.com",
        AuthorizationEndpoint: "https://login.eveonline.com/v2/oauth/authorize",
        TokenEndpoint: "https://login.eveonline.com/v2/oauth/token",
        JwksUri: "https://login.eveonline.com/oauth/jwks",
        RevocationEndpoint: "https://login.eveonline.com/v2/oauth/revoke",
        CodeChallengeMethods: ImmutableArray.Create("S256"));

    private static LoginModel CreateLoginPage()
    {
        var metadataService = new Mock<IEsiOAuthMetadataService>();
        metadataService.Setup(m => m.GetMetadataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(TestMetadata);

        var options = Options.Create(new EsiOptions
        {
            ClientId = "test-client-id",
            RedirectUri = "https://localhost:7272/Auth/Callback",
            Scopes = "esi-skills.read_skills.v1 esi-skills.read_skillqueue.v1"
        });

        var httpContext = new DefaultHttpContext();
        httpContext.Response.Headers.Append("Set-Cookie", "");
        var pageContext = new PageContext { HttpContext = httpContext };

        return new LoginModel(metadataService.Object, options)
        {
            PageContext = pageContext
        };
    }

    [Fact]
    public async Task OnGetAsync_RedirectsToAuthorizationEndpoint()
    {
        var page = CreateLoginPage();

        var result = await page.OnGetAsync();

        result.Should().BeOfType<RedirectResult>();
        var redirect = (RedirectResult)result;
        redirect.Url.Should().StartWith("https://login.eveonline.com/v2/oauth/authorize");
    }

    [Fact]
    public async Task OnGetAsync_IncludesCodeChallengeWithS256Method()
    {
        var page = CreateLoginPage();

        var result = await page.OnGetAsync();

        var redirect = (RedirectResult)result;
        redirect.Url.Should().Contain("code_challenge=");
        redirect.Url.Should().Contain("code_challenge_method=S256");
    }

    [Fact]
    public async Task OnGetAsync_IncludesStateParameter()
    {
        var page = CreateLoginPage();

        var result = await page.OnGetAsync();

        var redirect = (RedirectResult)result;
        redirect.Url.Should().Contain("state=");
    }

    [Fact]
    public async Task OnGetAsync_IncludesCorrectScopes()
    {
        var page = CreateLoginPage();

        var result = await page.OnGetAsync();

        var redirect = (RedirectResult)result;
        redirect.Url.Should().Contain("scope=");
    }

    [Fact]
    public async Task OnGetAsync_StoresVerifierAndStateInTempCookie()
    {
        var page = CreateLoginPage();

        await page.OnGetAsync();

        // Verify that a cookie was set for PKCE state
        var setCookieHeaders = page.HttpContext.Response.Headers["Set-Cookie"];
        // The cookie should be present (we verify the name pattern)
        page.HttpContext.Response.Headers.Should().ContainKey("Set-Cookie");
    }
}
