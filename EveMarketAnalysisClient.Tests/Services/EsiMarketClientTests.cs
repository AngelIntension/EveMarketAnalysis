using EveMarketAnalysisClient.Services;
using EveStableInfrastructure;
using EveStableInfrastructure.Models;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace EveMarketAnalysisClient.Tests.Services;

public class EsiMarketClientTests
{
    private static (EsiMarketClient Client, Mock<ApiClient> ApiMock, IMemoryCache Cache) CreateClient()
    {
        var adapter = new Mock<Microsoft.Kiota.Abstractions.IRequestAdapter>();
        var apiClient = new Mock<ApiClient>(adapter.Object);
        var cache = new MemoryCache(new MemoryCacheOptions());

        var logger = new Mock<ILogger<EsiMarketClient>>();
        var client = new EsiMarketClient(apiClient.Object, cache, logger.Object);
        return (client, apiClient, cache);
    }

    [Fact]
    public async Task GetMarketSnapshotAsync_CachesResult()
    {
        // This test verifies caching behavior by checking the cache directly
        var cache = new MemoryCache(new MemoryCacheOptions());

        // Pre-populate cache
        var snapshot = new EveMarketAnalysisClient.Models.MarketSnapshot(
            34, 10000002, 5.0m, 4.5m, 1000.0, DateTimeOffset.UtcNow);
        cache.Set("market:10000002:34", snapshot, TimeSpan.FromMinutes(5));

        var adapter = new Mock<Microsoft.Kiota.Abstractions.IRequestAdapter>();
        var apiClient = new Mock<ApiClient>(adapter.Object);
        var logger = new Mock<ILogger<EsiMarketClient>>();
        var client = new EsiMarketClient(apiClient.Object, cache, logger.Object);

        var result = await client.GetMarketSnapshotAsync(10000002, 34);

        result.LowestSellPrice.Should().Be(5.0m);
        result.HighestBuyPrice.Should().Be(4.5m);
        result.AverageDailyVolume.Should().Be(1000.0);
    }

    [Fact]
    public void MarketSnapshot_HasCorrectRegionAndTypeId()
    {
        var snapshot = new EveMarketAnalysisClient.Models.MarketSnapshot(
            34, 10000002, 5.0m, 4.5m, 1000.0, DateTimeOffset.UtcNow);

        snapshot.TypeId.Should().Be(34);
        snapshot.RegionId.Should().Be(10000002);
    }

    [Fact]
    public void MarketSnapshot_HandlesNullPrices()
    {
        var snapshot = new EveMarketAnalysisClient.Models.MarketSnapshot(
            34, 10000002, null, null, 0.0, DateTimeOffset.UtcNow);

        snapshot.LowestSellPrice.Should().BeNull();
        snapshot.HighestBuyPrice.Should().BeNull();
    }
}
