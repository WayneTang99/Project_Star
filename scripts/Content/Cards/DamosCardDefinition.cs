using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 天启套装无阵营卡牌，组合通用发动与日蚀进入能力。
public sealed class DamosCardDefinition : CardDefinition
{
    public DamosCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.damos"), "征服之手达摩斯", new StringName("neutral"),
            CardSize.Medium, [GameElements.General], setKey: new StringName("set.apocalypse"),
            illustration: new StringName("res://art/ui/card-face/artwork/damos-illustration-neutral-v3.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却5秒，对敌方英雄造成20/40/80/160点伤害。"),
                new(CardKeywords.Echo, "进入日蚀时间时，此卡牌攻击翻倍，仅本场战斗生效。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 50 })), new TagSet([GameTags.Human]), initialLevel: 1,
            levels: [CreateLevel(1, 20), CreateLevel(2, 40), CreateLevel(3, 80), CreateLevel(4, 160)]) { }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = amount },
        [
            new AbilityDefinition(new StringName("ability.attack"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 50, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            new AbilityDefinition(new StringName("ability.eclipse_double_attackdamage"), AbilityActivation.PassiveOnEclipseStart,
                AbilityTarget.SelfCard, 0, 0, [new MultiplySourceCardAttributeEffectDefinition(GameAttributeKeys.AttackDamage, 2)]),
        ]);
}

