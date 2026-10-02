using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.Verification;

// 小型生命药水治疗、消耗顺序与跨战斗恢复验证（表现层验证模块）。
internal static class SmallRedPotionChecks
{
    internal static bool HealingAndConsumption()
    {
        var definition = new SmallRedPotionCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.Key != new StringName("card.small_red_potion") || identity.DisplayName != "小型生命药水"
            || identity.FactionKey != new StringName("mona") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !definition.Tags.Contains(GameTags.Consumable) || !definition.Tags.Contains(GameTags.Small)
            || definition.InitialLevel != 1 || definition.SupportsLevel(5)
            || CardKeywords.DisplayName(CardKeywords.Consume) != "消耗"
            || identity.DescriptionEntries[1].KeywordKey != CardKeywords.Consume) return false;
        int[] healing = [40, 80, 160, 240];
        for (var level = 1; level <= 4; level++)
        {
            var instance = new EntityFactory().CreateCard(definition, level);
            var abilities = definition.GetLevel(level)!.Abilities;
            var active = abilities.Single();
            if (active.CooldownTicks != 30 || active.ManaCost != 0
                || instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != 30) return false;
            var setup = new BattleSetup(
                new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0),
                    [new CardBattleSetup(instance.Id, 0, 0, 30, abilities, UseLegacyAttack: false)]),
                new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0),
                    [new CardBattleSetup(EntityId.New(), 0, 0, 1,
                        [new AbilityDefinition(new StringName("verification.potion.opening_damage"),
                            AbilityActivation.PassiveOnBattleStart, AbilityTarget.EnemyHero, 0, 0,
                            [new DamageEffectDefinition(500)])], UseLegacyAttack: false)]),
                42, new BattleTick(65));
            var simulator = new CombatSimulator();
            var result = simulator.Simulate(setup);
            var destroyed = result.Events.OfType<CardDestroyedEvent>().Single();
            var beforeDestruction = result.States.Last(frame => frame.Tick.Value == 30
                && !frame.Cards.Single(card => card.Id == instance.Id).Destroyed);
            if (destroyed.CardId != instance.Id || destroyed.Tick.Value != 30
                || beforeDestruction.Player.Health != 500 + healing[level - 1]
                || result.Events.OfType<AbilityActivatedEvent>().Count(item => item.SourceCardId == instance.Id) != 1
                || !result.States[^1].Cards.Single(card => card.Id == instance.Id).Destroyed
                || result.PermanentChanges.Count != 0
                || simulator.Simulate(setup).States[0].Cards.Single(card => card.Id == instance.Id).Destroyed) return false;
        }
        return true;
    }
}
