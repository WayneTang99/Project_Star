using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 攻城弩车圣骑士机械：属性攻击与自身疾速获得后的本场成长。
public sealed class SiegeBallistaCardDefinition : CardDefinition
{
    public SiegeBallistaCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.siege_ballista"), "攻城弩车", new StringName("paladin"), CardSize.Large, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/siege_ballista-illustration-action-v3.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却7秒，魔法消耗0，对敌方英雄造成160/320伤害。"),
                new(CardKeywords.Echo, "当此卡牌获得疾速后，此卡牌攻击增加20/40，仅本场战斗生效。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.AttackDamage] = 160, [GameAttributeKeys.CooldownTicks] = 70 })),
            new TagSet([GameTags.Mechanical]), initialLevel: 3,
            levels: [CreateLevel(3, 160, 20), CreateLevel(4, 320, 40)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage, int bonus) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 70, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            new AbilityDefinition(new StringName("ability.increase_attack_on_source_haste"), AbilityActivation.EchoOnSourceCardStatusGained,
                AbilityTarget.SelfCard, 0, 0, [new IncreaseSourceCardAttributeEffectDefinition(GameAttributeKeys.AttackDamage, bonus)],
                triggerStatus: BattleStatus.HasteDuration),
        ]);
}
