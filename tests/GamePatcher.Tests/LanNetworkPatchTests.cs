using Mono.Cecil;
using Mono.Cecil.Cil;
using STS2Mobile.GamePatcher;

namespace STS2Mobile.GamePatcher.Tests;

public sealed class LanNetworkPatchTests
{
    [Fact]
    public void BothClientAndHostAreGuardedAndRawSocketsRemainBlocked()
    {
        using var fixture = new Fixture();
        NetworkPatches.Steps.Single().Apply(fixture.Patch);
        var instructions = fixture.Connect.Body.Instructions;
        Assert.Contains(instructions, i => i.Operand is MethodReference m && m.DeclaringType.Name == "ENetConnection" && m.Name == ".ctor");
        int guard = instructions.ToList().FindIndex(i => i.Operand is MethodReference m && m.Name == "RequirePeer");
        int connect = instructions.ToList().FindIndex(i => i.Operand is MethodReference m && m.Name == "ConnectToHost");
        Assert.True(guard >= 0 && guard < connect);
        Assert.DoesNotContain(fixture.Host.Body.Instructions, i => i.OpCode == OpCodes.Throw);
        var hostInstructions = fixture.Host.Body.Instructions;
        int hostGuard = hostInstructions.ToList().FindIndex(i => i.Operand is MethodReference m && m.Name == "ResolveListenAddress");
        int bind = hostInstructions.ToList().FindIndex(i => i.Operand is MethodReference m && m.Name == "CreateHostBound");
        Assert.True(hostGuard >= 0 && hostGuard < bind);
        Assert.StartsWith("stloc", hostInstructions[hostGuard + 1].OpCode.Name);
        Assert.Contains(fixture.RawSocket.Body.Instructions, i => i.OpCode == OpCodes.Throw);
        Assert.Single(fixture.Opened.Body.Instructions);
        Assert.Equal(OpCodes.Ret, fixture.Opened.Body.Instructions[0].OpCode);
        Assert.Contains(fixture.Connect.Body.Instructions, i => i.OpCode.FlowControl == FlowControl.Branch && ReferenceEquals(i.Operand, fixture.GuardStart));
        Assert.NotEqual(OpCodes.Callvirt, fixture.GuardStart.OpCode); // Existing branches enter the guard, never skip it.
    }

    [Fact]
    public void AnUnexpectedEnetClientFailsClosedEvenInTolerantMode()
    {
        using var fixture = new Fixture(tolerant: true);
        fixture.Connect.DeclaringType.Name = "UnknownClient";
        Assert.Throws<InvalidOperationException>(() => NetworkPatches.Steps.Single().Apply(fixture.Patch));
    }

    [Fact]
    public void AnUnexpectedConnectSignatureFailsClosed()
    {
        using var fixture = new Fixture();
        fixture.EngineConnect.Parameters[1].ParameterType = fixture.Game.MainModule.TypeSystem.String;
        Assert.Throws<InvalidOperationException>(() => NetworkPatches.Steps.Single().Apply(fixture.Patch));
    }

    [Fact]
    public void AChangedJoinScreenApiFailsPreparation()
    {
        using var fixture = new Fixture();
        fixture.Opened.DeclaringType.Methods.Single(m => m.Name == "JoinGameAsync").Name = "RenamedJoin";
        Assert.Throws<InvalidOperationException>(() => NetworkPatches.Steps.Single().Apply(fixture.Patch));
    }

    [InstalledGameFact]
    public void TheRealGameHasOneGuardedClientAndOneGuardedHost()
    {
        string folder = Directory.CreateTempSubdirectory("lan-real-game-").FullName;
        try
        {
            GameAssemblyBuilder.Build(InstalledGame.Data!, [], folder);
            using var game = ModuleDefinition.ReadModule(Path.Combine(folder, GameProfile.GameAssembly));
            var methods = CecilWalk.Flatten(game.Types).SelectMany(t => t.Methods).Where(m => m.HasBody).ToArray();
            var guards = methods.SelectMany(m => m.Body.Instructions).Where(i => i.Operand is MethodReference r &&
                r.DeclaringType.FullName == "STS2Mobile.Platform.LanNetworkPolicy" && r.Name == "RequirePeer");
            Assert.Single(guards);
            Assert.Equal(2, methods.SelectMany(m => m.Body.Instructions).Count(i => i.OpCode == OpCodes.Newobj &&
                i.Operand is MethodReference r && r.DeclaringType.FullName == "Godot.ENetConnection"));
            Assert.Single(methods.SelectMany(m => m.Body.Instructions), i => i.Operand is MethodReference r &&
                r.DeclaringType.FullName == "STS2Mobile.Platform.LanNetworkPolicy" && r.Name == "ResolveListenAddress");
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    [Fact]
    public void AnUnexpectedHostOrBindSignatureFailsClosed()
    {
        using var fixture = new Fixture(tolerant: true);
        fixture.Host.DeclaringType.Name = "UnknownHost";
        Assert.Throws<InvalidOperationException>(() => NetworkPatches.Steps.Single().Apply(fixture.Patch));
        using var badBind = new Fixture();
        badBind.EngineBind.Parameters[1].ParameterType = badBind.Game.MainModule.TypeSystem.String;
        Assert.Throws<InvalidOperationException>(() => NetworkPatches.Steps.Single().Apply(badBind.Patch));
    }

    [Fact]
    public void NativeFastmpModeIsEnabledWithoutReplacingOtherArgumentChecks()
    {
        using var fixture = new Fixture();
        var original = fixture.HasArg.Body.Instructions[0];
        NetworkPatches.Steps.Single().Apply(fixture.Patch);
        var instructions = fixture.HasArg.Body.Instructions;
        Assert.Contains(instructions, i => i.Operand is MethodReference m && m.Name == "IsFastMultiplayerArgument");
        Assert.Contains(instructions, i => i.OpCode.FlowControl == FlowControl.Cond_Branch && ReferenceEquals(i.Operand, original));
        Assert.Contains(original, instructions);
        Assert.Equal(OpCodes.Ret, instructions[^1].OpCode);
    }

    private sealed class Fixture : IDisposable
    {
        public AssemblyDefinition Game { get; } = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition("sts2", new Version(1, 0)), "sts2", ModuleKind.Dll);
        private readonly ModuleDefinition _platform = ModuleDefinition.CreateModule("STS2Mobile.Platform", ModuleKind.Dll);
        public GamePatch Patch { get; }
        public MethodDefinition Connect { get; }
        public MethodDefinition Host { get; }
        public MethodDefinition RawSocket { get; }
        public MethodDefinition Opened { get; }
        public MethodReference EngineConnect { get; }
        public MethodReference EngineBind { get; }
        public MethodDefinition HasArg { get; }
        public Instruction GuardStart { get; }

        public Fixture(bool tolerant = false)
        {
            var module = Game.MainModule;
            var policy = new TypeDefinition("STS2Mobile.Platform", "LanNetworkPolicy", TypeAttributes.Public, _platform.TypeSystem.Object);
            _platform.Types.Add(policy);
            var require = new MethodDefinition("RequirePeer", MethodAttributes.Public | MethodAttributes.Static, _platform.TypeSystem.Void);
            require.Parameters.Add(new ParameterDefinition(_platform.TypeSystem.String));
            require.Parameters.Add(new ParameterDefinition(_platform.TypeSystem.Int32));
            policy.Methods.Add(require);
            var listen = new MethodDefinition("ResolveListenAddress", MethodAttributes.Public | MethodAttributes.Static, _platform.TypeSystem.String);
            listen.Parameters.Add(new ParameterDefinition(_platform.TypeSystem.String));
            listen.Parameters.Add(new ParameterDefinition(_platform.TypeSystem.Int32));
            policy.Methods.Add(listen);
            var argument = new MethodDefinition("IsFastMultiplayerArgument", MethodAttributes.Public | MethodAttributes.Static, _platform.TypeSystem.Boolean);
            argument.Parameters.Add(new ParameterDefinition(_platform.TypeSystem.String));
            policy.Methods.Add(argument);
            var enet = new TypeReference("Godot", "ENetConnection", module, module.TypeSystem.CoreLibrary);
            var ctor = new MethodReference(".ctor", module.TypeSystem.Void, enet) { HasThis = true };
            EngineConnect = new MethodReference("ConnectToHost", module.TypeSystem.Object, enet) { HasThis = true };
            foreach (var type in new[] { module.TypeSystem.String, module.TypeSystem.Int32, module.TypeSystem.Int32, module.TypeSystem.Int32 })
                EngineConnect.Parameters.Add(new ParameterDefinition(type));
            Connect = Method("MegaCrit.Sts2.Core.Multiplayer.Transport.ENet", "ENetClient", "Connect");
            var il = Connect.Body.GetILProcessor();
            il.Emit(OpCodes.Newobj, ctor);
            il.Emit(OpCodes.Ldstr, "192.168.1.2");
            il.Emit(OpCodes.Ldc_I4, 33771);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ldc_I4_0);
            GuardStart = il.Create(OpCodes.Callvirt, EngineConnect);
            il.Emit(OpCodes.Br, GuardStart);
            il.Append(GuardStart);
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ret);
            Host = Method("MegaCrit.Sts2.Core.Multiplayer.Transport.ENet", "ENetHost", "StartHost");
            EngineBind = new MethodReference("CreateHostBound", module.TypeSystem.Int32, enet) { HasThis = true };
            EngineBind.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
            for (int n = 0; n < 5; n++) EngineBind.Parameters.Add(new ParameterDefinition(module.TypeSystem.Int32));
            il = Host.Body.GetILProcessor();
            il.Emit(OpCodes.Newobj, ctor);
            il.Emit(OpCodes.Ldstr, "*");
            il.Emit(OpCodes.Ldc_I4, 33771);
            il.Emit(OpCodes.Ldc_I4_4);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Callvirt, EngineBind);
            il.Emit(OpCodes.Pop);
            il.Emit(OpCodes.Ret);
            HasArg = Method("MegaCrit.Sts2.Core.Helpers", "CommandLineHelper", "HasArg");
            HasArg.ReturnType = module.TypeSystem.Boolean;
            HasArg.Parameters.Add(new ParameterDefinition(module.TypeSystem.String));
            HasArg.Body.GetILProcessor().Emit(OpCodes.Ldc_I4_0);
            HasArg.Body.GetILProcessor().Emit(OpCodes.Ret);
            RawSocket = Method("Example", "Discovery", "Broadcast");
            var socket = new TypeReference("System.Net.Sockets", "Socket", module, module.TypeSystem.CoreLibrary);
            RawSocket.Body.GetILProcessor().Emit(OpCodes.Call, new MethodReference("Open", module.TypeSystem.Void, socket));
            RawSocket.Body.GetILProcessor().Emit(OpCodes.Ret);
            Opened = Method("MegaCrit.Sts2.Core.Nodes.Screens.MainMenu", "NJoinFriendScreen", "OnSubmenuOpened");
            Opened.Body.GetILProcessor().Emit(OpCodes.Nop);
            Opened.Body.GetILProcessor().Emit(OpCodes.Ret);
            var initializer = new TypeDefinition("MegaCrit.Sts2.Core.Multiplayer.Connection", "ENetClientConnectionInitializer", TypeAttributes.Public, module.TypeSystem.Object);
            module.Types.Add(initializer);
            var initializerCtor = new MethodDefinition(".ctor", MethodAttributes.Public | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName, module.TypeSystem.Void);
            foreach (var type in new[] { module.TypeSystem.UInt64, module.TypeSystem.String, module.TypeSystem.UInt16 })
                initializerCtor.Parameters.Add(new ParameterDefinition(type));
            initializer.Methods.Add(initializerCtor);
            var join = new MethodDefinition("JoinGameAsync", MethodAttributes.Public, new TypeReference("System.Threading.Tasks", "Task", module, module.TypeSystem.CoreLibrary));
            join.Parameters.Add(new ParameterDefinition(new TypeReference("MegaCrit.Sts2.Core.Multiplayer.Connection", "IClientConnectionInitializer", module, module.TypeSystem.CoreLibrary)));
            Opened.DeclaringType.Methods.Add(join);
            Patch = new GamePatch(Game, _platform) { Tolerant = tolerant };
        }

        private MethodDefinition Method(string ns, string typeName, string name)
        {
            var module = Game.MainModule;
            var type = new TypeDefinition(ns, typeName, TypeAttributes.Public, module.TypeSystem.Object);
            module.Types.Add(type);
            var method = new MethodDefinition(name, MethodAttributes.Public | MethodAttributes.Static, module.TypeSystem.Void);
            type.Methods.Add(method);
            return method;
        }

        public void Dispose() { Game.Dispose(); _platform.Dispose(); }
    }
}
