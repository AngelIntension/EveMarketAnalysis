using System.Security.Cryptography;
using System.Text;
using EveMarketAnalysisClient.Services;
using FluentAssertions;

namespace EveMarketAnalysisClient.Tests.Unit;

public class PkceServiceTests
{
    [Fact]
    public void GenerateCodeVerifier_ReturnsCorrectLength()
    {
        var verifier = PkceService.GenerateCodeVerifier();

        verifier.Length.Should().BeInRange(43, 128);
    }

    [Fact]
    public void GenerateCodeVerifier_ContainsOnlyUnreservedCharacters()
    {
        var verifier = PkceService.GenerateCodeVerifier();

        verifier.Should().MatchRegex(@"^[A-Za-z0-9\-._~]+$");
    }

    [Fact]
    public void GenerateCodeVerifier_ProducesDifferentValuesEachCall()
    {
        var verifier1 = PkceService.GenerateCodeVerifier();
        var verifier2 = PkceService.GenerateCodeVerifier();

        verifier1.Should().NotBe(verifier2);
    }

    [Fact]
    public void GenerateCodeChallenge_ProducesCorrectSha256Base64Url()
    {
        // RFC 7636 Appendix B test vector
        var verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";

        var challenge = PkceService.GenerateCodeChallenge(verifier);

        challenge.Should().Be("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM");
    }

    [Fact]
    public void GenerateCodeChallenge_DoesNotContainPaddingOrUnsafeChars()
    {
        var verifier = PkceService.GenerateCodeVerifier();

        var challenge = PkceService.GenerateCodeChallenge(verifier);

        challenge.Should().NotContain("=");
        challenge.Should().NotContain("+");
        challenge.Should().NotContain("/");
    }

    [Fact]
    public void GenerateState_ProducesNonEmptyString()
    {
        var state = PkceService.GenerateState();

        state.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GenerateState_ProducesDifferentValuesEachCall()
    {
        var state1 = PkceService.GenerateState();
        var state2 = PkceService.GenerateState();

        state1.Should().NotBe(state2);
    }
}
