using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 碎骨法师莫娜卡牌内容定义（内容层）。
public sealed class BonebreakerMageCardDefinition : CardDefinition
{
    public BonebreakerMageCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.bonebreaker_mage"), "碎骨法师", new StringName("mona"),
                CardSize.Medium, [GameElements.Dark],
                illustration: new StringName("res://art/ui/card-face/artwork/bonebreaker_mage-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却9秒，魔法消耗0，摧毁己方所有存活战场小型暗属性卡牌，每实际摧毁一张，对敌方英雄造成80/160伤害；摧毁仅本场战斗生效。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.AttackDamage] = 80, [GameAttributeKeys.CooldownTicks] = 90 })),
            new TagSet([GameTags.Human]), initialLevel: 3,
            levels: [CreateLevel(3, 80), CreateLevel(4, 160)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [new AbilityDefinition(new StringName("ability.destroy_allied_cards_and_damage"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 90,
            [new DestroyAlliedCardsAndDamageEffectDefinition(CardSize.Small, GameElements.Dark, GameAttributeKeys.AttackDamage)])]);
}
