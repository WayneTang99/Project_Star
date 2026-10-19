using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 神像广场圣骑士地域建筑，按三项独立己方条件提供多重治疗（内容层）。
public sealed class StatuePlazaCardDefinition : CardDefinition
{
    public StatuePlazaCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.statue_plaza"), "神像广场", new StringName("paladin"), CardSize.Large, [GameElements.Light],
            illustration: new StringName("res://art/ui/card-face/artwork/statue_plaza-illustration-standing-20261019.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却7秒，魔法消耗0，治疗己方英雄200/400生命。"),
                new(CardKeywords.Aura, "己方存活战场存在光属性卡牌时，此卡牌获得多重1，包含此卡牌自身。"),
                new(CardKeywords.Aura, "己方存活战场存在人类卡牌时，此卡牌获得多重1。"),
                new(CardKeywords.Aura, "己方存活战场存在装备卡牌时，此卡牌获得多重1。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 70 })),
            new TagSet([GameTags.Location, GameTags.Building]), initialLevel: 3,
            levels: [CreateLevel(3, 200), CreateLevel(4, 400)]) { }

    private static CardLevelDefinition CreateLevel(int level, int healing) => new(level, null,
        [
            new AbilityDefinition(new StringName("ability.heal"), AbilityActivation.Active,
                AbilityTarget.AlliedHero, 0, 70, [new HealEffectDefinition(healing)]),
            new AbilityDefinition(new StringName("ability.allied_conditions_multicast_aura"), AbilityActivation.PassiveAura,
                AbilityTarget.SelfCard, 0, 0,
                [new GrantSourceMulticastWhileAlliedCardMatchesEffectDefinition(1, RequiredElement: GameElements.Light),
                 new GrantSourceMulticastWhileAlliedCardMatchesEffectDefinition(1, RequiredTag: GameTags.Human),
                 new GrantSourceMulticastWhileAlliedCardMatchesEffectDefinition(1, RequiredTag: GameTags.Equipment)]),
        ]);
}
