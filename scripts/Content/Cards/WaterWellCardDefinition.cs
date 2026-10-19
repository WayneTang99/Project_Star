using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 水井圣骑士水属性卡牌，组合治疗与固定灼伤扣减（内容层）。
public sealed class WaterWellCardDefinition : CardDefinition
{
    public WaterWellCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.water_well"), "水井", new StringName("paladin"), CardSize.Small, [GameElements.Water],
            illustration: new StringName("res://art/ui/card-face/artwork/water_well-illustration-redraw-20261019.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "冷却6秒，魔法消耗0，治疗己方英雄10/20/40/80生命，并减少己方英雄身上4/8/16/32灼伤，最多扣除当前拥有的量。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 60 })),
            new TagSet([]), initialLevel: 1,
            levels: [CreateLevel(1, 10, 4), CreateLevel(2, 20, 8), CreateLevel(3, 40, 16), CreateLevel(4, 80, 32)]) { }

    private static CardLevelDefinition CreateLevel(int level, int healing, int reduction) => new(level, null,
        [new AbilityDefinition(new StringName("ability.heal_and_reduce_hero_status"), AbilityActivation.Active,
            AbilityTarget.AlliedHero, 0, 60,
            [new HealEffectDefinition(healing), new ReduceHeroStatusEffectDefinition(BattleStatus.Burn, reduction)])]);
}
