using System.Collections.Immutable;

namespace EveMarketAnalysisClient.Models;

public record ProfitabilityResponse(
    ImmutableArray<ProfitabilityResult> Results,
    int RegionId,
    string RegionName,
    decimal TaxRate,
    int TotalBlueprints,
    int SuccessCount,
    int ErrorCount,
    DateTimeOffset FetchedAt);
