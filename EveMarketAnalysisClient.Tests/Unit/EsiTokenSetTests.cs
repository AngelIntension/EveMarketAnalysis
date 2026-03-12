using System.Collections.Immutable;
using EveMarketAnalysisClient.Models;
using FluentAssertions;

namespace EveMarketAnalysisClient.Tests.Unit;

public class EsiTokenSetTests
{
    private static EsiTokenSet CreateTokenSet(DateTimeOffset expiresAt) =>
        new(
            AccessToken: "access-token",
            RefreshToken: "refresh-token",
            ExpiresAt: expiresAt,
            CharacterId: 12345,
            CharacterName: "Test Character",
            Scopes: ImmutableArray.Create("esi-skills.read_skills.v1"));

    [Fact]
    public void IsExpired_ReturnsFalse_WhenTokenNotExpired()
    {
        var tokenSet = CreateTokenSet(DateTimeOffset.UtcNow.AddMinutes(10));

        tokenSet.IsExpired.Should().BeFalse();
    }

    [Fact]
    public void IsExpired_ReturnsTrue_WhenTokenExpired()
    {
        var tokenSet = CreateTokenSet(DateTimeOffset.UtcNow.AddMinutes(-1));

        tokenSet.IsExpired.Should().BeTrue();
    }

    [Fact]
    public void Record_SupportsWithExpressions()
    {
        var original = CreateTokenSet(DateTimeOffset.UtcNow.AddMinutes(10));

        var updated = original with { AccessToken = "new-access-token" };

        updated.AccessToken.Should().Be("new-access-token");
        updated.RefreshToken.Should().Be(original.RefreshToken);
        updated.CharacterId.Should().Be(original.CharacterId);
    }

    [Fact]
    public void Record_IsImmutable()
    {
        var tokenSet = CreateTokenSet(DateTimeOffset.UtcNow.AddMinutes(10));

        var sameTokenSet = tokenSet with { };

        sameTokenSet.Should().Be(tokenSet);
        ReferenceEquals(sameTokenSet, tokenSet).Should().BeFalse();
    }
}
