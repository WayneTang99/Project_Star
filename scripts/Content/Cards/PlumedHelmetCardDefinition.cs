using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 羽饰头盔圣骑士卡牌定义（内容层）。
public sealed class PlumedHelmetCardDefinition : CardDefinition
{
    public PlumedHelmetCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.plumed_helmet"), "羽饰头盔", new StringName("paladin"),
                CardSize.Small, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/plumed_helmet-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却5秒，魔法消耗0，对敌方英雄造成20/40/80伤害（随此卡牌攻击加成提高）。"),
                    new(CardKeywords.Aura, "相邻己方战场人类卡牌发动时，此卡牌获得1秒充能；多重发动分别触发。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 20,
                    [GameAttributeKeys.CooldownTicks] = 50,
                })), new TagSet([GameTags.Equipment]), initialLevel: 2,
            levels: [CreateLevel(2, 20), CreateLevel(3, 40), CreateLevel(4, 80)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 50,
                [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            ChargeOnActivation(AdjacentCardSide.Left, "ability.charge_on_left_tagged_activation"),
            ChargeOnActivation(AdjacentCardSide.Right, "ability.charge_on_right_tagged_activation"),
        ]);

    private static AbilityDefinition ChargeOnActivation(AdjacentCardSide side, StringName key) => new(key,
        AbilityActivation.EchoOnAdjacentAlliedCardActivated, AbilityTarget.SelfCard, 0, 0,
        [new ChargeSourceCardEffectDefinition(10)], triggerCardTag: GameTags.Human, triggerCardSide: side);
}
