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
        var skillNames = await ResolveNamesAsync(skillIds, cancellationToken);

        return response.Skills
            .Select(s =>
            {
                var skillId = (int)(s.SkillId ?? 0);
                var skillName = skillNames.TryGetValue(skillId, out var name)
                    ? name
                    : $"Unknown ({skillId})";
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
        var skillNames = await ResolveNamesAsync(skillIds, cancellationToken);

        return response
            .Select(q =>
            {
                var skillId = (int)(q.SkillId ?? 0);
                var skillName = skillNames.TryGetValue(skillId, out var name)
                    ? name
                    : $"Unknown ({skillId})";
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

        var groupTasks = category.Groups
            .Select(g => (int)(g ?? 0))
            .Select(async groupId =>
            {
                var group = await _apiClient.Universe.Groups[groupId].GetAsync(cancellationToken: cancellationToken);
                return (GroupId: groupId, Group: group);
            });
        var groups = await Task.WhenAll(groupTasks);

        foreach (var (groupId, group) in groups)
        {
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

    private async Task<Dictionary<int, string>> ResolveNamesAsync(
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
            try
            {
                var body = idsToFetch.Select(id => (long?)id).ToList();
                var results = await _apiClient.Universe.Names.PostAsync(body, cancellationToken: cancellationToken);
                if (results != null)
                {
                    foreach (var entry in results)
                    {
                        if (entry.Id.HasValue && entry.Name != null)
                            nameCache[(int)entry.Id.Value] = entry.Name;
                    }
                }
            }
            catch
            {
                // Fall back to placeholder names if bulk resolve fails
            }

            // Fill any IDs that weren't resolved
            foreach (var id in idsToFetch)
            {
                nameCache.TryAdd(id, $"Unknown ({id})");
            }
        }

        return nameCache;
    }

    private static readonly Dictionary<int, string> ActivityNames = new()
    {
        [1] = "Manufacturing",
        [3] = "Researching TE",
        [4] = "Researching ME",
        [5] = "Copying",
        [8] = "Invention",
        [9] = "Reactions"
    };

    public async Task<ImmutableArray<IndustryJob>> GetIndustryJobsAsync(
        int characterId, CancellationToken cancellationToken = default)
    {
        var jobs = await _apiClient.Characters[characterId].Industry.Jobs.GetAsync(cancellationToken: cancellationToken);
        if (jobs == null || jobs.Count == 0)
            return ImmutableArray<IndustryJob>.Empty;

        // Collect all type IDs and station/structure IDs that need name resolution
        var typeIds = jobs
            .Select(j => (int)(j.BlueprintTypeId ?? 0))
            .Where(id => id != 0)
            .Distinct()
            .ToList();

        var locationIds = jobs
            .Select(j => (int)(j.StationId ?? j.FacilityId ?? 0))
            .Where(id => id != 0)
            .Distinct()
            .ToList();

        var allIds = typeIds.Concat(locationIds).Distinct().ToList();
        var names = await ResolveNamesAsync(allIds, cancellationToken);

        var now = DateTimeOffset.UtcNow;

        return jobs
            .Select(j =>
            {
                var activityId = (int)(j.ActivityId ?? 0);
                var activity = ActivityNames.TryGetValue(activityId, out var actName)
                    ? actName
                    : $"Activity {activityId}";

                var blueprintTypeId = (int)(j.BlueprintTypeId ?? 0);
                var blueprintName = names.TryGetValue(blueprintTypeId, out var bpName)
                    ? bpName
                    : $"Unknown ({blueprintTypeId})";

                var locationId = (int)(j.StationId ?? j.FacilityId ?? 0);
                var location = names.TryGetValue(locationId, out var locName)
                    ? locName
                    : $"Unknown ({locationId})";

                var status = j.Status?.ToString() ?? "Unknown";

                var progress = 0.0;
                if (j.StartDate.HasValue && j.EndDate.HasValue && j.EndDate > j.StartDate)
                {
                    var total = (j.EndDate.Value - j.StartDate.Value).TotalSeconds;
                    var elapsed = (now - j.StartDate.Value).TotalSeconds;
                    progress = status == "Active"
                        ? Math.Clamp(elapsed / total * 100, 0, 100)
                        : status is "Ready" or "Delivered" ? 100 : 0;
                }

                return new IndustryJob(
                    JobId: j.JobId ?? 0,
                    Activity: activity,
                    BlueprintName: blueprintName,
                    Status: status,
                    Location: location,
                    Runs: (int)(j.Runs ?? 0),
                    StartDate: j.StartDate,
                    EndDate: j.EndDate,
                    ProgressPercent: Math.Round(progress, 1));
            })
            .OrderBy(j => j.EndDate)
            .ToImmutableArray();
    }

    public async Task<int> GetBlueprintCountAsync(int characterId, CancellationToken cancellationToken = default)
    {
        var blueprints = await _apiClient.Characters[characterId].Blueprints.GetAsync(cancellationToken: cancellationToken);
        return blueprints?.Count ?? 0;
    }

    public async Task<ImmutableArray<CharacterBlueprint>> GetCharacterBlueprintsAsync(
        int characterId, CancellationToken cancellationToken = default)
    {
        var blueprints = await _apiClient.Characters[characterId].Blueprints.GetAsync(cancellationToken: cancellationToken);
        if (blueprints == null || blueprints.Count == 0)
            return ImmutableArray<CharacterBlueprint>.Empty;

        var typeIds = blueprints
            .Select(b => (int)(b.TypeId ?? 0))
            .Where(id => id != 0)
            .Distinct()
            .ToList();

        var names = await ResolveNamesAsync(typeIds, cancellationToken);

        return blueprints
            .Select(b =>
            {
                var typeId = (int)(b.TypeId ?? 0);
                var typeName = names.TryGetValue(typeId, out var name)
                    ? name
                    : $"Unknown ({typeId})";
                var quantity = (int)(b.Quantity ?? 0);
                var isCopy = quantity == -2;
                var runs = (int)(b.Runs ?? -1);

                return new CharacterBlueprint(
                    ItemId: b.ItemId ?? 0,
                    TypeId: typeId,
                    TypeName: typeName,
                    MaterialEfficiency: (int)(b.MaterialEfficiency ?? 0),
                    TimeEfficiency: (int)(b.TimeEfficiency ?? 0),
                    Runs: runs,
                    IsCopy: isCopy);
            })
            .ToImmutableArray();
    }
}
