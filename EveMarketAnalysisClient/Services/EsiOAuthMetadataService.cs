using System.Collections.Immutable;
using System.Text.Json;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace EveMarketAnalysisClient.Services;

public class EsiOAuthMetadataService : IEsiOAuthMetadataService
{
    private const string DiscoveryUrl = "https://login.eveonline.com/.well-known/oauth-authorization-server";
    private const string CacheKey = "esi:metadata";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromHours(24);

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;

    public EsiOAuthMetadataService(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
    }

    public async Task<EsiOAuthMetadata> GetMetadataAsync(CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(CacheKey, out EsiOAuthMetadata? cached) && cached is not null)
            return cached;

        var response = await _httpClient.GetAsync(DiscoveryUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var codeChallengeMethodsArray = root.GetProperty("code_challenge_methods_supported")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToImmutableArray();

        if (!codeChallengeMethodsArray.Contains("S256"))
            throw new InvalidOperationException(
                "EVE SSO does not support S256 code challenge method. Supported methods: " +
                string.Join(", ", codeChallengeMethodsArray));

        var metadata = new EsiOAuthMetadata(
            Issuer: root.GetProperty("issuer").GetString()!,
            AuthorizationEndpoint: root.GetProperty("authorization_endpoint").GetString()!,
            TokenEndpoint: root.GetProperty("token_endpoint").GetString()!,
            JwksUri: root.GetProperty("jwks_uri").GetString()!,
            RevocationEndpoint: root.GetProperty("revocation_endpoint").GetString()!,
            CodeChallengeMethods: codeChallengeMethodsArray);

        _cache.Set(CacheKey, metadata, CacheDuration);

        return metadata;
    }
}
