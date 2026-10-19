using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 铁蒺藜圣骑士陷阱定义，敌方获得疾速后造成属性伤害（内容层）。
public sealed class CaltropsCardDefinition : CardDefinition
{
    public CaltropsCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.caltrops"), "铁蒺藜", new StringName("paladin"), CardSize.Small, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/caltrops-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Echo, "当敌方存活战场卡牌获得疾速后，对敌方英雄造成10/20/40/80伤害。")])),
            new TagSet([GameTags.Trap]), initialLevel: 1,
            levels: [CreateLevel(1, 10), CreateLevel(2, 20), CreateLevel(3, 40), CreateLevel(4, 80)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [new AbilityDefinition(new StringName("ability.damage_on_enemy_haste_gained"), AbilityActivation.EchoOnEnemyCardStatusGained,
            AbilityTarget.EnemyHero, 0, 0, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)],
            triggerStatus: BattleStatus.HasteDuration)]);
}
