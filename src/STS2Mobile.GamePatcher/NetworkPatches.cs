using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace STS2Mobile.GamePatcher;

internal static class NetworkPatches
{
    private const string Client = "MegaCrit.Sts2.Core.Multiplayer.Transport.ENet.ENetClient";
    private const string Host = "MegaCrit.Sts2.Core.Multiplayer.Transport.ENet.ENetHost";
    public const string StepName = "LAN hosts and joins use private or Tailscale addresses";
    public static IEnumerable<PatchStep> Steps => [new(StepName, true, GuardLan)];

    private static void GuardLan(GamePatch patch)
    {
        int clients = 0, hosts = 0, connects = 0, binds = 0;
        foreach (var method in patch.MethodsWithBodies.ToArray())
        {
            if (method.Body.Instructions.Any(i => i.Operand is MethodReference target && target.DeclaringType.Namespace == "System.Net.Sockets"))
            {
                patch.Reject(method, "Raw socket networking is disabled; use the built-in LAN host or join screens.");
                continue;
            }
            bool opensEnet = method.Body.Instructions.Any(i => i.OpCode == OpCodes.Newobj && i.Operand is MethodReference target &&
                target.DeclaringType.FullName == "Godot.ENetConnection");
            TypeDefinition owner = method.DeclaringType;
            while (owner.DeclaringType != null) owner = owner.DeclaringType;
            if (opensEnet && owner.FullName != Client && owner.FullName != Host)
                throw new InvalidOperationException("Unexpected ENet opener: " + method.FullName);
            if (opensEnet && owner.FullName == Client) clients++;
            if (opensEnet && owner.FullName == Host) hosts++;
            foreach (var instruction in method.Body.Instructions.ToArray())
            {
                if (instruction.Operand is not MethodReference call || call.DeclaringType.FullName != "Godot.ENetConnection") continue;
                if (call.Name == "ConnectToHost")
                {
                    if (owner.FullName != Client) throw new InvalidOperationException("Unexpected ENet client: " + method.FullName);
                    GuardCall(patch, method, instruction, call, "RequirePeer", replaceAddress: false);
                    connects++;
                }
                else if (call.Name == "CreateHostBound")
                {
                    if (owner.FullName != Host) throw new InvalidOperationException("Unexpected ENet listener: " + method.FullName);
                    GuardCall(patch, method, instruction, call, "ResolveListenAddress", replaceAddress: true);
                    binds++;
                }
            }
        }
        // None of these guards can be skipped in tolerant mode: unknown networking must fail preparation.
        if (clients != 1 || connects != 1 || hosts != 1 || binds != 1)
            throw new InvalidOperationException($"Expected one ENet client/connect and host/bind, found {clients}/{connects} and {hosts}/{binds}.");
        EnableNativeLanMode(patch);
        const string initializerName = "MegaCrit.Sts2.Core.Multiplayer.Connection.ENetClientConnectionInitializer";
        var initializer = patch.Type(initializerName);
        if (!initializer.Methods.Any(m => m.IsConstructor && m.IsPublic && m.Parameters.Select(p => p.ParameterType.FullName)
            .SequenceEqual(new[] { "System.UInt64", "System.String", "System.UInt16" })))
            throw new InvalidOperationException("The LAN initializer signature changed.");
        var join = patch.Method("MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NJoinFriendScreen", "JoinGameAsync");
        if (!join.IsPublic || join.Parameters.Count != 1 || join.Parameters[0].ParameterType.FullName !=
            "MegaCrit.Sts2.Core.Multiplayer.Connection.IClientConnectionInitializer" || join.ReturnType.FullName != "System.Threading.Tasks.Task")
            throw new InvalidOperationException("The LAN join screen signature changed.");
        patch.Noop(patch.Method("MegaCrit.Sts2.Core.Nodes.Screens.MainMenu.NJoinFriendScreen", "OnSubmenuOpened"),
            "The phone's join screen uses manual IP entry, without Steamworks friend discovery.");
    }

    private static void EnableNativeLanMode(GamePatch patch)
    {
        var hasArg = patch.Method("MegaCrit.Sts2.Core.Helpers.CommandLineHelper", "HasArg");
        if (!hasArg.IsStatic || hasArg.ReturnType.MetadataType != MetadataType.Boolean || hasArg.Parameters.Count != 1 ||
            hasArg.Parameters[0].ParameterType.MetadataType != MetadataType.String || !hasArg.HasBody)
            throw new InvalidOperationException("The native LAN mode selector signature changed.");
        hasArg.Body.SimplifyMacros();
        var il = hasArg.Body.GetILProcessor();
        var original = hasArg.Body.Instructions[0];
        foreach (var instruction in new[] {
            il.Create(OpCodes.Ldarg_0),
            il.Create(OpCodes.Call, patch.Adapter("LanNetworkPolicy", "IsFastMultiplayerArgument")),
            il.Create(OpCodes.Brfalse, original), il.Create(OpCodes.Ldc_I4_1), il.Create(OpCodes.Ret) })
            il.InsertBefore(original, instruction);
        hasArg.Body.OptimizeMacros();
        patch.Record(hasArg.FullName, "Select native fastmp hosting and saved-run loading; preserve all other argument checks.");
    }

    private static void GuardCall(GamePatch patch, MethodDefinition method, Instruction instruction, MethodReference call, string guard, bool replaceAddress)
    {
        if (instruction.OpCode != OpCodes.Callvirt || call.Parameters.Count < 2 || call.Parameters[0].ParameterType.FullName != "System.String" ||
            call.Parameters[1].ParameterType.FullName != "System.Int32" || call.Parameters.Any(p => p.ParameterType.IsByReference))
            throw new InvalidOperationException("Unexpected ENet address/port signature: " + call.FullName);
        method.Body.SimplifyMacros();
        var args = call.Parameters.Select(p => new VariableDefinition(p.ParameterType)).ToArray();
        foreach (var arg in args) method.Body.Variables.Add(arg);
        var il = method.Body.GetILProcessor();
        var added = new List<Instruction>();
        for (int n = args.Length - 1; n >= 0; n--) added.Add(il.Create(OpCodes.Stloc, args[n]));
        added.Add(il.Create(OpCodes.Ldloc, args[0]));
        added.Add(il.Create(OpCodes.Ldloc, args[1]));
        added.Add(il.Create(OpCodes.Call, patch.Adapter("LanNetworkPolicy", guard)));
        if (replaceAddress) added.Add(il.Create(OpCodes.Stloc, args[0]));
        foreach (var arg in args) added.Add(il.Create(OpCodes.Ldloc, arg));
        added.Add(il.Create(OpCodes.Callvirt, call));
        // Keep the old branch target at the start of the guard, so no branch can jump past validation.
        instruction.OpCode = added[0].OpCode;
        instruction.Operand = added[0].Operand;
        var previous = instruction;
        foreach (var next in added.Skip(1)) { il.InsertAfter(previous, next); previous = next; }
        method.Body.OptimizeMacros();
        patch.Record(method.FullName, replaceAddress ? "Bind the native ENet host to the selected private or Tailscale interface." :
            "Allow ENet clients only to explicit private or Tailscale IP addresses.");
    }
}
