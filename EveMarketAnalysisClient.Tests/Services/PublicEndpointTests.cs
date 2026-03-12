using EveMarketAnalysisClient.Services;
using EveMarketAnalysisClient.Services.Interfaces;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Kiota.Abstractions;
using Moq;

namespace EveMarketAnalysisClient.Tests.Services;

public class PublicEndpointTests
{
    [Fact]
    public async Task AnonymousClient_DoesNotInjectAuthHeader()
    {
        // When no user is authenticated, EsiAuthenticationProvider should not add headers
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

    [Fact]
    public async Task AnonymousClient_DoesNotCallTokenService()
    {
        var httpContext = new DefaultHttpContext();

        var httpContextAccessor = new Mock<IHttpContextAccessor>();
        httpContextAccessor.Setup(a => a.HttpContext).Returns(httpContext);

        var tokenService = new Mock<IEsiTokenService>();
        var provider = new EsiAuthenticationProvider(httpContextAccessor.Object, tokenService.Object);

        var requestInfo = new RequestInformation
        {
            HttpMethod = Method.GET,
            URI = new Uri("https://esi.evetech.net/latest/universe/types/")
        };

        await provider.AuthenticateRequestAsync(requestInfo);

        tokenService.Verify(t => t.RefreshAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
