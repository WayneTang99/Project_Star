using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 隐秘竹林极云卡牌内容定义（内容层）。
public sealed class HiddenBambooGroveCardDefinition : CardDefinition
{
    public HiddenBambooGroveCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.hidden_bamboo_grove"), "隐秘竹林", new StringName("jiyun"),
                CardSize.Large, [GameElements.Wood],
                illustration: new StringName("res://art/ui/card-face/artwork/hidden_bamboo_grove-illustration.png"),
                descriptionEntries:
                [new(CardKeywords.Echo, "当己方木属性卡牌发动后，给予随机一张敌方战场卡牌1/2/3秒迟缓。")])),
            new TagSet([GameTags.Plant, GameTags.Location]), initialLevel: 2,
            levels: [CreateLevel(2, 10), CreateLevel(3, 20), CreateLevel(4, 30)]) { }

    private static CardLevelDefinition CreateLevel(int level, int slowTicks) => new(level, null,
        [new AbilityDefinition(new StringName("ability.slow_on_allied_element_activation"),
            AbilityActivation.EchoOnMatchingAlliedCardActivated, AbilityTarget.SelfCard, 0, 0,
            [new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.SlowDuration, slowTicks)],
            triggerCardElement: GameElements.Wood)]);
}
