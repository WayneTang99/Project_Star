using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 南瓜灯莫娜卡牌定义，组合随机迟缓与相邻发动生命再生（内容层）。
public sealed class PumpkinLanternCardDefinition : CardDefinition
{
    public PumpkinLanternCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.pumpkin_lantern"), "南瓜灯", new StringName("mona"),
            CardSize.Medium, [GameElements.Wood, GameElements.Dark],
            illustration: new StringName("res://art/ui/card-face/artwork/pumpkin_lantern-illustration.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "给予敌方随机2张卡牌1秒迟缓。"),
                new(CardKeywords.Echo, "当直接相邻的己方战场卡牌发动后，己方英雄获得2/4/8/16生命再生。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 50 })), new TagSet(), initialLevel: 1,
            levels: [CreateLevel(1, 2), CreateLevel(2, 4), CreateLevel(3, 8), CreateLevel(4, 16)]) { }

    private static CardLevelDefinition CreateLevel(int level, int regeneration) => new(level, null,
        [
            new AbilityDefinition(new StringName("ability.random_two_slow"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 50,
                [new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.SlowDuration, 10, 2)]),
            Echo(AdjacentCardSide.Left, new StringName("ability.health_regen_on_left_activation"), regeneration),
            Echo(AdjacentCardSide.Right, new StringName("ability.health_regen_on_right_activation"), regeneration),
        ]);

    private static AbilityDefinition Echo(AdjacentCardSide side, StringName key, int regeneration) => new(key,
        AbilityActivation.EchoOnAdjacentAlliedCardActivated, AbilityTarget.AlliedHero, 0, 0,
        [new ApplyStatusEffectDefinition(BattleStatus.HealthRegen, regeneration)], triggerCardSide: side);
}
