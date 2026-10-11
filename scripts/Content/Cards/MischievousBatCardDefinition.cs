using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 捣蛋鬼蝙蝠莫娜卡牌内容定义（内容层）。
public sealed class MischievousBatCardDefinition : CardDefinition
{
    public MischievousBatCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.mischievous_bat"), "捣蛋鬼蝙蝠", new StringName("mona"),
                CardSize.Small, [GameElements.Dark],
                illustration: new StringName("res://art/ui/card-face/artwork/mischievous_bat-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却2秒，魔法消耗0，对敌方英雄造成1伤害，并使敌方随机一张未被摧毁的战场卡牌获得1/2/3秒迟缓。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.AttackDamage] = 1, [GameAttributeKeys.CooldownTicks] = 20 })),
            new TagSet([GameTags.Beast]), initialLevel: 2,
            levels: [CreateLevel(2, 10), CreateLevel(3, 20), CreateLevel(4, 30)]) { }

    private static CardLevelDefinition CreateLevel(int level, int slowTicks) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = 1 },
        [new AbilityDefinition(new StringName("ability.attribute_attack_and_random_slow"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 20,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.SlowDuration, slowTicks)])]);
}
