using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 猛犸近卫圣骑士卡牌定义：机械师为骑士团打造、由人类驾驶的城邦中心阶层保镖机甲（内容层）。
public sealed class MammothGuardCardDefinition : CardDefinition
{
    public MammothGuardCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.mammoth_guard"), "猛犸近卫", new StringName("paladin"),
            CardSize.Large, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/mammoth_guard-illustration.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却10秒，魔法消耗0，己方英雄获得180/360护甲。"),
                new(CardKeywords.Echo, "当敌方卡牌发动后，此卡牌获得1秒充能。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 100 })), new TagSet([GameTags.Human, GameTags.Mechanical]),
            initialLevel: 3, levels: [CreateLevel(3, 180), CreateLevel(4, 360)]) { }

    private static CardLevelDefinition CreateLevel(int level, int armor) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.Armor] = armor },
        [
            new AbilityDefinition(new StringName("ability.gain_source_hero_armor"), AbilityActivation.Active,
                AbilityTarget.AlliedHero, 0, 100,
                [new GainSourceHeroArmorFromAttributeEffectDefinition(GameAttributeKeys.Armor)]),
            new AbilityDefinition(new StringName("ability.charge_self_on_enemy_activation"),
                AbilityActivation.EchoOnEnemyCardActivated, AbilityTarget.SelfCard, 0, 0,
                [new ChargeSourceCardEffectDefinition(10)]),
        ]);
}
