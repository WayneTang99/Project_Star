using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 雷史莱姆无阵营卡牌定义，复用通用属性伤害效果（内容层）。
public sealed class LightningSlimeCardDefinition : CardDefinition
{
    public LightningSlimeCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.lightning_slime"), "雷史莱姆", GameFactions.Neutral, CardSize.Small, [GameElements.Lightning],
            illustration: new StringName("res://art/ui/card-face/artwork/lightning_slime-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "冷却3秒，魔法消耗0，对敌方英雄造成10/20/40/80伤害。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 30 })),
            new TagSet([GameTags.Material]), initialLevel: 1,
            levels: [CreateLevel(1, 10), CreateLevel(2, 20), CreateLevel(3, 40), CreateLevel(4, 80)]) { }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = amount },
        [new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 30,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)])]);
}
