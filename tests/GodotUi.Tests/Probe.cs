using System.Reflection;
using Godot;
using STS2Mobile.Platform;

public partial class Probe : Node
{
    private static readonly MethodInfo OpenPanel = Assembly.Load("STS2Mobile.GameHost")
        .GetType("STS2Mobile.GameHost.LanHostScreen", throwOnError: true)!
        .GetMethod("ShowSettings", BindingFlags.Static | BindingFlags.NonPublic)!;
    private string? _copiedAddress;

    private void Open(Control screen) => OpenPanel.Invoke(null, [screen, new Action<string>(value => _copiedAddress = value)]);

    public override async void _Ready()
    {
        try
        {
            foreach (var size in new[] { new Vector2I(2337, 1080), new Vector2I(1680, 1080) })
            {
                GetTree().Root.Size = size;
                var screen = new Control();
                AddChild(screen);
                screen.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
                string? originalAddress = LanNetworkPolicy.HostAddress;
                Open(screen);
                Open(screen);
                await Frames();
                Check(screen.GetChildren().Count == 1, "opening twice must not stack panels");
                var panel = screen.GetNode<Control>("PhoneLanHostPanel");
                var close = (Button)panel.FindChild("Close", recursive: true, owned: false);
                string capture = OS.GetEnvironment("STS2_UI_SCREENSHOTS");
                if (capture.Length > 0)
                    GetViewport().GetTexture().GetImage().SavePng(System.IO.Path.Combine(capture, $"lan-panel-{size.X}.png"));
                Check(new Rect2(Vector2.Zero, size).Encloses(close.GetGlobalRect()), "Close must remain within the viewport");
                await Tap(close, touch: true);
                Check(!screen.HasNode("PhoneLanHostPanel"), "a pointer tap must close the panel");
                Check(LanNetworkPolicy.HostAddress == originalAddress, "Close must not commit an address");
                Open(screen);
                await Frames();
                panel = screen.GetNode<Control>("PhoneLanHostPanel");
                var address = (LineEdit)panel.FindChild("Address", true, false);
                var use = (Button)panel.FindChild("UseAddress", true, false);
                address.Text = "8.8.8.8";
                await Tap(use);
                Check(screen.HasNode("PhoneLanHostPanel"), "invalid addresses must leave the editor open");
                address.Text = "192.168.0.148";
                await Tap(use);
                Check(!screen.HasNode("PhoneLanHostPanel"), "valid submission must close the panel");
                Check(LanNetworkPolicy.HostAddress == "192.168.0.148", "submission must select the host address");
                Check(_copiedAddress == "192.168.0.148:33771", "submission must copy the share address without changing the test machine's clipboard");
                screen.QueueFree();
                await Frames();
                GD.Print($"LAN panel pointer interactions passed at {size}.");
            }
            GetTree().Quit(0);
        }
        catch (Exception error)
        {
            GD.PrintErr(error);
            GetTree().Quit(1);
        }
    }

    private async Task Tap(Button button, bool touch = false)
    {
        Check(button.IsVisibleInTree(), "button must be visible");
        Vector2 position = button.GetGlobalRect().GetCenter();
        if (touch)
        {
            Input.ParseInputEvent(new InputEventScreenTouch { Position = position, Index = 0, Pressed = true });
            Input.ParseInputEvent(new InputEventScreenTouch { Position = position, Index = 0, Pressed = false });
        }
        else
        {
            GetTree().Root.PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = true });
            GetTree().Root.PushInput(new InputEventMouseButton { Position = position, GlobalPosition = position, ButtonIndex = MouseButton.Left, Pressed = false });
        }
        await Frames();
    }

    private async Task Frames()
    {
        for (int frame = 0; frame < 5; frame++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
