using System.Collections.Immutable;
using EveMarketAnalysisClient.Models;

namespace EveMarketAnalysisClient.Services.Interfaces;

public interface IProfitabilityCalculator
{
    Task<ImmutableArray<ProfitabilityResult>> CalculateAsync(
        ImmutableArray<CharacterBlueprint> blueprints,
        ProfitabilitySettings settings,
        CancellationToken cancellationToken = default);
}
