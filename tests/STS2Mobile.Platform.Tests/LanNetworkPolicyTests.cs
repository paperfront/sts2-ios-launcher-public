using STS2Mobile.Platform;

namespace STS2Mobile.Platform.Tests;

public sealed class LanNetworkPolicyTests
{
    [Theory]
    [InlineData("10.0.0.1")]
    [InlineData("172.16.0.1")]
    [InlineData("172.31.255.254")]
    [InlineData("192.168.1.2")]
    [InlineData("100.64.0.1")]
    [InlineData("100.127.255.254")]
    [InlineData("fd7a:115c:a1e0::1234")]
    public void PrivateAndTailscalePeersAreAllowed(string host) => Assert.True(LanNetworkPolicy.IsAllowedAddress(host));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("8.8.8.8")]
    [InlineData("100.63.255.255")]
    [InlineData("100.128.0.0")]
    [InlineData("172.15.1.1")]
    [InlineData("172.32.0.1")]
    [InlineData("127.0.0.1")]
    [InlineData("0.0.0.0")]
    [InlineData("169.254.169.254")]
    [InlineData("224.0.0.1")]
    [InlineData("255.255.255.255")]
    [InlineData("example.com")]
    [InlineData("10.1")]
    [InlineData("167772161")]
    [InlineData("010.0.0.1")]
    [InlineData(" 10.0.0.1")]
    [InlineData("::ffff:192.168.1.2")]
    [InlineData("fe80::1%en0")]
    [InlineData("fd7a:115c:a1e1::1")]
    [InlineData("https://192.168.1.2")]
    public void OtherDestinationsAreRejected(string? host) => Assert.False(LanNetworkPolicy.IsAllowedAddress(host));

    [Theory]
    [InlineData("192.168.1.2", "192.168.1.2", 33771)]
    [InlineData(" 100.64.1.2:33772 ", "100.64.1.2", 33772)]
    [InlineData("[fd7a:115c:a1e0::1]:33771", "fd7a:115c:a1e0::1", 33771)]
    [InlineData("fd7a:115c:a1e0::1", "fd7a:115c:a1e0::1", 33771)]
    public void ManualAddressesAreParsed(string input, string host, int port)
    {
        Assert.True(LanNetworkPolicy.TryParse(input, out string parsed, out int parsedPort));
        Assert.Equal((host, port), (parsed, parsedPort));
    }

    [Theory]
    [InlineData("192.168.1.2:0")]
    [InlineData("192.168.1.2:65536")]
    [InlineData("192.168.1.2:-1")]
    [InlineData("192.168.1.2:")]
    [InlineData("192.168.1.2:+33771")]
    [InlineData("[fd7a:115c:a1e0::1]garbage")]
    [InlineData("[fd7a:115c:a1e0::1")]
    [InlineData("public.example:33771")]
    [InlineData("8.8.8.8:33771")]
    public void InvalidEntriesAreRejected(string input) => Assert.False(LanNetworkPolicy.TryParse(input, out _, out _));

    [Fact]
    public void RuntimeGuardAlsoRejectsPublicDestinationsAndInvalidPorts()
    {
        Assert.Throws<NotSupportedException>(() => LanNetworkPolicy.RequirePeer("8.8.8.8", 33771));
        Assert.Throws<NotSupportedException>(() => LanNetworkPolicy.RequirePeer("192.168.1.2", 0));
        LanNetworkPolicy.RequirePeer("100.64.1.2", 33771);
    }

    [Fact]
    public void HostingBindsOnlyTheSelectedPrivateInterfaceOnTheNativePort()
    {
        LanNetworkPolicy.SelectHostAddress("192.168.1.20");
        Assert.Equal("192.168.1.20", LanNetworkPolicy.ResolveListenAddress("*", 33771));
        Assert.Equal("192.168.1.20", LanNetworkPolicy.ResolveListenAddress("192.168.1.20", 33771));
        Assert.Throws<NotSupportedException>(() => LanNetworkPolicy.SelectHostAddress("8.8.8.8"));
        Assert.Throws<NotSupportedException>(() => LanNetworkPolicy.ResolveListenAddress("*", 33772));
        Assert.Throws<NotSupportedException>(() => LanNetworkPolicy.ResolveListenAddress("0.0.0.0", 33771));
        Assert.Throws<NotSupportedException>(() => LanNetworkPolicy.ResolveListenAddress("192.168.1.99", 33771));
        LanNetworkPolicy.SelectHostAddress("100.64.1.2");
        Assert.Equal("100.64.1.2", LanNetworkPolicy.ResolveListenAddress("*", 33771));
        LanNetworkPolicy.SelectHostAddress("fd7a:115c:a1e0::1");
        Assert.Equal("fd7a:115c:a1e0::1", LanNetworkPolicy.ResolveListenAddress("*", 33771));
    }

    [Theory]
    [InlineData("fastmp", true)]
    [InlineData("other", false)]
    [InlineData("", false)]
    [InlineData("fastmp-other", false)]
    public void OnlyTheFastmpArgumentIsOverridden(string argument, bool expected) =>
        Assert.Equal(expected, LanNetworkPolicy.IsFastMultiplayerArgument(argument));
}
