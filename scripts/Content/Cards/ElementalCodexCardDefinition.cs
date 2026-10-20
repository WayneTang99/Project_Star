using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 元素法典莫娜装备，组合伤害治疗与逐元素条件多重光环（内容层）。
public sealed class ElementalCodexCardDefinition : CardDefinition
{
    public ElementalCodexCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.elemental_codex"), "元素法典", new StringName("mona"),
            CardSize.Medium, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/elemental_codex-illustration.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "消耗60魔法，对敌方英雄造成40/80/160伤害，治疗己方英雄40/80/160生命。"),
                new(CardKeywords.Aura, "此卡牌获得等同于己方存活战场不同元素种类数的多重，包含普通属性和此卡牌自身。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 70 })), new TagSet([GameTags.Equipment]), initialLevel: 2,
            levels: [CreateLevel(2, 40), CreateLevel(3, 80), CreateLevel(4, 160)]) { }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = amount },
        [
            new AbilityDefinition(new StringName("ability.attribute_damage_and_heal"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 60, 70,
                [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage), new HealEffectDefinition(amount, TargetsSourceHero: true)]),
            new AbilityDefinition(new StringName("ability.all_elements_conditional_multicast"), AbilityActivation.PassiveAura,
                AbilityTarget.SelfCard, 0, 0,
                GameElements.All.Select(element => (EffectDefinition)new GrantSourceMulticastWhileAlliedCardMatchesEffectDefinition(
                    1, RequiredElement: element)).ToArray()),
        ]);
}
