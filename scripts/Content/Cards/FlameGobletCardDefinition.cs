using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 火焰杯莫娜卡牌内容定义（内容层）。
public sealed class FlameGobletCardDefinition : CardDefinition
{
    public FlameGobletCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.flame_goblet"), "火焰杯", new StringName("mona"),
                CardSize.Medium, [GameElements.Fire],
                illustration: new StringName("res://art/ui/card-face/artwork/flame_goblet-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却6秒，魔法消耗0，对敌方英雄施加6/12/24灼伤。"),
                    new(CardKeywords.Echo, "己方火属性卡牌发动时，此卡牌灼伤增加2/4/8，仅本场战斗生效。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 60 })), new TagSet([]), initialLevel: 2,
            levels: [CreateLevel(2, 6, 2), CreateLevel(3, 12, 4), CreateLevel(4, 24, 8)]) { }

    private static CardLevelDefinition CreateLevel(int level, int burn, int bonus) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.Burn] = burn },
        [
            new AbilityDefinition(new StringName("ability.apply_burn"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 60,
                [new ApplyAttributeStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.Burn)]),
            new AbilityDefinition(new StringName("ability.increase_source_burn_on_fire_activation"),
                AbilityActivation.EchoOnMatchingAlliedCardActivated, AbilityTarget.SelfCard, 0, 0,
                [new IncreaseSourceCardAttributeEffectDefinition(GameAttributeKeys.Burn, bonus)],
                triggerCardElement: GameElements.Fire),
        ]);
}
