using System.Collections.Immutable;
using System.Security.Claims;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Pages.Auth;
using EveMarketAnalysisClient.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace EveMarketAnalysisClient.Tests.Pages;

public class CallbackPageTests
{
    private static EsiTokenSet CreateTestTokenSet() => new(
        AccessToken: "test-access-token",
        RefreshToken: "test-refresh-token",
        ExpiresAt: DateTimeOffset.UtcNow.AddMinutes(20),
        CharacterId: 12345,
        CharacterName: "Test Pilot",
        Scopes: ImmutableArray.Create("esi-skills.read_skills.v1"));

    private static CallbackModel CreateCallbackPage(
        Mock<IEsiTokenService>? tokenService = null,
        string? cookieState = "test-state",
        string? cookieVerifier = "test-verifier")
    {
        tokenService ??= new Mock<IEsiTokenService>();

        var httpContext = new DefaultHttpContext();

        // Set up auth service mock
        var authService = new Mock<IAuthenticationService>();
        authService.Setup(a => a.SignInAsync(
                It.IsAny<HttpContext>(),
                It.IsAny<string>(),
                It.IsAny<ClaimsPrincipal>(),
                It.IsAny<AuthenticationProperties>()))
            .Returns(Task.CompletedTask);

        var services = new ServiceCollection();
        services.AddSingleton(authService.Object);
        httpContext.RequestServices = services.BuildServiceProvider();

        // Simulate PKCE cookie
        if (cookieState != null && cookieVerifier != null)
        {
            httpContext.Request.Headers.Append("Cookie", $"pkce_state={cookieState}; pkce_verifier={cookieVerifier}");
        }

        var pageContext = new PageContext { HttpContext = httpContext };

        return new CallbackModel(tokenService.Object)
        {
            PageContext = pageContext
        };
    }

    [Fact]
    public async Task OnGetAsync_RejectsMismatchedState()
    {
        var page = CreateCallbackPage(cookieState: "expected-state");

        var result = await page.OnGetAsync("auth-code", "wrong-state");

        result.Should().BeOfType<PageResult>();
        page.ErrorMessage.Should().Contain("state");
    }

    [Fact]
    public async Task OnGetAsync_ExchangesCodeForTokens()
    {
        var tokenService = new Mock<IEsiTokenService>();
        tokenService.Setup(t => t.ExchangeCodeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestTokenSet());

        var page = CreateCallbackPage(tokenService: tokenService, cookieState: "test-state", cookieVerifier: "test-verifier");

        await page.OnGetAsync("auth-code", "test-state");

        tokenService.Verify(t => t.ExchangeCodeAsync("auth-code", "test-verifier", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task OnGetAsync_SignsInUserOnSuccess()
    {
        var tokenService = new Mock<IEsiTokenService>();
        tokenService.Setup(t => t.ExchangeCodeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateTestTokenSet());

        var page = CreateCallbackPage(tokenService: tokenService, cookieState: "test-state");

        var result = await page.OnGetAsync("auth-code", "test-state");

        result.Should().BeOfType<RedirectToPageResult>();
    }

    [Fact]
    public async Task OnGetAsync_DisplaysErrorOnDeniedConsent()
    {
        var page = CreateCallbackPage(cookieState: "test-state");

        var result = await page.OnGetAsync(code: null, state: "test-state", error: "access_denied");

        result.Should().BeOfType<PageResult>();
        page.ErrorMessage.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task OnGetAsync_DisplaysErrorOnExchangeFailure()
    {
        var tokenService = new Mock<IEsiTokenService>();
        tokenService.Setup(t => t.ExchangeCodeAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("Exchange failed"));

        var page = CreateCallbackPage(tokenService: tokenService, cookieState: "test-state");

        var result = await page.OnGetAsync("auth-code", "test-state");

        result.Should().BeOfType<PageResult>();
        page.ErrorMessage.Should().NotBeNullOrEmpty();
    }
}

