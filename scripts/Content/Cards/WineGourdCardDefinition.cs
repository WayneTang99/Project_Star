using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 酒葫芦极云卡牌内容定义（内容层）。
public sealed class WineGourdCardDefinition : CardDefinition
{
    public WineGourdCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.wine_gourd"), "酒葫芦", new StringName("jiyun"),
                CardSize.Small, [GameElements.Wood],
                illustration: new StringName("res://art/ui/card-face/artwork/wine_gourd-illustration.png"),
                descriptionEntries:
                [new(CardKeywords.Activate, "冷却3秒，魔法消耗0，治疗己方英雄40/80/160/360生命，并给予随机一张己方战场卡牌1秒迟缓。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            {
                [GameAttributeKeys.CooldownTicks] = 30,
            })), new TagSet([]), initialLevel: 1,
            levels: [CreateLevel(1, 40), CreateLevel(2, 80), CreateLevel(3, 160), CreateLevel(4, 360)]) { }

    private static CardLevelDefinition CreateLevel(int level, int healing) => new(level, null,
        [new AbilityDefinition(new StringName("ability.heal_and_slow_allied_card"), AbilityActivation.Active,
            AbilityTarget.AlliedHero, 0, 30,
            [new HealEffectDefinition(healing), new ApplyStatusToRandomAlliedCardEffectDefinition(BattleStatus.SlowDuration, 10)])]);
}
