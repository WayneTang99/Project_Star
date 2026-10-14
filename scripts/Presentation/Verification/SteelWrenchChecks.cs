using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 钢制扳手分级伤害、修复生命周期、位置及回放验证（表现层验证模块）。
internal static class SteelWrenchChecks
{
    internal static bool LevelsAndRestart()
    {
        var definition = new SteelWrenchCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.Key != new StringName("card.steel_wrench") || identity.DisplayName != "钢制扳手"
            || identity.FactionKey != new StringName("harla") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Equipment)
            || definition.InitialLevel != 1 || definition.SupportsLevel(0) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(SteelWrenchChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (texture is null || texture.GetWidth() * 2 != texture.GetHeight()) return false;
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        for (var level = 1; level <= 4; level++)
        {
            var wrench = Wrench(level);
            var machine = Machine(1) with { Abilities = [Attack(20), Opening([
                new IncreaseSourceCardAttributeEffectDefinition(GameAttributeKeys.AttackDamage, 7),
                new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, true), new DestroyCardEffectDefinition(false)])] };
            var setup = side == SideId.Player ? Battle([wrench, machine], [], 120) : Battle([], [wrench, machine], 120);
            var result = new CombatSimulator().Simulate(setup);
            var repairs = result.Events.OfType<CardRepairedEvent>().ToArray();
            var hits = result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == wrench.EntityId).ToArray();
            var machineHits = result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == machine.EntityId).ToArray();
            var repaired = LiveFrame(result, 50).Cards.Single(card => card.Id == machine.EntityId);
            if (repairs.Length != 1 || repairs[0].Tick.Value != 50 || repairs[0].CardId != machine.EntityId || repairs[0].Side != side
                || hits.Length != 2 || !hits.Select(hit => hit.Tick.Value).SequenceEqual(new long[] { 50, 100 })
                || hits.Any(hit => hit.RawDamage != 10 << (level - 1) || hit.TargetSide == side)
                || !machineHits.Select(hit => hit.Tick.Value).SequenceEqual(new long[] { 70, 90, 110 })
                || machineHits.Any(hit => hit.RawDamage != 38) || repaired.Destroyed || !repaired.IsFlying
                || repaired.CooldownUnits[0] != 40 || result.Events.OfType<CardAttributeChangedEvent>().Count() != 1
                || result.Events.OfType<CardDestroyedEvent>().Count() != 1 || result.PermanentChanges.Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            if (!CardDisplayAdapter.FaceEffects(snapshot).Any(effect => effect.Kind == CardFaceEffectKind.Damage
                    && effect.Value == (10 << (level - 1)).ToString())
                || !CardDisplayAdapter.AbilityDetails(snapshot.Abilities).Contains("随机修复1张")) return false;
        }
        var empty = new CombatSimulator().Simulate(Battle([Wrench(1)], [], 100));
        return empty.Events.OfType<DamageDealtEvent>().Count() == 2 && !empty.Events.OfType<CardRepairedEvent>().Any();
    }

    internal static bool SelectionAndMulticast()
    {
        var wrench = Wrench(1) with { Multicast = 1 };
        var first = Machine(1); var second = Machine(2); var third = Machine(3);
        var human = Machine(4) with { Tags = TagSet.ForCard(CardSize.Small, [GameTags.Human]) };
        var alive = Machine(5) with { Abilities = [Attack(100)] };
        var permanent = Machine(6) with { Abilities = [Opening([new DestroyCardEffectDefinition(true)])] };
        var bench = Machine(0) with { IsOnBench = true, Abilities = [Opening([new DestroyCardEffectDefinition(false)], true)] };
        var enemy = Machine(0);
        var setup = Battle([wrench, first, second, third, human, alive, permanent, bench], [enemy], 52);
        var result = new CombatSimulator().Simulate(setup);
        var repairs = result.Events.OfType<CardRepairedEvent>().ToArray();
        var eligible = new[] { first.EntityId, second.EntityId, third.EntityId };
        if (repairs.Length != 2 || repairs.Select(repair => repair.CardId).Distinct().Count() != 2
            || repairs.Any(repair => !eligible.Contains(repair.CardId) || repair.SourceCardId != wrench.EntityId)
            || result.Events.OfType<DamageDealtEvent>().Count(hit => hit.SourceCardId == wrench.EntityId) != 2
            || LiveFrame(result, 52).Cards.Count(card => eligible.Contains(card.Id) && card.Destroyed) != 1
            || !result.PermanentChanges.Any(change => change.CardId == permanent.EntityId)
            || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
        foreach (var source in new[] { Wrench(1) with { IsOnBench = true }, Wrench(1) with
            { Abilities = [Opening([new DestroyCardEffectDefinition(false)])] } })
            if (new CombatSimulator().Simulate(Battle([source, Machine(1)], [], 100)).Events.OfType<CardRepairedEvent>().Any())
                return false;
        try
        {
            _ = new AbilityDefinition(new StringName("verification.repair.invalid"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 50, [new RepairRandomDestroyedAlliedCardEffectDefinition(new StringName(""))]);
            return false;
        }
        catch (ArgumentException) { return true; }
    }

    internal static bool QueueAndRepeatedRepair()
    {
        var machine = Machine(3) with { Abilities = [Attack(50)], ElementKeys = [GameElements.Dark] };
        var killer = new CardBattleSetup(EntityId.New(), 0, 1, 50,
            [new AbilityDefinition(new StringName("verification.repair.kill"), AbilityActivation.Active, AbilityTarget.EnemyHero,
                0, 50, [new DestroyAlliedCardsAndDamageEffectDefinition(CardSize.Small, GameElements.Dark, GameAttributeKeys.AttackDamage),
                    new DestroyCardEffectDefinition(false)])], UseLegacyAttack: false, OccupiedSlots: 2,
            ElementKeys: [GameElements.General]);
        var wrench = Wrench(1) with { BoardStart = 2 };
        var result = new CombatSimulator().Simulate(Battle([killer, wrench, machine], [], 100));
        var machineHits = result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == machine.EntityId).ToArray();
        if (machineHits.Length != 1 || machineHits[0].Tick.Value != 100
            || LiveFrame(result, 50).Cards.Single(card => card.Id == machine.EntityId).CooldownUnits[0] != 100) return false;
        // 修复保留发动历史，首次倍率不会因再次修复而重新获得。
        var selfDestroy = Machine(1) with { Abilities = [new AbilityDefinition(new StringName("verification.repair.repeat"),
            AbilityActivation.Active, AbilityTarget.EnemyHero, 0, 40,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage, FirstActivationMultiplier: 2),
                new DestroyCardEffectDefinition(false)])] };
        var repeatedSetup = Battle([Wrench(1), selfDestroy], [], 150);
        var repeated = new CombatSimulator().Simulate(repeatedSetup);
        var hits = repeated.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == selfDestroy.EntityId).ToArray();
        if (!hits.Select(hit => hit.Tick.Value).SequenceEqual(new long[] { 40, 90, 140 })
            || !hits.Select(hit => hit.RawDamage).SequenceEqual(new[] { 62, 31, 31 })
            || repeated.Events.OfType<CardRepairedEvent>().Count() != 3 || repeated.PermanentChanges.Any()
            || !repeated.Events.SequenceEqual(new CombatSimulator().Simulate(repeatedSetup).Events)) return false;
        // 恢复来源光环后，完整冷却使用当下倍率，而不是摧毁期间的倍率。
        var aura = Machine(1) with { ElementKeys = [GameElements.Dark], Abilities = [Attack(40),
            Opening([new DestroyCardEffectDefinition(false)]), new AbilityDefinition(new StringName("verification.repair.aura"),
                AbilityActivation.PassiveAura, AbilityTarget.SelfCard, 0, 0,
                [new ReduceAlliedElementCardCooldownAuraEffectDefinition(GameElements.Dark, 50)])] };
        var auraResult = new CombatSimulator().Simulate(Battle([Wrench(1), aura], [], 70));
        return LiveFrame(auraResult, 50).Cards.Single(card => card.Id == aura.EntityId).CooldownUnits[0] == 40
            && auraResult.Events.OfType<DamageDealtEvent>().Single(hit => hit.SourceCardId == aura.EntityId).Tick.Value == 70;
    }

    internal static bool OccupancyAndPlayback()
    {
        var factory = new EntityFactory();
        var player = new CreateMatchService(factory).Create(42, 100, new HarlaHeroDefinition());
        var opponent = new CreateMatchService(factory).Create(43, 100, new HarlaHeroDefinition());
        opponent.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, 10000);
        var wrench = factory.CreateCard(new SteelWrenchCardDefinition(), 1);
        var machine = factory.CreateCard(new PlaybackMachineDefinition(), 1);
        var summoner = factory.CreateCard(new PlaybackSummonerDefinition(), 1);
        var killer = factory.CreateCard(new PlaybackKillerDefinition(), 1);
        var board = VerificationFixtures.CreateBoardService();
        foreach (var card in new[] { wrench, machine, summoner, killer }) player.Player.Inventory.Add(card);
        if (board.PlaceCard(player, wrench.Id, BoardZone.Battlefield, 0).IsFailure
            || board.PlaceCard(player, machine.Id, BoardZone.Battlefield, 1).IsFailure
            || board.PlaceCard(player, summoner.Id, BoardZone.Battlefield, 2).IsFailure
            || board.PlaceCard(player, killer.Id, BoardZone.Battlefield, 4).IsFailure) return false;
        var before = MatchSnapshot.From(player); var enemyBefore = MatchSnapshot.From(opponent);
        var setup = new BattleSetupFactory().Create(player, opponent, 42, new BattleTick(120), new BattleTick(121));
        var result = new CombatSimulator().Simulate(setup);
        var summon = result.Events.OfType<CardSummonedEvent>().Single();
        var repair = result.Events.OfType<CardRepairedEvent>().Single();
        if (summon.BoardStart != 1 || repair.Tick.Value != 100 || repair.CardId != machine.Id
            || result.Events.OfType<DamageDealtEvent>().Single(hit => hit.SourceCardId == machine.Id).Tick.Value != 120) return false;
        var playback = new BattlePlaybackPresenter(new BattleResolution(before, enemyBefore, result, before));
        playback.Advance(5);
        var blocked = playback.Project(SideId.Player);
        if (blocked.BoardPlacements.Single(placement => placement.Start == 1).CardId != summon.SummonedCardId
            || !playback.Capture().State.Cards.Single(card => card.Id == machine.Id).Destroyed) return false;
        playback.Advance(5);
        var restored = playback.Project(SideId.Player); var capture = playback.Capture();
        if (restored.BoardPlacements.Single(placement => placement.Start == 1).CardId != machine.Id
            || restored.BoardPlacements.Count != 4 || capture.State.Cards.Single(card => card.Id == machine.Id).Destroyed
            || capture.State.Cards.Single(card => card.Id == machine.Id).CooldownUnits[0] != 40
            || !capture.Feedback.Any(text => text.Contains("已修复"))) return false;
        playback.Skip();
        return playback.Project(SideId.Player).BoardPlacements.Count == 4 && before.Cards.Count == 4
            && MatchSnapshot.From(player).Cards.Count == 4 && result.PermanentChanges.Count == 0
            && result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events);
    }

    private static CardBattleSetup Wrench(int level)
    {
        var definition = new SteelWrenchCardDefinition(); var card = new EntityFactory().CreateCard(definition, level);
        return new CardBattleSetup(card.Id, 0, card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
            card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks), definition.GetLevel(level)!.Abilities,
            UseLegacyAttack: false, Tags: card.Tags, ElementKeys: definition.Attributes.Identity.ElementKeys) { Level = level };
    }
    private static CardBattleSetup Machine(int start) => new(EntityId.New(), start, 31, 20,
        [Attack(20), Opening([new DestroyCardEffectDefinition(false)])], UseLegacyAttack: false,
        Tags: TagSet.ForCard(CardSize.Small, [GameTags.Mechanical]), ElementKeys: [GameElements.General]);
    private static AbilityDefinition Attack(int ticks) => new(new StringName("verification.repair.attack"), AbilityActivation.Active,
        AbilityTarget.EnemyHero, 0, ticks, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]);
    private static AbilityDefinition Opening(IReadOnlyList<EffectDefinition> effects, bool allowsBench = false) => new(
        new StringName("verification.repair.opening"), AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0,
        effects, allowsBench);
    private static BattleStateSnapshot LiveFrame(BattleResult result, int tick) => result.States.Last(
        frame => frame.Tick.Value == tick && frame.Player.Health > 0 && frame.Opponent.Health > 0);
    private static BattleSetup Battle(CardBattleSetup[] player, CardBattleSetup[] opponent, int ticks) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), player),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), opponent),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));

    // 修复回放使用的正式工厂夹具，保持战前卡池与Runtime隔离（表现层验证模块）。
    private sealed class PlaybackMachineDefinition : CardDefinition
    {
        public PlaybackMachineDefinition() : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("verification.repair.machine"), "验证机械", new StringName("harla"), CardSize.Small, [GameElements.Dark]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = 31,
                [GameAttributeKeys.CooldownTicks] = 20 })), new TagSet([GameTags.Mechanical]),
            [Attack(20), Opening([new DestroyCardEffectDefinition(false)])]) { }
    }
    // 临时人类仅用于验证被摧毁位置与修复的位置冲突（表现层验证模块）。
    private sealed class PlaybackSummonDefinition : CardDefinition
    {
        public PlaybackSummonDefinition() : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("verification.repair.summon"), "验证占位物", new StringName("harla"), CardSize.Small, [GameElements.Dark])),
            new TagSet([GameTags.Human])) { }
    }
    // 使用通用开战召唤在原机械位置产生占位物（表现层验证模块）。
    private sealed class PlaybackSummonerDefinition : CardDefinition
    {
        public PlaybackSummonerDefinition() : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("verification.repair.summoner"), "验证召唤源", new StringName("harla"), CardSize.Medium, [GameElements.General])),
            new TagSet(), [Opening([new SummonAdjacentCardEffectDefinition(new PlaybackSummonDefinition(), AdjacentCardSide.Left)])]) { }
    }
    // 使用通用摧毁效果释放占位物的位置（表现层验证模块）。
    private sealed class PlaybackKillerDefinition : CardDefinition
    {
        public PlaybackKillerDefinition() : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("verification.repair.killer"), "验证摧毁源", new StringName("harla"), CardSize.Medium, [GameElements.General]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = 1,
                [GameAttributeKeys.CooldownTicks] = 80 })), new TagSet(), [new AbilityDefinition(new StringName("verification.repair.release"),
                AbilityActivation.Active, AbilityTarget.EnemyHero, 0, 80,
                [new DestroyAlliedCardsAndDamageEffectDefinition(CardSize.Small, GameElements.Dark, GameAttributeKeys.AttackDamage)])]) { }
    }
}
