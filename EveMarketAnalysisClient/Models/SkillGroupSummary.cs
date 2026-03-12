using System.Collections.Immutable;

namespace EveMarketAnalysisClient.Models;

public record SkillGroupSummary(
    int GroupId,
    string GroupName,
    ImmutableArray<CharacterSkill> Skills,
    long TotalSp);
