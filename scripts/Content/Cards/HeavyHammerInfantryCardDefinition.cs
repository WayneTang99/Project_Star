using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 重锤兵帕拉帝恩卡牌内容定义（内容层）。
public sealed class HeavyHammerInfantryCardDefinition : CardDefinition
{
    public HeavyHammerInfantryCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.heavy_hammer_infantry"), "重锤兵", new StringName("paladin"),
                CardSize.Medium, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/heavy_hammer_infantry-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却8秒，魔法消耗0，对敌方英雄造成60/120/240伤害；己方英雄增加20/40/80最大生命值及等量当前生命，仅本场战斗生效。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.AttackDamage] = 60, [GameAttributeKeys.CooldownTicks] = 80 })),
            new TagSet([GameTags.Human]), initialLevel: 2,
            levels: [CreateLevel(2, 60, 20), CreateLevel(3, 120, 40), CreateLevel(4, 240, 80)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage, int health) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [new AbilityDefinition("ability.attack_and_gain_max_health", AbilityActivation.Active, AbilityTarget.EnemyHero,
            0, 80,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                new IncreaseHeroMaxHealthEffectDefinition(health, TargetsSourceHero: true)])]);
}
