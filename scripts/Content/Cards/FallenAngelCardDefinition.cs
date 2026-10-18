using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

public sealed class FallenAngelCardDefinition : CardDefinition
{
    public FallenAngelCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.fallen_angel"), "堕天使", new StringName("paladin"), CardSize.Medium, [GameElements.Dark],
            illustration: new StringName("res://art/ui/card-face/artwork/fallen_angel-illustration-20261018-v4.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "冷却6秒，魔法消耗0，治疗己方英雄等同于己方存活战场暗属性卡牌数量×20/40/80的生命，包含此卡牌；敌方随机1张存活战场卡牌获得1秒禁锢，此卡牌进入飞行。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 60 })),
            new TagSet(), initialLevel: 2, levels: [CreateLevel(2, 20), CreateLevel(3, 40), CreateLevel(4, 80)]) { }

    private static CardLevelDefinition CreateLevel(int level, int coefficient) => new(level, null,
        [new AbilityDefinition(new StringName("ability.element_count_heal_and_random_immobilize"), AbilityActivation.Active,
            AbilityTarget.AlliedHero, 0, 60,
            [new HealEffectDefinition(coefficient, PerAlliedElementKey: GameElements.Dark),
             new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.ImmobilizeDuration, 10),
             new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, true)])]);
}
