namespace EveMarketAnalysisClient.Models;

public record ProfitabilitySettings(
    int RegionId = 10000002,
    decimal TaxRate = 0.08m,
    decimal InstallationFeeRate = 0.01m);
