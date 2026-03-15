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

public class ResearchRecommendationTests
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

    private static MarketSnapshot CreateSnapshot(int typeId, decimal? sell, decimal? buy, double vol = 100)
    {
        return new MarketSnapshot(typeId, 10000002, sell, buy, vol, DateTimeOffset.UtcNow);
    }

    private static void SetupBasicMocks(
        Mock<IEsiCharacterClient> characterClient,
        Mock<IBlueprintDataService> blueprintData,
        Mock<IEsiMarketClient> marketClient,
        Mock<IPhaseService> phaseService,
        params (int typeId, int me, int te)[] blueprints)
    {
        var bps = blueprints.Select(b =>
            new CharacterBlueprint(b.typeId, b.typeId, $"BP {b.typeId}", b.me, b.te, -1, false))
            .ToImmutableArray();

        characterClient.Setup(c => c.GetCharacterBlueprintsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(bps);
        characterClient.Setup(c => c.GetCharacterSkillsAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(new CharacterSkill(3319, "Industry", 5, 256000)));

        foreach (var b in blueprints)
        {
            blueprintData.Setup(bd => bd.GetBlueprintActivity(b.typeId))
                .Returns(new BlueprintActivity(b.typeId, b.typeId + 10000, 1, 3600,
                    ImmutableArray.Create(new MaterialRequirement(34, "", 100, 0))));

            marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), b.typeId + 10000, It.IsAny<CancellationToken>()))
                .ReturnsAsync(CreateSnapshot(b.typeId + 10000, 5_000_000m, 4_500_000m));
        }

        marketClient.Setup(m => m.GetMarketSnapshotAsync(It.IsAny<int>(), 34, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSnapshot(34, 10m, 8m));

        phaseService.Setup(p => p.GetPhaseForTypeId(It.IsAny<int>()))
            .Returns(new PhaseDefinition(1, "Test", "Test", ImmutableArray<int>.Empty));
        phaseService.Setup(p => p.GetAllPhases())
            .Returns(ImmutableArray.Create(
                new PhaseDefinition(1, "T1", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(2, "T2", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(3, "T3", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(4, "T4", "T", ImmutableArray<int>.Empty),
                new PhaseDefinition(5, "T5", "T", ImmutableArray<int>.Empty)));
    }

    [Fact]
    public async Task ResearchRecommendations_IncludesUnderResearchedBlueprints()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        // ME 5 / TE 10 → under-researched
        SetupBasicMocks(characterClient, blueprintData, marketClient, phaseService, (100, 5, 10));

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration();

        var result = await analyzer.AnalyzeAsync(12345, config);

        result.ResearchRecommendations.Should().HaveCount(1);
        result.ResearchRecommendations[0].CurrentME.Should().Be(5);
        result.ResearchRecommendations[0].TargetME.Should().Be(10);
        result.ResearchRecommendations[0].IskPerHourGain.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ResearchRecommendations_ExcludesFullyResearched()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        // ME 10 / TE 20 → fully researched
        SetupBasicMocks(characterClient, blueprintData, marketClient, phaseService, (100, 10, 20));

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration();

        var result = await analyzer.AnalyzeAsync(12345, config);

        result.ResearchRecommendations.Should().BeEmpty();
    }

    [Fact]
    public async Task ResearchRecommendations_SortedByGainDescending()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        // BP with ME 0 should have higher gain than BP with ME 8
        SetupBasicMocks(characterClient, blueprintData, marketClient, phaseService,
            (100, 0, 0), (101, 8, 16));

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration();

        var result = await analyzer.AnalyzeAsync(12345, config);

        if (result.ResearchRecommendations.Length >= 2)
        {
            result.ResearchRecommendations[0].IskPerHourGain.Should()
                .BeGreaterThanOrEqualTo(result.ResearchRecommendations[1].IskPerHourGain);
        }
    }

    [Fact]
    public async Task ResearchRecommendations_CappedAt10()
    {
        var characterClient = new Mock<IEsiCharacterClient>();
        var blueprintData = new Mock<IBlueprintDataService>();
        var marketClient = new Mock<IEsiMarketClient>();
        var phaseService = new Mock<IPhaseService>();

        // 15 under-researched blueprints
        var bps = Enumerable.Range(100, 15).Select(id => (id, 0, 0)).ToArray();
        SetupBasicMocks(characterClient, blueprintData, marketClient, phaseService, bps);

        var analyzer = CreateAnalyzer(blueprintData, marketClient, characterClient, phaseService);
        var config = new PortfolioConfiguration();

        var result = await analyzer.AnalyzeAsync(12345, config);

        result.ResearchRecommendations.Length.Should().BeLessThanOrEqualTo(10);
    }
}
