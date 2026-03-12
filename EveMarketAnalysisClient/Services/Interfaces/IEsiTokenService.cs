using EveMarketAnalysisClient.Models;

namespace EveMarketAnalysisClient.Services.Interfaces;

public interface IEsiTokenService
{
    Task<EsiTokenSet> ExchangeCodeAsync(string code, string codeVerifier, CancellationToken cancellationToken = default);
    Task<EsiTokenSet> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
}
