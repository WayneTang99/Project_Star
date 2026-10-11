using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 冒险者背包罗宾装备定义，复用按英雄等级计算的属性光环（内容层）。
public sealed class AdventurerBackpackCardDefinition : CardDefinition
{
    public AdventurerBackpackCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.adventurer_backpack"), "冒险者背包", new StringName("robin"),
            CardSize.Medium, [GameElements.General], setKey: new StringName("set.adventurer"),
            illustration: new StringName("res://art/ui/card-face/artwork/adventurer_backpack-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Aura, "己方攻击卡牌获得等同于当前英雄等级×10/20/30的攻击。")]
        )), new TagSet([GameTags.Equipment]), initialLevel: 2,
            levels: [CreateLevel(2, 10), CreateLevel(3, 20), CreateLevel(4, 30)]) { }

    private static CardLevelDefinition CreateLevel(int level, int multiplier) => new(level, null,
        [new AbilityDefinition(new StringName("ability.allied_card_hero_level_attack_aura"), AbilityActivation.PassiveAura,
            AbilityTarget.SelfCard, 0, 0,
            [new IncreaseAlliedCardAttributeAuraEffectDefinition(GameAttributeKeys.AttackDamage, 0, multiplier)])]);
}
