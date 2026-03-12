using System.Net;
using System.Text.Json;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services;
using EveMarketAnalysisClient.Services.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using Moq.Protected;

namespace EveMarketAnalysisClient.Tests.Services;

public class EsiOAuthMetadataServiceTests
{
    private const string MetadataJson = """
        {
            "issuer": "https://login.eveonline.com",
            "authorization_endpoint": "https://login.eveonline.com/v2/oauth/authorize",
            "token_endpoint": "https://login.eveonline.com/v2/oauth/token",
            "jwks_uri": "https://login.eveonline.com/oauth/jwks",
            "revocation_endpoint": "https://login.eveonline.com/v2/oauth/revoke",
            "code_challenge_methods_supported": ["S256"]
        }
        """;

    private static HttpClient CreateMockHttpClient(HttpStatusCode statusCode, string content)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = statusCode,
                Content = new StringContent(content)
            });

        return new HttpClient(handler.Object);
    }

    [Fact]
    public async Task GetMetadataAsync_ParsesMetadataCorrectly()
    {
        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, MetadataJson);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new EsiOAuthMetadataService(httpClient, cache);

        var metadata = await service.GetMetadataAsync();

        metadata.Issuer.Should().Be("https://login.eveonline.com");
        metadata.AuthorizationEndpoint.Should().Be("https://login.eveonline.com/v2/oauth/authorize");
        metadata.TokenEndpoint.Should().Be("https://login.eveonline.com/v2/oauth/token");
        metadata.JwksUri.Should().Be("https://login.eveonline.com/oauth/jwks");
        metadata.RevocationEndpoint.Should().Be("https://login.eveonline.com/v2/oauth/revoke");
        metadata.CodeChallengeMethods.Should().Contain("S256");
    }

    [Fact]
    public async Task GetMetadataAsync_CachesAfterFirstFetch()
    {
        var callCount = 0;
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return new HttpResponseMessage
                {
                    StatusCode = HttpStatusCode.OK,
                    Content = new StringContent(MetadataJson)
                };
            });

        var httpClient = new HttpClient(handler.Object);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new EsiOAuthMetadataService(httpClient, cache);

        await service.GetMetadataAsync();
        await service.GetMetadataAsync();

        callCount.Should().Be(1);
    }

    [Fact]
    public async Task GetMetadataAsync_ThrowsOnUnreachableEndpoint()
    {
        var handler = new Mock<HttpMessageHandler>();
        handler.Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("Connection refused"));

        var httpClient = new HttpClient(handler.Object);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new EsiOAuthMetadataService(httpClient, cache);

        var act = () => service.GetMetadataAsync();

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetMetadataAsync_ValidatesS256InCodeChallengeMethods()
    {
        var metadata = """
            {
                "issuer": "https://login.eveonline.com",
                "authorization_endpoint": "https://login.eveonline.com/v2/oauth/authorize",
                "token_endpoint": "https://login.eveonline.com/v2/oauth/token",
                "jwks_uri": "https://login.eveonline.com/oauth/jwks",
                "revocation_endpoint": "https://login.eveonline.com/v2/oauth/revoke",
                "code_challenge_methods_supported": ["plain"]
            }
            """;

        var httpClient = CreateMockHttpClient(HttpStatusCode.OK, metadata);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var service = new EsiOAuthMetadataService(httpClient, cache);

        var act = () => service.GetMetadataAsync();

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*S256*");
    }
}
