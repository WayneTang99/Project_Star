using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 冒险者猎弓罗宾卡牌定义，组合攻击与怪物胜利奖励（内容层）。
public sealed class AdventurerHuntingBowCardDefinition : CardDefinition
{
    public AdventurerHuntingBowCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.adventurer_hunting_bow"), "冒险者猎弓", new StringName("robin"),
            CardSize.Medium, [GameElements.General], setKey: new StringName("set.adventurer"),
            illustration: new StringName("res://art/ui/card-face/artwork/adventurer_hunting_bow-illustration.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却6秒，魔法消耗0，对敌方英雄造成40/80/160/320伤害。"),
                new(CardKeywords.Victory, "此卡牌在战场区击败怪物后，获得一张与此卡牌等级相同的兽皮。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 60 })), new TagSet([GameTags.Equipment]), initialLevel: 1,
            levels: [CreateLevel(1, 40), CreateLevel(2, 80), CreateLevel(3, 160), CreateLevel(4, 320)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition(new StringName("ability.hunting_bow_attack"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 60, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            new AbilityDefinition(new StringName("ability.monster_victory_beast_hide"), AbilityActivation.PassiveOnMonsterVictory,
                AbilityTarget.AlliedHero, 0, 0, [new GrantSourceLevelCardEffectDefinition(new BeastHideCardDefinition())]),
        ]);
}
