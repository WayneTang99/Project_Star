using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 教团秘法师帕拉帝恩卡牌内容定义（内容层）。
public sealed class OrderArcanistCardDefinition : CardDefinition
{
    public OrderArcanistCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.order_arcanist"), "教团秘法师", new StringName("paladin"),
                CardSize.Small, [GameElements.Light],
                illustration: new StringName("res://art/ui/card-face/artwork/order_arcanist-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却5秒，魔法消耗30，对敌方英雄造成80/160/320伤害，治疗己方英雄40/80/160生命。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 50 })),
            new TagSet([GameTags.Human]), initialLevel: 2,
            levels: [CreateLevel(2, 80, 40), CreateLevel(3, 160, 80), CreateLevel(4, 320, 160)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage, int healing) => new(level,
        new Dictionary<StringName, int>
        { [GameAttributeKeys.AttackDamage] = damage, [GameAttributeKeys.HealingBonus] = healing },
        [new AbilityDefinition(new StringName("ability.attribute_attack_and_heal"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 30, 50,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                new HealEffectDefinition(0, TargetsSourceHero: true)])]);
}
