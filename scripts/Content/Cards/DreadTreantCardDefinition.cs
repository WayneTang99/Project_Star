using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 恐惧树精莫娜卡牌定义，组合魔法恢复、治疗与随机迟缓（内容层）。
public sealed class DreadTreantCardDefinition : CardDefinition
{
    public DreadTreantCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.dread_treant"), "恐惧树精", new StringName("mona"), CardSize.Medium, [GameElements.Wood],
            illustration: new StringName("res://art/ui/card-face/artwork/dread_treant-illustration-20261022.png"),
            descriptionEntries: [new(CardKeywords.Activate, "恢复40/80/160魔法，治疗40/80/160生命，给予敌方随机1张卡牌1秒迟缓。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 70 })), new TagSet([GameTags.Plant, GameTags.Spirit]), initialLevel: 2,
            levels: [CreateLevel(2, 40), CreateLevel(3, 80), CreateLevel(4, 160)]) { }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level, null,
        [new AbilityDefinition(new StringName("ability.restore_mana_heal_and_random_slow"), AbilityActivation.Active,
            AbilityTarget.AlliedHero, 0, 70,
            [new RestoreManaEffectDefinition(amount), new HealEffectDefinition(amount),
                new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.SlowDuration, 10)])]);
}
