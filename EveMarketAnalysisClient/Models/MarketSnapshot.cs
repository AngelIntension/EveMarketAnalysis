namespace EveMarketAnalysisClient.Models;

public record MarketSnapshot(
    int TypeId,
    int RegionId,
    decimal? LowestSellPrice,
    decimal? HighestBuyPrice,
    double AverageDailyVolume,
    DateTimeOffset FetchedAt,
    decimal? NpcSellPrice = null);
