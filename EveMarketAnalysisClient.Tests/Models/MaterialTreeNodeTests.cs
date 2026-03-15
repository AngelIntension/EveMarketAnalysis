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

namespace EveMarketAnalysisClient.Tests.Models;

public class MaterialTreeNodeTests
{
    private static ShoppingListService CreateService(
        Mock<IBlueprintDataService> blueprintData)
    {
        var esiClient = new Mock<IEsiCharacterClient>();
        var marketClient = new Mock<IEsiMarketClient>();
        var cache = new MemoryCache(new MemoryCacheOptions());
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

    // === T009: Flat Material Expansion ===

    [Fact]
    public void ExpandBlueprintToMaterials_Flat_ReturnsCorrectMaterialTree()
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        blueprintData.Setup(b => b.GetBlueprintActivity(691))
            .Returns(new BlueprintActivity(691, 587, 1, 3600,
                ImmutableArray.Create(
                    new MaterialRequirement(34, "Tritanium", 32000, 0),
                    new MaterialRequirement(35, "Pyerite", 6000, 0),
                    new MaterialRequirement(36, "Mexallon", 2500, 0))));

        var service = CreateService(blueprintData);
        var selection = new BlueprintSelection(691, "Rifter BP", 10, 20, 1, -1, false, false, 587);
        var ownedMap = new Dictionary<int, CharacterBlueprint>().ToFrozenDictionary();

        var result = service.ExpandBlueprintToMaterials(selection, ownedMap);

        result.TypeId.Should().Be(587);
        result.IsExpanded.Should().BeTrue();
        result.Children.Should().HaveCount(3);

        // All children should be leaf nodes (not expanded)
        result.Children.Should().AllSatisfy(c =>
        {
            c.IsExpanded.Should().BeFalse();
            c.Children.Should().BeEmpty();
        });

        // Verify ME 10 applied: max(1, ceil(base * 0.9))
        result.Children.First(c => c.TypeId == 34).AdjustedQuantity.Should().Be(28800);
        result.Children.First(c => c.TypeId == 35).AdjustedQuantity.Should().Be(5400);
        result.Children.First(c => c.TypeId == 36).AdjustedQuantity.Should().Be(2250);
    }

    [Fact]
    public void ExpandBlueprintToMaterials_Flat_CorrectTotalQuantityWithRuns()
    {
        var blueprintData = new Mock<IBlueprintDataService>();
        blueprintData.Setup(b => b.GetBlueprintActivity(691))
            .Returns(new BlueprintActivity(691, 587, 1, 3600,
                ImmutableArray.Create(
                    new MaterialRequirement(34, "Tritanium", 32000, 0))));

        var service = CreateService(blueprintData);
        var selection = new BlueprintSelection(691, "Rifter BP", 10, 20, 5, -1, false, false, 587);
        var ownedMap = new Dictionary<int, CharacterBlueprint>().ToFrozenDictionary();

        var result = service.ExpandBlueprintToMaterials(selection, ownedMap);

        // ME 10: ceil(32000 * 0.9) = 28800, * 5 runs = 144000
        result.Children[0].TotalQuantity.Should().Be(144000);
    }

    // === T021: Recursive Expansion WITH Owned Component ===

    [Fact]
    public void ExpandBlueprintToMaterials_Recursive_ExpandsOwnedComponents()
    {
        var blueprintData = new Mock<IBlueprintDataService>();

        // Ship blueprint (100) requires Component (50) and Tritanium (34)
        blueprintData.Setup(b => b.GetBlueprintActivity(100))
            .Returns(new BlueprintActivity(100, 200, 1, 3600,
                ImmutableArray.Create(
                    new MaterialRequirement(50, "Component", 10, 0),
                    new MaterialRequirement(34, "Tritanium", 1000, 0))));

        // Component blueprint (150) produces Component (50), requires Pyerite (35)
        blueprintData.Setup(b => b.GetBlueprintActivity(150))
            .Returns(new BlueprintActivity(150, 50, 1, 1800,
                ImmutableArray.Create(
                    new MaterialRequirement(35, "Pyerite", 500, 0))));

        var ownedMap = new Dictionary<int, CharacterBlueprint>
        {
            { 50, new CharacterBlueprint(1, 150, "Component BP", 5, 10, -1, false) }
        }.ToFrozenDictionary();

        var service = CreateService(blueprintData);
        var selection = new BlueprintSelection(100, "Ship BP", 10, 20, 1, -1, false, true, 200);

        var result = service.ExpandBlueprintToMaterials(selection, ownedMap);

        // The Component (50) should be expanded
        var componentNode = result.Children.First(c => c.TypeId == 50);
        componentNode.IsExpanded.Should().BeTrue();

        // Tritanium should remain as leaf
        var tritaniumNode = result.Children.First(c => c.TypeId == 34);
        tritaniumNode.IsExpanded.Should().BeFalse();
    }

    // === T022: Recursive Expansion WITHOUT Owned Component ===

    [Fact]
    public void ExpandBlueprintToMaterials_NoOwnedBlueprint_LeavesAsLeaf()
    {
        var blueprintData = new Mock<IBlueprintDataService>();

        blueprintData.Setup(b => b.GetBlueprintActivity(100))
            .Returns(new BlueprintActivity(100, 200, 1, 3600,
                ImmutableArray.Create(
                    new MaterialRequirement(50, "Component", 10, 0),
                    new MaterialRequirement(34, "Tritanium", 1000, 0))));

        // No owned blueprint for Component (50)
        var ownedMap = new Dictionary<int, CharacterBlueprint>().ToFrozenDictionary();

        var service = CreateService(blueprintData);
        var selection = new BlueprintSelection(100, "Ship BP", 10, 20, 1, -1, false, true, 200);

        var result = service.ExpandBlueprintToMaterials(selection, ownedMap);

        // Component should NOT be expanded
        var componentNode = result.Children.First(c => c.TypeId == 50);
        componentNode.IsExpanded.Should().BeFalse();
        componentNode.Children.Should().BeEmpty();
    }

    // === T023: Multi-Level Recursion (3+ levels) ===

    [Fact]
    public void ExpandBlueprintToMaterials_ThreeLevelsDeep_ResolvesFullChain()
    {
        var blueprintData = new Mock<IBlueprintDataService>();

        // Level 1: Ship (100) → Component A (50)
        blueprintData.Setup(b => b.GetBlueprintActivity(100))
            .Returns(new BlueprintActivity(100, 200, 1, 3600,
                ImmutableArray.Create(
                    new MaterialRequirement(50, "Component A", 5, 0))));

        // Level 2: Component A BP (150) produces Component A (50) → Component B (60)
        blueprintData.Setup(b => b.GetBlueprintActivity(150))
            .Returns(new BlueprintActivity(150, 50, 1, 1800,
                ImmutableArray.Create(
                    new MaterialRequirement(60, "Component B", 3, 0))));

        // Level 3: Component B BP (160) produces Component B (60) → Tritanium (34)
        blueprintData.Setup(b => b.GetBlueprintActivity(160))
            .Returns(new BlueprintActivity(160, 60, 1, 900,
                ImmutableArray.Create(
                    new MaterialRequirement(34, "Tritanium", 100, 0))));

        var ownedMap = new Dictionary<int, CharacterBlueprint>
        {
            { 50, new CharacterBlueprint(1, 150, "Component A BP", 10, 10, -1, false) },
            { 60, new CharacterBlueprint(2, 160, "Component B BP", 5, 10, -1, false) }
        }.ToFrozenDictionary();

        var service = CreateService(blueprintData);
        var selection = new BlueprintSelection(100, "Ship BP", 10, 20, 1, -1, false, true, 200);

        var result = service.ExpandBlueprintToMaterials(selection, ownedMap);

        // Component A should be expanded
        var compA = result.Children.First(c => c.TypeId == 50);
        compA.IsExpanded.Should().BeTrue();

        // Should eventually resolve to Tritanium at the leaf level
        // Collecting all leaf TypeIds
        var leafTypeIds = CollectLeafTypeIds(result);
        leafTypeIds.Should().Contain(34); // Tritanium is the final raw material
        leafTypeIds.Should().NotContain(50); // Component A should be expanded away
        leafTypeIds.Should().NotContain(60); // Component B should be expanded away
    }

    // === T024: Cycle Detection ===

    [Fact]
    public void ExpandBlueprintToMaterials_CycleDetection_BreaksCircularDependency()
    {
        var blueprintData = new Mock<IBlueprintDataService>();

        // A (100) requires B (50)
        blueprintData.Setup(b => b.GetBlueprintActivity(100))
            .Returns(new BlueprintActivity(100, 200, 1, 3600,
                ImmutableArray.Create(
                    new MaterialRequirement(50, "Material B", 10, 0))));

        // B BP (150) produces B (50), requires A (200) — circular!
        blueprintData.Setup(b => b.GetBlueprintActivity(150))
            .Returns(new BlueprintActivity(150, 50, 1, 1800,
                ImmutableArray.Create(
                    new MaterialRequirement(200, "Material A", 5, 0))));

        var ownedMap = new Dictionary<int, CharacterBlueprint>
        {
            { 50, new CharacterBlueprint(1, 150, "B Blueprint", 10, 10, -1, false) },
            { 200, new CharacterBlueprint(2, 100, "A Blueprint", 10, 10, -1, false) }
        }.ToFrozenDictionary();

        var service = CreateService(blueprintData);
        var selection = new BlueprintSelection(100, "A Blueprint", 10, 20, 1, -1, false, true, 200);

        // Should NOT throw — cycle should be detected and broken
        var act = () => service.ExpandBlueprintToMaterials(selection, ownedMap);
        act.Should().NotThrow();

        var result = service.ExpandBlueprintToMaterials(selection, ownedMap);

        // The result should have finite depth, not infinite recursion
        result.Should().NotBeNull();
    }

    // === T025: ProduceComponents=false ===

    [Fact]
    public void ExpandBlueprintToMaterials_ProduceComponentsFalse_NoRecursion()
    {
        var blueprintData = new Mock<IBlueprintDataService>();

        blueprintData.Setup(b => b.GetBlueprintActivity(100))
            .Returns(new BlueprintActivity(100, 200, 1, 3600,
                ImmutableArray.Create(
                    new MaterialRequirement(50, "Component", 10, 0))));

        // Component blueprint exists and character owns it
        blueprintData.Setup(b => b.GetBlueprintActivity(150))
            .Returns(new BlueprintActivity(150, 50, 1, 1800,
                ImmutableArray.Create(
                    new MaterialRequirement(34, "Tritanium", 100, 0))));

        var ownedMap = new Dictionary<int, CharacterBlueprint>
        {
            { 50, new CharacterBlueprint(1, 150, "Component BP", 10, 10, -1, false) }
        }.ToFrozenDictionary();

        var service = CreateService(blueprintData);
        // ProduceComponents = false
        var selection = new BlueprintSelection(100, "Ship BP", 10, 20, 1, -1, false, false, 200);

        var result = service.ExpandBlueprintToMaterials(selection, ownedMap);

        // Component should NOT be expanded even though owned
        var componentNode = result.Children.First(c => c.TypeId == 50);
        componentNode.IsExpanded.Should().BeFalse();
        componentNode.Children.Should().BeEmpty();
    }

    private static List<int> CollectLeafTypeIds(MaterialTreeNode node)
    {
        var result = new List<int>();
        if (node.Children.IsEmpty && node.TotalQuantity > 0)
        {
            result.Add(node.TypeId);
        }
        else
        {
            foreach (var child in node.Children)
                result.AddRange(CollectLeafTypeIds(child));
        }
        return result;
    }
}
