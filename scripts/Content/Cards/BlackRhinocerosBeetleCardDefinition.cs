using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 黑犀金龟莫娜卡牌内容定义（内容层）。
public sealed class BlackRhinocerosBeetleCardDefinition : CardDefinition
{
    public BlackRhinocerosBeetleCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.black_rhinoceros_beetle"), "黑犀金龟", new StringName("mona"),
                CardSize.Small, [GameElements.Earth],
                illustration: new StringName("res://art/ui/card-face/artwork/black_rhinoceros_beetle-illustration-v2.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却4秒，魔法消耗0，随机使一张敌方战场卡牌获得1秒迟缓。"),
                    new(CardKeywords.Sell, "出售此卡牌后，从己方战场区最左侧向右查找第一张土属性卡牌，使其冷却永久缩减1/2/3/4%；战场区没有土属性卡牌时不生效。"),
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
        onSellReward: new ReduceLeftmostElementCardCooldownOnSellDefinition(GameElements.Earth, level));
}
