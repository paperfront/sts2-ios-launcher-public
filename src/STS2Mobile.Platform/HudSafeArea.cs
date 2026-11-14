namespace STS2Mobile.Platform;

/// <summary>Maps a HUD anchor into the safe part of its parent, preserving the control's own offsets and size.</summary>
public static class HudSafeArea
{
    public static float Anchor(float anchor, float parentLength, float safeStart, float safeEnd)
    {
        if (!float.IsFinite(parentLength) || parentLength <= 0 || !float.IsFinite(safeStart) || !float.IsFinite(safeEnd)) return anchor;
        float start = Math.Clamp(safeStart, 0, parentLength);
        float end = Math.Clamp(safeEnd, 0, parentLength);
        // UIKit can briefly report no safe rectangle during rotation. Keep the current layout until it settles.
        if (end <= start) return anchor;
        return (start + anchor * (end - start)) / parentLength;
    }
}
