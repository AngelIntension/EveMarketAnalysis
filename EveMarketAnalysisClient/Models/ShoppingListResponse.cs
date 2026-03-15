using System.Collections.Immutable;

namespace EveMarketAnalysisClient.Models;

public record ShoppingListResponse(
    ImmutableArray<ShoppingListItem> Items,
    decimal? TotalEstimatedCost,
    double TotalVolume,
    int BlueprintCount,
    DateTimeOffset GeneratedAt,
    ImmutableArray<string> Errors);
