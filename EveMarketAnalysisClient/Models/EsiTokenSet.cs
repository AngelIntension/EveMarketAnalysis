using System.Collections.Immutable;

namespace EveMarketAnalysisClient.Models;

public record EsiTokenSet(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    int CharacterId,
    string CharacterName,
    ImmutableArray<string> Scopes)
{
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
}
