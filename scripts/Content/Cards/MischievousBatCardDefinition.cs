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
                    new(CardKeywords.Activate, "对敌方英雄造成1伤害，并使敌方随机1张未被摧毁的战场卡牌获得1秒迟缓。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.AttackDamage] = 1, [GameAttributeKeys.CooldownTicks] = 50 })),
            new TagSet([GameTags.Beast]), initialLevel: 1,
            levels: [CreateLevel(1, 50), CreateLevel(2, 40), CreateLevel(3, 30), CreateLevel(4, 20)]) { }

    private static CardLevelDefinition CreateLevel(int level, int cooldownTicks) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = 1, [GameAttributeKeys.CooldownTicks] = cooldownTicks },
        [new AbilityDefinition(new StringName("ability.attribute_attack_and_random_slow"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, cooldownTicks,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.SlowDuration, 10)])]);
}
