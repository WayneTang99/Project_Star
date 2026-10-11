using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 见习剑士帕拉帝恩卡牌内容定义（内容层）。
public sealed class ApprenticeSwordsmanCardDefinition : CardDefinition
{
    public ApprenticeSwordsmanCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.apprentice_swordsman"), "见习剑士", new StringName("paladin"),
                CardSize.Small, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/apprentice_swordsman-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却7秒，魔法消耗0，对敌方英雄造成10/20/40/80伤害。"),
                    new(CardKeywords.Echo, "当己方存活战场中型人类卡牌发动后，此卡牌获得1秒充能；多重发动分别触发。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.AttackDamage] = 10, [GameAttributeKeys.CooldownTicks] = 70 })),
            new TagSet([GameTags.Human]), initialLevel: 1,
            levels: [CreateLevel(1, 10), CreateLevel(2, 20), CreateLevel(3, 40), CreateLevel(4, 80)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 70, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            new AbilityDefinition(new StringName("ability.charge_on_allied_matching_card_activation"),
                AbilityActivation.EchoOnMatchingAlliedCardActivated, AbilityTarget.SelfCard, 0, 0,
                [new ChargeSourceCardEffectDefinition(10)], triggerCardTag: GameTags.Human, triggerCardSize: CardSize.Medium),
        ]);
}
