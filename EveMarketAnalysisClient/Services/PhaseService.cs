using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services.Interfaces;

namespace EveMarketAnalysisClient.Services;

public class PhaseService : IPhaseService
{
    private readonly Lazy<ImmutableArray<PhaseDefinition>> _phases;
    private readonly Lazy<Dictionary<int, PhaseDefinition>> _typeIdToPhase;

    public PhaseService()
    {
        _phases = new Lazy<ImmutableArray<PhaseDefinition>>(LoadPhases);
        _typeIdToPhase = new Lazy<Dictionary<int, PhaseDefinition>>(() =>
        {
            var map = new Dictionary<int, PhaseDefinition>();
            foreach (var phase in _phases.Value)
                foreach (var typeId in phase.CandidateTypeIds)
                    map[typeId] = phase;
            return map;
        });
    }

    public ImmutableArray<PhaseDefinition> GetAllPhases() => _phases.Value;

    public PhaseDefinition? GetPhaseForTypeId(int blueprintTypeId)
    {
        _typeIdToPhase.Value.TryGetValue(blueprintTypeId, out var phase);
        return phase;
    }

    private static ImmutableArray<PhaseDefinition> LoadPhases()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = "EveMarketAnalysisClient.Data.phases.json";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Embedded resource '{resourceName}' not found.");

        using var document = JsonDocument.Parse(stream);
        var phasesArray = document.RootElement.GetProperty("phases");

        return phasesArray.EnumerateArray()
            .Select(element => new PhaseDefinition(
                PhaseNumber: element.GetProperty("phaseNumber").GetInt32(),
                Name: element.GetProperty("name").GetString()!,
                Description: element.GetProperty("description").GetString()!,
                CandidateTypeIds: element.GetProperty("candidateTypeIds")
                    .EnumerateArray()
                    .Select(id => id.GetInt32())
                    .ToImmutableArray()))
            .OrderBy(p => p.PhaseNumber)
            .ToImmutableArray();
    }
}
