using System;
using System.Linq;
using Godot;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Verification;

// 捣蛋鬼蝙蝠分级冷却、随机迟缓和战场目标验证（表现层验证模块）。
internal static class MischievousBatChecks
{
    internal static bool LevelsAndTargets()
    {
        var definition = new MischievousBatCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.Key != new StringName("card.mischievous_bat") || identity.DisplayName != "捣蛋鬼蝙蝠"
            || identity.FactionKey != new StringName("mona") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Dark }) || !definition.Tags.Contains(GameTags.Beast)
            || definition.InitialLevel != 1 || definition.SupportsLevel(0) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(MischievousBatChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (texture is null || texture.GetWidth() * 2 != texture.GetHeight()) return false;
        foreach (var level in new[] { 1, 2, 3, 4 })
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        {
            var cooldown = (6 - level) * 10;
            var bat = Bat(level);
            var ally = Target(1);
            var first = Target(0);
            var second = Target(1);
            var bench = Target(2) with { IsOnBench = true };
            var dead = Target(3) with { Abilities = [Opening(new DestroyCardEffectDefinition(false))] };
            CardBattleSetup[] allies = [bat, ally];
            CardBattleSetup[] enemies = [first, second, bench, dead];
            var setup = side == SideId.Player ? Battle(allies, enemies, cooldown * 2) : Battle(enemies, allies, cooldown * 2);
            var result = new CombatSimulator().Simulate(setup);
            var hits = result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == bat.EntityId).ToArray();
            var frame = LiveFrame(result, cooldown);
            var slowed = frame.Cards.Where(card => card.Slow > 0).ToArray();
            if (hits.Length != 2 || !hits.Select(hit => hit.Tick.Value).SequenceEqual(new long[] { cooldown, cooldown * 2 })
                || hits.Any(hit => hit.RawDamage != 1 || hit.HealthDamage != 1 || hit.TargetSide == side)
                || slowed.Length != 1 || !new[] { first.EntityId, second.EntityId }.Contains(slowed[0].Id)
                || slowed[0].Slow != 10 || !frame.Cards.Single(card => card.Id == dead.EntityId).Destroyed
                || frame.Cards.Single(card => card.Id == bat.EntityId).IsFlying || result.PermanentChanges.Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            var offer = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            if (!CardDisplayAdapter.FaceEffects(offer).Any(effect => effect.Kind == CardFaceEffectKind.Damage && effect.Value == "1")
                || !CardDisplayAdapter.Details(offer).Contains("1秒迟缓")) return false;
        }
        return true;
    }

    internal static bool StackingAndEmptyTargets()
    {
        foreach (var level in new[] { 1, 2, 3, 4 })
        {
            var bat = Bat(level) with { AttackDamage = 7, Multicast = 1 };
            var target = Target(0);
            var ticks = 10;
            var cooldown = (6 - level) * 10;
            var result = new CombatSimulator().Simulate(Battle([bat], [target], cooldown * 2 + 2));
            if (result.Events.OfType<DamageDealtEvent>().Count(hit => hit.SourceCardId == bat.EntityId && hit.RawDamage == 7) != 4
                || LiveFrame(result, cooldown + 2).Cards.Single(card => card.Id == target.EntityId).Slow != ticks * 2 - 2
                || LiveFrame(result, cooldown * 2 + 2).Cards.Single(card => card.Id == target.EntityId).Slow != Math.Max(0, ticks * 2 - cooldown) + ticks * 2 - 2)
                return false;
            var flying = target with { Abilities = [Opening(new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, true))] };
            var flight = new CombatSimulator().Simulate(Battle([Bat(level)], [flying], cooldown));
            if (LiveFrame(flight, cooldown).Cards.Single(card => card.Id == flying.EntityId).Slow != ticks / 2) return false;
        }
        var source = Bat(4);
        foreach (var enemies in new[] { Array.Empty<CardBattleSetup>(), new[] { Target(0) with { IsOnBench = true } } })
        {
            var result = new CombatSimulator().Simulate(Battle([source], enemies, 40));
            if (result.Events.OfType<DamageDealtEvent>().Count(hit => hit.SourceCardId == source.EntityId) != 2
                || result.Events.OfType<StatusChangedEvent>().Any(status => status.Status == BattleStatus.SlowDuration)) return false;
        }
        var bench = new CombatSimulator().Simulate(Battle([source with { IsOnBench = true }], [Target(0)], 40));
        var dead = source with { Abilities = source.Abilities!.Append(Opening(new DestroyCardEffectDefinition(false))).ToArray() };
        var destroyed = new CombatSimulator().Simulate(Battle([dead], [Target(0)], 40));
        return !bench.Events.OfType<DamageDealtEvent>().Any() && !destroyed.Events.OfType<DamageDealtEvent>().Any();
    }

    private static CardBattleSetup Bat(int level)
    {
        var definition = new MischievousBatCardDefinition();
        var card = new EntityFactory().CreateCard(definition, level);
        return new CardBattleSetup(card.Id, 0, card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
            card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks), definition.GetLevel(level)!.Abilities,
            UseLegacyAttack: false, Tags: card.Tags, ElementKeys: definition.Attributes.Identity.ElementKeys) { Level = level };
    }
    private static CardBattleSetup Target(int start) => new(EntityId.New(), start, 0, 0, [], UseLegacyAttack: false);
    private static AbilityDefinition Opening(EffectDefinition effect) => new(new StringName("verification.bat.opening"),
        AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [effect]);
    private static BattleStateSnapshot LiveFrame(BattleResult result, int tick) => result.States.Last(
        frame => frame.Tick.Value == tick && frame.Player.Health > 0 && frame.Opponent.Health > 0);
    private static BattleSetup Battle(CardBattleSetup[] player, CardBattleSetup[] opponent, int ticks) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), player),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), opponent),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
