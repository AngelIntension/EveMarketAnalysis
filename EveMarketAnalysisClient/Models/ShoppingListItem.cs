using System.Collections.Immutable;

namespace EveMarketAnalysisClient.Models;

public record ShoppingListItem(
    int TypeId,
    string TypeName,
    string Category,
    long TotalQuantity,
    double Volume,
    double TotalVolume,
    decimal? EstimatedUnitCost,
    decimal? EstimatedTotalCost,
    ImmutableArray<MaterialSource> Sources);
