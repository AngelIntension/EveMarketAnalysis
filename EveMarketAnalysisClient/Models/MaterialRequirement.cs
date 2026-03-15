namespace EveMarketAnalysisClient.Models;

public record MaterialRequirement(
    int TypeId,
    string TypeName,
    int BaseQuantity,
    int AdjustedQuantity,
    double Volume = 0.0);
