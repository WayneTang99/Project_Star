using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 随军牧师帕拉帝恩卡牌内容定义（内容层）。
public sealed class ArmyPriestCardDefinition : CardDefinition
{
    public ArmyPriestCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.army_priest"), "随军牧师", new StringName("paladin"),
                CardSize.Small, [GameElements.Light],
                illustration: new StringName("res://art/ui/card-face/artwork/army_priest-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却7秒，魔法消耗10，治疗己方英雄40/80/140生命，并恢复10/20/40魔法。"),
                    new(CardKeywords.Echo, "己方人类或光属性卡牌发动时，此卡牌获得1秒充能；包括自身和多重发动，同时符合两项条件只触发一次。"),
                ]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            {
                [GameAttributeKeys.CooldownTicks] = 70,
            })), new TagSet([GameTags.Human]), initialLevel: 2,
            levels: [CreateLevel(2, 40, 10), CreateLevel(3, 80, 20), CreateLevel(4, 140, 40)])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int healing, int manaRestore) => new(level, null,
        [
            new AbilityDefinition(new StringName("ability.army_priest_recovery"), AbilityActivation.Active,
                AbilityTarget.AlliedHero, 10, 70,
                [new HealEffectDefinition(healing), new RestoreManaEffectDefinition(manaRestore)]),
            new AbilityDefinition(new StringName("ability.army_priest_charge_echo"),
                AbilityActivation.EchoOnMatchingAlliedCardActivated, AbilityTarget.SelfCard, 0, 0,
                [new ChargeSourceCardEffectDefinition(10)],
                triggerCardTag: GameTags.Human, triggerCardElement: GameElements.Light),
        ]);
}
