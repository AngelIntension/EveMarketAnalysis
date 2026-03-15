using System.Collections.Frozen;
using System.Collections.Immutable;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services;
using EveMarketAnalysisClient.Services.Interfaces;
using EveStableInfrastructure;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace EveMarketAnalysisClient.Tests.Services;

public class ShoppingListServiceTests
{
    private static ShoppingListService CreateService(
        Mock<IBlueprintDataService>? blueprintData = null,
        Mock<IEsiCharacterClient>? esiClient = null,
        Mock<IEsiMarketClient>? marketClient = null,
        IMemoryCache? cache = null)
    {
        blueprintData ??= new Mock<IBlueprintDataService>();
        esiClient ??= new Mock<IEsiCharacterClient>();
        marketClient ??= new Mock<IEsiMarketClient>();
        cache ??= new MemoryCache(new MemoryCacheOptions());
        var apiClient = new Mock<ApiClient>(
            new Mock<Microsoft.Kiota.Abstractions.IRequestAdapter>().Object);
        var logger = new Mock<ILogger<ShoppingListService>>();

        return new ShoppingListService(
            blueprintData.Object,
            esiClient.Object,
            marketClient.Object,
            apiClient.Object,
            cache,
            logger.Object);
    }

    // === T008: ME Calculation Tests ===

    [Theory]
    [InlineData(100, 0, 100)]   // ME 0: no reduction
    [InlineData(100, 5, 95)]    // ME 5: 5% reduction
    [InlineData(100, 10, 90)]   // ME 10: 10% reduction
    [InlineData(1, 10, 1)]      // ME 10 on 1 = max(1, ceil(0.9)) = 1
    [InlineData(10, 10, 9)]     // ME 10 on 10 = ceil(9.0) = 9
    [InlineData(3, 10, 3)]      // ME 10 on 3 = max(1, ceil(2.7)) = 3
    public void ExpandBlueprintToMaterials_AppliesMeFormula_Correctly(
        int baseQty, int me, int expectedAdjusted)
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        blueprintData.Setup(b => b.GetBlueprintActivity(100))
            .Returns(new BlueprintActivity(100, 200, 1, 3600,
                ImmutableArray.Create(new MaterialRequirement(34, "", baseQty, 0))));

        var service = CreateService(blueprintData: blueprintData);
        var selection = new BlueprintSelection(100, "Test BP", me, 0, 1, -1, false, false, 200);
        var ownedMap = new Dictionary<int, CharacterBlueprint>().ToFrozenDictionary();

        var result = service.ExpandBlueprintToMaterials(selection, ownedMap);

        result.Children.Should().HaveCount(1);
        result.Children[0].AdjustedQuantity.Should().Be(expectedAdjusted);
    }

    [Fact]
    public void ExpandBlueprintToMaterials_MultipliesAdjustedByRuns()
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        blueprintData.Setup(b => b.GetBlueprintActivity(100))
            .Returns(new BlueprintActivity(100, 200, 1, 3600,
                ImmutableArray.Create(new MaterialRequirement(34, "", 100, 0))));

        var service = CreateService(blueprintData: blueprintData);
        var selection = new BlueprintSelection(100, "Test BP", 10, 0, 5, -1, false, false, 200);
        var ownedMap = new Dictionary<int, CharacterBlueprint>().ToFrozenDictionary();

        var result = service.ExpandBlueprintToMaterials(selection, ownedMap);

        result.Children[0].AdjustedQuantity.Should().Be(90);
        result.Children[0].TotalQuantity.Should().Be(450); // 90 * 5
    }

    // === T009: Flat Material Expansion Tests (tested in MaterialTreeNodeTests.cs) ===
    // These are tested via ExpandBlueprintToMaterials above

    // === T010: AggregateMaterials Tests ===

    [Fact]
    public void AggregateMaterials_MergesDuplicateTypeIds()
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        var service = CreateService(blueprintData: blueprintData);

        var tree1 = new MaterialTreeNode(200, "Product A", 0, 0, 1, 0, true, 100,
            ImmutableArray.Create(
                new MaterialTreeNode(34, "Tritanium", 100, 90, 1, 90, false, 100,
                    ImmutableArray<MaterialTreeNode>.Empty)));

        var tree2 = new MaterialTreeNode(300, "Product B", 0, 0, 1, 0, true, 101,
            ImmutableArray.Create(
                new MaterialTreeNode(34, "Tritanium", 200, 180, 1, 180, false, 101,
                    ImmutableArray<MaterialTreeNode>.Empty)));

        var result = service.AggregateMaterials(ImmutableArray.Create(tree1, tree2));

        result.Should().HaveCount(1);
        result[0].TypeId.Should().Be(34);
        result[0].TotalQuantity.Should().Be(270); // 90 + 180
    }

    [Fact]
    public void AggregateMaterials_TracksMaterialSources()
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        var service = CreateService(blueprintData: blueprintData);

        var tree1 = new MaterialTreeNode(200, "Product A", 0, 0, 1, 0, true, 100,
            ImmutableArray.Create(
                new MaterialTreeNode(34, "Tritanium", 100, 90, 1, 90, false, 100,
                    ImmutableArray<MaterialTreeNode>.Empty)));

        var tree2 = new MaterialTreeNode(300, "Product B", 0, 0, 1, 0, true, 101,
            ImmutableArray.Create(
                new MaterialTreeNode(34, "Tritanium", 200, 180, 1, 180, false, 101,
                    ImmutableArray<MaterialTreeNode>.Empty)));

        var result = service.AggregateMaterials(ImmutableArray.Create(tree1, tree2));

        result[0].Sources.Should().HaveCount(2);
        result[0].Sources[0].BlueprintTypeId.Should().Be(100);
        result[0].Sources[0].Quantity.Should().Be(90);
        result[0].Sources[1].BlueprintTypeId.Should().Be(101);
        result[0].Sources[1].Quantity.Should().Be(180);
    }

    [Fact]
    public void AggregateMaterials_KeepsDistinctMaterialsSeparate()
    {
        var service = CreateService();

        var tree = new MaterialTreeNode(200, "Product", 0, 0, 1, 0, true, 100,
            ImmutableArray.Create(
                new MaterialTreeNode(34, "Tritanium", 100, 90, 1, 90, false, 100,
                    ImmutableArray<MaterialTreeNode>.Empty),
                new MaterialTreeNode(35, "Pyerite", 50, 45, 1, 45, false, 100,
                    ImmutableArray<MaterialTreeNode>.Empty)));

        var result = service.AggregateMaterials(ImmutableArray.Create(tree));

        result.Should().HaveCount(2);
        result.Should().Contain(i => i.TypeId == 34 && i.TotalQuantity == 90);
        result.Should().Contain(i => i.TypeId == 35 && i.TotalQuantity == 45);
    }

    // === T011: BuildOwnedBlueprintMap Tests ===

    [Fact]
    public void BuildOwnedBlueprintMap_KeysByProducedTypeId()
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        blueprintData.Setup(b => b.GetBlueprintActivity(691))
            .Returns(new BlueprintActivity(691, 587, 1, 3600,
                ImmutableArray<MaterialRequirement>.Empty));

        var service = CreateService(blueprintData: blueprintData);

        var blueprints = ImmutableArray.Create(
            new CharacterBlueprint(1, 691, "Rifter BP", 10, 20, -1, false));

        var map = service.BuildOwnedBlueprintMap(blueprints);

        map.Should().ContainKey(587);
        map[587].TypeId.Should().Be(691);
    }

    [Fact]
    public void BuildOwnedBlueprintMap_HighestMeWins_WhenDuplicates()
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        blueprintData.Setup(b => b.GetBlueprintActivity(691))
            .Returns(new BlueprintActivity(691, 587, 1, 3600,
                ImmutableArray<MaterialRequirement>.Empty));

        var service = CreateService(blueprintData: blueprintData);

        var blueprints = ImmutableArray.Create(
            new CharacterBlueprint(1, 691, "Rifter BP ME5", 5, 20, -1, false),
            new CharacterBlueprint(2, 691, "Rifter BP ME10", 10, 20, -1, false));

        var map = service.BuildOwnedBlueprintMap(blueprints);

        map.Should().ContainKey(587);
        map[587].MaterialEfficiency.Should().Be(10);
    }

    [Fact]
    public void BuildOwnedBlueprintMap_SkipsBlueprintsWithoutActivity()
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        blueprintData.Setup(b => b.GetBlueprintActivity(It.IsAny<int>()))
            .Returns((BlueprintActivity?)null);

        var service = CreateService(blueprintData: blueprintData);

        var blueprints = ImmutableArray.Create(
            new CharacterBlueprint(1, 999, "Unknown BP", 10, 20, -1, false));

        var map = service.BuildOwnedBlueprintMap(blueprints);

        map.Should().BeEmpty();
    }

    // === T014: Integration Test ===

    [Fact]
    public async Task GenerateShoppingListAsync_EndToEnd_ReturnsCorrectResponse()
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        var esiClient = new Mock<IEsiCharacterClient>();
        var cache = new MemoryCache(new MemoryCacheOptions());

        // Setup blueprint activity for blueprint 691 (produces type 587)
        blueprintData.Setup(b => b.GetBlueprintActivity(691))
            .Returns(new BlueprintActivity(691, 587, 1, 3600,
                ImmutableArray.Create(
                    new MaterialRequirement(34, "Tritanium", 32000, 0),
                    new MaterialRequirement(35, "Pyerite", 6000, 0))));

        // Character owns this blueprint
        esiClient.Setup(e => e.GetCharacterBlueprintsAsync(12345, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ImmutableArray.Create(
                new CharacterBlueprint(1, 691, "Rifter Blueprint", 10, 20, -1, false)));

        var service = CreateService(
            blueprintData: blueprintData,
            esiClient: esiClient,
            cache: cache);

        var selections = ImmutableArray.Create(
            new BlueprintSelection(691, "Rifter Blueprint", 10, 20, 2, -1, false, false, 587));

        var response = await service.GenerateShoppingListAsync(selections, 12345);

        response.BlueprintCount.Should().Be(1);
        response.Errors.Should().BeEmpty();
        response.Items.Should().HaveCount(2);

        var tritanium = response.Items.First(i => i.TypeId == 34);
        // ME 10: max(1, ceil(32000 * 0.9)) = 28800, * 2 runs = 57600
        tritanium.TotalQuantity.Should().Be(57600);

        var pyerite = response.Items.First(i => i.TypeId == 35);
        // ME 10: max(1, ceil(6000 * 0.9)) = 5400, * 2 runs = 10800
        pyerite.TotalQuantity.Should().Be(10800);
    }

    // === T026: Aggregation After Recursive Expansion ===

    [Fact]
    public void AggregateMaterials_AfterRecursiveExpansion_MergesLeafNodes()
    {
        var service = CreateService();

        // Tree with an expanded intermediate:
        // Product requires Component (expanded) and Tritanium (leaf)
        // Component expanded to Tritanium and Pyerite (leaves)
        var expandedComponent = new MaterialTreeNode(50, "Component", 10, 9, 1, 9, true, 100,
            ImmutableArray.Create(
                new MaterialTreeNode(34, "Tritanium", 100, 90, 9, 810, false, 50,
                    ImmutableArray<MaterialTreeNode>.Empty),
                new MaterialTreeNode(35, "Pyerite", 50, 45, 9, 405, false, 50,
                    ImmutableArray<MaterialTreeNode>.Empty)));

        var tree = new MaterialTreeNode(200, "Product", 0, 0, 1, 0, true, 100,
            ImmutableArray.Create(
                expandedComponent,
                new MaterialTreeNode(34, "Tritanium", 200, 180, 1, 180, false, 100,
                    ImmutableArray<MaterialTreeNode>.Empty)));

        var result = service.AggregateMaterials(ImmutableArray.Create(tree));

        // Tritanium should be merged: 810 (from expanded component) + 180 (direct) = 990
        var tritanium = result.First(i => i.TypeId == 34);
        tritanium.TotalQuantity.Should().Be(990);

        var pyerite = result.First(i => i.TypeId == 35);
        pyerite.TotalQuantity.Should().Be(405);
    }

    // === T029: FetchCostsAsync Tests ===

    [Fact]
    public async Task FetchCostsAsync_ReturnsPricesFromMarketClient()
    {
        var marketClient = new Mock<IEsiMarketClient>();
        marketClient.Setup(m => m.GetMarketSnapshotAsync(10000002, 34, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketSnapshot(34, 10000002, 5.50m, null, 500000, DateTimeOffset.UtcNow));
        marketClient.Setup(m => m.GetMarketSnapshotAsync(10000002, 35, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MarketSnapshot(35, 10000002, 8.00m, null, 300000, DateTimeOffset.UtcNow));

        var service = CreateService(marketClient: marketClient);

        var result = await service.FetchCostsAsync(
            ImmutableArray.Create(34, 35), 10000002);

        result.Should().ContainKey(34);
        result[34].SellPrice.Should().Be(5.50m);
        result.Should().ContainKey(35);
        result[35].SellPrice.Should().Be(8.00m);
    }

    [Fact]
    public async Task FetchCostsAsync_ReturnsNull_WhenMarketCallFails()
    {
        var marketClient = new Mock<IEsiMarketClient>();
        marketClient.Setup(m => m.GetMarketSnapshotAsync(10000002, 34, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("ESI error"));

        var service = CreateService(marketClient: marketClient);

        var result = await service.FetchCostsAsync(
            ImmutableArray.Create(34), 10000002);

        result.Should().ContainKey(34);
        result[34].SellPrice.Should().BeNull();
        result[34].BuyPrice.Should().BeNull();
    }

    // === T032: Integration Test for Cost Fetch Pipeline ===

    [Fact]
    public async Task FetchCostsAsync_DispatchesParallelCalls()
    {
        var marketClient = new Mock<IEsiMarketClient>();
        var typeIds = Enumerable.Range(34, 10).ToImmutableArray();

        foreach (var id in typeIds)
        {
            marketClient.Setup(m => m.GetMarketSnapshotAsync(10000002, id, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new MarketSnapshot(id, 10000002, id * 1.0m, null, 100, DateTimeOffset.UtcNow));
        }

        var service = CreateService(marketClient: marketClient);

        var result = await service.FetchCostsAsync(typeIds, 10000002);

        result.Should().HaveCount(10);
        foreach (var id in typeIds)
        {
            marketClient.Verify(m => m.GetMarketSnapshotAsync(10000002, id, It.IsAny<CancellationToken>()), Times.Once);
        }
    }

    // === T037: GenerateCsv Tests ===

    [Fact]
    public void GenerateCsv_GeneratesCorrectOutput_WithoutCosts()
    {
        var service = CreateService();
        var items = ImmutableArray.Create(
            new ShoppingListItem(34, "Tritanium", "Mineral", 1000, 0.01, 10.0, null, null,
                ImmutableArray<MaterialSource>.Empty));

        var csv = service.GenerateCsv(items);

        csv.Should().Contain("Material Name,Quantity,Category");
        csv.Should().Contain("Tritanium,1000,Mineral");
        csv.Should().NotContain("Estimated Unit Cost");
    }

    [Fact]
    public void GenerateCsv_IncludesCostColumns_WhenCostsAvailable()
    {
        var service = CreateService();
        var items = ImmutableArray.Create(
            new ShoppingListItem(34, "Tritanium", "Mineral", 1000, 0.01, 10.0, 5.50m, 5500.00m,
                ImmutableArray<MaterialSource>.Empty));

        var csv = service.GenerateCsv(items);

        csv.Should().Contain("Estimated Unit Cost");
        csv.Should().Contain("5.50");
        csv.Should().Contain("5500.00");
    }

    [Fact]
    public void GenerateCsv_EscapesCommasAndQuotes()
    {
        var service = CreateService();
        var items = ImmutableArray.Create(
            new ShoppingListItem(34, "Trit, \"Special\"", "Mineral", 100, 0, 0, null, null,
                ImmutableArray<MaterialSource>.Empty));

        var csv = service.GenerateCsv(items);

        csv.Should().Contain("\"Trit, \"\"Special\"\"\"");
    }

    // === T038: GenerateClipboardText Tests ===

    [Fact]
    public void GenerateClipboardText_UsesTabSeparators()
    {
        var service = CreateService();
        var items = ImmutableArray.Create(
            new ShoppingListItem(34, "Tritanium", "Mineral", 1000, 0.01, 10.0, null, null,
                ImmutableArray<MaterialSource>.Empty));

        var text = service.GenerateClipboardText(items);

        text.Should().Contain("Material Name\tQuantity\tCategory");
        text.Should().Contain("Tritanium\t1000\tMineral");
    }

    [Fact]
    public void GenerateClipboardText_IncludesCosts_WhenAvailable()
    {
        var service = CreateService();
        var items = ImmutableArray.Create(
            new ShoppingListItem(34, "Tritanium", "Mineral", 1000, 0.01, 10.0, 5.50m, 5500.00m,
                ImmutableArray<MaterialSource>.Empty));

        var text = service.GenerateClipboardText(items);

        text.Should().Contain("Estimated Unit Cost");
        text.Should().Contain("5.50");
    }

    // === T041: Source Details Tests ===

    [Fact]
    public void AggregateMaterials_SingleSource_HasExactlyOneSourceEntry()
    {
        var service = CreateService();

        var tree = new MaterialTreeNode(200, "Product", 0, 0, 1, 0, true, 100,
            ImmutableArray.Create(
                new MaterialTreeNode(34, "Tritanium", 100, 90, 1, 90, false, 100,
                    ImmutableArray<MaterialTreeNode>.Empty)));

        var result = service.AggregateMaterials(ImmutableArray.Create(tree));

        result[0].Sources.Should().HaveCount(1);
        result[0].Sources[0].BlueprintTypeId.Should().Be(100);
        result[0].Sources[0].Quantity.Should().Be(90);
    }

    [Fact]
    public void AggregateMaterials_MultipleSources_TracksAllContributions()
    {
        var service = CreateService();

        var tree1 = new MaterialTreeNode(200, "Product A", 0, 0, 1, 0, true, 100,
            ImmutableArray.Create(
                new MaterialTreeNode(34, "Tritanium", 100, 90, 1, 90, false, 100,
                    ImmutableArray<MaterialTreeNode>.Empty)));
        var tree2 = new MaterialTreeNode(300, "Product B", 0, 0, 1, 0, true, 101,
            ImmutableArray.Create(
                new MaterialTreeNode(34, "Tritanium", 200, 180, 1, 180, false, 101,
                    ImmutableArray<MaterialTreeNode>.Empty)));
        var tree3 = new MaterialTreeNode(400, "Product C", 0, 0, 1, 0, true, 102,
            ImmutableArray.Create(
                new MaterialTreeNode(34, "Tritanium", 50, 45, 1, 45, false, 102,
                    ImmutableArray<MaterialTreeNode>.Empty)));

        var result = service.AggregateMaterials(ImmutableArray.Create(tree1, tree2, tree3));

        result[0].Sources.Should().HaveCount(3);
        result[0].TotalQuantity.Should().Be(315); // 90+180+45
    }
}
