using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 手弩圣骑士装备卡牌定义（内容层）。
public sealed class HandCrossbowCardDefinition : CardDefinition
{
    public HandCrossbowCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.hand_crossbow"), "手弩", new StringName("paladin"), CardSize.Small, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/hand_crossbow-illustration.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却4秒，魔法消耗0，对敌方英雄造成20/40/80/160伤害。"),
                new(CardKeywords.Aura, "此卡牌每次获得的疾速时长翻倍，包含施加来源的疾速时长加成；不改变已有疾速的自然衰减。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 40 })), new TagSet([GameTags.Equipment]), initialLevel: 1,
            levels: [CreateLevel(1, 20), CreateLevel(2, 40), CreateLevel(3, 80), CreateLevel(4, 160)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 40, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            new AbilityDefinition(new StringName("ability.double_received_haste_duration"), AbilityActivation.PassiveAura,
                AbilityTarget.SelfCard, 0, 0, [new MultiplyReceivedCardStatusDurationEffectDefinition(BattleStatus.HasteDuration)]),
        ]);
}
