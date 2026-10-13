using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 天启套装无阵营卡牌，组合通用发动与日蚀进入能力。
public sealed class DebilisCardDefinition : CardDefinition
{
    public DebilisCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.debilis"), "死亡之手德比利斯", new StringName("neutral"),
            CardSize.Medium, [GameElements.Dark], setKey: new StringName("set.apocalypse"),
            illustration: new StringName("res://art/ui/card-face/artwork/debilis-illustration-neutral-v2.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却12秒，提前进入日蚀时间；全场只进入一次。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 120 })), new TagSet([GameTags.Human]), initialLevel: 4,
            levels: [CreateLevel(4)]) { }

    private static CardLevelDefinition CreateLevel(int level) => new(level,
        null,
        [
            new AbilityDefinition(new StringName("ability.enter_eclipse"), AbilityActivation.Active,
                AbilityTarget.AlliedHero, 0, 120, [new EnterEclipseEffectDefinition()]),
        ]);
}

