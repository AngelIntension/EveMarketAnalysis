using System.Collections.Immutable;

namespace EveMarketAnalysisClient.Models;

public record CharacterSummary(
    int CharacterId,
    string Name,
    string PortraitUrl,
    ImmutableArray<SkillGroupSummary> SkillGroups,
    ImmutableArray<SkillQueueEntry> SkillQueue,
    int? IndustryJobCount,
    int? BlueprintCount,
    DateTimeOffset FetchedAt);
