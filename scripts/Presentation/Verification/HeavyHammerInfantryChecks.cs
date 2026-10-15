using System;
using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 重锤兵分级伤害、生命成长、多重与战斗隔离验证（表现层）。
internal static class HeavyHammerInfantryChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new HeavyHammerInfantryCardDefinition();
        var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.Key != new StringName("card.heavy_hammer_infantry") || identity.DisplayName != "重锤兵"
            || identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Medium
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Human)
            || definition.InitialLevel != 2 || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(HeavyHammerInfantryCardDefinition).Assembly).Cards.ContainsKey(identity.Key)
            || texture is null || texture.GetHeight() != texture.GetWidth()) return false;
        for (var level = 2; level <= 4; level++)
        {
            var entity = new EntityFactory().CreateCard(definition, level);
            var damage = 60 << (level - 2);
            var growth = 20 << (level - 2);
            var card = new CardBattleSetup(entity.Id, 0,
                entity.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
                entity.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks), entity.Abilities,
                UseLegacyAttack: false, Tags: entity.Tags, OccupiedSlots: 2);
            if (card.AttackDamage != damage || card.CooldownTicks != 80) return false;
            foreach (var side in new[] { SideId.Player, SideId.Opponent })
            foreach (var multicast in new[] { 0, 1 })
            {
                var source = card with { Multicast = multicast };
                var attacker = new CardBattleSetup(EntityId.New(), 0, 7, 20);
                var setup = side == SideId.Player ? Battle([source], [attacker]) : Battle([attacker], [source]);
                var result = new CombatSimulator().Simulate(setup);
                var hits = result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == card.EntityId).ToArray();
                var expectedTicks = multicast == 0 ? new long[] { 80, 160 } : new long[] { 80, 82, 160, 162 };
                if (!hits.Select(hit => hit.Tick.Value).SequenceEqual(expectedTicks)
                    || hits.Any(hit => hit.RawDamage != damage || hit.TargetSide == side)
                    || result.Events.OfType<ManaChangedEvent>().Any()
                    || result.PermanentAttributeBonuses.Any() || result.PermanentChanges.Any()) return false;
                foreach (var tick in new long[] { 0, 80, 82, 160, 162 })
                {
                    var state = result.States.Last(frame => frame.Tick.Value == tick);
                    var hero = side == SideId.Player ? state.Player : state.Opponent;
                    var activations = expectedTicks.Count(value => value <= tick);
                    var incomingDamage = result.Events.OfType<DamageDealtEvent>()
                        .Where(hit => hit.TargetSide == side && hit.Tick.Value <= tick).Sum(hit => hit.HealthDamage);
                    if (hero.MaxHealth != 10000 + activations * growth
                        || hero.Health != 10000 + activations * growth - incomingDamage) return false;
                }
                var fresh = new CombatSimulator().Simulate(setup);
                if (!result.Events.SequenceEqual(fresh.Events) || fresh.States[0].Player.MaxHealth != 10000
                    || fresh.States[0].Opponent.MaxHealth != 10000) return false;
            }
        }
        foreach (var amount in new[] { 0, -1 })
        {
            try
            {
                _ = new AbilityDefinition("verification.hammer.invalid", AbilityActivation.Active, AbilityTarget.AlliedHero,
                    0, 80, [new IncreaseHeroMaxHealthEffectDefinition(amount)]);
                return false;
            }
            catch (ArgumentException) { }
        }
        return true;
    }

    private static BattleSetup Battle(CardBattleSetup[] player, CardBattleSetup[] opponent) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), player),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), opponent),
        42, new BattleTick(163), new BattleTick(164));
}
