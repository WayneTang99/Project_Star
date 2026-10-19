using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 暗史莱姆无阵营卡牌定义，复用通用随机禁锢效果（内容层）。
public sealed class DarkSlimeCardDefinition : CardDefinition
{
    public DarkSlimeCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.dark_slime"), "暗史莱姆", GameFactions.Neutral, CardSize.Small, [GameElements.Dark],
            illustration: new StringName("res://art/ui/card-face/artwork/dark_slime-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "冷却3秒，魔法消耗0，给予敌方随机1/2张存活战场卡牌1秒禁锢。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 30 })),
            new TagSet([GameTags.Material]), initialLevel: 3,
            levels: [CreateLevel(3), CreateLevel(4)]) { }

    private static CardLevelDefinition CreateLevel(int level) => new(level,
        null,
        [new AbilityDefinition(new StringName("ability.random_enemy_immobilize"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 30,
            [new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.ImmobilizeDuration, 10, level - 2)])]);
}
