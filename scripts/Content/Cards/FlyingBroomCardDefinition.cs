using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 飞天扫帚莫娜卡牌内容定义（内容层）。
public sealed class FlyingBroomCardDefinition : CardDefinition
{
    public FlyingBroomCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(new StringName("card.flying_broom"), "飞天扫帚",
                    new StringName("mona"), CardSize.Medium, [GameElements.General],
                    illustration: new StringName("res://art/ui/card-face/artwork/flying_broom-illustration.png"),
                    descriptionEntries:
                    [
                        new(CardKeywords.Activate, "冷却5/4/3/2秒，魔法消耗0，此卡牌进入飞行。"),
                        new(CardKeywords.Echo, "当己方卡牌进入飞行时，该卡牌获得1秒疾速。"),
                    ]),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.CooldownTicks] = 50,
                })),
            new TagSet([GameTags.Equipment, GameTags.Mount]),
            initialLevel: 1,
            levels: [CreateLevel(1, 50), CreateLevel(2, 40), CreateLevel(3, 30), CreateLevel(4, 20)])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int cooldown) => new(
        level,
        new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = cooldown },
        [
            new AbilityDefinition(new StringName("ability.enter_flying"), AbilityActivation.Active,
                AbilityTarget.SelfCard, 0, cooldown,
                [new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, true)]),
            new AbilityDefinition(new StringName("ability.hasten_allied_flying_entry"),
                AbilityActivation.EchoOnAlliedCardEnteredState, AbilityTarget.EventCard, 0, 0,
                [new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 10)],
                triggerStateKey: GameAttributeKeys.Flying),
        ]);
}
