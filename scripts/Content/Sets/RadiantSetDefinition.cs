using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Sets;

public sealed class RadiantSetDefinition : CardSetDefinition
{
    public RadiantSetDefinition() : base(new EntityAttributes<CardSetIdentityAttributes>(
        new CardSetIdentityAttributes(new StringName("set.radiant"), "光辉")),
        [new CardSetThresholdDefinition(3,
            [new AbilityDefinition(new StringName("ability.allied_light_equipment_tag_aura"), AbilityActivation.PassiveAura,
                AbilityTarget.AlliedHero, 0, 0, [new GrantTagToAlliedElementCardsEffectDefinition(GameElements.Light, GameTags.Equipment)])])]) { }
}
