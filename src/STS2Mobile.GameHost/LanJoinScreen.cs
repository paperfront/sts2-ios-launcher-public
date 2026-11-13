using System;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using STS2Mobile.Platform;

namespace STS2Mobile.GameHost;

/// <summary>Direct entry into the game's ENet client. Runs with Mods off and needs no desktop mod.</summary>
internal sealed partial class LanJoinScreen : Node
{
    private const string JoinType = "MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NJoinFriendScreen";
    private const string InitializerType = "MegaCrit.Sts2.Core.Multiplayer.Connection.ENetClientConnectionInitializer";
    public override void _Ready() => GetTree().NodeAdded += OnNodeAdded;

    public override void _ExitTree() => GetTree().NodeAdded -= OnNodeAdded;

    private static void OnNodeAdded(Node node)
    {
        if (node.GetType().FullName == JoinType && node is Control screen)
            screen.Connect(Node.SignalName.Ready, Callable.From(() => Attach(screen)), (uint)ConnectFlags.OneShot);
    }

    private static void Attach(Control screen)
    {
        // Only replace discovery presentation; JoinGameAsync keeps the game's handshake, errors and save handling.
        foreach (string field in new[] { "_buttonContainer", "_loadingFriendsIndicator", "_noFriendsLabel", "_loadingOverlay" })
            if (screen.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)?.GetValue(screen) is Control control)
                control.Hide();
        if (screen.GetNodeOrNull<Label>("TitleLabel") is { } title) title.Text = "JOIN LAN GAME";
        var panel = new PanelContainer { Name = "PhoneLanJoin", AnchorLeft = 0.15f, AnchorRight = 0.85f,
            AnchorTop = 0.25f, AnchorBottom = 0.85f };
        var padding = new MarginContainer();
        foreach (string side in new[] { "left", "right", "top", "bottom" }) padding.AddThemeConstantOverride("margin_" + side, 20);
        panel.AddChild(padding);
        var column = new VBoxContainer();
        column.AddThemeConstantOverride("separation", 16);
        padding.AddChild(column);
        var help = Text("A friend can host from iPhone with Multiplayer > Host, or desktop with --fastmp.\nEnter their Wi-Fi or Tailscale IP. Everyone needs the same game version; leave Mods off.");
        column.AddChild(help);
        var address = new LineEdit { PlaceholderText = "192.168.1.20 or 100.x.x.x[:port]", CustomMinimumSize = new Vector2(0, 64),
            VirtualKeyboardType = LineEdit.VirtualKeyboardTypeEnum.Default };
        address.AddThemeFontSizeOverride("font_size", 28);
        column.AddChild(address);
        var status = Text("Default UDP port: " + LanNetworkPolicy.DefaultPort + ". Allow Local Network access when iOS asks.");
        column.AddChild(status);
        var join = new Button { Text = "Join LAN", CustomMinimumSize = new Vector2(0, 64) };
        join.AddThemeFontSizeOverride("font_size", 28);
        column.AddChild(join);
        bool joining = false;
        async Task SubmitAsync()
        {
            if (joining) return;
            if (!LanNetworkPolicy.TryParse(address.Text, out string host, out int port))
            {
                status.Text = "Enter a private Wi-Fi or Tailscale IP, optionally followed by :port. Use [IPv6]:port for Tailscale IPv6.";
                return;
            }
            joining = true;
            join.Disabled = true;
            address.Editable = false;
            status.Text = "Connecting to your friend…";
            try
            {
                LanNetworkPolicy.RequirePeer(host, port);
                Assembly game = screen.GetType().Assembly;
                var type = game.GetType(InitializerType, throwOnError: true)!;
                var constructor = type.GetConstructor([typeof(ulong), typeof(string), typeof(ushort)])
                    ?? throw new MissingMethodException("This game version has a different LAN client API.");
                object initializer = constructor.Invoke([RuntimeServices.GetLocalPlayerId(), host, (ushort)port]);
                var method = screen.GetType().GetMethod("JoinGameAsync", BindingFlags.Public | BindingFlags.Instance)
                    ?? throw new MissingMethodException("This game version has a different LAN join screen.");
                if (method.Invoke(screen, [initializer]) is not Task task) throw new InvalidDataException("The LAN join did not return a task.");
                await task;
                if (GodotObject.IsInstanceValid(status)) status.Text = "If the connection failed, check the host’s --fastmp option, matching versions, firewall and IP address.";
            }
            catch (Exception error)
            {
                Exception cause = error is TargetInvocationException { InnerException: { } inner } ? inner : error;
                GD.Print($"LAN join failed: {cause.GetType().Name}.");
                if (GodotObject.IsInstanceValid(status)) status.Text = "Could not join. Check Local Network permission, the host’s --fastmp option, matching game versions and network access.";
            }
            finally
            {
                joining = false;
                if (GodotObject.IsInstanceValid(join)) join.Disabled = false;
                if (GodotObject.IsInstanceValid(address)) address.Editable = true;
            }
        }
        join.Pressed += () => _ = SubmitAsync();
        address.TextSubmitted += text => { _ = SubmitAsync(); };
        screen.AddChild(panel);
    }

    private static Label Text(string value)
    {
        var label = new Label { Text = value, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        label.AddThemeFontSizeOverride("font_size", 24);
        return label;
    }
}
