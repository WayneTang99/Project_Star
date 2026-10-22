using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 蕈灵莫娜卡牌，组合施毒、生命再生与亡语治疗。
public sealed class MushroomSpiritCardDefinition : CardDefinition
{
    public MushroomSpiritCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.mushroom_spirit"), "蕈灵", new StringName("mona"), CardSize.Small, [GameElements.Wood],
            illustration: new StringName("res://art/ui/card-face/artwork/mushroom_spirit-illustration-20261022.png"),
            descriptionEntries: [new(CardKeywords.Activate, "施加2/4/8/16中毒，获得1/2/4/8生命再生。"),
                new(CardKeywords.Deathrattle, "治疗60/120/240/480生命。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 40 })),
            new TagSet([GameTags.Plant, GameTags.Spirit]), initialLevel: 1,
            levels: [CreateLevel(1, 2, 1, 60), CreateLevel(2, 4, 2, 120), CreateLevel(3, 8, 4, 240), CreateLevel(4, 16, 8, 480)]) { }

    private static CardLevelDefinition CreateLevel(int level, int poison, int regeneration, int healing) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.Poison] = poison },
        [new AbilityDefinition(new StringName("ability.poison_and_health_regen"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 40,
            [new ApplyAttributeStatusEffectDefinition(BattleStatus.Poison, GameAttributeKeys.Poison),
                new ApplyStatusEffectDefinition(BattleStatus.HealthRegen, regeneration, TargetsSourceHero: true)]),
         new AbilityDefinition(new StringName("ability.heal_on_source_destroyed"), AbilityActivation.EchoOnSourceDestroyed,
            AbilityTarget.AlliedHero, 0, 0, [new HealEffectDefinition(healing)])]);
}
