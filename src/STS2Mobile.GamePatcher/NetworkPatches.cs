using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;

namespace STS2Mobile.GamePatcher;

internal static class NetworkPatches
{
    private const string Client = "MegaCrit.Sts2.Core.Multiplayer.Transport.ENet.ENetClient";
    public const string StepName = "LAN joins use private or Tailscale addresses";
    public static IEnumerable<PatchStep> Steps => [new(StepName, true, GuardLan)];

    private static void GuardLan(GamePatch patch)
    {
        int clients = 0, blocked = 0, connects = 0;
        foreach (var method in patch.MethodsWithBodies.ToArray())
        {
            if (method.Body.Instructions.Any(i => i.Operand is MethodReference target && target.DeclaringType.Namespace == "System.Net.Sockets"))
            {
                patch.Reject(method, "Raw socket networking is disabled; use the explicit LAN join screen.");
                continue;
            }
            bool opensEnet = method.Body.Instructions.Any(i => i.OpCode == OpCodes.Newobj && i.Operand is MethodReference target &&
                target.DeclaringType.FullName == "Godot.ENetConnection");
            TypeDefinition owner = method.DeclaringType;
            while (owner.DeclaringType != null) owner = owner.DeclaringType;
            if (opensEnet && owner.FullName != Client)
            {
                patch.Reject(method, "Hosting LAN games on the iPhone is disabled. Host on a desktop with --fastmp.");
                blocked++;
                continue;
            }
            if (opensEnet) clients++;
            foreach (var instruction in method.Body.Instructions.ToArray())
            {
                if (instruction.Operand is not MethodReference call || call.DeclaringType.FullName != "Godot.ENetConnection" || call.Name != "ConnectToHost") continue;
                if (owner.FullName != Client) throw new InvalidOperationException("Unexpected ENet client: " + method.FullName);
                GuardConnect(patch, method, instruction, call);
                connects++;
            }
        }
        // Admission cannot be skipped in tolerant mode: an unknown client shape must never become an unguarded connection.
        if (clients != 1 || connects != 1) throw new InvalidOperationException($"Expected one ENet client and guarded connection, found {clients} and {connects}.");
        patch.Expect(blocked, 1, "disabled ENet host openers");
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

    private static void GuardConnect(GamePatch patch, MethodDefinition method, Instruction instruction, MethodReference call)
    {
        if (instruction.OpCode != OpCodes.Callvirt || call.Parameters.Count < 2 || call.Parameters[0].ParameterType.FullName != "System.String" ||
            call.Parameters[1].ParameterType.FullName != "System.Int32" || call.Parameters.Any(p => p.ParameterType.IsByReference))
            throw new InvalidOperationException("Unexpected ENet ConnectToHost signature: " + call.FullName);
        method.Body.SimplifyMacros();
        var args = call.Parameters.Select(p => new VariableDefinition(p.ParameterType)).ToArray();
        foreach (var arg in args) method.Body.Variables.Add(arg);
        var il = method.Body.GetILProcessor();
        var added = new List<Instruction>();
        for (int n = args.Length - 1; n >= 0; n--) added.Add(il.Create(OpCodes.Stloc, args[n]));
        added.Add(il.Create(OpCodes.Ldloc, args[0]));
        added.Add(il.Create(OpCodes.Ldloc, args[1]));
        added.Add(il.Create(OpCodes.Call, patch.Adapter("LanNetworkPolicy", "RequirePeer")));
        foreach (var arg in args) added.Add(il.Create(OpCodes.Ldloc, arg));
        added.Add(il.Create(OpCodes.Callvirt, call));
        // Keep the old branch target at the start of the guard, so no branch can jump past validation.
        instruction.OpCode = added[0].OpCode;
        instruction.Operand = added[0].Operand;
        var previous = instruction;
        foreach (var next in added.Skip(1)) { il.InsertAfter(previous, next); previous = next; }
        method.Body.OptimizeMacros();
        patch.Record(method.FullName, "Allow ENet clients only to explicit private or Tailscale IP addresses.");
    }
}
