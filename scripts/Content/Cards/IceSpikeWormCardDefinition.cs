using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 冰刺蠕虫莫娜卡牌内容定义（内容层）。
public sealed class IceSpikeWormCardDefinition : CardDefinition
{
    public IceSpikeWormCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.ice_spike_worm"), "冰刺蠕虫", new StringName("mona"),
                CardSize.Small, [GameElements.Ice],
                illustration: new StringName("res://art/ui/card-face/artwork/ice_spike_worm-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "使敌方随机1张卡牌获得1秒迟缓。"),
                    new(CardKeywords.Sell, "己方最左侧冰属性卡牌冷却永久缩减1/2/3/4%。"),
                ]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            {
                [GameAttributeKeys.CooldownTicks] = 40,
            })), new TagSet([GameTags.Insect]), initialLevel: 1,
            levels: [CreateLevel(1), CreateLevel(2), CreateLevel(3), CreateLevel(4)])
    {
    }

    private static CardLevelDefinition CreateLevel(int level) => new(level, null,
        [new AbilityDefinition(new StringName("ability.slow_random_enemy_card"), AbilityActivation.Active,
            AbilityTarget.OtherBattlefieldCards, 0, 40,
            [new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.SlowDuration, 10)])],
        onSellReward: new ReduceLeftmostElementCardCooldownOnSellDefinition(GameElements.Ice, level));
}
