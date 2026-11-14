using System;
using System.Collections.Generic;
using Godot;
using STS2Mobile.Platform;

namespace STS2Mobile.GameHost;

/// <summary>Insets edge HUD controls while leaving the game's canvas and artwork full-screen.</summary>
internal sealed partial class PhoneHudSafeArea : Node
{
    private const float BreathingRoom = 12;
    private readonly List<EdgeControl> _controls = new();

    private sealed class EdgeControl(Control control)
    {
        public readonly Control Control = control;
        public readonly Vector4 Anchors = new(control.AnchorLeft, control.AnchorTop, control.AnchorRight, control.AnchorBottom);
        public Rect2? LastSafeArea;
        public Vector2 LastParentSize;
    }

    public override void _Ready()
    {
        if (!OperatingSystem.IsIOS()) return;
        GetTree().NodeAdded += OnNodeAdded;
        GetTree().Root.SizeChanged += Refresh;
        // Safe-area insets can settle after the size event during rotation or foregrounding.
        // Only these few controls are visited; there is no scene-tree traversal or per-frame callback.
        var timer = new Timer { WaitTime = 0.5, Autostart = true, ProcessMode = ProcessModeEnum.Always };
        timer.Timeout += Refresh;
        AddChild(timer);
    }

    public override void _ExitTree()
    {
        GetTree().NodeAdded -= OnNodeAdded;
        GetTree().Root.SizeChanged -= Refresh;
    }

    private static bool IsEdgeControl(Control control)
    {
        for (Type? type = control.GetType(); type != null && type != typeof(Control); type = type.BaseType)
            if (type.Namespace?.StartsWith("MegaCrit.Sts2.", StringComparison.Ordinal) == true &&
                type.Name is "NTopBar" or "NBackButton" or "NProceedButton") return true;
        return false;
    }

    private void OnNodeAdded(Node node)
    {
        if (node is not Control control || !IsEdgeControl(control)) return;
        control.Connect(Node.SignalName.Ready, Callable.From(() => Track(control)), (uint)ConnectFlags.OneShot);
    }

    private void Track(Control control)
    {
        // Containers own their children's layout. Also avoid insetting a button inside an already inset HUD.
        if (control.GetParent() is Container) return;
        for (Node? parent = control.GetParent(); parent != null; parent = parent.GetParent())
            if (parent is Control ancestor && IsEdgeControl(ancestor)) return;
        _controls.Add(new EdgeControl(control));
        GD.Print("Phone safe-area HUD: ", control.GetType().Name);
        Refresh();
    }

    private void Refresh()
    {
        Window window = GetTree().Root;
        if (window.Size.X <= window.Size.Y || _controls.Count == 0) return;
        var screen = new Rect2(Vector2.Zero, window.Size);
        Rect2 safe = DisplayServer.GetDisplaySafeArea();
        safe = safe.Intersection(screen);
        if (!safe.HasArea()) return;
        // UIKit is also available when the engine queried its safe area before landscape layout settled.
        float side = (float)SafeAreaCanvas.SideInset() * window.Size.X;
        if (side > 0 && side < window.Size.X / 2f)
            safe = safe.Intersection(new Rect2(side, 0, window.Size.X - 2 * side, window.Size.Y));

        for (int index = _controls.Count - 1; index >= 0; index--)
        {
            EdgeControl entry = _controls[index];
            Control control = entry.Control;
            if (!IsInstanceValid(control) || !control.IsInsideTree())
            {
                _controls.RemoveAt(index);
                continue;
            }
            Control? parent = control.GetParentControl();
            Vector2 parentSize = parent?.Size ?? control.GetViewportRect().Size;
            if (parentSize.X <= 0 || parentSize.Y <= 0) continue;
            Transform2D toScreen = parent?.GetScreenTransform() ?? control.GetViewport().GetScreenTransform();
            if (Mathf.IsZeroApprox(toScreen.Determinant())) continue;
            Rect2 localSafe = toScreen.AffineInverse() * safe;
            if (entry.LastSafeArea == localSafe && entry.LastParentSize == parentSize) continue;
            entry.LastSafeArea = localSafe;
            entry.LastParentSize = parentSize;
            Vector4 original = entry.Anchors;
            // keepOffset leaves the game's button sizes and animation offsets intact.
            control.SetAnchor(Side.Left, HudSafeArea.Anchor(original.X, parentSize.X, localSafe.Position.X + BreathingRoom, localSafe.End.X - BreathingRoom), keepOffset: true);
            control.SetAnchor(Side.Right, HudSafeArea.Anchor(original.Z, parentSize.X, localSafe.Position.X + BreathingRoom, localSafe.End.X - BreathingRoom), keepOffset: true);
            control.SetAnchor(Side.Top, HudSafeArea.Anchor(original.Y, parentSize.Y, localSafe.Position.Y + BreathingRoom, localSafe.End.Y - BreathingRoom), keepOffset: true);
            control.SetAnchor(Side.Bottom, HudSafeArea.Anchor(original.W, parentSize.Y, localSafe.Position.Y + BreathingRoom, localSafe.End.Y - BreathingRoom), keepOffset: true);
        }
    }
}
