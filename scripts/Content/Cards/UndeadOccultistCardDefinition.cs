using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 亡灵禁术师莫娜亡灵卡牌，以非暗属性小型摧毁回响原地召唤骷髅（内容层）。
public sealed class UndeadOccultistCardDefinition : CardDefinition
{
    public UndeadOccultistCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.undead_occultist"), "亡灵禁术师", new StringName("mona"), CardSize.Medium, [GameElements.Dark],
            illustration: new StringName("res://art/ui/card-face/artwork/undead_occultist-illustration-staff-20261020.png"),
            descriptionEntries: [new(CardKeywords.Echo, "当己方战场小型非暗属性卡牌被摧毁后，在该卡牌原位置召唤1张【骷髅】。") ])),
            new TagSet([GameTags.Undead]), initialLevel: 4,
            levels: [new CardLevelDefinition(4, null,
                [new AbilityDefinition(new StringName("ability.replace_non_dark_small_with_skeleton"), AbilityActivation.EchoOnAlliedCardDestroyed,
                    AbilityTarget.AlliedHero, 0, 0, [new SummonAtDestroyedCardEffectDefinition(new SkeletonCardDefinition(), UseSourceLevel: true)],
                    triggerCardSize: CardSize.Small, excludedTriggerCardElement: GameElements.Dark)])]) { }
}
