using System.Collections.Immutable;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services;
using EveMarketAnalysisClient.Services.Interfaces;
using EveStableInfrastructure;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace EveMarketAnalysisClient.Tests.Unit;

public class PortfolioEdgeCaseTests
{
    private static PortfolioAnalyzer CreateAnalyzer(
        Mock<IBlueprintDataService>? blueprintData = null,
        Mock<IEsiMarketClient>? marketClient = null,
        Mock<IEsiCharacterClient>? characterClient = null,
        Mock<IPhaseService>? phaseService = null)
    {
        blueprintData ??= new Mock<IBlueprintDataService>();
        marketClient ??= new Mock<IEsiMarketClient>();
        characterClient ??= new Mock<IEsiCharacterClient>();
        phaseService ??= new Mock<IPhaseService>();
        var apiClient = new Mock<ApiClient>(
            new Mock<Microsoft.Kiota.Abstractions.IRequestAdapter>().Object);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var logger = new Mock<ILogger<PortfolioAnalyzer>>();

        return new PortfolioAnalyzer(
            blueprintData.Object,
            marketClient.Object,
            characterClient.Object,
            phaseService.Object,
            apiClient.Object,
            cache,
            logger.Object);
    }

    private static MarketSnapshot Snap(int typeId, decimal? sell, decimal? buy, double vol = 0)
        => new(typeId, 10000002, sell, buy, vol, DateTimeOffset.UtcNow);

    [Fact]
    public async Task ZeroBlueprints_ReturnsEmptyState()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        characterClient.Setup(c => c.GetCharacterBlueprintsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray<CharacterBlueprint>.Empty);
        characterClient.Setup(c => c.GetCharacterSkillsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray<CharacterSkill>.Empty);

        var phaseService = new Mock<IPhaseService>();
        phaseService.Setup(p => p.GetAllPhases())
            .Returns(ImmutableArray.Create(
                new PhaseDefinition(1, "P1", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(2, "P2", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(3, "P3", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(4, "P4", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(5, "P5", "T", ImmutableArray<int>.Empty)));
        phaseService.Setup(p => p.GetCandidateTypeIdsForPhase(It.IsAny<int>()))
            .Returns(ImmutableArray<int>.Empty);

        var analyzer = CreateAnalyzer(characterClient: characterClient, phaseService: phaseService);
        var result = await analyzer.AnalyzeAsync(12345, new PortfolioConfiguration());

        result.Rankings.Should().BeEmpty();
        result.TotalBlueprintsEvaluated.Should().Be(0);
        result.SuccessCount.Should().Be(0);
        result.ErrorCount.Should().Be(0);
    }

    [Fact]
    public async Task MissingMarketData_ProducesErrorMessage()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        var bp = new CharacterBlueprint(1, 100, "BP", 10, 20, -1, false);
        characterClient.Setup(c => c.GetCharacterBlueprintsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(bp));
        characterClient.Setup(c => c.GetCharacterSkillsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(new CharacterSkill(3319, "Industry", 5, 256000)));

        blueprintData.Setup(b => b.GetBlueprintActivity(100))
            .Returns(new BlueprintActivity(100, 200, 1, 3600,
                ImmutableArray.Create(new MaterialRequirement(34, "", 100, 0))));

        // No market data for the product
        marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), 200, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Snap(200, null, null));
        marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), 34, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Snap(34, 10m, 8m));

        phaseService.Setup(p => p.GetPhaseForTypeId(It.IsAny<int>())).Returns((PhaseDefinition?)null);
        phaseService.Setup(p => p.GetAllPhases())
            .Returns(ImmutableArray.Create(
                new PhaseDefinition(1, "P1", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(2, "P2", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(3, "P3", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(4, "P4", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(5, "P5", "T", ImmutableArray<int>.Empty)));
        phaseService.Setup(p => p.GetCandidateTypeIdsForPhase(It.IsAny<int>()))
            .Returns(ImmutableArray<int>.Empty);

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var result = await analyzer.AnalyzeAsync(12345, new PortfolioConfiguration());

        result.Rankings.Should().HaveCount(1);
        result.Rankings[0].HasMarketData.Should().BeFalse();
        result.Rankings[0].ErrorMessage.Should().Contain("market data");
    }

    [Fact]
    public async Task PortfolioSizeWarning_NotSetFor300OrLess()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        var bps = Enumerable.Range(1, 300)
            .Select(i => new CharacterBlueprint(i, 100, "BP", 10, 20, -1, false))
            .ToImmutableArray();

        characterClient.Setup(c => c.GetCharacterBlueprintsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bps);
        characterClient.Setup(c => c.GetCharacterSkillsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(new CharacterSkill(3319, "Industry", 5, 256000)));

        blueprintData.Setup(b => b.GetBlueprintActivity(100))
            .Returns(new BlueprintActivity(100, 200, 1, 3600,
                ImmutableArray.Create(new MaterialRequirement(34, "", 100, 0))));
        marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Snap(34, 10m, 8m));

        phaseService.Setup(p => p.GetPhaseForTypeId(It.IsAny<int>())).Returns((PhaseDefinition?)null);
        phaseService.Setup(p => p.GetAllPhases())
            .Returns(ImmutableArray.Create(
                new PhaseDefinition(1, "P1", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(2, "P2", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(3, "P3", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(4, "P4", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(5, "P5", "T", ImmutableArray<int>.Empty)));
        phaseService.Setup(p => p.GetCandidateTypeIdsForPhase(It.IsAny<int>()))
            .Returns(ImmutableArray<int>.Empty);

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var result = await analyzer.AnalyzeAsync(12345, new PortfolioConfiguration());

        result.PortfolioSizeWarning.Should().BeFalse();
    }

    [Fact]
    public void PortfolioConfiguration_DefaultsAreValid()
    {
        var config = new PortfolioConfiguration();

        config.ManufacturingSlots.Should().BeInRange(1, 50);
        config.BuyingBrokerFeePercent.Should().BeInRange(0, 100);
        config.SellingBrokerFeePercent.Should().BeInRange(0, 100);
        config.SalesTaxPercent.Should().BeInRange(0, 100);
        config.MinIskPerHour.Should().BeGreaterOrEqualTo(0);
        config.DailyIncomeGoal.Should().BeGreaterOrEqualTo(0);
    }
}
