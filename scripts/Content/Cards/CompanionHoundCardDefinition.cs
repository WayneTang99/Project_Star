using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 伙伴猎犬罗宾卡牌定义，组合属性攻击与相邻小型发动成长（内容层）。
public sealed class CompanionHoundCardDefinition : CardDefinition
{
    public CompanionHoundCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.companion_hound"), "伙伴猎犬", new StringName("robin"),
            CardSize.Medium, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/companion_hound-illustration.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却7秒，魔法消耗0，对敌方英雄造成40/80/160/320伤害。"),
                new(CardKeywords.Echo, "当直接相邻的己方战场小型卡牌发动后，此卡牌攻击增加5/10/20/40；多重发动分别触发，仅本场战斗生效。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.AttackDamage] = 40, [GameAttributeKeys.CooldownTicks] = 70 })),
            new TagSet([GameTags.Beast]), initialLevel: 1,
            levels: [CreateLevel(1, 40, 5), CreateLevel(2, 80, 10), CreateLevel(3, 160, 20), CreateLevel(4, 320, 40)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage, int bonus) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition("ability.attribute_attack", AbilityActivation.Active, AbilityTarget.EnemyHero,
                0, 70, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            GrowOnActivation(AdjacentCardSide.Left, "ability.grow_attack_on_left_small_activation", bonus),
            GrowOnActivation(AdjacentCardSide.Right, "ability.grow_attack_on_right_small_activation", bonus),
        ]);

    private static AbilityDefinition GrowOnActivation(AdjacentCardSide side, StringName key, int bonus) => new(key,
        AbilityActivation.EchoOnAdjacentAlliedCardActivated, AbilityTarget.SelfCard, 0, 0,
        [new IncreaseSourceCardAttributeEffectDefinition(GameAttributeKeys.AttackDamage, bonus)],
        triggerCardSide: side, triggerCardSize: CardSize.Small);
}
