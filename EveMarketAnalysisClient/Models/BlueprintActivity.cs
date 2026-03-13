using System.Collections.Immutable;

namespace EveMarketAnalysisClient.Models;

public record BlueprintActivity(
    int BlueprintTypeId,
    int ProducedTypeId,
    int ProducedQuantity,
    int BaseTime,
    ImmutableArray<MaterialRequirement> Materials);
