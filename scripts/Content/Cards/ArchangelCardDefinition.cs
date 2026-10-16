using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 大天使号圣骑士战舰卡牌定义（内容层）。
public sealed class ArchangelCardDefinition : CardDefinition
{
    public ArchangelCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.archangel"), "大天使号", new StringName("paladin"),
            CardSize.Large, [GameElements.Light, GameElements.Water],
            illustration: new StringName("res://art/ui/card-face/artwork/archangel-illustration.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却20秒，魔法消耗0，对敌方英雄造成500/1000伤害。"),
                new(CardKeywords.Echo, "当己方光属性卡牌或敌方恶魔或亡灵卡牌发动后，此卡牌获得1秒充能；包括自身和多重发动，敌方同时具有恶魔和亡灵标签时只触发一次。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 200 })), new TagSet([GameTags.Mechanical, GameTags.Vehicle]),
            initialLevel: 3, levels: [CreateLevel(3, 500), CreateLevel(4, 1000)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 200, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            new AbilityDefinition(new StringName("ability.charge_self_on_allied_light_activation"),
                AbilityActivation.EchoOnMatchingAlliedCardActivated, AbilityTarget.SelfCard, 0, 0,
                [new ChargeSourceCardEffectDefinition(10)], triggerCardElement: GameElements.Light),
            new AbilityDefinition(new StringName("ability.charge_self_on_enemy_demon_or_undead_activation"),
                AbilityActivation.EchoOnEnemyCardActivated, AbilityTarget.SelfCard, 0, 0,
                [new ChargeSourceCardEffectDefinition(10)], triggerCardAnyTags: [GameTags.Demon, GameTags.Undead]),
        ]);
}
