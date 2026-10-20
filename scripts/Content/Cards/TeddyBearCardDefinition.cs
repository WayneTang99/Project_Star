using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 玩具熊莫娜卡牌定义，半血回响献祭自身及邻居并召唤（内容层）。
public sealed class TeddyBearCardDefinition : CardDefinition
{
    public TeddyBearCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.teddy_bear"), "玩具熊", new StringName("mona"), CardSize.Small, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/teddy_bear-illustration.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "对敌方英雄造成20/40/80伤害。"),
                new(CardKeywords.Echo, "己方英雄生命低于一半时，摧毁此卡牌和两侧相邻卡牌，召唤【暴怒熊】并使其进入狂暴。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 40 })), new TagSet(), initialLevel: 2,
            levels: [CreateLevel(2, 20), CreateLevel(3, 40), CreateLevel(4, 80)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 40, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            new AbilityDefinition(new StringName("ability.sacrifice_neighbors_summon_on_low_health"),
                AbilityActivation.EchoOnSourceHeroHealthBelow, AbilityTarget.SelfCard, 0, 0,
                [new SacrificeSourceAndNeighborsToSummonEffectDefinition(new RagingBearCardDefinition(), GameAttributeKeys.Berserk)],
                triggerHeroHealthPercent: 50),
        ]);
}
