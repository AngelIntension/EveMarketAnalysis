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

    private static async Task<T> SafeAsync<T>(Func<Task<T>> func, T fallback)
    {
        try { return await func(); }
        catch { return fallback; }
    }

    public async Task<CharacterSummary?> GetCharacterSummaryAsync(
        int characterId, string characterName, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"esi:{characterId}:summary";
        if (_cache.TryGetValue(cacheKey, out CharacterSummary? cached) && cached != null)
            return cached;

        var defaultPortrait = "https://images.evetech.net/characters/" + characterId + "/portrait?size=128";

        // Kick off all independent fetches in parallel
        var portraitTask = SafeAsync(
            () => _esiClient.GetCharacterPortraitAsync(characterId, cancellationToken), defaultPortrait);
        var groupMappingTask = SafeAsync(
            () => _esiClient.GetSkillGroupMappingAsync(cancellationToken),
            new Dictionary<int, (string GroupName, int GroupId)>());
        var skillsTask = SafeAsync(
            () => _esiClient.GetCharacterSkillsAsync(characterId, cancellationToken),
            ImmutableArray<CharacterSkill>.Empty);
        var queueTask = SafeAsync(
            () => _esiClient.GetSkillQueueAsync(characterId, cancellationToken),
            ImmutableArray<SkillQueueEntry>.Empty);
        var industryTask = SafeAsync<int?>(
            async () => await _esiClient.GetIndustryJobCountAsync(characterId, cancellationToken), null);
        var blueprintTask = SafeAsync<int?>(
            async () => await _esiClient.GetBlueprintCountAsync(characterId, cancellationToken), null);

        await Task.WhenAll(portraitTask, groupMappingTask, skillsTask, queueTask, industryTask, blueprintTask);

        var portraitUrl = portraitTask.Result;
        var groupMapping = groupMappingTask.Result;
        var skills = skillsTask.Result;
        var skillQueue = queueTask.Result;

        var filteredSkills = _skillFilter.FilterToRelevantGroups(skills, groupMapping, RelevantGroupNames);
        var skillGroups = _skillFilter.GroupByCategory(filteredSkills, groupMapping);

        int? industryJobCount = industryTask.Result;
        int? blueprintCount = blueprintTask.Result;

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
