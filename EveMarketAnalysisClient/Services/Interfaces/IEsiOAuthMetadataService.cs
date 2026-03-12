using EveMarketAnalysisClient.Models;

namespace EveMarketAnalysisClient.Services.Interfaces;

public interface IEsiOAuthMetadataService
{
    Task<EsiOAuthMetadata> GetMetadataAsync(CancellationToken cancellationToken = default);
}
