using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 魔导士长袍莫娜卡牌内容定义（内容层）。
public sealed class WizardRobeCardDefinition : CardDefinition
{
    public WizardRobeCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.wizard_robe"), "魔导士长袍", new StringName("mona"),
                CardSize.Medium, [GameElements.General],
                setKey: new StringName("set.mage"), illustration: new StringName("res://art/ui/card-face/artwork/wizard_robe-illustration.png"),
                descriptionEntries:
                [new(CardKeywords.Activate, "冷却6秒，消耗40魔法，己方英雄获得4/8/16/32魔法再生，仅本场战斗生效。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 60 })), new TagSet([GameTags.Equipment]), initialLevel: 1,
            levels: [CreateLevel(1, 4), CreateLevel(2, 8), CreateLevel(3, 16), CreateLevel(4, 32)]) { }

    private static CardLevelDefinition CreateLevel(int level, int regeneration) => new(level, null,
        [new AbilityDefinition(new StringName("ability.gain_mana_regeneration"), AbilityActivation.Active,
            AbilityTarget.AlliedHero, 40, 60, [new ApplyStatusEffectDefinition(BattleStatus.ManaRegen, regeneration)])]);
}
