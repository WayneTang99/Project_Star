using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 战舰炮手圣骑士人类卡牌定义（内容层）。
public sealed class WarshipGunnerCardDefinition : CardDefinition
{
    public WarshipGunnerCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.warship_gunner"), "战舰炮手", new StringName("paladin"),
            CardSize.Medium, [GameElements.Water],
            illustration: new StringName("res://art/ui/card-face/artwork/warship_gunner-illustration-v2.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却5秒，魔法消耗0，己方所有存活战场载具卡牌攻击增加20/40/80，可累计，仅本场战斗生效。"),
                new(CardKeywords.Echo, "当己方战场载具卡牌发动后，对敌方英雄造成60/120/240伤害；多重发动分别触发。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 50 })), new TagSet([GameTags.Human]), initialLevel: 2,
            levels: [CreateLevel(2, 20, 60), CreateLevel(3, 40, 120), CreateLevel(4, 80, 240)]) { }

    private static CardLevelDefinition CreateLevel(int level, int bonus, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [
            new AbilityDefinition(new StringName("ability.increase_allied_vehicle_attack"), AbilityActivation.Active,
                AbilityTarget.SelfCard, 0, 50,
                [new ModifyTaggedAlliedCardsAttributeEffectDefinition(GameTags.Vehicle, GameAttributeKeys.AttackDamage,
                    bonus, RequiresAttributeSupport: false)]),
            new AbilityDefinition(new StringName("ability.damage_on_allied_vehicle_activation"),
                AbilityActivation.EchoOnMatchingAlliedCardActivated, AbilityTarget.EnemyHero, 0, 0,
                [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)], triggerCardTag: GameTags.Vehicle),
        ]);
}
