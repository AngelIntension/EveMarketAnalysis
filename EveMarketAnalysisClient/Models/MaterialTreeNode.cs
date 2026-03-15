using System.Collections.Immutable;

namespace EveMarketAnalysisClient.Models;

public record MaterialTreeNode(
    int TypeId,
    string TypeName,
    int BaseQuantity,
    int AdjustedQuantity,
    int Runs,
    long TotalQuantity,
    bool IsExpanded,
    int SourceBlueprintTypeId,
    ImmutableArray<MaterialTreeNode> Children);
