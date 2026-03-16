using System.Collections.Immutable;
using System.Text.Json;
using EveMarketAnalysisClient.Models;
using FluentAssertions;

namespace EveMarketAnalysisClient.Tests.Models;

public class ProductionBatchExportTests
{
    private static readonly JsonSerializerOptions CamelCase = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // T002: Serialization round-trip tests
    [Fact]
    public void ProductionBatchExport_RoundTrips_ThroughJsonSerializer()
    {
        var items = ImmutableArray.Create(
            new BlueprintRunExport(691, "Rifter", 5),
            new BlueprintRunExport(11379, "Raven", 2));
        var original = new ProductionBatchExport(items);

        var json = JsonSerializer.Serialize(original, CamelCase);
        var deserialized = JsonSerializer.Deserialize<ProductionBatchExport>(json, CamelCase);

        deserialized.Should().NotBeNull();
        deserialized!.Items.Should().HaveCount(2);
        deserialized.Items[0].TypeId.Should().Be(691);
        deserialized.Items[0].Name.Should().Be("Rifter");
        deserialized.Items[0].Runs.Should().Be(5);
        deserialized.Items[1].TypeId.Should().Be(11379);
        deserialized.Items[1].Name.Should().Be("Raven");
        deserialized.Items[1].Runs.Should().Be(2);
    }

    [Fact]
    public void ProductionBatchExport_Serializes_WithCamelCasePropertyNames()
    {
        var items = ImmutableArray.Create(new BlueprintRunExport(691, "Rifter", 5));
        var batch = new ProductionBatchExport(items);

        var json = JsonSerializer.Serialize(batch, CamelCase);

        json.Should().Contain("\"items\":");
        json.Should().Contain("\"typeId\":");
        json.Should().Contain("\"name\":");
        json.Should().Contain("\"runs\":");
    }

    [Fact]
    public void ProductionBatchExport_ImmutableArray_SerializesAsJsonArray()
    {
        var items = ImmutableArray.Create(
            new BlueprintRunExport(1, "A", 1),
            new BlueprintRunExport(2, "B", 2));
        var batch = new ProductionBatchExport(items);

        var json = JsonSerializer.Serialize(batch, CamelCase);
        var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("items").ValueKind.Should().Be(JsonValueKind.Array);
        doc.RootElement.GetProperty("items").GetArrayLength().Should().Be(2);
    }

    // T003: Runs-clamping tests
    [Fact]
    public void ClampRuns_Zero_ReturnsOne()
    {
        BlueprintRunExport.ClampRuns(0).Should().Be(1);
    }

    [Fact]
    public void ClampRuns_Negative_ReturnsOne()
    {
        BlueprintRunExport.ClampRuns(-5).Should().Be(1);
    }

    [Fact]
    public void ClampRuns_Positive_ReturnsValue()
    {
        BlueprintRunExport.ClampRuns(10).Should().Be(10);
    }

    [Fact]
    public void ClampRuns_One_ReturnsOne()
    {
        BlueprintRunExport.ClampRuns(1).Should().Be(1);
    }

    // T006: Creating from tuples with clamping
    [Fact]
    public void ProductionBatchExport_FromTuples_ClampsZeroRunsToOne()
    {
        var tuples = new[]
        {
            (typeId: 691, name: "Rifter", runs: 0),
            (typeId: 11379, name: "Raven", runs: 5),
            (typeId: 624, name: "Badger", runs: -1)
        };

        var items = tuples.Select(t =>
            new BlueprintRunExport(t.typeId, t.name, BlueprintRunExport.ClampRuns(t.runs)))
            .ToImmutableArray();
        var batch = new ProductionBatchExport(items);

        batch.Items.Should().HaveCount(3);
        batch.Items[0].Runs.Should().Be(1, "runs=0 should be clamped to 1");
        batch.Items[1].Runs.Should().Be(5, "valid runs should be preserved");
        batch.Items[2].Runs.Should().Be(1, "runs=-1 should be clamped to 1");
    }

    // T011: JSON deserialization from raw string
    [Fact]
    public void ProductionBatchExport_Deserializes_FromRawJsonString()
    {
        var json = """{"items":[{"typeId":691,"name":"Rifter","runs":5},{"typeId":11379,"name":"Raven","runs":2},{"typeId":624,"name":"Badger","runs":10}]}""";

        var batch = JsonSerializer.Deserialize<ProductionBatchExport>(json, CamelCase);

        batch.Should().NotBeNull();
        batch!.Items.Should().HaveCount(3);
        batch.Items[0].TypeId.Should().Be(691);
        batch.Items[0].Name.Should().Be("Rifter");
        batch.Items[0].Runs.Should().Be(5);
        batch.Items[1].TypeId.Should().Be(11379);
        batch.Items[1].Name.Should().Be("Raven");
        batch.Items[1].Runs.Should().Be(2);
        batch.Items[2].TypeId.Should().Be(624);
        batch.Items[2].Name.Should().Be("Badger");
        batch.Items[2].Runs.Should().Be(10);
    }

    [Fact]
    public void ProductionBatchExport_Deserializes_EmptyItemsArray()
    {
        var json = """{"items":[]}""";

        var batch = JsonSerializer.Deserialize<ProductionBatchExport>(json, CamelCase);

        batch.Should().NotBeNull();
        batch!.Items.Should().BeEmpty();
    }

    // T015: Merge logic tests
    [Fact]
    public void MergeSelections_PreservesExistingItemsNotInIncoming()
    {
        var existing = ImmutableArray.Create(
            new BlueprintRunExport(1, "A", 3),
            new BlueprintRunExport(2, "B", 5));
        var incoming = ImmutableArray.Create(
            new BlueprintRunExport(3, "C", 7));

        var result = ProductionBatchExport.MergeSelections(existing, incoming);

        result.Should().HaveCount(3);
        result.Should().Contain(r => r.TypeId == 1 && r.Runs == 3);
        result.Should().Contain(r => r.TypeId == 2 && r.Runs == 5);
        result.Should().Contain(r => r.TypeId == 3 && r.Runs == 7);
    }

    [Fact]
    public void MergeSelections_AddsIncomingItemsNotInExisting()
    {
        var existing = ImmutableArray<BlueprintRunExport>.Empty;
        var incoming = ImmutableArray.Create(
            new BlueprintRunExport(1, "A", 3));

        var result = ProductionBatchExport.MergeSelections(existing, incoming);

        result.Should().HaveCount(1);
        result[0].TypeId.Should().Be(1);
        result[0].Runs.Should().Be(3);
    }

    [Fact]
    public void MergeSelections_IncomingWinsOnConflict()
    {
        var existing = ImmutableArray.Create(
            new BlueprintRunExport(1, "A", 3));
        var incoming = ImmutableArray.Create(
            new BlueprintRunExport(1, "A", 10));

        var result = ProductionBatchExport.MergeSelections(existing, incoming);

        result.Should().HaveCount(1);
        result[0].TypeId.Should().Be(1);
        result[0].Runs.Should().Be(10, "incoming should win on conflict");
    }

    [Fact]
    public void MergeSelections_NoDuplicateTypeIds()
    {
        var existing = ImmutableArray.Create(
            new BlueprintRunExport(1, "A", 3),
            new BlueprintRunExport(2, "B", 5));
        var incoming = ImmutableArray.Create(
            new BlueprintRunExport(2, "B", 8),
            new BlueprintRunExport(3, "C", 7));

        var result = ProductionBatchExport.MergeSelections(existing, incoming);

        result.Select(r => r.TypeId).Should().OnlyHaveUniqueItems();
        result.Should().HaveCount(3);
    }
}
