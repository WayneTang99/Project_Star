using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 巫师权杖莫娜装备定义，暗属性召唤流派（内容层）。
public sealed class MagisterScepterCardDefinition : CardDefinition
{
    public MagisterScepterCardDefinition() : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
        new StringName("card.magister_scepter"), "巫师权杖", new StringName("mona"), CardSize.Medium, [GameElements.Dark],
        setKey: new StringName("set.wizard"), illustration: new StringName("res://art/ui/card-face/artwork/magister_scepter-illustration-dark-red.png"), descriptionEntries:
        [new(CardKeywords.Activate, "对敌方英雄造成100/200伤害。"),
         new(CardKeywords.Echo, "当己方卡牌被召唤后，此卡牌获得80/160攻击，仅本场战斗生效。")]),
        baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 50 })),
        new TagSet([GameTags.Equipment]), initialLevel: 3,
        levels: [CreateLevel(3, 100, 80), CreateLevel(4, 200, 160)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage, int growth) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 50, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
         new AbilityDefinition(new StringName("ability.gain_attack_on_allied_summon"), AbilityActivation.EchoOnAlliedCardSummoned,
            AbilityTarget.SelfCard, 0, 0, [new IncreaseSourceCardAttributeEffectDefinition(GameAttributeKeys.AttackDamage, growth)])]);
}
