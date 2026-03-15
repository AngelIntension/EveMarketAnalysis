using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services.Interfaces;
using EveStableInfrastructure;
using EveStableInfrastructure.Markets.Item.Orders;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace EveMarketAnalysisClient.Services;

public class EsiMarketClient : IEsiMarketClient
{
    private readonly ApiClient _apiClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<EsiMarketClient> _logger;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public EsiMarketClient(ApiClient apiClient, IMemoryCache cache, ILogger<EsiMarketClient> logger)
    {
        _apiClient = apiClient;
        _cache = cache;
        _logger = logger;
    }

    public async Task<MarketSnapshot> GetMarketSnapshotAsync(
        int regionId, int typeId, CancellationToken cancellationToken = default)
    {
        var cacheKey = $"market:{regionId}:{typeId}";
        if (_cache.TryGetValue(cacheKey, out MarketSnapshot? cached) && cached != null)
            return cached;

        var ordersTask = FetchAllOrdersAsync(regionId, typeId, cancellationToken);
        var historyTask = FetchHistoryAsync(regionId, typeId, cancellationToken);

        await Task.WhenAll(ordersTask, historyTask);

        var orders = ordersTask.Result;
        var history = historyTask.Result;

        // Filter to trade hub station only
        var hub = TradeHubRegion.All.FirstOrDefault(r => r.RegionId == regionId);
        var stationId = hub?.StationId;

        decimal? lowestSell = null;
        decimal? highestBuy = null;

        foreach (var order in orders)
        {
            if (order.Price == null || order.Price <= 0)
                continue;

            if (stationId.HasValue && order.LocationId != stationId.Value)
                continue;

            var price = (decimal)order.Price.Value;
            if (order.IsBuyOrder == true)
            {
                if (highestBuy == null || price > highestBuy)
                    highestBuy = price;
            }
            else
            {
                if (lowestSell == null || price < lowestSell)
                    lowestSell = price;
            }
        }

        var averageVolume = 0.0;
        if (history.Count > 0)
        {
            var recentHistory = history
                .OrderByDescending(h => h.Date?.ToString() ?? "")
                .Take(30)
                .ToList();
            averageVolume = recentHistory.Average(h => (double)(h.Volume ?? 0));
        }

        var snapshot = new MarketSnapshot(
            TypeId: typeId,
            RegionId: regionId,
            LowestSellPrice: lowestSell,
            HighestBuyPrice: highestBuy,
            AverageDailyVolume: averageVolume,
            FetchedAt: DateTimeOffset.UtcNow);

        _cache.Set(cacheKey, snapshot, CacheDuration);
        return snapshot;
    }

    private async Task<List<EveStableInfrastructure.Models.MarketsRegionIdOrdersGet>> FetchAllOrdersAsync(
        int regionId, int typeId, CancellationToken cancellationToken)
    {
        try
        {
            return await _apiClient.Markets[regionId].Orders.GetAsync(config =>
            {
                config.QueryParameters.TypeId = typeId;
                config.QueryParameters.OrderTypeAsGetOrderTypeQueryParameterType = GetOrder_typeQueryParameterType.All;
                config.QueryParameters.Page = 1;
            }, cancellationToken) ?? new List<EveStableInfrastructure.Models.MarketsRegionIdOrdersGet>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch orders for region {RegionId} type {TypeId}", regionId, typeId);
            return new List<EveStableInfrastructure.Models.MarketsRegionIdOrdersGet>();
        }
    }

    private async Task<List<EveStableInfrastructure.Models.MarketsRegionIdHistoryGet>> FetchHistoryAsync(
        int regionId, int typeId, CancellationToken cancellationToken)
    {
        try
        {
            return await _apiClient.Markets[regionId].History.GetAsync(config =>
            {
                config.QueryParameters.TypeId = typeId;
            }, cancellationToken) ?? new List<EveStableInfrastructure.Models.MarketsRegionIdHistoryGet>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch history for region {RegionId} type {TypeId}", regionId, typeId);
            return new List<EveStableInfrastructure.Models.MarketsRegionIdHistoryGet>();
        }
    }
}
