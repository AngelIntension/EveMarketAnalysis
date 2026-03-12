namespace EveMarketAnalysisClient.Models;

public record IndustryJob(
    long JobId,
    string Activity,
    string BlueprintName,
    string Status,
    string Location,
    int Runs,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    double ProgressPercent);
