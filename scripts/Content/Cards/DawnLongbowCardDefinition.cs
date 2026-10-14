using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 破晓长弓帕拉帝恩装备卡牌定义（内容层）。
public sealed class DawnLongbowCardDefinition : CardDefinition
{
    public DawnLongbowCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.dawn_longbow"), "破晓长弓", new StringName("paladin"),
                CardSize.Medium, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/dawn_longbow-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却8秒，魔法消耗0，对敌方英雄造成200/400伤害。"),
                    new(CardKeywords.Echo, "当此卡牌每次发动后，此卡牌冷却缩短1秒，可累加，仅本场战斗生效（包含多重的额外发动）。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 200,
                    [GameAttributeKeys.CooldownTicks] = 80,
                })), new TagSet([GameTags.Equipment]), initialLevel: 3,
            levels: [CreateLevel(3, 200), CreateLevel(4, 400)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 80, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            new AbilityDefinition(new StringName("ability.reduce_source_cooldown"), AbilityActivation.EchoOnSourceCardActivated,
                AbilityTarget.SelfCard, 0, 0, [new ReduceSourceCooldownEffectDefinition(10)]),
        ]);
}
