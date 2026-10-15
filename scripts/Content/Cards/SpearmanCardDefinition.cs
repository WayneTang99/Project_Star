using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 枪矛手帕拉帝恩卡牌内容定义（内容层）。
public sealed class SpearmanCardDefinition : CardDefinition
{
    public SpearmanCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.spearman"), "枪矛手", new StringName("paladin"),
                CardSize.Small, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/spearman-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却6秒，魔法消耗0，对敌方英雄造成10/20/40/80伤害。"),
                    new(CardKeywords.Echo, "当直接相邻的己方战场人类卡牌发动后，此卡牌攻击增加5/10/15/20；多重发动分别触发，仅本场战斗生效。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.AttackDamage] = 10, [GameAttributeKeys.CooldownTicks] = 60 })),
            new TagSet([GameTags.Human]), initialLevel: 1,
            levels: [CreateLevel(1, 10, 5), CreateLevel(2, 20, 10), CreateLevel(3, 40, 15), CreateLevel(4, 80, 20)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage, int bonus) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition("ability.attribute_attack", AbilityActivation.Active, AbilityTarget.EnemyHero,
                0, 60, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            GrowOnActivation(AdjacentCardSide.Left, "ability.grow_attack_on_left_human_activation", bonus),
            GrowOnActivation(AdjacentCardSide.Right, "ability.grow_attack_on_right_human_activation", bonus),
        ]);

    private static AbilityDefinition GrowOnActivation(AdjacentCardSide side, StringName key, int bonus) => new(key,
        AbilityActivation.EchoOnAdjacentAlliedCardActivated, AbilityTarget.SelfCard, 0, 0,
        [new IncreaseSourceCardAttributeEffectDefinition(GameAttributeKeys.AttackDamage, bonus)],
        triggerCardTag: GameTags.Human, triggerCardSide: side);
}
