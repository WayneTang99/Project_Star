using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 魔源锻造仪哈尔拉卡牌定义（内容层）。
public sealed class ManaForgeApparatusCardDefinition : CardDefinition
{
    public ManaForgeApparatusCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.mana_forge_apparatus"), "魔源锻造仪", new StringName("harla"), CardSize.Medium, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/mana_forge_apparatus-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "消耗50魔法，己方战场随机1张其他未满级卡牌升1级，仅本场战斗生效。"),
             new(CardKeywords.Echo, "己方卡牌升级后，该卡牌冷却缩减2/4%；永久升级时永久生效，临时升级时仅本场生效。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 50 })),
            new TagSet([GameTags.Mechanical]), initialLevel: 3, levels: [CreateLevel(3, 50, 2), CreateLevel(4, 40, 4)]) { }

    private static CardLevelDefinition CreateLevel(int level, int cooldown, int percent) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = cooldown },
        [new AbilityDefinition(new StringName("ability.upgrade_random_other_allied_card"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 50, cooldown, [new UpgradeRandomOtherAlliedCardEffectDefinition()]),
         new AbilityDefinition(new StringName("ability.reduce_upgraded_card_cooldown"), AbilityActivation.EchoOnAlliedCardUpgraded,
            AbilityTarget.EventCard, 0, 0, [new ReduceEventCardCooldownPercentEffectDefinition(percent)])]);
}
