using System.Collections.Immutable;
using System.Security.Claims;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services;
using EveMarketAnalysisClient.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Kiota.Abstractions;
using Moq;

namespace EveMarketAnalysisClient.Tests.Services;

public class EsiAuthenticationProviderTests
{
    private static DefaultHttpContext CreateAuthenticatedHttpContext(
        string accessToken = "test-access-token",
        DateTimeOffset? expiresAt = null)
    {
        var httpContext = new DefaultHttpContext();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "12345"),
            new(ClaimTypes.Name, "Test Pilot"),
            new("access_token", accessToken),
            new("refresh_token", "test-refresh-token"),
            new("expires_at", (expiresAt ?? DateTimeOffset.UtcNow.AddMinutes(20)).ToString("O"))
        };

        var identity = new ClaimsIdentity(claims, "TestAuth");
        httpContext.User = new ClaimsPrincipal(identity);

        return httpContext;
    }

    [Fact]
    public async Task AuthenticateRequestAsync_InjectsBearerHeaderWithValidToken()
    {
        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(a => a.HttpContext)
            .Returns(CreateAuthenticatedHttpContext("valid-token"));

        var tokenService = new Mock<IEsiTokenService>();
        var provider = new EsiAuthenticationProvider(httpContextAccessor.Object, tokenService.Object);

        var requestInfo = new RequestInformation
        {
            HttpMethod = Method.GET,
            URI = new Uri("https://esi.evetech.net/latest/characters/12345/skills/")
        };

        await provider.AuthenticateRequestAsync(requestInfo);

        requestInfo.Headers.Should().ContainKey("Authorization");
        requestInfo.Headers["Authorization"].Should().Contain("Bearer valid-token");
    }

    [Fact]
    public async Task AuthenticateRequestAsync_TriggersRefreshWhenTokenExpired()
    {
        var httpContext = CreateAuthenticatedHttpContext(
            accessToken: "expired-token",
            expiresAt: DateTimeOffset.UtcNow.AddMinutes(-5));

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(a => a.HttpContext).Returns(httpContext);

        var newTokenSet = new EsiTokenSet(
            "new-access-token", "new-refresh-token",
            DateTimeOffset.UtcNow.AddMinutes(20), 12345, "Test Pilot",
            ImmutableArray.Create("esi-skills.read_skills.v1"));

        var tokenService = new Mock<IEsiTokenService>();
        tokenService.Setup(t => t.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(newTokenSet);

        var provider = new EsiAuthenticationProvider(httpContextAccessor.Object, tokenService.Object);

        var requestInfo = new RequestInformation
        {
            HttpMethod = Method.GET,
            URI = new Uri("https://esi.evetech.net/latest/characters/12345/skills/")
        };

        await provider.AuthenticateRequestAsync(requestInfo);

        tokenService.Verify(t => t.RefreshAsync("test-refresh-token", It.IsAny<CancellationToken>()), Times.Once);
        requestInfo.Headers["Authorization"].Should().Contain("Bearer new-access-token");
    }

    [Fact]
    public async Task AuthenticateRequestAsync_DoesNotInjectHeaderWhenNoTokenPresent()
    {
        var httpContext = new DefaultHttpContext(); // unauthenticated

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(a => a.HttpContext).Returns(httpContext);

        var tokenService = new Mock<IEsiTokenService>();
        var provider = new EsiAuthenticationProvider(httpContextAccessor.Object, tokenService.Object);

        var requestInfo = new RequestInformation
        {
            HttpMethod = Method.GET,
            URI = new Uri("https://esi.evetech.net/latest/markets/prices/")
        };

        await provider.AuthenticateRequestAsync(requestInfo);

        requestInfo.Headers.Should().NotContainKey("Authorization");
    }
}
