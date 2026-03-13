namespace EveMarketAnalysisClient.Models;

public record CharacterBlueprint(
    long ItemId,
    int TypeId,
    string TypeName,
    int MaterialEfficiency,
    int TimeEfficiency,
    int Runs,
    bool IsCopy);
