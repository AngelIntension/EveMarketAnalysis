using System.Collections.Immutable;

namespace EveMarketAnalysisClient.Models;

public record TradeHubRegion(
    int RegionId,
    string RegionName,
    string HubName,
    bool IsDefault)
{
    public static ImmutableArray<TradeHubRegion> All { get; } = ImmutableArray.Create(
        new TradeHubRegion(10000002, "The Forge", "Jita", true),
        new TradeHubRegion(10000043, "Domain", "Amarr", false),
        new TradeHubRegion(10000032, "Sinq Laison", "Dodixie", false),
        new TradeHubRegion(10000042, "Metropolis", "Hek", false),
        new TradeHubRegion(10000030, "Heimatar", "Rens", false));

    public static TradeHubRegion Default => All[0];
}
