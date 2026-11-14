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

    private static void ShowSettings(Control screen, Action<string>? copyAddress = null)
    {
        if (screen.HasNode("PhoneLanHostPanel")) return;
        // Use the main game's viewport: embedded Window title bars and minimum-size negotiation
        // do not behave like touch controls, and the old AcceptDialog could not be dismissed.
        var dialog = new ColorRect { Name = "PhoneLanHostPanel", Color = new Color(0, 0, 0, 0.75f),
            MouseFilter = Control.MouseFilterEnum.Stop, ZIndex = 1000 };
        screen.AddChild(dialog);
        dialog.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var frame = new MarginContainer { AnchorLeft = 0.15f, AnchorRight = 0.85f, AnchorTop = 0.06f, AnchorBottom = 0.94f };
        dialog.AddChild(frame);
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        frame.AddChild(panel);
        var padding = new MarginContainer();
        foreach (string side in new[] { "left", "right", "top", "bottom" }) padding.AddThemeConstantOverride("margin_" + side, 24);
        panel.AddChild(padding);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 16);
        padding.AddChild(column);
        void Close()
        {
            if (DisplayServer.HasFeature(DisplayServer.Feature.VirtualKeyboard)) DisplayServer.VirtualKeyboardHide();
            dialog.Hide();
            dialog.QueueFree();
        }
        var header = new HBoxContainer();
        var title = new Label { Text = "Host from this iPhone", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        title.AddThemeFontSizeOverride("font_size", 34);
        header.AddChild(title);
        var close = new Button { Name = "Close", Text = "Close", CustomMinimumSize = new Vector2(180, 80) };
        close.AddThemeFontSizeOverride("font_size", 30);
        close.Pressed += Close;
        header.AddChild(close);
        column.AddChild(header);
        var scroll = new ScrollContainer { SizeFlagsVertical = Control.SizeFlags.ExpandFill,
            HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
        column.AddChild(scroll);
        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        content.AddThemeConstantOverride("separation", 16);
        scroll.AddChild(content);
        var instructions = new Label { Text = "Choose this iPhone’s Wi-Fi or Tailscale IP, then use Host or Load.\nLeave Mods off on every phone. Keep the host app open and in the foreground.",
            AutowrapMode = TextServer.AutowrapMode.WordSmart };
        instructions.AddThemeFontSizeOverride("font_size", 24);
        content.AddChild(instructions);
        var choices = new OptionButton { CustomMinimumSize = new Vector2(0, 56) };
        choices.AddThemeFontSizeOverride("font_size", 26);
        content.AddChild(choices);
        var address = new LineEdit { Name = "Address", Text = LanNetworkPolicy.HostAddress ?? "", PlaceholderText = "This iPhone’s private IP address",
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
        var use = new Button { Name = "UseAddress", Text = "Use and copy address", CustomMinimumSize = new Vector2(0, 96) };
        use.AddThemeFontSizeOverride("font_size", 26);
        void UseAddress()
        {
            string ip = address.Text.Trim();
            if (!LanNetworkPolicy.IsAllowedAddress(ip))
            {
                status.Text = "Choose this phone’s private Wi-Fi or Tailscale IP, without a port.";
                return;
            }
            LanNetworkPolicy.SelectHostAddress(ip);
            string shareAddress = ip.Contains(':') ? $"[{ip}]:{LanNetworkPolicy.DefaultPort}" : $"{ip}:{LanNetworkPolicy.DefaultPort}";
            if (copyAddress != null) copyAddress(shareAddress);
            else DisplayServer.ClipboardSet(shareAddress);
            if (screen.GetNodeOrNull<Button>("PhoneLanHost") is { } settings) settings.Text = "LAN host: " + ip;
            Close();
        }
        use.Pressed += UseAddress;
        address.TextSubmitted += _ => UseAddress();
        column.AddChild(use);
        Refresh();
    }
}
