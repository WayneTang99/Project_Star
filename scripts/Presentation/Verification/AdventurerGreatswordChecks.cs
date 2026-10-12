using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 大剑身份、两级英雄等级伤害、冷却与确定性验证（表现层）。
internal static class AdventurerGreatswordChecks
{
    internal static bool IdentityAndDamage()
    {
        var definition = new AdventurerGreatswordCardDefinition();
        var identity = definition.Attributes.Identity;
        var registry = DefinitionRegistry.Scan(typeof(AdventurerGreatswordCardDefinition).Assembly);
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (!registry.Cards.ContainsKey(identity.Key) || identity.DisplayName != "冒险者大剑"
            || identity.FactionKey != new StringName("robin") || identity.SetKey != new StringName("set.adventurer")
            || identity.Size != CardSize.Large || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.InitialLevel != 3 || definition.SupportsLevel(2) || definition.SupportsLevel(5)
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Equipment)
            || texture is null || texture.GetWidth() * 2 != texture.GetHeight() * 3) return false;
        for (var level = 3; level <= 4; level++)
        {
            var card = new EntityFactory().CreateCard(definition, level);
            var ability = definition.GetLevel(level)!.Abilities.Single();
            var multiplier = level == 3 ? 20 : 40;
            if (ability.Activation != AbilityActivation.Active || ability.Target != AbilityTarget.EnemyHero
                || ability.CooldownTicks != 80 || ability.ManaCost != 0
                || card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != 80
                || ability.Effects.Single() is not SourceHeroLevelScaledDamageEffectDefinition effect
                || effect.Multiplier != multiplier || effect.BypassArmor) return false;
            foreach (var heroLevel in new[] { 1, 3, 7 })
            {
                var source = new CardBattleSetup(card.Id, 0, 0, 80, [ability], UseLegacyAttack: false,
                    Tags: card.Tags, OccupiedSlots: 3, ElementKeys: identity.ElementKeys) { Level = level };
                var setup = new BattleSetup(
                    new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0, Level: heroLevel), [source]),
                    new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 15, Level: 9), []),
                    42, new BattleTick(160), new BattleTick(161));
                var result = new CombatSimulator().Simulate(setup);
                var hits = result.Events.OfType<DamageDealtEvent>().ToArray();
                if (hits.Length != 2 || !hits.Select(hit => hit.Tick.Value).SequenceEqual(new long[] { 80, 160 })
                    || hits.Any(hit => hit.RawDamage != heroLevel * multiplier || hit.SourceCardId != card.Id)
                    || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            }
        }
        return true;
    }
}
