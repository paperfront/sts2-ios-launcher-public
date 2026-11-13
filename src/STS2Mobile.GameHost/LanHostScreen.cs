using System;
using System.Linq;
using Godot;
using STS2Mobile.Platform;

namespace STS2Mobile.GameHost;

/// <summary>Choose and share the native LAN host's bind address; the game's own Host/Load actions own the server.</summary>
internal sealed partial class LanHostScreen : Node
{
    public override void _Ready() => GetTree().NodeAdded += OnNodeAdded;
    public override void _ExitTree() => GetTree().NodeAdded -= OnNodeAdded;

    private static void OnNodeAdded(Node node)
    {
        if (node is Control screen && node.GetType().FullName is
            "MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NMultiplayerSubmenu" or
            "MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NMultiplayerHostSubmenu")
            screen.Connect(Node.SignalName.Ready, Callable.From(() => Attach(screen)), (uint)ConnectFlags.OneShot);
    }

    private static string[] Addresses() => IP.GetLocalAddresses().Where(LanNetworkPolicy.IsAllowedAddress)
        .Distinct(StringComparer.Ordinal).OrderBy(address => address.StartsWith("100.", StringComparison.Ordinal) || address.Contains(':'))
        .ThenBy(address => address, StringComparer.Ordinal).ToArray();

    private static void Attach(Control screen)
    {
        if (screen.HasNode("PhoneLanHost")) return;
        // Select a private interface, without opening a listener. The user can change it before Host or Load.
        if (LanNetworkPolicy.HostAddress == null && Addresses().FirstOrDefault() is { } first)
            LanNetworkPolicy.SelectHostAddress(first);
        var settings = new Button { Name = "PhoneLanHost", Text = "LAN host address", AnchorLeft = 0.67f, AnchorRight = 0.97f,
            AnchorTop = 0.88f, AnchorBottom = 0.98f, CustomMinimumSize = new Vector2(260, 56) };
        settings.AddThemeFontSizeOverride("font_size", 26);
        settings.Pressed += () => ShowSettings(screen);
        screen.AddChild(settings);
    }

    private static void ShowSettings(Control screen)
    {
        var dialog = new AcceptDialog { Title = "Host from this iPhone", MinSize = new Vector2I(650, 300), Exclusive = true };
        screen.AddChild(dialog);
        dialog.Confirmed += () => dialog.QueueFree();
        dialog.Canceled += () => dialog.QueueFree();
        var content = new VBoxContainer();
        content.AddThemeConstantOverride("separation", 16);
        var instructions = new Label { Text = "Choose this iPhone’s Wi-Fi or Tailscale IP, then use Host or Load.\nLeave Mods off on every phone. Keep the host app open and in the foreground.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(600, 0) };
        instructions.AddThemeFontSizeOverride("font_size", 24);
        content.AddChild(instructions);
        var choices = new OptionButton { CustomMinimumSize = new Vector2(0, 56) };
        choices.AddThemeFontSizeOverride("font_size", 26);
        content.AddChild(choices);
        var address = new LineEdit { Text = LanNetworkPolicy.HostAddress ?? "", PlaceholderText = "This iPhone’s private IP address",
            CustomMinimumSize = new Vector2(0, 56) };
        address.AddThemeFontSizeOverride("font_size", 26);
        content.AddChild(address);
        var status = new Label { Text = "Share the selected address with your friends. UDP port: " + LanNetworkPolicy.DefaultPort,
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        status.AddThemeFontSizeOverride("font_size", 24);
        content.AddChild(status);
        string[] addresses = [];
        void Refresh()
        {
            addresses = Addresses();
            choices.Clear();
            foreach (string ip in addresses) choices.AddItem(ip);
            choices.Disabled = addresses.Length == 0;
            int selected = Array.IndexOf(addresses, address.Text);
            if (selected >= 0) choices.Select(selected);
            else if (address.Text.Length == 0 && addresses.Length > 0) address.Text = addresses[0];
            if (addresses.Length == 0) status.Text = "No private IP was found. Connect Wi-Fi/Tailscale and refresh, or enter this phone’s Tailscale IP.";
        }
        choices.ItemSelected += index => address.Text = addresses[(int)index];
        var refresh = new Button { Text = "Refresh addresses", CustomMinimumSize = new Vector2(0, 56) };
        refresh.AddThemeFontSizeOverride("font_size", 26);
        refresh.Pressed += Refresh;
        content.AddChild(refresh);
        var use = new Button { Text = "Use and copy address", CustomMinimumSize = new Vector2(0, 56) };
        use.AddThemeFontSizeOverride("font_size", 26);
        use.Pressed += () =>
        {
            string ip = address.Text.Trim();
            if (!LanNetworkPolicy.IsAllowedAddress(ip))
            {
                status.Text = "Choose this phone’s private Wi-Fi or Tailscale IP, without a port.";
                return;
            }
            LanNetworkPolicy.SelectHostAddress(ip);
            DisplayServer.ClipboardSet(ip.Contains(':') ? $"[{ip}]:{LanNetworkPolicy.DefaultPort}" : $"{ip}:{LanNetworkPolicy.DefaultPort}");
            status.Text = "Address copied. Close this panel, then choose Host or Load. Friends use Multiplayer > Join.";
        };
        content.AddChild(use);
        dialog.AddChild(content);
        Refresh();
        dialog.PopupCentered();
    }
}
