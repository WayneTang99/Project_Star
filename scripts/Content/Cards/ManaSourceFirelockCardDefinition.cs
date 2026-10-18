using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

public sealed class ManaSourceFirelockCardDefinition : CardDefinition
{
    public ManaSourceFirelockCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.mana_source_firelock"), "魔源火铳", new StringName("paladin"), CardSize.Medium, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/mana_source_firelock-illustration-20261018.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "冷却6秒，消耗50/40/30/20魔法，对敌方英雄造成20/40/80/160伤害和6/12/24/48灼伤。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 60 })),
            new TagSet([GameTags.Equipment]), initialLevel: 1,
            levels: [CreateLevel(1, 50, 20, 6), CreateLevel(2, 40, 40, 12), CreateLevel(3, 30, 80, 24), CreateLevel(4, 20, 160, 48)]) { }

    private static CardLevelDefinition CreateLevel(int level, int mana, int damage, int burn) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage, [GameAttributeKeys.Burn] = burn },
        [new AbilityDefinition(new StringName("ability.attribute_attack_and_burn"), AbilityActivation.Active, AbilityTarget.EnemyHero,
            mana, 60, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage), new ApplyAttributeStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.Burn)])]);
}
