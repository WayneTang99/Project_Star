using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 魔枭莫娜卡牌内容定义（内容层）。
public sealed class MagicOwlCardDefinition : CardDefinition
{
    public MagicOwlCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.magic_owl"), "魔枭", new StringName("mona"), CardSize.Small, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/magic_owl-illustration-arcane.png"),
            descriptionEntries: [new(CardKeywords.Activate, "获得2/4/8/16魔法再生，此卡牌进入飞行。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 40 })), new TagSet([GameTags.Beast]), initialLevel: 1,
            levels: [CreateLevel(1, 2), CreateLevel(2, 4), CreateLevel(3, 8), CreateLevel(4, 16)]) { }

    private static CardLevelDefinition CreateLevel(int level, int regeneration) => new(level, null,
        [new AbilityDefinition(new StringName("ability.gain_mana_regeneration"), AbilityActivation.Active,
            AbilityTarget.AlliedHero, 0, 40, [new ApplyStatusEffectDefinition(BattleStatus.ManaRegen, regeneration),
                new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, true)])]);
}
