using EveMarketAnalysisClient.Models;

namespace EveMarketAnalysisClient.Services.Interfaces;

public interface IPortfolioAnalyzer
{
    Task<PortfolioAnalysis> AnalyzeAsync(
        int characterId,
        PortfolioConfiguration configuration,
        int? phaseOverride = null,
        bool simulateNextPhase = false,
        CancellationToken cancellationToken = default);
}
