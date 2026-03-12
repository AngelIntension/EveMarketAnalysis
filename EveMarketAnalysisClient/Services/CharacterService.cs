using System.Collections.Immutable;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace EveMarketAnalysisClient.Services;

public class CharacterService : ICharacterService
{
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private static readonly HashSet<string> RelevantGroupNames = new()
    {
        "Science", "Industry", "Trade", "Resource Processing", "Planet Management", "Social"
    };

    private readonly IMemoryCache _cache;
    private readonly IEsiCharacterClient _esiClient;
    private readonly ISkillFilterService _skillFilter;

    public CharacterService(
        IMemoryCache cache,
        IEsiCharacterClient esiClient,
        ISkillFilterService skillFilter)
    {
        _cache = cache;
        _esiClient = esiClient;
        _skillFilter = skillFilter;
    }

    public async Task<CharacterSummary?> GetCharacterSummaryAsync(
        int characterId, string characterName, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"esi:{characterId}:summary";
        if (_cache.TryGetValue(cacheKey, out CharacterSummary? cached) && cached != null)
            return cached;

        var portraitUrl = "https://images.evetech.net/characters/" + characterId + "/portrait?size=128";
        var skills = ImmutableArray<CharacterSkill>.Empty;
        var skillQueue = ImmutableArray<SkillQueueEntry>.Empty;
        var groupMapping = new Dictionary<int, (string GroupName, int GroupId)>();

        try
        {
            portraitUrl = await _esiClient.GetCharacterPortraitAsync(characterId, cancellationToken);
        }
        catch (Exception) { /* use default */ }

        try
        {
            groupMapping = await _esiClient.GetSkillGroupMappingAsync(cancellationToken);
            skills = await _esiClient.GetCharacterSkillsAsync(characterId, cancellationToken);
        }
        catch (Exception) { /* empty skills */ }

        try
        {
            skillQueue = await _esiClient.GetSkillQueueAsync(characterId, cancellationToken);
        }
        catch (Exception) { /* empty queue */ }

        var filteredSkills = _skillFilter.FilterToRelevantGroups(skills, groupMapping, RelevantGroupNames);
        var skillGroups = _skillFilter.GroupByCategory(filteredSkills, groupMapping);

        int? industryJobCount = null;
        int? blueprintCount = null;

        try
        {
            industryJobCount = await _esiClient.GetIndustryJobCountAsync(characterId, cancellationToken);
        }
        catch (Exception) { /* null if fetch fails */ }

        try
        {
            blueprintCount = await _esiClient.GetBlueprintCountAsync(characterId, cancellationToken);
        }
        catch (Exception) { /* null if fetch fails */ }

        var summary = new CharacterSummary(
            CharacterId: characterId,
            Name: characterName,
            PortraitUrl: portraitUrl,
            SkillGroups: skillGroups,
            SkillQueue: skillQueue,
            IndustryJobCount: industryJobCount,
            BlueprintCount: blueprintCount,
            FetchedAt: DateTimeOffset.UtcNow);

        _cache.Set(cacheKey, summary, CacheDuration);

        return summary;
    }
}
