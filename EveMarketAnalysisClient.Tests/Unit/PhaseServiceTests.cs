using System.Collections.Immutable;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services;
using EveMarketAnalysisClient.Services.Interfaces;
using FluentAssertions;
using Moq;

namespace EveMarketAnalysisClient.Tests.Unit;

public class PhaseServiceTests
{
    private static PhaseService CreateService(Mock<IBlueprintDataService>? blueprintData = null)
    {
        blueprintData ??= new Mock<IBlueprintDataService>();

        if (!blueprintData.Setups.Any())
        {
            // Default: provide a mix of blueprints at different build times
            var activities = new Dictionary<int, BlueprintActivity>
            {
                // Phase 1: < 900s
                [100] = new(100, 200, 100, 300, ImmutableArray.Create(new MaterialRequirement(34, "", 100, 0))),
                [101] = new(101, 201, 1, 600, ImmutableArray.Create(new MaterialRequirement(34, "", 50, 0))),
                // Phase 2: 900-7200s
                [102] = new(102, 202, 1, 3600, ImmutableArray.Create(new MaterialRequirement(34, "", 500, 0))),
                [103] = new(103, 203, 1, 6000, ImmutableArray.Create(new MaterialRequirement(34, "", 1000, 0))),
                // Phase 3: 7200-21600s
                [104] = new(104, 204, 1, 12000, ImmutableArray.Create(new MaterialRequirement(34, "", 5000, 0))),
                // Phase 4: 21600-86400s
                [105] = new(105, 205, 1, 36000, ImmutableArray.Create(new MaterialRequirement(34, "", 10000, 0))),
                // Phase 5: > 86400s
                [106] = new(106, 206, 1, 100000, ImmutableArray.Create(new MaterialRequirement(34, "", 50000, 0))),
            };

            blueprintData.Setup(b => b.GetAllBlueprintActivities())
                .Returns(activities);
            blueprintData.Setup(b => b.GetBlueprintActivity(It.IsAny<int>()))
                .Returns((int id) => activities.GetValueOrDefault(id));
        }

        return new PhaseService(blueprintData.Object);
    }

    [Fact]
    public void GetAllPhases_ReturnsFivePhases()
    {
        var service = CreateService();
        var phases = service.GetAllPhases();

        phases.Should().HaveCount(5);
    }

    [Fact]
    public void GetAllPhases_PhasesAreOrderedByPhaseNumber()
    {
        var service = CreateService();
        var phases = service.GetAllPhases();

        phases.Select(p => p.PhaseNumber).Should().BeInAscendingOrder();
    }

    [Fact]
    public void GetAllPhases_EachPhaseHasNameAndDescription()
    {
        var service = CreateService();
        var phases = service.GetAllPhases();

        foreach (var phase in phases)
        {
            phase.Name.Should().NotBeNullOrWhiteSpace();
            phase.Description.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void GetAllPhases_PhaseNumbersAreContinuous()
    {
        var service = CreateService();
        var phases = service.GetAllPhases();

        phases.Select(p => p.PhaseNumber).Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public void GetPhaseForTypeId_ClassifiesByBuildTime()
    {
        var service = CreateService();

        // Phase 1: 300s build time (< 900s)
        service.GetPhaseForTypeId(100)!.PhaseNumber.Should().Be(1);
        service.GetPhaseForTypeId(101)!.PhaseNumber.Should().Be(1);

        // Phase 2: 3600s and 6000s (900-7200s)
        service.GetPhaseForTypeId(102)!.PhaseNumber.Should().Be(2);
        service.GetPhaseForTypeId(103)!.PhaseNumber.Should().Be(2);

        // Phase 3: 12000s (7200-21600s)
        service.GetPhaseForTypeId(104)!.PhaseNumber.Should().Be(3);

        // Phase 4: 36000s (21600-86400s)
        service.GetPhaseForTypeId(105)!.PhaseNumber.Should().Be(4);

        // Phase 5: 100000s (> 86400s)
        service.GetPhaseForTypeId(106)!.PhaseNumber.Should().Be(5);
    }

    [Fact]
    public void GetPhaseForTypeId_ReturnsNull_ForUnknownTypeId()
    {
        var service = CreateService();
        var phase = service.GetPhaseForTypeId(99999999);

        phase.Should().BeNull();
    }

    [Fact]
    public void GetCandidateTypeIdsForPhase_ReturnsCorrectIds()
    {
        var service = CreateService();

        var phase1Candidates = service.GetCandidateTypeIdsForPhase(1);
        phase1Candidates.Should().Contain(100);
        phase1Candidates.Should().Contain(101);
        phase1Candidates.Should().NotContain(102); // Phase 2

        var phase2Candidates = service.GetCandidateTypeIdsForPhase(2);
        phase2Candidates.Should().Contain(102);
        phase2Candidates.Should().Contain(103);
    }

    [Fact]
    public void GetAllPhases_ReturnsSameData_OnRepeatedCalls()
    {
        var service = CreateService();
        var phases1 = service.GetAllPhases();
        var phases2 = service.GetAllPhases();

        phases1.Should().BeEquivalentTo(phases2);
        phases1.Length.Should().Be(phases2.Length);
    }

    [Fact]
    public void RealBlueprintData_ClassifiesKnownShips()
    {
        // Use real BlueprintDataService to verify known ships
        var realService = new PhaseService(new BlueprintDataService());

        // Griffin blueprint (683): 6000s build time → Phase 2 (900-7200s)
        var griffin = realService.GetPhaseForTypeId(683);
        griffin.Should().NotBeNull();
        griffin!.PhaseNumber.Should().Be(2);

        // Battleship blueprints (688): 18000s → Phase 3 (7200-21600s)
        var bs = realService.GetPhaseForTypeId(688);
        bs.Should().NotBeNull();
        bs!.PhaseNumber.Should().Be(3);

        // Small ammo (812): 300s → Phase 1 (< 900s)
        var ammo = realService.GetPhaseForTypeId(812);
        ammo.Should().NotBeNull();
        ammo!.PhaseNumber.Should().Be(1);
    }
}
