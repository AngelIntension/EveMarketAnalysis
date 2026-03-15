using System.Collections.Immutable;
using EveMarketAnalysisClient.Models;

namespace EveMarketAnalysisClient.Services.Interfaces;

public interface IPhaseService
{
    ImmutableArray<PhaseDefinition> GetAllPhases();
    PhaseDefinition? GetPhaseForTypeId(int blueprintTypeId);
}
