using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Godot;
using STS2Mobile.Platform;

namespace STS2Mobile.GameHost;

/// <summary>Records HUD geometry locally for investigating device layout. Never changes controls or records their text.</summary>
internal sealed partial class HudLayoutReport : Node
{
    private readonly HashSet<string> _recorded = new(StringComparer.Ordinal);
    private string? _file;

    public override void _Ready()
    {
        if (!OperatingSystem.IsIOS()) return;
        try
        {
            _file = ProjectSettings.GlobalizePath("user://hud-layout.txt");
            File.WriteAllText(_file, "HUD geometry only. No control text, saves or credentials.\n");
            GetTree().NodeAdded += OnNodeAdded;
        }
        catch (IOException) { _file = null; }
        catch (UnauthorizedAccessException) { _file = null; }
    }

    public override void _ExitTree()
    {
        if (_file != null) GetTree().NodeAdded -= OnNodeAdded;
    }

    private void OnNodeAdded(Node node)
    {
        if (node is not Control control || node.GetType().Name is not ("NTopBar" or "NBackButton" or "NProceedButton")) return;
        control.Connect(Node.SignalName.Ready, Callable.From(() => Watch(control)), (uint)ConnectFlags.OneShot);
    }

    private void Watch(Control control)
    {
        control.VisibilityChanged += () => { if (control.IsVisibleInTree()) Schedule(control); };
        if (control.IsVisibleInTree()) Schedule(control);
    }

    private void Schedule(Control control)
    {
        string type = control.GetType().Name;
        if (!_recorded.Add(type)) return;
        var timer = new Timer { WaitTime = 1, OneShot = true, Autostart = true };
        timer.Timeout += () =>
        {
            try
            {
                if (IsInstanceValid(control) && control.IsInsideTree() && control.IsVisibleInTree()) Write(control);
                else _recorded.Remove(type);
            }
            catch (Exception error) { GD.Print("HUD geometry report unavailable: ", error.GetType().Name); }
            finally { timer.QueueFree(); }
        };
        AddChild(timer);
    }

    private void Write(Control control)
    {
        Window window = GetTree().Root;
        var text = new StringBuilder();
        text.AppendLine($"\n{control.GetType().Name}: window={window.Size} canvas={window.ContentScaleSize} aspect={window.ContentScaleAspect}");
        text.AppendLine($"viewport={window.GetVisibleRect()} screen_transform={window.GetScreenTransform()}");
        text.AppendLine($"engine_safe={DisplayServer.GetDisplaySafeArea()} native_side_fraction={SafeAreaCanvas.SideInset()}");
        if (control.GetParentControl() is { } parent) Describe(parent, text, "parent ");
        int remaining = 128;
        DescribeTree(control, text, 0, ref remaining);
        File.AppendAllText(_file!, text.ToString());
    }

    private static void DescribeTree(Node node, StringBuilder text, int depth, ref int remaining)
    {
        if (depth > 4 || remaining-- <= 0) return;
        if (node is Control control) Describe(control, text, new string(' ', depth * 2));
        foreach (Node child in node.GetChildren()) DescribeTree(child, text, depth + 1, ref remaining);
    }

    private static void Describe(Control control, StringBuilder text, string prefix)
    {
        text.AppendLine($"{prefix}{control.Name} [{control.GetType().FullName}] visible={control.IsVisibleInTree()} position={control.Position} size={control.Size}");
        text.AppendLine($"{prefix}anchors={control.AnchorLeft},{control.AnchorTop},{control.AnchorRight},{control.AnchorBottom} offsets={control.OffsetLeft},{control.OffsetTop},{control.OffsetRight},{control.OffsetBottom}");
        text.AppendLine($"{prefix}screen_transform={control.GetScreenTransform()}");
    }
}
