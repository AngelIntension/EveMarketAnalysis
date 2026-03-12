namespace EveMarketAnalysisClient.Models;

public record CharacterSkill(
    int SkillId,
    string SkillName,
    int TrainedLevel,
    long SkillPointsInSkill);
