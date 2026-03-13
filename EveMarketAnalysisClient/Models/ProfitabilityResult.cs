using System.Collections.Immutable;

namespace EveMarketAnalysisClient.Models;

public record ProfitabilityResult(
    CharacterBlueprint Blueprint,
    string ProducedTypeName,
    int ProducedTypeId,
    ImmutableArray<MaterialRequirement> Materials,
    decimal TotalMaterialCost,
    decimal ProductSellValue,
    decimal TaxAmount,
    decimal InstallationFee,
    decimal GrossProfit,
    double ProfitMarginPercent,
    int ProductionTimeSeconds,
    double IskPerHour,
    double AverageDailyVolume,
    bool HasMarketData,
    string? ErrorMessage);
