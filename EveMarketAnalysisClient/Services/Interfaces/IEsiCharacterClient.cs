using System.Collections.Immutable;
using EveMarketAnalysisClient.Models;

namespace EveMarketAnalysisClient.Services.Interfaces;

public interface IEsiCharacterClient
{
    Task<string> GetCharacterPortraitAsync(int characterId, CancellationToken cancellationToken = default);
    Task<ImmutableArray<CharacterSkill>> GetCharacterSkillsAsync(int characterId, CancellationToken cancellationToken = default);
    Task<ImmutableArray<SkillQueueEntry>> GetSkillQueueAsync(int characterId, CancellationToken cancellationToken = default);
    Task<Dictionary<int, (string GroupName, int GroupId)>> GetSkillGroupMappingAsync(CancellationToken cancellationToken = default);
    Task<int> GetIndustryJobCountAsync(int characterId, CancellationToken cancellationToken = default);
    Task<int> GetBlueprintCountAsync(int characterId, CancellationToken cancellationToken = default);
}
