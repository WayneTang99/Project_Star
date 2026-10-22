using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 冰封祭坛莫娜卡牌，半血首次回响禁锢全部敌方卡牌后自毁。
public sealed class FrozenAltarCardDefinition : CardDefinition
{
    public FrozenAltarCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.frozen_altar"), "冰封祭坛", new StringName("mona"), CardSize.Large, [GameElements.Ice],
            illustration: new StringName("res://art/ui/card-face/artwork/frozen_altar-illustration-20261022.png"),
            descriptionEntries: [new(CardKeywords.Echo,
                "每场战斗，己方英雄生命首次低于最大生命的50%时，给予敌方所有存活战场卡牌1/2秒禁锢，并摧毁此卡牌。")]),
            baseCombat: new ModifiableAttributeSet()), new TagSet([GameTags.Building]), initialLevel: 3,
            levels: [CreateLevel(3, 10), CreateLevel(4, 20)]) { }

    private static CardLevelDefinition CreateLevel(int level, int duration) => new(level, null,
        [new AbilityDefinition(new StringName("ability.immobilize_enemies_on_low_health"),
            AbilityActivation.EchoOnSourceHeroHealthBelow, AbilityTarget.SelfCard, 0, 0,
            [new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.ImmobilizeDuration, duration, TargetCount: int.MaxValue),
             new DestroyCardEffectDefinition(false)], triggerHeroHealthPercent: 50)]);
}
