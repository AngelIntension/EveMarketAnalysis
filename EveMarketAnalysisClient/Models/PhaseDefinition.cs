using System.Collections.Immutable;

namespace EveMarketAnalysisClient.Models;

public record PhaseDefinition(
    int PhaseNumber,
    string Name,
    string Description,
    ImmutableArray<int> CandidateTypeIds);
