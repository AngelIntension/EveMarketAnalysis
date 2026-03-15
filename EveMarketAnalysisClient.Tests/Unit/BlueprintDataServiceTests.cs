using EveMarketAnalysisClient.Services;
using FluentAssertions;

namespace EveMarketAnalysisClient.Tests.Unit;

public class BlueprintDataServiceTests
{
    [Fact]
    public void GetAllBlueprintActivities_ReturnsNonEmptyDictionary()
    {
        var service = new BlueprintDataService();

        var activities = service.GetAllBlueprintActivities();

        activities.Should().NotBeEmpty();
    }

    [Fact]
    public void GetBlueprintActivity_ValidTypeId_ReturnsBlueprintActivity()
    {
        var service = new BlueprintDataService();

        var activity = service.GetBlueprintActivity(691); // Rifter Blueprint (type ID 691 produces Rifter 587)

        activity.Should().NotBeNull();
        activity!.ProducedTypeId.Should().Be(587);
        activity.Materials.Should().NotBeEmpty();
        activity.BaseTime.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetBlueprintActivity_UnknownTypeId_ReturnsNull()
    {
        var service = new BlueprintDataService();

        var activity = service.GetBlueprintActivity(999999);

        activity.Should().BeNull();
    }

    [Fact]
    public void GetBlueprintActivity_HasCorrectMaterialData()
    {
        var service = new BlueprintDataService();

        var activity = service.GetBlueprintActivity(691); // Rifter Blueprint

        activity.Should().NotBeNull();
        // Rifter blueprint should have Tritanium (34) as a material
        activity!.Materials.Should().Contain(m => m.TypeId == 34);
        activity.Materials.Should().AllSatisfy(m =>
        {
            m.BaseQuantity.Should().BeGreaterThan(0);
            m.TypeId.Should().BeGreaterThan(0);
        });
    }

    [Fact]
    public void GetAllBlueprintActivities_ContainsExpectedBlueprints()
    {
        var service = new BlueprintDataService();

        var activities = service.GetAllBlueprintActivities();

        // Blueprint type IDs (not produced item IDs)
        activities.Should().ContainKey(691);     // Rifter Blueprint
        activities.Should().ContainKey(24691);   // Thorax Blueprint
        activities.Should().ContainKey(16241);   // Catalyst Blueprint
    }
}
