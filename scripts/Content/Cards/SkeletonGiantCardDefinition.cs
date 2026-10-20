using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 骷髅巨人莫娜亡灵卡牌，组合重击与己方小型摧毁后的自身充能（内容层）。
public sealed class SkeletonGiantCardDefinition : CardDefinition
{
    public SkeletonGiantCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.skeleton_giant"), "骷髅巨人", new StringName("mona"), CardSize.Large, [GameElements.Dark],
            illustration: new StringName("res://art/ui/card-face/artwork/skeleton_giant-illustration-colossal-20261020.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "对敌方英雄造成240/480/980伤害。"),
             new(CardKeywords.Echo, "当己方战场小型卡牌被摧毁后，此卡牌获得1秒充能。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 160 })),
            new TagSet([GameTags.Undead]), initialLevel: 2,
            levels: [CreateLevel(2, 240), CreateLevel(3, 480), CreateLevel(4, 980)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 160, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
         new AbilityDefinition(new StringName("ability.charge_on_small_allied_destruction"), AbilityActivation.EchoOnAlliedCardDestroyed,
            AbilityTarget.SelfCard, 0, 0, [new ChargeSourceCardEffectDefinition(10)], triggerCardSize: CardSize.Small)]);
}
