using System.Collections.Immutable;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services;
using FluentAssertions;

namespace EveMarketAnalysisClient.Tests.Unit;

public class PhaseServiceTests
{
    private readonly PhaseService _service = new();

    [Fact]
    public void GetAllPhases_ReturnsFivePhases()
    {
        var phases = _service.GetAllPhases();

        phases.Should().HaveCount(5);
    }

    [Fact]
    public void GetAllPhases_PhasesAreOrderedByPhaseNumber()
    {
        var phases = _service.GetAllPhases();

        phases.Select(p => p.PhaseNumber).Should().BeInAscendingOrder();
    }

    [Fact]
    public void GetAllPhases_EachPhaseHasNonEmptyCandidateTypeIds()
    {
        var phases = _service.GetAllPhases();

        foreach (var phase in phases)
        {
            phase.CandidateTypeIds.Should().NotBeEmpty(
                $"Phase {phase.PhaseNumber} ({phase.Name}) should have candidate type IDs");
        }
    }

    [Fact]
    public void GetAllPhases_EachPhaseHasNameAndDescription()
    {
        var phases = _service.GetAllPhases();

        foreach (var phase in phases)
        {
            phase.Name.Should().NotBeNullOrWhiteSpace();
            phase.Description.Should().NotBeNullOrWhiteSpace();
        }
    }

    [Fact]
    public void GetAllPhases_PhaseNumbersAreContinuous()
    {
        var phases = _service.GetAllPhases();

        phases.Select(p => p.PhaseNumber).Should().BeEquivalentTo(new[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public void GetPhaseForTypeId_ReturnsCorrectPhase_ForKnownTypeId()
    {
        // 691 is a Phase 1 frigate blueprint
        var phase = _service.GetPhaseForTypeId(691);

        phase.Should().NotBeNull();
        phase!.PhaseNumber.Should().Be(1);
    }

    [Fact]
    public void GetPhaseForTypeId_ReturnsNull_ForUnknownTypeId()
    {
        var phase = _service.GetPhaseForTypeId(99999999);

        phase.Should().BeNull();
    }

    [Fact]
    public void GetPhaseForTypeId_ReturnsCorrectPhase_ForDifferentPhases()
    {
        // Phase 2 destroyer
        var phase2 = _service.GetPhaseForTypeId(686);
        phase2.Should().NotBeNull();
        phase2!.PhaseNumber.Should().Be(2);

        // Phase 3 cruiser
        var phase3 = _service.GetPhaseForTypeId(946);
        phase3.Should().NotBeNull();
        phase3!.PhaseNumber.Should().Be(3);

        // Phase 4 battleship
        var phase4 = _service.GetPhaseForTypeId(688);
        phase4.Should().NotBeNull();
        phase4!.PhaseNumber.Should().Be(4);
    }

    [Fact]
    public void GetAllPhases_ReturnsSameData_OnRepeatedCalls()
    {
        var phases1 = _service.GetAllPhases();
        var phases2 = _service.GetAllPhases();

        // ImmutableArray is a struct — compare by content
        phases1.Should().BeEquivalentTo(phases2);
        phases1.Length.Should().Be(phases2.Length);
    }
}
