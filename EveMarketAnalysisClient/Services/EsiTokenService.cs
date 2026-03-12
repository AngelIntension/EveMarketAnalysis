using System.Collections.Immutable;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using EveMarketAnalysisClient.Configuration;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services.Interfaces;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace EveMarketAnalysisClient.Services;

public class EsiTokenService : IEsiTokenService
{
    private readonly HttpClient _httpClient;
    private readonly IEsiOAuthMetadataService _metadataService;
    private readonly EsiOptions _options;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public EsiTokenService(
        HttpClient httpClient,
        IEsiOAuthMetadataService metadataService,
        IOptions<EsiOptions> options)
    {
        _httpClient = httpClient;
        _metadataService = metadataService;
        _options = options.Value;
    }

    public async Task<EsiTokenSet> ExchangeCodeAsync(
        string code, string codeVerifier, CancellationToken cancellationToken = default)
    {
        var metadata = await _metadataService.GetMetadataAsync(cancellationToken);

        var requestBody = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["client_id"] = _options.ClientId,
            ["code_verifier"] = codeVerifier
        });

        var response = await _httpClient.PostAsync(metadata.TokenEndpoint, requestBody, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var tokenResponse = JsonDocument.Parse(json).RootElement;

        var accessToken = tokenResponse.GetProperty("access_token").GetString()!;
        var refreshToken = tokenResponse.GetProperty("refresh_token").GetString()!;
        var expiresIn = tokenResponse.GetProperty("expires_in").GetInt32();

        var validatedToken = await ValidateJwtAsync(accessToken, metadata, cancellationToken);

        return CreateTokenSet(accessToken, refreshToken, expiresIn, validatedToken);
    }

    public async Task<EsiTokenSet> RefreshAsync(
        string refreshToken, CancellationToken cancellationToken = default)
    {
        await _refreshLock.WaitAsync(cancellationToken);
        try
        {
            var metadata = await _metadataService.GetMetadataAsync(cancellationToken);

            var requestBody = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"] = _options.ClientId
            });

            var response = await _httpClient.PostAsync(metadata.TokenEndpoint, requestBody, cancellationToken);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var tokenResponse = JsonDocument.Parse(json).RootElement;

            var newAccessToken = tokenResponse.GetProperty("access_token").GetString()!;
            var newRefreshToken = tokenResponse.GetProperty("refresh_token").GetString()!;
            var expiresIn = tokenResponse.GetProperty("expires_in").GetInt32();

            var validatedToken = await ValidateJwtAsync(newAccessToken, metadata, cancellationToken);

            return CreateTokenSet(newAccessToken, newRefreshToken, expiresIn, validatedToken);
        }
        finally
        {
            _refreshLock.Release();
        }
    }

    private async Task<JwtSecurityToken> ValidateJwtAsync(
        string accessToken, EsiOAuthMetadata metadata, CancellationToken cancellationToken)
    {
        var jwksJson = await _httpClient.GetStringAsync(metadata.JwksUri, cancellationToken);
        var jwks = new JsonWebKeySet(jwksJson);

        var validationParameters = new TokenValidationParameters
        {
            ValidIssuer = metadata.Issuer,
            ValidAudience = _options.ClientId,
            IssuerSigningKeys = jwks.GetSigningKeys(),
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5)
        };

        var handler = new JwtSecurityTokenHandler();
        handler.ValidateToken(accessToken, validationParameters, out var validatedToken);

        return (JwtSecurityToken)validatedToken;
    }

    private static EsiTokenSet CreateTokenSet(
        string accessToken, string refreshToken, int expiresIn, JwtSecurityToken jwt)
    {
        var sub = jwt.Claims.First(c => c.Type == "sub").Value;
        var characterIdStr = sub.Split(':').Last();
        var characterId = int.Parse(characterIdStr);
        var characterName = jwt.Claims.First(c => c.Type == "name").Value;

        var scopes = jwt.Claims
            .Where(c => c.Type == "scp")
            .Select(c => c.Value)
            .ToImmutableArray();

        return new EsiTokenSet(
            AccessToken: accessToken,
            RefreshToken: refreshToken,
            ExpiresAt: DateTimeOffset.UtcNow.AddSeconds(expiresIn),
            CharacterId: characterId,
            CharacterName: characterName,
            Scopes: scopes);
    }
}
