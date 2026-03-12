using System.Collections.Immutable;
using EveMarketAnalysisClient.Models;
using EveMarketAnalysisClient.Services;
using FluentAssertions;

namespace EveMarketAnalysisClient.Tests.Unit;

public class SkillFilterServiceTests
{
    private static readonly ImmutableArray<CharacterSkill> TestSkills = ImmutableArray.Create(
        new CharacterSkill(3380, "Industry", 5, 256000),
        new CharacterSkill(3388, "Science", 4, 135765),
        new CharacterSkill(3443, "Gunnery", 5, 256000),
        new CharacterSkill(16622, "Accounting", 4, 135765),
        new CharacterSkill(25544, "Reactions", 3, 40000));

    private static readonly Dictionary<int, (string GroupName, int GroupId)> TestGroupMapping = new()
    {
        [3380] = ("Industry", 268),
        [3388] = ("Science", 270),
        [3443] = ("Gunnery", 255),
        [16622] = ("Trade", 274),
        [25544] = ("Resource Processing", 1218)
    };

    private readonly SkillFilterService _service = new();

    [Fact]
    public void FilterToRelevantGroups_FiltersToCorrectSkillGroups()
    {
        var relevantGroups = new HashSet<string>
        {
            "Science", "Industry", "Trade", "Resource Processing", "Planet Management", "Social"
        };

        var filtered = _service.FilterToRelevantGroups(TestSkills, TestGroupMapping, relevantGroups);

        filtered.Should().HaveCount(4); // Industry, Science, Accounting (Trade), Reactions (Resource Processing)
        filtered.Should().NotContain(s => s.SkillName == "Gunnery");
    }

    [Fact]
    public void GroupByCategory_GroupsSkillsByCategory()
    {
        var grouped = _service.GroupByCategory(TestSkills, TestGroupMapping);

        grouped.Should().HaveCount(5); // Industry, Science, Gunnery, Trade, Resource Processing (unique groups)
        grouped.Should().Contain(g => g.GroupName == "Industry");
        grouped.Should().Contain(g => g.GroupName == "Science");
    }

    [Fact]
    public void GroupByCategory_CalculatesSpTotalsPerGroup()
    {
        var grouped = _service.GroupByCategory(TestSkills, TestGroupMapping);

        var industryGroup = grouped.First(g => g.GroupName == "Industry");
        industryGroup.TotalSp.Should().Be(256000);
    }

    [Fact]
    public void FilterToRelevantGroups_HandlesEmptySkillList()
    {
        var relevantGroups = new HashSet<string> { "Science", "Industry" };

        var filtered = _service.FilterToRelevantGroups(
            ImmutableArray<CharacterSkill>.Empty,
            TestGroupMapping,
            relevantGroups);

        filtered.Should().BeEmpty();
    }

    [Fact]
    public void FilterToRelevantGroups_HandlesCharacterWithNoRelevantSkills()
    {
        var skills = ImmutableArray.Create(
            new CharacterSkill(3443, "Gunnery", 5, 256000));

        var relevantGroups = new HashSet<string> { "Science", "Industry", "Trade" };

        var filtered = _service.FilterToRelevantGroups(skills, TestGroupMapping, relevantGroups);

        filtered.Should().BeEmpty();
    }
}
