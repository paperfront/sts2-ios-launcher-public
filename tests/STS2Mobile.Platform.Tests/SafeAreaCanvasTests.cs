namespace STS2Mobile.Platform.Tests;

public class SafeAreaCanvasTests
{
    [Theory]
    // The world canvas fills the display even when the HUD needs insets.
    [InlineData(1920, 1080, SafeAreaCanvas.Expand, 1680, 1080)]
    [InlineData(2532, 1170, SafeAreaCanvas.Expand, 1680, 1080)]
    [InlineData(3840, 1440, SafeAreaCanvas.KeepWidth, 2580, 1080)]
    [InlineData(1080, 1080, SafeAreaCanvas.KeepHeight, 1680, 1260)]
    public void InsetsDoNotLetterboxTheWorld(int width, int height, long aspect, int canvasWidth, int canvasHeight)
    {
        foreach (double inset in new[] { 0, 44.0 / 812, 59.0 / 852, 62.0 / 956 })
        {
            Assert.Equal(aspect, SafeAreaCanvas.Choose(width, height, inset, out int w, out int h));
            Assert.Equal((canvasWidth, canvasHeight), (w, h));
        }
    }

    [Fact]
    public void OffThePhoneThereIsNoInset()
    {
        Assert.Equal(0, SafeAreaCanvas.SideInset());
        Assert.Equal(SafeAreaCanvas.Expand, SafeAreaCanvas.Layout(2532, 1170, out int w, out int h));
        Assert.Equal((1680, 1080), (w, h));
    }
}
