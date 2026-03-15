using EveMarketAnalysisClient.Models;

namespace EveMarketAnalysisClient.Services.Interfaces;

public interface IEsiMarketClient
{
    Task<MarketSnapshot> GetMarketSnapshotAsync(int regionId, int typeId, CancellationToken cancellationToken = default);
    Task<MarketSnapshot> GetRegionMarketSnapshotAsync(int regionId, int typeId, CancellationToken cancellationToken = default);
}
