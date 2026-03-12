using EveMarketAnalysisClient.Models;

namespace EveMarketAnalysisClient.Services.Interfaces;

public interface ICharacterService
{
    Task<CharacterSummary?> GetCharacterSummaryAsync(
        int characterId, string characterName, CancellationToken cancellationToken = default);
}
