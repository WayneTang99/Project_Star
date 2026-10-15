using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 正义天使圣骑士卡牌定义，组合属性攻击与敌方标签多重光环（内容层）。
public sealed class AngelOfJusticeCardDefinition : CardDefinition
{
    public AngelOfJusticeCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.angel_of_justice"), "正义天使", new StringName("paladin"),
                CardSize.Medium, [GameElements.Light],
                illustration: new StringName("res://art/ui/card-face/artwork/angel_of_justice-illustration-redraw.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却4秒，魔法消耗0，对敌方英雄造成60/120/240伤害。"),
                    new(CardKeywords.Aura, "敌方战场存在未被摧毁的恶魔或亡灵卡牌时，此卡牌获得多重1；同时满足或存在多张时不重复叠加。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.AttackDamage] = 60, [GameAttributeKeys.CooldownTicks] = 40 })),
            new TagSet(), initialLevel: 2,
            levels: [CreateLevel(2, 60), CreateLevel(3, 120), CreateLevel(4, 240)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition("ability.attribute_attack", AbilityActivation.Active, AbilityTarget.EnemyHero,
                0, 40, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            new AbilityDefinition("ability.enemy_tag_source_multicast_aura", AbilityActivation.PassiveAura,
                AbilityTarget.SelfCard, 0, 0,
                [new GrantSourceMulticastWhileEnemyTaggedEffectDefinition([GameTags.Demon, GameTags.Undead], 1)]),
        ]);
}
