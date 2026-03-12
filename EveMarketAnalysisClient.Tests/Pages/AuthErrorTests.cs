using System.Collections.Immutable;
using EveMarketAnalysisClient.Pages.Auth;
using EveMarketAnalysisClient.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Moq;

namespace EveMarketAnalysisClient.Tests.Pages;

public class AuthErrorTests
{
    private static CallbackModel CreateCallbackPage(Mock<IEsiTokenService>? tokenService = null)
    {
        tokenService ??= new Mock<IEsiTokenService>();
        var httpContext = new DefaultHttpContext();
        var pageContext = new PageContext { HttpContext = httpContext };

        return new CallbackModel(tokenService.Object) { PageContext = pageContext };
    }

    [Fact]
    public async Task DeniedConsent_ShowsUserFriendlyError()
    {
        var page = CreateCallbackPage();

        var result = await page.OnGetAsync(error: "access_denied", state: "test");

        result.Should().BeOfType<PageResult>();
        page.ErrorMessage.Should().Contain("denied consent");
    }

    [Fact]
    public async Task InvalidState_ShowsSecurityError()
    {
        var page = CreateCallbackPage();
        page.HttpContext.Request.Headers.Append("Cookie", "pkce_state=expected-state");

        var result = await page.OnGetAsync(code: "auth-code", state: "wrong-state");

        result.Should().BeOfType<PageResult>();
        page.ErrorMessage.Should().Contain("state");
    }

    [Fact]
    public async Task ExchangeFailure_ShowsRetryMessage()
    {
        var tokenService = new Mock<IEsiTokenService>();
        tokenService.Setup(t => t.ExchangeCodeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Network error"));

        var page = CreateCallbackPage(tokenService);
        page.HttpContext.Request.Headers.Append("Cookie", "pkce_state=test-state; pkce_verifier=test-verifier");

        var result = await page.OnGetAsync(code: "auth-code", state: "test-state");

        result.Should().BeOfType<PageResult>();
        page.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task MissingCode_ShowsError()
    {
        var page = CreateCallbackPage();
        page.HttpContext.Request.Headers.Append("Cookie", "pkce_state=test-state");

        var result = await page.OnGetAsync(code: null, state: "test-state");

        result.Should().BeOfType<PageResult>();
        page.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}
