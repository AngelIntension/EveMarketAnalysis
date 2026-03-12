using System.Collections.Immutable;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
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

public class EsiTokenServiceTests
{
    private const string Issuer = "https://login.eveonline.com";
    private const string ClientId = "test-client-id";

    private static readonly RSA TestRsa = RSA.Create(2048);
    private static readonly RsaSecurityKey TestKey = new(TestRsa);

    private static EsiOAuthMetadata CreateMetadata() => new(
        Issuer: Issuer,
        AuthorizationEndpoint: "https://login.eveonline.com/v2/oauth/authorize",
        TokenEndpoint: "https://login.eveonline.com/v2/oauth/token",
        JwksUri: "https://login.eveonline.com/oauth/jwks",
        RevocationEndpoint: "https://login.eveonline.com/v2/oauth/revoke",
        CodeChallengeMethods: ImmutableArray.Create("S256"));

    private static string CreateTestJwt(
        string issuer = Issuer,
        string audience = ClientId,
        int characterId = 12345,
        string characterName = "Test Pilot",
        DateTime? expiry = null)
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
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiry ?? DateTime.UtcNow.AddMinutes(20),
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

    private static IOptions<EsiOptions> CreateOptions() => Options.Create(new EsiOptions
    {
        ClientId = ClientId,
        RedirectUri = "https://localhost:7272/Auth/Callback",
        Scopes = "esi-skills.read_skills.v1"
    });

    private static Mock<IEsiOAuthMetadataService> CreateMetadataServiceMock()
    {
        var mock = new Mock<IEsiOAuthMetadataService>();
        mock.Setup(m => m.GetMetadataAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateMetadata());
        return mock;
    }

    /// <summary>
    /// A test HTTP handler that captures requests and returns canned responses based on URL patterns.
    /// </summary>
    private class TestHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, Func<HttpRequestMessage, HttpResponseMessage>> _handlers = new();
        public HttpRequestMessage? LastTokenRequest { get; private set; }
        public string? LastTokenRequestBody { get; private set; }

        public void On(string urlContains, HttpStatusCode status, string content)
        {
            _handlers[urlContains] = _ => new HttpResponseMessage(status)
            {
                Content = new StringContent(content)
            };
        }

        public void OnToken(HttpStatusCode status, string content)
        {
            _handlers["token"] = req =>
            {
                LastTokenRequest = req;
                // Read body synchronously since we can't use async here
                LastTokenRequestBody = req.Content?.ReadAsStringAsync().GetAwaiter().GetResult();
                return new HttpResponseMessage(status) { Content = new StringContent(content) };
            };
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.ToString();
            foreach (var (key, handler) in _handlers)
            {
                if (url.Contains(key))
                    return Task.FromResult(handler(request));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    private static (EsiTokenService service, TestHandler handler) CreateService(string? jwt = null, int expiresIn = 1199)
    {
        jwt ??= CreateTestJwt();
        var tokenResponse = JsonSerializer.Serialize(new
        {
            access_token = jwt,
            refresh_token = "refresh-123",
            expires_in = expiresIn,
            token_type = "Bearer"
        });

        var handler = new TestHandler();
        handler.OnToken(HttpStatusCode.OK, tokenResponse);
        handler.On("jwks", HttpStatusCode.OK, CreateJwksJson());

        var httpClient = new HttpClient(handler);
        var service = new EsiTokenService(httpClient, CreateMetadataServiceMock().Object, CreateOptions());

        return (service, handler);
    }

    [Fact]
    public async Task ExchangeCodeAsync_BuildsCorrectTokenExchangeRequest()
    {
        var (service, handler) = CreateService();

        await service.ExchangeCodeAsync("auth-code", "verifier-123");

        handler.LastTokenRequestBody.Should().Contain("grant_type=authorization_code");
        handler.LastTokenRequestBody.Should().Contain("code=auth-code");
        handler.LastTokenRequestBody.Should().Contain("code_verifier=verifier-123");
        handler.LastTokenRequestBody.Should().Contain($"client_id={ClientId}");
    }

    [Fact]
    public async Task ExchangeCodeAsync_DoesNotIncludeClientSecret()
    {
        var (service, handler) = CreateService();

        await service.ExchangeCodeAsync("auth-code", "verifier-123");

        handler.LastTokenRequestBody.Should().NotContain("client_secret");
    }

    [Fact]
    public async Task ExchangeCodeAsync_ExtractsCharacterIdAndName()
    {
        var jwt = CreateTestJwt(characterId: 98765, characterName: "Space Pilot");
        var (service, _) = CreateService(jwt);

        var tokenSet = await service.ExchangeCodeAsync("auth-code", "verifier-123");

        tokenSet.CharacterId.Should().Be(98765);
        tokenSet.CharacterName.Should().Be("Space Pilot");
    }

    [Fact]
    public async Task ExchangeCodeAsync_RejectsExpiredJwt()
    {
        var jwt = CreateTestJwt(expiry: DateTime.UtcNow.AddMinutes(-10));
        var (service, _) = CreateService(jwt);

        var act = () => service.ExchangeCodeAsync("auth-code", "verifier-123");

        await act.Should().ThrowAsync<SecurityTokenException>();
    }

    [Fact]
    public async Task ExchangeCodeAsync_RejectsWrongIssuer()
    {
        var jwt = CreateTestJwt(issuer: "https://evil.example.com");
        var (service, _) = CreateService(jwt);

        var act = () => service.ExchangeCodeAsync("auth-code", "verifier-123");

        await act.Should().ThrowAsync<SecurityTokenException>();
    }

    [Fact]
    public async Task ExchangeCodeAsync_HandlesHttpErrorResponse()
    {
        var handler = new TestHandler();
        handler.OnToken(HttpStatusCode.BadRequest, """{"error":"invalid_grant"}""");
        handler.On("jwks", HttpStatusCode.OK, CreateJwksJson());

        var httpClient = new HttpClient(handler);
        var service = new EsiTokenService(httpClient, CreateMetadataServiceMock().Object, CreateOptions());

        var act = () => service.ExchangeCodeAsync("auth-code", "verifier-123");

        await act.Should().ThrowAsync<HttpRequestException>();
    }
}
