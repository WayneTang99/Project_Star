using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 骑佩短剑帕拉帝恩卡牌内容定义（内容层）。
public sealed class RidingShortswordCardDefinition : CardDefinition
{
    public RidingShortswordCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.riding_shortsword"), "骑佩短剑", new StringName("paladin"),
                CardSize.Small, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/riding_shortsword-illustration.png"), descriptionEntries:
                [
                    new(CardKeywords.Echo, "当相邻己方人类攻击卡牌发动后，对敌方英雄造成10/20/40伤害（随此卡牌攻击加成提高，包含多重的额外发动）。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.AttackDamage] = 10 })), new TagSet([GameTags.Equipment]), initialLevel: 2,
            levels: [CreateLevel(2, 10), CreateLevel(3, 20), CreateLevel(4, 40)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [new AbilityDefinition(new StringName("ability.adjacent_human_attack_echo"),
            AbilityActivation.EchoOnAdjacentAlliedAttackCardActivated, AbilityTarget.EnemyHero, 0, 0,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)], triggerCardTag: GameTags.Human)]);
}
