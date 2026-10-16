using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Content.Sets;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 卡面最终施加量与多重的只读预览、实时回放和布局验证（表现层）。
internal static class CardFinalValueChecks
{
    private static readonly Dictionary<StringName, CardSetDefinition> NoSets = new()
    { [new StringName("set.adventurer")] = new AdventurerSetDefinition() };

    internal static bool Preparation()
    {
        var factory = new EntityFactory();
        var session = new CreateMatchService(factory).Create(42, 100, new RobinHeroDefinition());
        var board = new BoardService(new BoardPlacementSolver());
        var receiver = factory.CreateCard(new DisplayCardDefinition());
        var provider = factory.CreateCard(new AuraDefinition());
        session.Player.Inventory.Add(receiver); session.Player.Inventory.Add(provider);
        Require(board.PlaceCard(session, receiver.Id, BoardZone.Battlefield, 0).IsSuccess, "receiver placement");
        Require(board.PlaceCard(session, provider.Id, BoardZone.Battlefield, 1).IsSuccess, "aura placement");
        var random = session.Random.State;
        var frozen = MatchDisplayQuery.Capture(session, NoSets).Cards.Single(card => card.Id == receiver.Id);
        Require(Values(frozen).SequenceEqual(new[] { "27", "13", "9" }), "preparation final damage/armor/healing");
        Require(new CardDisplayAdapter().Build(frozen).Multicast == 2, "preparation aura multicast");
        Require(receiver.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) == 20
            && receiver.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Armor) == 10
            && session.Random.State == random, "preview has no writes");
        Require(board.PlaceCard(session, provider.Id, BoardZone.Bench, 0).IsSuccess, "aura removal");
        var removed = MatchDisplayQuery.Capture(session, NoSets).Cards.Single(card => card.Id == receiver.Id);
        Require(Values(removed).SequenceEqual(new[] { "20", "10", "5" })
            && new CardDisplayAdapter().Build(removed).Multicast == 0, "aura contribution removed");
        Require(Values(frozen).SequenceEqual(new[] { "27", "13", "9" }), "old preview frozen");
        Require(board.PlaceCard(session, receiver.Id, BoardZone.Bench, 1).IsSuccess, "receiver bench");
        Require(Values(MatchDisplayQuery.Capture(session, NoSets).Cards.Single(card => card.Id == receiver.Id))
            .SequenceEqual(new[] { "20", "10", "5" }), "bench base values retained");
        var backpack = factory.CreateCard(new AdventurerBackpackCardDefinition(), 2);
        session.Player.Inventory.Add(backpack);
        Require(board.PlaceCard(session, backpack.Id, BoardZone.Battlefield, 0).IsSuccess, "backpack placement");
        var bag = MatchDisplayQuery.Capture(session, NoSets).Cards.Single(card => card.Id == backpack.Id);
        Require(Values(bag).Single() == "+10", "hero level aura numeric");
        var shield = MatchDisplayQuery.FromOffer(ShopOffer.Create(new ArcaneShieldCardDefinition()));
        Require(Values(shield).SequenceEqual(new[] { "累计魔法" }), "zero armor bonus merged");
        var shieldRule = CardDisplayAdapter.Rules(shield).Single();
        Require(shieldRule.Contains("本场累计魔法消耗的护甲（包含本次消耗）")
            && shieldRule.Contains("） + 0")
            && !shieldRule.Contains("获得护甲 0"), "mana armor rule preserves formula without separate zero armor");
        var enhanced = shield with { CurrentValues = new Dictionary<StringName, int>(shield.CurrentValues)
            { [GameAttributeKeys.Armor] = 7 } };
        Require(Values(enhanced).SequenceEqual(new[] { "累计魔法+7" }), "nonzero armor bonus merged");
        Require(CardDisplayAdapter.Rules(enhanced).Single() == shieldRule.Replace(" + 0", " + 7"), "mana armor rule includes current bonus without replacing formula");
        Require(CardDisplayAdapter.Rules(shield with { CurrentValues = new Dictionary<StringName, int>(shield.CurrentValues)
            { [GameAttributeKeys.Armor] = -3 } }).Single() == shieldRule.Replace(" + 0", " − 3"), "mana armor rule displays negative bonus");
        var ability = shield.Abilities.Single();
        var replay = enhanced with { Abilities = new[] { new AbilityDefinition(ability.Key, ability.Activation,
            ability.Target, ability.ManaCost, ability.CooldownTicks,
            [.. ability.Effects, new RestoreManaEffectDefinition(3)]) }, EffectAmounts = new int?[] { 30, 9, 3 } };
        Require(Values(replay).SequenceEqual(new[] { "39", "3" }), "frozen armor sum and subsequent effect index");
        Require(CardDisplayAdapter.Rules(replay).Single().Contains("包含本次消耗")
            && CardDisplayAdapter.Rules(replay).Single().Contains("恢复 3 魔法"), "mana armor replay preserves formula and subsequent effect");
        return true;
    }

    internal static bool Playback()
    {
        var factory = new EntityFactory();
        var player = new CreateMatchService(factory).Create(42, 100, new RobinHeroDefinition());
        var enemy = new CreateMatchService(factory).Create(43, 100, new RobinHeroDefinition());
        var receiver = factory.CreateCard(new DisplayCardDefinition());
        var provider = factory.CreateCard(new AuraDefinition(true));
        var bench = factory.CreateCard(new DisplayCardDefinition());
        player.Player.Inventory.Add(receiver); player.Player.Inventory.Add(provider);
        player.Player.Inventory.Add(bench);
        var board = new BoardService(new BoardPlacementSolver());
        Require(board.PlaceCard(player, receiver.Id, BoardZone.Battlefield, 0).IsSuccess, "live receiver");
        Require(board.PlaceCard(player, provider.Id, BoardZone.Battlefield, 1).IsSuccess, "live provider");
        Require(board.PlaceCard(player, bench.Id, BoardZone.Bench, 0).IsSuccess, "live bench");
        var setup = new BattleSetupFactory().Create(player, enemy, 42, new BattleTick(30), new BattleTick(31));
        var result = new CombatSimulator().Simulate(setup);
        var before = MatchSnapshot.From(player);
        var playback = new BattlePlaybackPresenter(new BattleResolution(before, MatchSnapshot.From(enemy), result, before));
        var initial = playback.Project(SideId.Player).Cards.Single(card => card.Id == receiver.Id);
        Require(Values(initial).SequenceEqual(new[] { "27", "13", "9" }) && new CardDisplayAdapter().Build(initial).Multicast == 2,
            "live aura projection");
        Require(Values(playback.Project(SideId.Player).Cards.Single(card => card.Id == bench.Id)).SequenceEqual(new[] { "20", "10", "5" })
            && !result.Events.OfType<AbilityActivatedEvent>().Any(item => item.SourceCardId == bench.Id), "bench frozen values without activation");
        playback.Advance(1.1);
        var removed = playback.Project(SideId.Player).Cards.Single(card => card.Id == receiver.Id);
        Require(Values(removed).SequenceEqual(new[] { "20", "10", "5" }) && new CardDisplayAdapter().Build(removed).Multicast == 0,
            "live destroyed aura removed");
        Require(Values(initial).SequenceEqual(new[] { "27", "13", "9" }), "frozen replay projection");
        playback.TogglePause();
        Require(!playback.Advance(1) && Values(playback.Project(SideId.Player).Cards.Single(card => card.Id == receiver.Id))
            .SequenceEqual(Values(removed)), "paused preview remains frozen");
        // 实时狂暴、生命公式和宝石百分比从战斗层冻结，UI不再套公式。
        var active = new AbilityDefinition("verification.face.scaled", AbilityActivation.Active, AbilityTarget.EnemyHero, 0, 20,
            [new SourceHeroHealthScaledAttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                new ApplyPrimaryEffectPercentStatusEffectDefinition(BattleStatus.Burn, 10)]);
        var scaled = new CardBattleSetup(EntityId.New(), 0, 100, 20,
            [active, new AbilityDefinition("verification.face.berserk", AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.SelfCard, 0, 0, [new SetSourceCardStateEffectDefinition(GameAttributeKeys.Berserk, true)]),
                new AbilityDefinition("verification.face.health", AbilityActivation.PassiveOnBattleStart,
                    AbilityTarget.AlliedHero, 0, 0, [new DamageEffectDefinition(50)])])
            { CombatValues = new Dictionary<StringName, int> { [GameAttributeKeys.BurnBonus] = 3 } };
        var scaledResult = new CombatSimulator().Simulate(new BattleSetup(
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 100, 0, ManaRegen: 0), [scaled]),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), []), 42,
            new BattleTick(20), new BattleTick(21)));
        var state = scaledResult.States.Last(frame => frame.Tick.Value == 20 && frame.Player.Health > 0).Cards.Single();
        var displayed = initial with { CurrentValues = state.Values, Abilities = scaled.Abilities!, EffectAmounts = state.EffectAmounts };
        Require(Values(displayed).Take(2).SequenceEqual(new[] { "60", "15" }),
            $"health/berserk/gem final amounts actual={string.Join(',', Values(displayed))}, frozen={string.Join(',', state.EffectAmounts)}");
        Require(scaledResult.Events.OfType<DamageDealtEvent>().Single(hit => hit.TargetSide == SideId.Opponent).RawDamage == 60,
            "displayed damage matches execution");
        Require(scaledResult.Events.OfType<StatusChangedEvent>().Single().Amount == 15, "gem preview matches execution");
        return true;
    }

    internal static bool FaceScene(Node owner)
    {
        var adapter = new CardDisplayAdapter();
        var scene = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardFace.tscn");
        foreach (var size in new[] { CardSize.Small, CardSize.Medium, CardSize.Large })
        {
            var face = scene.Instantiate<Project_Star.Presentation.CardFace.CardFace>(); owner.AddChild(face);
            try
            {
                var sample = MatchDisplayQuery.FromOffer(ShopOffer.Create(new ArmguardCardDefinition())) with { Size = size,
                    CurrentValues = new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = 37,
                        [GameAttributeKeys.Armor] = 18, [GameAttributeKeys.Multicast] = 3 } };
                face.SetDisplaySize(new Vector2(76 * (int)size, 152)); face.SetCard(adapter.Build(sample));
                var rows = face.GetNode<Control>("Effects");
                var multicast = rows.GetChildren().OfType<CardEffectRow>().Last();
                Require(multicast.Visible && multicast.GetNode<Label>("Value").Text == "3"
                    && face.GetGlobalRect().Encloses(multicast.GetGlobalRect()), "multicast effect bounds");
                face.SetCard(adapter.Build(sample with { CurrentValues = new Dictionary<StringName, int>() }));
                Require(rows.GetChildren().OfType<CardEffectRow>().All(row => row.GetNode<Label>("Value").Text != "3"), "zero multicast removed after refresh");
            }
            finally { owner.RemoveChild(face); face.Free(); }
        }
        return true;
    }

    private static string[] Values(CardSnapshot card) => CardDisplayAdapter.FaceEffects(card).Select(effect => effect.Value).ToArray();
    internal static async Task Capture(Node owner)
    {
        Require(Preparation() && Playback() && FaceScene(owner), "focused checks");
        var viewport = new SubViewport { Size = new Vector2I(640, 420), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Size = new Vector2(640, 420), Color = new Color("333c41") });
            var samples = new[] { MatchDisplayQuery.FromOffer(ShopOffer.Create(new ArmguardCardDefinition())),
                MatchDisplayQuery.FromOffer(ShopOffer.Create(new RadiantDragonCardDefinition())),
                MatchDisplayQuery.FromOffer(ShopOffer.Create(new MechanismHeavyCrossbowCardDefinition())) };
            for (var index = 0; index < samples.Length; index++)
            {
                var sample = samples[index];
                var face = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardFace.tscn")
                    .Instantiate<Project_Star.Presentation.CardFace.CardFace>(); viewport.AddChild(face);
                face.Position = index == 0 ? new Vector2(20, 35) : new Vector2(125, 35 + (index - 1) * 185);
                face.SetDisplaySize(index == 0 ? new Vector2(76, 152) : new Vector2(240, 160));
                face.SetCard(new CardDisplayAdapter().Build(sample with
                {
                    CurrentValues = new Dictionary<StringName, int>(sample.CurrentValues) { [GameAttributeKeys.Multicast] = index + 1 },
                }));
            }
            var medium = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardFace.tscn")
                .Instantiate<Project_Star.Presentation.CardFace.CardFace>(); viewport.AddChild(medium);
            medium.Position = new Vector2(395, 35); medium.SetDisplaySize(new Vector2(152, 152));
            var bag = MatchDisplayQuery.FromOffer(ShopOffer.Create(new AdventurerBackpackCardDefinition()));
            medium.SetCard(new CardDisplayAdapter().Build(bag with { EffectAmounts = Array.AsReadOnly(new int?[] { 10 }) }));
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            Require(image.SavePng("res://output/card-final-values/card-faces.png") == Error.Ok, "capture saved");
            GD.Print("卡面最终值和多重：通过");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("Card final values: " + message); }

    private sealed class DisplayCardDefinition : CardDefinition
    {
        internal DisplayCardDefinition() : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes("verification.face.receiver", "卡面验证", GameFactions.Neutral, CardSize.Small, [GameElements.Light]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = 20,
                [GameAttributeKeys.Armor] = 10, [GameAttributeKeys.CooldownTicks] = 20 })), new TagSet([]),
            [new AbilityDefinition("verification.face.attack", AbilityActivation.Active, AbilityTarget.EnemyHero, 0, 20,
                [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                    new GainSourceHeroArmorFromAttributeEffectDefinition(GameAttributeKeys.Armor), new HealEffectDefinition(5, true)])]) { }
    }

    private sealed class AuraDefinition : CardDefinition
    {
        internal AuraDefinition(bool destroy = false) : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes("verification.face.aura", "光环验证", GameFactions.Neutral, CardSize.Small, [GameElements.General])),
            new TagSet([]), destroy ? [Aura(), new AbilityDefinition("verification.face.destroy", AbilityActivation.Active,
                AbilityTarget.SelfCard, 0, 10, [new DestroyCardEffectDefinition(false)])] : [Aura()]) { }
        private static AbilityDefinition Aura() => new("verification.face.aura", AbilityActivation.PassiveAura, AbilityTarget.SelfCard, 0, 0,
            [new IncreaseAlliedCardAttributeAuraEffectDefinition(GameAttributeKeys.AttackDamage, 7),
                new IncreaseAlliedCardAttributeAuraEffectDefinition(GameAttributeKeys.Armor, 3),
                new IncreaseAlliedCardAttributeAuraEffectDefinition(GameAttributeKeys.HealingBonus, 4),
                new GrantMulticastToAlliedElementCardsEffectDefinition(GameElements.Light, 2)]);
    }
}
