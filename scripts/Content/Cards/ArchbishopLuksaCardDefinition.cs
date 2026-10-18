using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

public sealed class ArchbishopLuksaCardDefinition : CardDefinition
{
    public ArchbishopLuksaCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.archbishop_luksa"), "大主教·卢克萨", new StringName("paladin"), CardSize.Medium,
            [GameElements.Light],
            illustration: new StringName("res://art/ui/card-face/artwork/archbishop_luksa-illustration-20261018.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "冷却10秒，魔法消耗0，触发己方其他存活战场光属性卡牌的发动效果，跳过魔法消耗与冷却，包含多重；同一触发链每张卡最多被额外发动一次。"),
             new(CardKeywords.Deathrattle, "发动此卡牌。")]),
            baseCombat: new ModifiableAttributeSet(new System.Collections.Generic.Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 100 })),
            new TagSet([GameTags.Human]), initialLevel: 5,
            levels: [new CardLevelDefinition(5, null,
                [new AbilityDefinition(new StringName("ability.trigger_other_allied_light_actives"), AbilityActivation.Active,
                    AbilityTarget.SelfCard, 0, 100, [new TriggerOtherAlliedElementActivesEffectDefinition(GameElements.Light)]),
                 new AbilityDefinition(new StringName("ability.activate_source_on_destruction"), AbilityActivation.EchoOnSourceDestroyed,
                    AbilityTarget.SelfCard, 0, 0, [new ActivateSourceCardEffectDefinition()])])]) { }
}
