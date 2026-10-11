using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 钢制扳手哈尔拉卡牌内容定义（内容层）。
public sealed class SteelWrenchCardDefinition : CardDefinition
{
    public SteelWrenchCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.steel_wrench"), "钢制扳手", new StringName("harla"),
                CardSize.Small, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/steel_wrench-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却5秒，魔法消耗0，对敌方英雄造成10/20/40/80伤害，并随机修复己方一张仅本场被摧毁的战场机械卡牌；原位置被占用时跳过。修复保留数值与能力，从完整冷却重新开始，不再次触发开战能力。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.AttackDamage] = 10, [GameAttributeKeys.CooldownTicks] = 50 })),
            new TagSet([GameTags.Equipment]), initialLevel: 1,
            levels: [CreateLevel(1, 10), CreateLevel(2, 20), CreateLevel(3, 40), CreateLevel(4, 80)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [new AbilityDefinition(new StringName("ability.attribute_attack_and_repair_tagged_card"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 50,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                new RepairRandomDestroyedAlliedCardEffectDefinition(GameTags.Mechanical)])]);
}
