using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 巨口食人花莫娜卡牌，摧毁直接相邻小型卡牌并固定获得再生。
public sealed class GiantMawMantrapCardDefinition : CardDefinition
{
    public GiantMawMantrapCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.giant_maw_mantrap"), "巨口食人花", new StringName("mona"), CardSize.Medium, [GameElements.Wood],
            illustration: new StringName("res://art/ui/card-face/artwork/giant_maw_mantrap-illustration-20261022.png"),
            descriptionEntries: [new(CardKeywords.Activate, "摧毁直接相邻的己方小型卡牌，获得10/20/40生命再生。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 100 })),
            new TagSet([GameTags.Plant]), initialLevel: 2,
            levels: [CreateLevel(2, 10), CreateLevel(3, 20), CreateLevel(4, 40)]) { }

    private static CardLevelDefinition CreateLevel(int level, int regeneration) => new(level, null,
        [new AbilityDefinition(new StringName("ability.destroy_adjacent_small_and_regenerate"), AbilityActivation.Active,
            AbilityTarget.AlliedHero, 0, 100, [new DestroyAdjacentAlliedCardsEffectDefinition(CardSize.Small),
                new ApplyStatusEffectDefinition(BattleStatus.HealthRegen, regeneration)])]);
}
