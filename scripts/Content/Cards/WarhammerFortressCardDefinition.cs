using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 战锤要塞帕拉帝恩地域卡牌定义（内容层）。
public sealed class WarhammerFortressCardDefinition : CardDefinition
{
    public WarhammerFortressCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.warhammer_fortress"), "战锤要塞", new StringName("paladin"),
                CardSize.Large, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/warhammer_fortress-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却12秒，魔法消耗0，对敌方英雄造成300/500伤害。"),
                    new(CardKeywords.Aura, "己方战场区每有一张未被摧毁的装备卡牌，此卡牌冷却缩短1秒；数量变化时立即调整，保留已走过的冷却进度。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 300,
                    [GameAttributeKeys.CooldownTicks] = 120,
                })), new TagSet([GameTags.Building, GameTags.Location]), initialLevel: 3,
            levels: [CreateLevel(3, 300), CreateLevel(4, 500)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 120, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            new AbilityDefinition(new StringName("ability.allied_tag_count_cooldown_aura"), AbilityActivation.PassiveAura,
                AbilityTarget.SelfCard, 0, 0,
                [new ReduceSourceCooldownPerAlliedTaggedCardEffectDefinition(GameTags.Equipment, 10)]),
        ]);
}
