namespace EveMarketAnalysisClient.Models;

public record BpoPurchaseRecommendation(
    int BlueprintTypeId,
    string BlueprintName,
    string ProducedTypeName,
    int PhaseNumber,
    decimal? NpcSeededPrice,
    decimal? PlayerMarketPrice,
    decimal ProjectedIskPerHour,
    decimal? PaybackPeriodDays,
    decimal? RoiPercent,
    bool HasMarketData,
    string? ErrorMessage);
