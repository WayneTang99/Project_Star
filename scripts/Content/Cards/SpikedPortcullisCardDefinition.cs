using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 尖桩吊闸圣骑士陷阱建筑，双方人类发动使自身充能（内容层）。
public sealed class SpikedPortcullisCardDefinition : CardDefinition
{
    public SpikedPortcullisCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.spiked_portcullis"), "尖桩吊闸", new StringName("paladin"), CardSize.Medium, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/spiked_portcullis-illustration.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却10秒，魔法消耗0，对敌方英雄造成120/240/480伤害。"),
                new(CardKeywords.Echo, "当己方或敌方存活战场人类卡牌发动后，此卡牌获得1秒充能；多重发动分别触发。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 100 })),
            new TagSet([GameTags.Trap, GameTags.Building]), initialLevel: 2,
            levels: [CreateLevel(2, 120), CreateLevel(3, 240), CreateLevel(4, 480)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 100, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            new AbilityDefinition(new StringName("ability.charge_on_allied_human_activation"), AbilityActivation.EchoOnMatchingAlliedCardActivated,
                AbilityTarget.SelfCard, 0, 0, [new ChargeSourceCardEffectDefinition(10)], triggerCardTag: GameTags.Human),
            new AbilityDefinition(new StringName("ability.charge_on_enemy_human_activation"), AbilityActivation.EchoOnEnemyCardActivated,
                AbilityTarget.SelfCard, 0, 0, [new ChargeSourceCardEffectDefinition(10)], triggerCardAnyTags: [GameTags.Human]),
        ]);
}
