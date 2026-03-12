namespace EveMarketAnalysisClient.Models;

public record SkillQueueEntry(
    int SkillId,
    string SkillName,
    int FinishedLevel,
    DateTimeOffset? StartDate,
    DateTimeOffset? FinishDate,
    int QueuePosition);
