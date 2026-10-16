using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 弓骑兵帕拉帝恩卡牌内容定义（内容层）。
public sealed class MountedArcherCardDefinition : CardDefinition
{
    public MountedArcherCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.mounted_archer"), "弓骑兵", new StringName("paladin"),
                CardSize.Medium, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/mounted_archer-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却5秒，魔法消耗0，对敌方英雄造成80/160/320伤害。"),
                    new(CardKeywords.Aura, "此卡牌与载具相邻时，发动时左右直接相邻的己方战场载具获得1秒疾速。"),
                ]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            {
                [GameAttributeKeys.CooldownTicks] = 50,
            })), new TagSet([GameTags.Human]), initialLevel: 2,
            levels: [CreateLevel(2, 80), CreateLevel(3, 160), CreateLevel(4, 320)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [new AbilityDefinition(new StringName("ability.attack_and_hasten_adjacent_mounts"),
            AbilityActivation.Active, AbilityTarget.EnemyHero, 0, 50,
            [
                new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                new ApplyStatusToAdjacentAlliedCardsEffectDefinition(BattleStatus.HasteDuration, 10,
                    GameTags.Vehicle, BonusMultiplier: 1, RequiredTag: GameTags.Vehicle),
            ])]);
}
