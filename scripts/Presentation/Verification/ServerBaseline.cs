using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Encounters;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Application.Mentors;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Infrastructure.Encounters;
using Project_Star.Infrastructure.Random;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// P0 无界面运行与性能基准入口，不作为正式存档或网络协议（表现层验证模块）。
public sealed partial class ServerBaseline : Node
{
    private static readonly JsonSerializerOptions Json = new() { Converters = { new StringNameConverter() } };

    // 独立场景运行，不创建 UI、不加载美术资源；失败返回非零退出码。
    public override void _Ready()
    {
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
            if (OS.GetCmdlineUserArgs().Contains("--probe-stdio"))
            {
                ProbeStandardStreams();
                GetTree().Quit();
                return;
            }
            var registry = DefinitionRegistry.Scan(typeof(ServerBaseline).Assembly);
            Require(CombatTraceChecks.PreservesTraces(), "existing fixed combat baseline");
            CheckWireAndRandom();
            var mechanismChecks = CheckMechanisms();
            var route = CheckRoute(registry);
            var reports = new[]
            {
                Measure("initial-formal-cards", CreateSetup(registry, false, 4)),
                Measure("level-four-full-board", CreateSetup(registry, true, 10))
            };
            using var process = Process.GetCurrentProcess();
            var report = new
            {
                schema = "p0-diagnostic-v2", platform = OS.GetName(),
                engine = Engine.GetVersionInfo()["string"].AsString(), runtime = System.Environment.Version.ToString(),
                assemblyModuleVersionId = typeof(ServerBaseline).Assembly.ManifestModule.ModuleVersionId,
                processorCount = System.Environment.ProcessorCount, processPeakWorkingSetBytes = process.PeakWorkingSet64,
                registeredContent = new { heroes = registry.Heroes.Count, cards = registry.Cards.Count,
                    skills = registry.Skills.Count, encounters = registry.Encounters.Count, monsters = registry.Monsters.Count },
                wireProbePassed = true, fixedCombatBaselinePassed = true, mechanismChecks, route, battles = reports
            };
            var output = OS.GetCmdlineUserArgs().FirstOrDefault(arg => arg.StartsWith("--report=", StringComparison.Ordinal));
            if (output is not null)
            {
                var path = Path.GetFullPath(output["--report=".Length..]);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllText(path, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
            }
            GD.Print("SERVER_BASELINE_JSON=" + JsonSerializer.Serialize(report));
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            GD.PrintErr("SERVER_BASELINE_FAILED=" + exception);
            GetTree().Quit(1);
        }
    }

    private static void ProbeStandardStreams()
    {
        // 固定两帧诊断，不作为正式规则 RPC 或长期进程协议。
        for (var sequence = 0; sequence < 2; sequence++)
        {
            var line = Console.ReadLine() ?? throw new InvalidOperationException("Missing probe input");
            var probe = JsonSerializer.Deserialize<WireProbe>(line, Json)
                ?? throw new InvalidOperationException("Invalid probe input");
            var seed = ulong.Parse(probe.Seed, CultureInfo.InvariantCulture);
            Require(!probe.Key.IsEmpty, "probe key");
            Console.WriteLine("SERVER_PROBE_JSON=" + JsonSerializer.Serialize(new
            {
                sequence, probe.Key, Seed = seed.ToString(CultureInfo.InvariantCulture), probe.Multiplier,
                nextRandom = new SeededRandom(seed).NextInt(0, 1000)
            }, Json));
        }
    }

    private static object[] CheckMechanisms()
    {
        object Check(string name, Func<bool> run)
        {
            var started = Stopwatch.GetTimestamp();
            Require(run(), "mechanism check: " + name);
            return new { name, passed = true, milliseconds = Stopwatch.GetElapsedTime(started).TotalMilliseconds };
        }
        return
        [
            Check("quest-thresholds", AscendantChecks.CombatLevelsAndThresholds),
            Check("quest-permanent-growth-upgrade", AscendantChecks.CrossBattleAndUpgrade),
            Check("quest-merge-persistence", AscendantChecks.MergePreservesProgress),
            Check("quest-source-destroyed", BoardChecks.CheckCardQuestBattleDestroy),
            Check("gem-primary-effects", GemShopChecks.ActivationAndPrimary),
            Check("gem-fees-refresh", GemShopChecks.FeesAndRefresh),
            Check("gem-upgrade-sockets", GemGloveChecks.UpgradeAndSockets),
            Check("summon-position-ordering", TemplarKnightChecks.PositionsAndOrdering),
            Check("summon-isolation", TemplarKnightChecks.LifecycleAndValidation),
            Check("repair-cooldown", SteelWrenchChecks.CombatLevelsAndRestart),
            Check("repair-queue", SteelWrenchChecks.QueueAndRepeatedRepair),
            Check("stealth-duration", ShadowCloakChecks.CombatLevelsAndTiming),
            Check("stealth-damage-kinds", ShadowCloakChecks.DamageKindsAndEchoes),
            Check("element-sequence", ElementSequenceChecks.WindowsAndResults),
            Check("flying-duration", CardStateChecks.FlyingDurations),
            Check("berserk-damage", CardStateChecks.BerserkDamageAndStatus)
        ];
    }

    private static object CheckRoute(DefinitionRegistry registry)
    {
        var factory = new EntityFactory();
        var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
        var economy = new CardEconomyService(factory, board, registry.Cards.Values);
        var presenter = new MatchPresenter(registry, board, economy, new ShopCardPoolService(),
            new ResolveEncounterOptionService(factory, board, registry.Cards.Values, registry.Monsters.Values),
            new MentorService(registry, new SkillAcquisitionService(factory)),
            new MonsterRewardClaimService(registry, economy, new SkillAcquisitionService(factory), board),
            new GameCoordinator(new CreateMatchService(factory, registry.Gems.Values, registry),
                new EncounterScheduler(registry, allowIncompleteMonsterChoices: true),
                new StartBattleService(new BattleSetupFactory(registry.Sets), new CombatSimulator()),
                new MatchResultService(board, registry.Cards.Values)), new LocalTestOpponentProvider(registry));
        presenter.Reset();
        presenter.SelectHero(new StringName("hero.paladin"));
        var startingWealth = presenter.View.Player!.Wealth;
        var purchases = 0;
        var monsters = 0;
        var pvp = 0;
        for (var turn = 1; turn <= 8; turn++)
        {
            Require(presenter.View.Page == MatchPage.EncounterChoice, $"route turn {turn}: {presenter.View.Message}");
            var choices = presenter.View.Player!.EncounterChoices;
            var selected = choices.FirstOrDefault(choice => turn is 4 or 8
                || registry.Encounters.TryGetValue(choice.Key, out var encounter) && encounter is CardShopEncounterDefinition);
            Require(selected is not null, $"route has no card shop/battle at turn {turn}");
            presenter.ChooseEncounter(selected!.Key);
            if (turn is 4 or 8)
            {
                Require(presenter.View.Page == MatchPage.Preparation, $"preparation at turn {turn}");
                presenter.StartBattle();
                Require(presenter.View.Page == MatchPage.BattlePlayback, $"playback at turn {turn}");
                presenter.SkipPlayback();
                Require(presenter.View.Page == MatchPage.BattleResult, $"battle result at turn {turn}");
                if (turn == 4) monsters++; else pvp++;
            }
            else
            {
                Require(presenter.View.Page == MatchPage.Shop, $"shop at turn {turn}");
                var offer = presenter.View.Offers.FirstOrDefault(item => item.Action.Enabled);
                if (offer is not null)
                {
                    var before = presenter.View.Player!.Wealth;
                    presenter.BuyCard(offer.Index, offer.Revision);
                    Require(presenter.View.Player!.Wealth == before - offer.Price, "purchase applied");
                    purchases++;
                }
            }
            presenter.ContinueMatch();
        }
        Require(purchases > 0 && monsters == 1 && pvp == 1, "route exercised purchase/monster/PvP");
        Require(presenter.View.DisplayRound == 2 && presenter.View.DisplayTurn == 1, "route reached round 2");
        return new { hero = "hero.paladin", purchases, monsters, pvp, startingWealth,
            reachedRound = presenter.View.DisplayRound, reachedTurn = presenter.View.DisplayTurn,
            opponentSource = nameof(LocalTestOpponentProvider), realPlayerMatching = false,
            incompleteMonsterChoicesAllowed = true };
    }

    private static BattleSetup CreateSetup(DefinitionRegistry registry, bool levelFour, int slots)
    {
        var factory = new EntityFactory();
        BattleSideSetup Side(int identityBase, bool reverse)
        {
            var session = new CreateMatchService(factory, registry.Gems.Values, registry)
                .Create(42, 0, registry.Heroes[new StringName("hero.paladin")]);
            var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
            var economy = new CardEconomyService(factory, board, registry.Cards.Values);
            var definitions = registry.Cards.Values.OrderBy(card => card.Attributes.Identity.Key.ToString(), StringComparer.Ordinal).ToArray();
            if (reverse) Array.Reverse(definitions);
            var occupied = 0;
            foreach (var definition in definitions)
            {
                var size = definition.Attributes.Identity.OccupiedSlots;
                var level = levelFour ? 4 : definition.InitialLevel;
                if (occupied + size > slots || !definition.SupportsLevel(level)) continue;
                var acquired = economy.AcquireCard(session, definition, level, CardAcquisitionSource.Reward);
                Require(acquired.IsSuccess, "formal card acquisition");
                Require(board.PlaceCard(session, acquired.Value!.Id, Project_Star.Domain.Match.BoardZone.Battlefield, occupied).IsSuccess,
                    "formal placement");
                occupied += size;
                if (occupied == slots) break;
            }
            var frozen = new BattleSetupFactory(registry.Sets).CreatePreview(session).Player;
            return new BattleSideSetup(frozen.Hero with { EntityId = Id(identityBase), MaxHealth = levelFour ? 10000 : frozen.Hero.MaxHealth },
                frozen.Cards.Select((card, index) => card with { EntityId = Id(identityBase + index + 1) }).ToArray(),
                frozen.Skills, frozen.Sets) { CardDefinitions = frozen.CardDefinitions, BattlefieldCapacity = frozen.BattlefieldCapacity };
        }
        return new BattleSetup(Side(100, false), Side(200, true), 42, new BattleTick(1200), new BattleTick(300));
    }

    private static object Measure(string name, BattleSetup setup)
    {
        var simulator = new CombatSimulator();
        var expected = Capture(simulator.Simulate(setup));
        var digest = Convert.ToHexString(SHA256.HashData(expected));
        var durations = new double[20];
        long allocated = 0;
        BattleResult result = null!;
        for (var index = 0; index < durations.Length; index++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var start = Stopwatch.GetTimestamp();
            result = simulator.Simulate(setup);
            durations[index] = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
            allocated += GC.GetAllocatedBytesForCurrentThread() - before;
            Require(Capture(result).SequenceEqual(expected), $"repeat fingerprint: {name}, sample {index}");
        }
        Array.Sort(durations);
        using var compressed = new MemoryStream();
        using (var gzip = new GZipStream(compressed, CompressionLevel.Fastest, true)) gzip.Write(expected);
        object[] Cards(BattleSideSetup side) => side.Cards.Select(card => (object)new
            { key = card.Identity!.Key.ToString(), card.Level, card.BoardStart, card.OccupiedSlots }).ToArray();
        return new { name, seed = setup.Seed.ToString(CultureInfo.InvariantCulture), timeoutTicks = setup.Timeout.Value,
            player = Cards(setup.Player), opponent = Cards(setup.Opponent), samples = durations.Length,
            medianMilliseconds = durations[durations.Length / 2], p95Milliseconds = durations[18], maxMilliseconds = durations[^1],
            meanAllocatedBytes = allocated / durations.Length, result.Outcome, endedAtTick = result.EndedAt.Value,
            eventCount = result.Events.Count, stateCount = result.States.Count, diagnosticSha256 = digest,
            diagnosticJsonBytes = expected.Length, diagnosticGzipBytes = compressed.Length };
    }

    private static byte[] Capture(BattleResult result) => JsonSerializer.SerializeToUtf8Bytes(new
    {
        result.Outcome, result.EndReason, endedAt = result.EndedAt.Value, result.PlayerRemainingHealth, result.OpponentRemainingHealth,
        events = result.Events.Select(item => new { type = item.GetType().Name,
            data = JsonSerializer.SerializeToElement(item, item.GetType(), Json) }),
        result.PermanentChanges, result.PermanentAttributeBonuses,
        // 诊断投影覆盖数值与位置事实；不声称是完整能力或正式回放序列化格式。
        states = result.States.Select(frame => new { tick = frame.Tick.Value, frame.EventCount, frame.Eclipse, frame.Player, frame.Opponent,
            cards = frame.Cards.Select(card => new { card.Id, card.Side, card.Destroyed, card.Haste, card.Slow, card.Immobilize,
                card.CooldownUnits, card.CooldownDurationUnits, card.CooldownMultiplier, card.EffectAmounts,
                values = card.Values.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal)
                    .Select(pair => new { key = pair.Key.ToString(), pair.Value }),
                card.Quests, card.QuestElementKeys, card.QuestTags, card.Level, card.IsFlying, card.IsBerserk, card.IsOnBench,
                summonedKey = card.SummonedCard?.Identity.Key.ToString(), summonedStart = card.SummonedCard?.BoardStart,
                transformedKey = card.TransformedIdentity?.Key.ToString() }) })
    }, Json);

    private static void CheckWireAndRandom()
    {
        var key = new StringName("verification.server_roundtrip");
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new WireProbe(key, ulong.MaxValue.ToString(CultureInfo.InvariantCulture), 0.125m), Json);
        var restored = JsonSerializer.Deserialize<WireProbe>(bytes, Json)!;
        Require(restored.Key == key && ulong.Parse(restored.Seed, CultureInfo.InvariantCulture) == ulong.MaxValue
            && restored.Multiplier == 0.125m, "StringName/ulong/decimal JSON probe");
        var original = new SeededRandom(42);
        for (var index = 0; index < 10; index++) original.NextInt(0, 100);
        var resumed = new SeededRandom(original.State);
        for (var index = 0; index < 20; index++) Require(original.NextInt(0, 100) == resumed.NextInt(0, 100), "resumed random");
    }

    private static EntityId Id(int value) => new(new Guid($"00000000-0000-0000-0000-{value:000000000000}"));
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    // 仅验证边界编码的固定数据，不表示对局存档格式（表现层验证模块）。
    private sealed record WireProbe(StringName Key, string Seed, decimal Multiplier);

    // 诊断数据中的 StringName 显式转为文本，避免序列化原生句柄（表现层验证模块）。
    private sealed class StringNameConverter : JsonConverter<StringName>
    {
        // 从诊断 JSON 的文本恢复 key。
        public override StringName Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => new(reader.GetString()!);
        // 只输出 key 的文本值。
        public override void Write(Utf8JsonWriter writer, StringName value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
    }
}
