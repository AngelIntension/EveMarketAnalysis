namespace EveMarketAnalysisClient.Models;

public record BlueprintSelection(
    int BlueprintTypeId,
    string BlueprintName,
    int MaterialEfficiency,
    int TimeEfficiency,
    int Runs,
    int MaxRuns,
    bool IsCopy,
    bool ProduceComponents,
    int ProducedTypeId);
