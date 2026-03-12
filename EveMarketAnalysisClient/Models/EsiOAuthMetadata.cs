using System.Collections.Immutable;

namespace EveMarketAnalysisClient.Models;

public record EsiOAuthMetadata(
    string Issuer,
    string AuthorizationEndpoint,
    string TokenEndpoint,
    string JwksUri,
    string RevocationEndpoint,
    ImmutableArray<string> CodeChallengeMethods);
