using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 巫毒娃娃莫娜卡牌定义，复用冷却筛选回响与属性中毒效果（内容层）。
public sealed class VoodooDollCardDefinition : CardDefinition
{
    public VoodooDollCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.voodoo_doll"), "巫毒娃娃", new StringName("mona"), CardSize.Small, [GameElements.Dark],
            illustration: new StringName("res://art/ui/card-face/artwork/voodoo_doll-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Echo, "当敌方存活战场当前完整冷却最短的卡牌发动后（并列取最左侧1张），对敌方英雄施加4/8/16中毒。")])),
            new TagSet([]), initialLevel: 2,
            levels: [CreateLevel(2, 4), CreateLevel(3, 8), CreateLevel(4, 16)]) { }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.Poison] = amount },
        [new AbilityDefinition(new StringName("ability.poison_on_shortest_cooldown_enemy_activation"),
            AbilityActivation.EchoOnShortestCooldownEnemyCardActivated, AbilityTarget.EnemyHero, 0, 0,
            [new ApplyAttributeStatusEffectDefinition(BattleStatus.Poison, GameAttributeKeys.Poison)])]);
}
