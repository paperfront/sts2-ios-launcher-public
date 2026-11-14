namespace STS2Mobile.Platform;

/// <summary>
/// The game's full-screen automatic canvas. Safe-area insets belong to the HUD, not to the world canvas:
/// letterboxing here would also shrink backgrounds, the map and combat. The host insets edge controls separately.
/// </summary>
public static unsafe class SafeAreaCanvas
{
    // Godot's Window.ContentScaleAspectEnum, whose underlying type is long.
    public const long Keep = 1, KeepWidth = 2, KeepHeight = 3, Expand = 4;

    // The game's own canvas sizes for its automatic aspect ratio (NGlobalUi.OnWindowChange).
    private const int BaseWidth = 1680, BaseHeight = 1080, WidestWidth = 2580, TallestHeight = 1260;
    private const float WidestRatio = 2.3888888f, NarrowestRatio = 1.3333334f;

    private static readonly delegate* unmanaged<double> SideInsetFunction = (delegate* unmanaged<double>)NativeExports.Find("sts2_screen_side_inset");

    /// <summary>
    /// The larger of the left and right safe-area insets as a fraction of the screen's width, from <c>native/orientation_guard.mm</c>.
    /// 0 off the phone, in portrait, and on a screen with no notch or island.
    /// </summary>
    public static double SideInset() => SideInsetFunction != null ? SideInsetFunction() : 0;

    /// <summary>The canvas for a window of this many pixels: the aspect mode to set, and the canvas size through the out values.</summary>
    public static long Layout(int windowWidth, int windowHeight, out int canvasWidth, out int canvasHeight) =>
        Choose(windowWidth, windowHeight, 0, out canvasWidth, out canvasHeight);

    /// <summary>
    /// Keep the game's full-screen rule even when a side inset exists. The inset parameter is retained for callers of older builds.
    /// </summary>
    public static long Choose(int windowWidth, int windowHeight, double sideInset, out int canvasWidth, out int canvasHeight)
    {
        float shape = windowHeight > 0 ? (float)windowWidth / windowHeight : 0;
        if (shape > WidestRatio)
        {
            (canvasWidth, canvasHeight) = (WidestWidth, BaseHeight);
            return KeepWidth;
        }
        if (shape < NarrowestRatio)
        {
            (canvasWidth, canvasHeight) = (BaseWidth, TallestHeight);
            return KeepHeight;
        }
        (canvasWidth, canvasHeight) = (BaseWidth, BaseHeight);
        return Expand;
    }
}
