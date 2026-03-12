using System.Collections.Immutable;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services.Interfaces;

namespace EveMarketAnalysisClient.Services;

public class SkillFilterService : ISkillFilterService
{
    public ImmutableArray<CharacterSkill> FilterToRelevantGroups(
        ImmutableArray<CharacterSkill> skills,
        Dictionary<int, (string GroupName, int GroupId)> groupMapping,
        HashSet<string> relevantGroupNames)
    {
        return skills
            .Where(s => groupMapping.TryGetValue(s.SkillId, out var group)
                        && relevantGroupNames.Contains(group.GroupName))
            .ToImmutableArray();
    }

    public ImmutableArray<SkillGroupSummary> GroupByCategory(
        ImmutableArray<CharacterSkill> skills,
        Dictionary<int, (string GroupName, int GroupId)> groupMapping)
    {
        return skills
            .Where(s => groupMapping.ContainsKey(s.SkillId))
            .GroupBy(s => groupMapping[s.SkillId])
            .Select(g => new SkillGroupSummary(
                GroupId: g.Key.GroupId,
                GroupName: g.Key.GroupName,
                Skills: g.OrderBy(s => s.SkillName).ToImmutableArray(),
                TotalSp: g.Sum(s => s.SkillPointsInSkill)))
            .OrderBy(g => g.GroupName)
            .ToImmutableArray();
    }
}
