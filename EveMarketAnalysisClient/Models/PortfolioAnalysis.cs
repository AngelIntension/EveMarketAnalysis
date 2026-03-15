using System.Collections.Immutable;

namespace EveMarketAnalysisClient.Models;

public record PortfolioAnalysis(
    ImmutableArray<BlueprintRankingEntry> Rankings,
    ImmutableArray<PhaseStatus> PhaseStatuses,
    int CurrentPhaseNumber,
    bool PhaseOverrideActive,
    ImmutableArray<BpoPurchaseRecommendation> BpoRecommendations,
    ImmutableArray<ResearchRecommendation> ResearchRecommendations,
    int TotalBlueprintsEvaluated,
    int SuccessCount,
    int ErrorCount,
    bool PortfolioSizeWarning,
    DateTimeOffset FetchedAt);
