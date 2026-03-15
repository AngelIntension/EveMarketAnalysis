using System.Collections.Immutable;

namespace EveMarketAnalysisClient.Models;

public record TradeHubRegion(
    int RegionId,
    string RegionName,
    string HubName,
    long StationId,
    bool IsDefault)
{
    public static ImmutableArray<TradeHubRegion> All { get; } = ImmutableArray.Create(
        new TradeHubRegion(10000002, "The Forge", "Jita", 60003760, true),
        new TradeHubRegion(10000043, "Domain", "Amarr", 60008494, false),
        new TradeHubRegion(10000032, "Sinq Laison", "Dodixie", 60011866, false),
        new TradeHubRegion(10000042, "Metropolis", "Hek", 60005686, false),
        new TradeHubRegion(10000030, "Heimatar", "Rens", 60004588, false));

    public static TradeHubRegion Default => All[0];
}
