using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 碎骨法杖莫娜装备卡牌内容定义（内容层）。
public sealed class BonebreakerMageCardDefinition : CardDefinition
{
    public BonebreakerMageCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.bonebreaker_mage"), "碎骨法杖", new StringName("mona"),
                CardSize.Medium, [GameElements.Dark],
                illustration: new StringName("res://art/ui/card-face/artwork/bonebreaking_staff-illustration-20261022.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "消耗40魔法，摧毁己方所有小型暗属性卡牌。"),
                    new(CardKeywords.Echo, "己方小型暗属性卡牌被摧毁后，对敌方英雄造成80/160伤害。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.AttackDamage] = 80, [GameAttributeKeys.CooldownTicks] = 90 })),
            new TagSet([GameTags.Equipment]), initialLevel: 3,
            levels: [CreateLevel(3, 80), CreateLevel(4, 160)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [new AbilityDefinition(new StringName("ability.destroy_allied_cards"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 40, 90,
            [new DestroyAlliedCardsEffectDefinition(CardSize.Small, GameElements.Dark)]),
         new AbilityDefinition(new StringName("ability.damage_on_allied_card_destroyed"), AbilityActivation.EchoOnAlliedCardDestroyed,
            AbilityTarget.EnemyHero, 0, 0, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)],
            triggerCardSize: CardSize.Small, triggerCardElement: GameElements.Dark)]);
}
