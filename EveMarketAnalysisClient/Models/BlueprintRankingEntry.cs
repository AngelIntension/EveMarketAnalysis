namespace EveMarketAnalysisClient.Models;

public record BlueprintRankingEntry(
    CharacterBlueprint Blueprint,
    string ProducedTypeName,
    int ProducedTypeId,
    int? PhaseNumber,
    decimal MaterialCost,
    decimal ProductRevenue,
    decimal BuyingBrokerFee,
    decimal SellingBrokerFee,
    decimal SalesTax,
    decimal SystemCostFee,
    decimal GrossProfit,
    decimal ProfitMarginPercent,
    double ProductionTimeSeconds,
    decimal IskPerHour,
    double AverageDailyVolume,
    bool IsCurrentPhase,
    bool MeetsThreshold,
    bool HasMarketData,
    string? ErrorMessage);
