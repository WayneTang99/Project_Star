using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 碧玉蟾极云卡牌内容定义（内容层）。
public sealed class JadeToadCardDefinition : CardDefinition
{
    public JadeToadCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.jade_toad"), "碧玉蟾", new StringName("jiyun"),
                CardSize.Medium, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/jade_toad-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却5秒，魔法消耗0，对敌方英雄施加等同于此卡牌当前价值的中毒。"),
                    new(CardKeywords.AfterBattle, "每场战斗结束后，若此卡牌开战时位于战场区，其价值永久增加3/5/8/12，不论胜负。"),
                ]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 50 })),
            new TagSet([GameTags.Beast]), initialLevel: 1,
            levels: [CreateLevel(1, 3), CreateLevel(2, 5), CreateLevel(3, 8), CreateLevel(4, 12)])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int valueBonus) => new(level, null,
        [new AbilityDefinition(new StringName("ability.poison_equal_to_value"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 50,
            [new ApplyAttributeStatusEffectDefinition(BattleStatus.Poison, GameAttributeKeys.Value)])],
        battleValueBonus: valueBonus);
}
