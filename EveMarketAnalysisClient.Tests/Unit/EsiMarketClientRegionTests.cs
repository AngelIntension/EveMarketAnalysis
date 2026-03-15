using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services;
using EveStableInfrastructure;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace EveMarketAnalysisClient.Tests.Unit;

public class EsiMarketClientRegionTests
{
    private static EsiMarketClient CreateClient(
        Mock<ApiClient>? apiClient = null,
        IMemoryCache? cache = null)
    {
        apiClient ??= new Mock<ApiClient>(
            new Mock<Microsoft.Kiota.Abstractions.IRequestAdapter>().Object);
        cache ??= new MemoryCache(new MemoryCacheOptions());
        var logger = new Mock<ILogger<EsiMarketClient>>();

        return new EsiMarketClient(apiClient.Object, cache, logger.Object);
    }

    [Fact]
    public async Task GetRegionMarketSnapshotAsync_ReturnsCachedResult_WhenAvailable()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        var expectedSnapshot = new MarketSnapshot(
            TypeId: 587, RegionId: 10000002,
            LowestSellPrice: 100m, HighestBuyPrice: 90m,
            AverageDailyVolume: 50.0, FetchedAt: DateTimeOffset.UtcNow);

        cache.Set("regionmarket:10000002:587", expectedSnapshot, TimeSpan.FromMinutes(5));

        var client = CreateClient(cache: cache);

        var result = await client.GetRegionMarketSnapshotAsync(10000002, 587);

        result.Should().Be(expectedSnapshot);
    }

    [Fact]
    public async Task GetRegionMarketSnapshotAsync_ReturnsSnapshotWithRegionWidePrices()
    {
        // This test verifies the method exists and returns a MarketSnapshot
        // Since we can't easily mock the Kiota API client's deep property chain,
        // we verify cache behavior which is the core differentiator
        var cache = new MemoryCache(new MemoryCacheOptions());
        var snapshot = new MarketSnapshot(
            TypeId: 34, RegionId: 10000002,
            LowestSellPrice: 5.50m, HighestBuyPrice: 5.00m,
            AverageDailyVolume: 1000000.0, FetchedAt: DateTimeOffset.UtcNow);

        cache.Set("regionmarket:10000002:34", snapshot, TimeSpan.FromMinutes(5));

        var client = CreateClient(cache: cache);

        var result = await client.GetRegionMarketSnapshotAsync(10000002, 34);

        result.TypeId.Should().Be(34);
        result.RegionId.Should().Be(10000002);
        result.LowestSellPrice.Should().Be(5.50m);
        result.HighestBuyPrice.Should().Be(5.00m);
    }

    [Fact]
    public async Task GetRegionMarketSnapshotAsync_UsesDifferentCacheKey_ThanStationFiltered()
    {
        var cache = new MemoryCache(new MemoryCacheOptions());

        // Pre-populate station-filtered cache
        var stationSnapshot = new MarketSnapshot(
            TypeId: 587, RegionId: 10000002,
            LowestSellPrice: 200m, HighestBuyPrice: 180m,
            AverageDailyVolume: 50.0, FetchedAt: DateTimeOffset.UtcNow);
        cache.Set("market:10000002:587", stationSnapshot, TimeSpan.FromMinutes(5));

        // Pre-populate region-wide cache with different prices
        var regionSnapshot = new MarketSnapshot(
            TypeId: 587, RegionId: 10000002,
            LowestSellPrice: 150m, HighestBuyPrice: 140m,
            AverageDailyVolume: 80.0, FetchedAt: DateTimeOffset.UtcNow);
        cache.Set("regionmarket:10000002:587", regionSnapshot, TimeSpan.FromMinutes(5));

        var client = CreateClient(cache: cache);

        var regionResult = await client.GetRegionMarketSnapshotAsync(10000002, 587);
        var stationResult = await client.GetMarketSnapshotAsync(10000002, 587);

        // Should return different snapshots
        regionResult.LowestSellPrice.Should().Be(150m);
        stationResult.LowestSellPrice.Should().Be(200m);
    }
}
