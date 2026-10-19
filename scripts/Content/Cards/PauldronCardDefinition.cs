using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 肩甲圣骑士装备定义，多重强化直接相邻的人类和装备（内容层）。
public sealed class PauldronCardDefinition : CardDefinition
{
    public PauldronCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.pauldron"), "肩甲", new StringName("paladin"), CardSize.Small, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/pauldron-illustration.png"),
            descriptionEntries:
            [
                new(CardKeywords.Aura, "此卡牌具有1层多重（每层多重额外发动一次）。"),
                new(CardKeywords.Activate, "冷却6秒，魔法消耗0，己方所有直接相邻存活战场人类卡牌攻击增加5/10/20/40，己方所有直接相邻存活战场装备卡牌护甲增加5/10/20/40，仅本场战斗生效。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 60, [GameAttributeKeys.Multicast] = 1 })),
            new TagSet([GameTags.Equipment]), initialLevel: 1,
            levels: [CreateLevel(1, 5), CreateLevel(2, 10), CreateLevel(3, 20), CreateLevel(4, 40)]) { }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level, null,
        [new AbilityDefinition(new StringName("ability.buff_adjacent_tagged_cards"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, 60,
            [new ModifyTaggedAlliedCardsAttributeEffectDefinition(GameTags.Human, GameAttributeKeys.AttackDamage,
                amount, RequiresAttributeSupport: false, AdjacentOnly: true),
             new ModifyTaggedAlliedCardsAttributeEffectDefinition(GameTags.Equipment, GameAttributeKeys.Armor,
                amount, RequiresAttributeSupport: false, AdjacentOnly: true)])]);
}
