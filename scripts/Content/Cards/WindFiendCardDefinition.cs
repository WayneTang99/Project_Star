using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 风魔怪莫娜风属性卡牌，组合多重攻击、发动飞行与自身疾速攻击成长（内容层）。
public sealed class WindFiendCardDefinition : CardDefinition
{
    public WindFiendCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.wind_fiend"), "风魔怪", new StringName("mona"), CardSize.Medium, [GameElements.Wind],
            illustration: new StringName("res://art/ui/card-face/artwork/wind_fiend-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "对敌方英雄造成10/20/40/80伤害，此卡牌进入飞行；多重1。"),
             new(CardKeywords.Echo, "当此卡牌获得疾速后，此卡牌攻击增加5/10/20/40，仅本场战斗生效。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 60, [GameAttributeKeys.Multicast] = 1 })), new TagSet(), initialLevel: 1,
            levels: [CreateLevel(1, 10, 5), CreateLevel(2, 20, 10), CreateLevel(3, 40, 20), CreateLevel(4, 80, 40)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage, int growth) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 60, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, true)]),
         new AbilityDefinition(new StringName("ability.increase_attack_on_source_haste"), AbilityActivation.EchoOnSourceCardStatusGained,
            AbilityTarget.SelfCard, 0, 0, [new IncreaseSourceCardAttributeEffectDefinition(GameAttributeKeys.AttackDamage, growth)],
            triggerStatus: BattleStatus.HasteDuration)]);
}
