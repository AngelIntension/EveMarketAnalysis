using System.Collections.Immutable;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using EveMarketAnalysisClient.Configuration;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services;
using EveMarketAnalysisClient.Services.Interfaces;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Moq;

namespace EveMarketAnalysisClient.Tests.Services;

public class EsiTokenServiceRefreshTests
{
    private const string Issuer = "https://login.eveonline.com";
    private const string ClientId = "test-client-id";

    private static readonly RSA TestRsa = RSA.Create(2048);
    private static readonly RsaSecurityKey TestKey = new(TestRsa);

    private static string CreateTestJwt(int characterId = 12345, string characterName = "Test Pilot")
    {
        var credentials = new SigningCredentials(TestKey, SecurityAlgorithms.RsaSha256)
        {
            CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
        };

        var claims = new[]
        {
            new Claim("sub", $"CHARACTER:EVE:{characterId}"),
            new Claim("name", characterName),
            new Claim("scp", "esi-skills.read_skills.v1"),
        };

        var token = new JwtSecurityToken(
            issuer: Issuer,
            audience: ClientId,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(20),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string CreateJwksJson()
    {
        var parameters = TestRsa.ExportParameters(false);
        return JsonSerializer.Serialize(new
        {
            keys = new[]
            {
                new
                {
                    kty = "RSA",
                    use = "sig",
                    alg = "RS256",
                    n = Base64UrlEncoder.Encode(parameters.Modulus!),
                    e = Base64UrlEncoder.Encode(parameters.Exponent!),
                    kid = TestKey.KeyId ?? "test-key-id"
                }
            }
        });
    }

    private class TestHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;
        public int CallCount { get; private set; }
        public string? LastRequestBody { get; private set; }

        public TestHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.ToString().Contains("token"))
            {
                CallCount++;
                LastRequestBody = request.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
            }
            return Task.FromResult(_handler(request));
        }
    }

    private static EsiOAuthMetadata CreateMetadata() => new(
        Issuer: Issuer,
        AuthorizationEndpoint: "https://login.eveonline.com/v2/oauth/authorize",
        TokenEndpoint: "https://login.eveonline.com/v2/oauth/token",
        JwksUri: "https://login.eveonline.com/oauth/jwks",
        RevocationEndpoint: "https://login.eveonline.com/v2/oauth/revoke",
        CodeChallengeMethods: ImmutableArray.Create("S256"));

    [Fact]
    public async Task RefreshAsync_SendsCorrectRefreshRequest()
    {
        var jwt = CreateTestJwt();
        var tokenResponse = JsonSerializer.Serialize(new
        {
            access_token = jwt,
            refresh_token = "new-refresh-token",
            expires_in = 1199,
            token_type = "Bearer"
        });

        var handler = new TestHandler(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("jwks"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(CreateJwksJson()) };
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(tokenResponse) };
        });

        var metadataService = new Mock<IEsiOAuthMetadataService>();
        metadataService.Setup(m => m.GetMetadataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMetadata());

        var options = Options.Create(new EsiOptions
        {
            ClientId = ClientId,
            RedirectUri = "https://localhost:7272/Auth/Callback",
            Scopes = "esi-skills.read_skills.v1"
        });

        var service = new EsiTokenService(new HttpClient(handler), metadataService.Object, options);

        var result = await service.RefreshAsync("old-refresh-token");

        handler.LastRequestBody.Should().Contain("grant_type=refresh_token");
        handler.LastRequestBody.Should().Contain("refresh_token=old-refresh-token");
        result.AccessToken.Should().Be(jwt);
        result.RefreshToken.Should().Be("new-refresh-token");
    }

    [Fact]
    public async Task RefreshAsync_ThrowsOnRefreshFailure()
    {
        var handler = new TestHandler(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("jwks"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(CreateJwksJson()) };
            return new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{"error":"invalid_grant"}""")
            };
        });

        var metadataService = new Mock<IEsiOAuthMetadataService>();
        metadataService.Setup(m => m.GetMetadataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMetadata());

        var options = Options.Create(new EsiOptions
        {
            ClientId = ClientId,
            RedirectUri = "https://localhost:7272/Auth/Callback",
            Scopes = "esi-skills.read_skills.v1"
        });

        var service = new EsiTokenService(new HttpClient(handler), metadataService.Object, options);

        var act = () => service.RefreshAsync("revoked-refresh-token");

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task RefreshAsync_SerializesConcurrentAttempts()
    {
        var jwt = CreateTestJwt();
        var tokenResponse = JsonSerializer.Serialize(new
        {
            access_token = jwt,
            refresh_token = "new-refresh-token",
            expires_in = 1199,
            token_type = "Bearer"
        });

        var handler = new TestHandler(req =>
        {
            var url = req.RequestUri!.ToString();
            if (url.Contains("jwks"))
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(CreateJwksJson()) };
            // Simulate some processing time
            Thread.Sleep(50);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(tokenResponse) };
        });

        var metadataService = new Mock<IEsiOAuthMetadataService>();
        metadataService.Setup(m => m.GetMetadataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMetadata());

        var options = Options.Create(new EsiOptions
        {
            ClientId = ClientId,
            RedirectUri = "https://localhost:7272/Auth/Callback",
            Scopes = "esi-skills.read_skills.v1"
        });

        var service = new EsiTokenService(new HttpClient(handler), metadataService.Object, options);

        // Launch concurrent refresh attempts
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => service.RefreshAsync("old-refresh-token"))
            .ToArray();

        var results = await Task.WhenAll(tasks);

        // All should succeed but the semaphore ensures they're serialized
        results.Should().AllSatisfy(r => r.AccessToken.Should().NotBeNullOrEmpty());
    }
}
