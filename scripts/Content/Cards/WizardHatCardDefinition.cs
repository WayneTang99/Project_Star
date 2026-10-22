using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 魔导士之帽莫娜卡牌内容定义（内容层）。
public sealed class WizardHatCardDefinition : CardDefinition
{
    public WizardHatCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.wizard_hat"), "魔导士之帽", new StringName("mona"),
                CardSize.Medium, [GameElements.General],
                setKey: new StringName("set.mage"), illustration: new StringName("res://art/ui/card-face/artwork/wizard_hat-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却5秒，魔法消耗0，恢复己方英雄20/40/80/160魔法。"),
                    new(CardKeywords.Echo, "己方每次实际消耗魔法后，己方英雄获得1/2/4/8魔法再生，仅本场战斗生效。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 50 })), new TagSet([GameTags.Equipment]), initialLevel: 1,
            levels: [CreateLevel(1, 20, 1), CreateLevel(2, 40, 2), CreateLevel(3, 80, 4), CreateLevel(4, 160, 8)]) { }

    private static CardLevelDefinition CreateLevel(int level, int mana, int regeneration) => new(level, null,
        [
            new AbilityDefinition(new StringName("ability.restore_mana"), AbilityActivation.Active,
                AbilityTarget.AlliedHero, 0, 50, [new RestoreManaEffectDefinition(mana)]),
            new AbilityDefinition(new StringName("ability.gain_mana_regeneration_on_mana_spent"),
                AbilityActivation.EchoOnAlliedManaSpent, AbilityTarget.AlliedHero, 0, 0,
                [new ApplyStatusEffectDefinition(BattleStatus.ManaRegen, regeneration)]),
        ]);
}
