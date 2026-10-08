using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 禅光寺极云卡牌内容定义（内容层）。
public sealed class ZenLightTempleCardDefinition : CardDefinition
{
    public ZenLightTempleCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.zen_light_temple"), "禅光寺", new StringName("jiyun"),
                CardSize.Large, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/zen_light_temple-illustration.png"),
                descriptionEntries:
                [new(CardKeywords.Echo, "当双方任何攻击卡牌发动后，禁锢该卡牌1秒。")])),
            new TagSet([GameTags.Location]), initialLevel: 4,
            levels:
            [new CardLevelDefinition(4, null,
                [new AbilityDefinition(new StringName("ability.immobilize_activated_attack_card"),
                    AbilityActivation.EchoOnAnyAttackCardActivated, AbilityTarget.EventCard, 0, 0,
                    [new ApplyStatusEffectDefinition(BattleStatus.ImmobilizeDuration, 10)])])]) { }
}
