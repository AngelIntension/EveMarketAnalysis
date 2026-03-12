using System.Collections.Immutable;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services.Interfaces;
using EveStableInfrastructure;
using Microsoft.Extensions.Caching.Memory;

namespace EveMarketAnalysisClient.Services;

public class EsiCharacterClient : IEsiCharacterClient
{
    private readonly ApiClient _apiClient;
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan SkillGroupCacheDuration = TimeSpan.FromHours(24);

    public EsiCharacterClient(ApiClient apiClient, IMemoryCache cache)
    {
        _apiClient = apiClient;
        _cache = cache;
    }

    public async Task<string> GetCharacterPortraitAsync(int characterId, CancellationToken cancellationToken = default)
    {
        var portrait = await _apiClient.Characters[characterId].Portrait.GetAsync(cancellationToken: cancellationToken);
        return portrait?.Px128x128 ?? $"https://images.evetech.net/characters/{characterId}/portrait?size=128";
    }

    public async Task<ImmutableArray<CharacterSkill>> GetCharacterSkillsAsync(
        int characterId, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.Characters[characterId].Skills.GetAsync(cancellationToken: cancellationToken);
        if (response?.Skills == null)
            return ImmutableArray<CharacterSkill>.Empty;

        var skillIds = response.Skills.Select(s => (int)(s.SkillId ?? 0)).ToList();
        var skillNames = await ResolveSkillNamesAsync(skillIds, cancellationToken);

        return response.Skills
            .Select(s =>
            {
                var skillId = (int)(s.SkillId ?? 0);
                var skillName = skillNames.TryGetValue(skillId, out var name)
                    ? name
                    : $"Skill {skillId}";
                return new CharacterSkill(
                    SkillId: skillId,
                    SkillName: skillName,
                    TrainedLevel: (int)(s.TrainedSkillLevel ?? 0),
                    SkillPointsInSkill: s.SkillpointsInSkill ?? 0);
            })
            .ToImmutableArray();
    }

    public async Task<ImmutableArray<SkillQueueEntry>> GetSkillQueueAsync(
        int characterId, CancellationToken cancellationToken = default)
    {
        var response = await _apiClient.Characters[characterId].Skillqueue.GetAsync(cancellationToken: cancellationToken);
        if (response == null)
            return ImmutableArray<SkillQueueEntry>.Empty;

        var skillIds = response.Select(q => (int)(q.SkillId ?? 0)).ToList();
        var skillNames = await ResolveSkillNamesAsync(skillIds, cancellationToken);

        return response
            .Select(q =>
            {
                var skillId = (int)(q.SkillId ?? 0);
                var skillName = skillNames.TryGetValue(skillId, out var name)
                    ? name
                    : $"Skill {skillId}";
                return new SkillQueueEntry(
                    SkillId: skillId,
                    SkillName: skillName,
                    FinishedLevel: (int)(q.FinishedLevel ?? 0),
                    StartDate: q.StartDate,
                    FinishDate: q.FinishDate,
                    QueuePosition: (int)(q.QueuePosition ?? 0));
            })
            .ToImmutableArray();
    }

    public async Task<Dictionary<int, (string GroupName, int GroupId)>> GetSkillGroupMappingAsync(
        CancellationToken cancellationToken = default)
    {
        const string cacheKey = "esi:skillgroups";
        if (_cache.TryGetValue(cacheKey, out Dictionary<int, (string, int)>? cached) && cached != null)
            return cached;

        // Fetch skill category (category 16 = Skills)
        var category = await _apiClient.Universe.Categories[16].GetAsync(cancellationToken: cancellationToken);
        if (category?.Groups == null)
            return new Dictionary<int, (string, int)>();

        var mapping = new Dictionary<int, (string GroupName, int GroupId)>();

        foreach (var groupIdLong in category.Groups)
        {
            var groupId = (int)(groupIdLong ?? 0);
            var group = await _apiClient.Universe.Groups[groupId].GetAsync(cancellationToken: cancellationToken);
            if (group?.Name == null || group.Types == null)
                continue;

            foreach (var typeIdLong in group.Types)
            {
                var typeId = (int)(typeIdLong ?? 0);
                mapping[typeId] = (group.Name, groupId);
            }
        }

        _cache.Set(cacheKey, mapping, SkillGroupCacheDuration);
        return mapping;
    }

    private async Task<Dictionary<int, string>> ResolveSkillNamesAsync(
        IEnumerable<int> skillIds,
        CancellationToken cancellationToken)
    {
        const string cacheKey = "esi:skillnames";
        var nameCache = _cache.GetOrCreate(cacheKey, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = SkillGroupCacheDuration;
            return new Dictionary<int, string>();
        })!;

        var idsToFetch = skillIds.Where(id => !nameCache.ContainsKey(id)).Distinct().ToList();

        if (idsToFetch.Count > 0)
        {
            var nameTasks = idsToFetch.Select(async typeId =>
            {
                try
                {
                    var typeInfo = await _apiClient.Universe.Types[typeId].GetAsync(cancellationToken: cancellationToken);
                    return (TypeId: typeId, Name: typeInfo?.Name ?? $"Skill {typeId}");
                }
                catch
                {
                    return (TypeId: typeId, Name: $"Skill {typeId}");
                }
            });
            var results = await Task.WhenAll(nameTasks);

            foreach (var (typeId, name) in results)
            {
                nameCache[typeId] = name;
            }
        }

        return nameCache;
    }

    public async Task<int> GetIndustryJobCountAsync(int characterId, CancellationToken cancellationToken = default)
    {
        var jobs = await _apiClient.Characters[characterId].Industry.Jobs.GetAsync(cancellationToken: cancellationToken);
        return jobs?.Count ?? 0;
    }

    public async Task<int> GetBlueprintCountAsync(int characterId, CancellationToken cancellationToken = default)
    {
        var blueprints = await _apiClient.Characters[characterId].Blueprints.GetAsync(cancellationToken: cancellationToken);
        return blueprints?.Count ?? 0;
    }
}
