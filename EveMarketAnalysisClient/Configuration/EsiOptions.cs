namespace EveMarketAnalysisClient.Configuration;

public record EsiOptions
{
    public required string ClientId { get; init; }
    public required string RedirectUri { get; init; }
    public required string Scopes { get; init; }
}
