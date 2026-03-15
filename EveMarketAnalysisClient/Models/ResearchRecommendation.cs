namespace EveMarketAnalysisClient.Models;

public record ResearchRecommendation(
    CharacterBlueprint Blueprint,
    string ProducedTypeName,
    decimal CurrentIskPerHour,
    decimal ProjectedIskPerHour,
    decimal IskPerHourGain,
    decimal GainPercent,
    int CurrentME,
    int CurrentTE,
    int TargetME,
    int TargetTE);
