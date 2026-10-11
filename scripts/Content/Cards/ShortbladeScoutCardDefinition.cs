using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 短刃斥候帕拉帝恩卡牌内容定义（内容层）。
public sealed class ShortbladeScoutCardDefinition : CardDefinition
{
    public ShortbladeScoutCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.shortblade_scout"), "短刃斥候", new StringName("paladin"),
                CardSize.Small, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/shortblade_scout-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却3秒，魔法消耗0，此卡牌获得1/2/3秒疾速。"),
                    new(CardKeywords.Echo, "当此卡牌获得疾速后，对敌方英雄造成20/40/80伤害。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.AttackDamage] = 20, [GameAttributeKeys.CooldownTicks] = 30 })),
            new TagSet([GameTags.Human]), initialLevel: 2,
            levels: [CreateLevel(2, 20, 10), CreateLevel(3, 40, 20), CreateLevel(4, 80, 30)])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int damage, int hasteTicks) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition("ability.shortblade_scout_haste", AbilityActivation.Active,
                AbilityTarget.SelfCard, 0, 30, [new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, hasteTicks)]),
            new AbilityDefinition("ability.shortblade_scout_haste_strike", AbilityActivation.EchoOnSourceCardStatusGained,
                AbilityTarget.EnemyHero, 0, 0, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)],
                triggerStatus: BattleStatus.HasteDuration),
        ]);
}
