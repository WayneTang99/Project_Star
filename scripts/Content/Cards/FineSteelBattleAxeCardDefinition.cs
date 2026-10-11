using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 精钢战斧帕拉帝恩卡牌内容定义（内容层）。
public sealed class FineSteelBattleAxeCardDefinition : CardDefinition
{
    public FineSteelBattleAxeCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.fine_steel_battle_axe"), "精钢战斧", new StringName("paladin"),
                CardSize.Medium, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/fine_steel_battle_axe-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却10秒，魔法消耗0，对敌方英雄造成40/80/160/320伤害。"),
                    new(CardKeywords.Echo, "当直接相邻的己方战场人类卡牌发动后，此卡牌攻击增加10/20/30/40；多重发动分别触发，仅本场战斗生效。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.AttackDamage] = 40, [GameAttributeKeys.CooldownTicks] = 100 })),
            new TagSet([GameTags.Equipment]), initialLevel: 1,
            levels: [CreateLevel(1, 40, 10), CreateLevel(2, 80, 20), CreateLevel(3, 160, 30), CreateLevel(4, 320, 40)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage, int bonus) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition("ability.attribute_attack", AbilityActivation.Active, AbilityTarget.EnemyHero,
                0, 100, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            GrowOnActivation(AdjacentCardSide.Left, "ability.grow_attack_on_left_human_activation", bonus),
            GrowOnActivation(AdjacentCardSide.Right, "ability.grow_attack_on_right_human_activation", bonus),
        ]);

    private static AbilityDefinition GrowOnActivation(AdjacentCardSide side, StringName key, int bonus) => new(key,
        AbilityActivation.EchoOnAdjacentAlliedCardActivated, AbilityTarget.SelfCard, 0, 0,
        [new IncreaseSourceCardAttributeEffectDefinition(GameAttributeKeys.AttackDamage, bonus)],
        triggerCardTag: GameTags.Human, triggerCardSide: side);
}
