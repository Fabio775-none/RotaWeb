using RotaWeb.Api.Security;
using Xunit;

namespace RotaWeb.Tests;

public sealed class SessionTokenServiceTests
{
    private readonly SessionTokenService _sut = new();

    [Fact]
    public void CreateToken_GeneratesUrlSafeRandom256BitToken()
    {
        var tokens = Enumerable.Range(0, 128).Select(_ => _sut.CreateToken()).ToArray();
        Assert.Equal(128, tokens.Distinct().Count());
        Assert.All(tokens, token =>
        {
            Assert.Equal(43, token.Length);
            Assert.Matches("^[A-Za-z0-9_-]+$", token);
        });
    }

    [Fact]
    public void HashToken_IsDeterministicAndDoesNotRevealToken()
    {
        const string token = "sensitive-test-token";
        var hash = _sut.HashToken(token);
        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, _sut.HashToken(token));
        Assert.NotEqual(token, hash);
        Assert.NotEqual(hash, _sut.HashToken(token + "x"));
    }

    [Fact]
    public void HashUserAgent_HandlesEmptyAndVeryLongHeaders()
    {
        Assert.Null(_sut.HashUserAgent(null));
        Assert.Null(_sut.HashUserAgent(" "));
        Assert.Equal(_sut.HashUserAgent(new string('x', 512)), _sut.HashUserAgent(new string('x', 2048)));
    }
}
