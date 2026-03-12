using System.Collections.Immutable;
using EveMarketAnalysisClient.Models;

namespace EveMarketAnalysisClient.Services.Interfaces;

public interface ISkillFilterService
{
    ImmutableArray<CharacterSkill> FilterToRelevantGroups(
        ImmutableArray<CharacterSkill> skills,
        Dictionary<int, (string GroupName, int GroupId)> groupMapping,
        HashSet<string> relevantGroupNames);

    ImmutableArray<SkillGroupSummary> GroupByCategory(
        ImmutableArray<CharacterSkill> skills,
        Dictionary<int, (string GroupName, int GroupId)> groupMapping);
}
