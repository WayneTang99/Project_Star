using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 遗迹石像冒险家卡牌定义，组合伤害、己方护甲与开战自身禁锢（内容层）。
public sealed class RuinStoneStatueCardDefinition : CardDefinition
{
    public RuinStoneStatueCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.ruin_stone_statue"), "遗迹石像", new StringName("robin"),
            CardSize.Large, [GameElements.Earth],
            illustration: new StringName("res://art/ui/card-face/artwork/ruin_stone_statue-illustration.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却6秒，魔法消耗0，造成100/200伤害，己方英雄获得100/200护甲。"),
                new(CardKeywords.Opening, "战斗开始时，此卡牌获得2秒禁锢。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 60 })), new TagSet([GameTags.Mount]), initialLevel: 3,
            levels: [CreateLevel(3, 100), CreateLevel(4, 200)]) { }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = amount, [GameAttributeKeys.Armor] = amount },
        [
            new AbilityDefinition(new StringName("ability.attack_and_gain_source_hero_armor"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 60,
                [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage), new GainSourceHeroArmorFromAttributeEffectDefinition(GameAttributeKeys.Armor)]),
            new AbilityDefinition(new StringName("ability.battle_start_self_immobilize"), AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.SelfCard, 0, 0, [new ApplyStatusEffectDefinition(BattleStatus.ImmobilizeDuration, 20)]),
        ]);
}
