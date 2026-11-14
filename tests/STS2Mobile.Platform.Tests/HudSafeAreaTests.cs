namespace STS2Mobile.Platform.Tests;

public class HudSafeAreaTests
{
    [Theory]
    [InlineData(812, 44)]
    [InlineData(852, 59)]
    [InlineData(956, 62)]
    public void EdgeHudFitsNotchInsetsAtEveryRenderScale(float pointsWide, float inset)
    {
        foreach (float scale in new[] { 2f, 2.5f, 3f })
        {
            float width = pointsWide * scale;
            float start = inset * scale, end = (pointsWide - inset) * scale;
            float left = HudSafeArea.Anchor(0, width, start, end) * width;
            float right = HudSafeArea.Anchor(1, width, start, end) * width;
            Assert.InRange(Math.Abs(left - start), 0, 0.001f);
            Assert.InRange(Math.Abs(right - end), 0, 0.001f);
            Assert.Equal(0.5f, HudSafeArea.Anchor(0.5f, width, start, end), 5);
        }
    }

    [Fact]
    public void FixedSizeBackButtonMovesWithoutChangingItsWidth()
    {
        float newAnchor = HudSafeArea.Anchor(0, 2000, 120, 1880);
        float left = newAnchor * 2000;
        float right = newAnchor * 2000 + 160;
        Assert.Equal(120, left);
        Assert.Equal(160, right - left);
    }

    [Fact]
    public void RotatingToTheOtherSideMirrorsAnAsymmetricSafeArea()
    {
        float left = HudSafeArea.Anchor(0, 1000, 80, 970);
        float mirroredRight = HudSafeArea.Anchor(1, 1000, 30, 920);
        Assert.Equal(1 - left, mirroredRight, 5);
    }

    [Fact]
    public void AParentAlreadyInsideTheSafeAreaKeepsItsAnchors()
    {
        Assert.Equal(0, HudSafeArea.Anchor(0, 200, -300, 900));
        Assert.Equal(1, HudSafeArea.Anchor(1, 200, -300, 900));
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(100, 80, 20)]
    [InlineData(100, float.NaN, 80)]
    public void UnsettledOrInvalidGeometryDoesNotCollapseControls(float size, float start, float end) =>
        Assert.Equal(0.75f, HudSafeArea.Anchor(0.75f, size, start, end));
}
