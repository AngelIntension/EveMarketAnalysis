namespace EveMarketAnalysisClient.Models;

public record PhaseStatus(
    PhaseDefinition Phase,
    int OwnedProfitableCount,
    int RequiredCount,
    bool IsComplete,
    decimal DailyPotentialIncome,
    string? CompletionReason);
