using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Combat;

namespace Project_Star.Content;

/// <summary>Minimal fantasy content used to verify registration and instance creation.</summary>
public sealed class WayfarerHeroDefinition : HeroDefinition
{
    public WayfarerHeroDefinition()
        : base(
            new EntityAttributes<HeroIdentityAttributes>(
                new HeroIdentityAttributes(new StringName("hero.wayfarer"), "漫游者", new StringName("wayfarer")),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.MaxHealth] = 100,
                    [GameAttributeKeys.Armor] = 0,
                    [GameAttributeKeys.MaxMana] = 100,
                    [GameAttributeKeys.Mana] = 0,
                    [GameAttributeKeys.ManaRegen] = 10,
                })),
            new TagSet([GameTags.Human]))
    {
    }
}

public sealed class IronSwordCardDefinition : CardDefinition
{
    public IronSwordCardDefinition()
        : base(
            new EntityAttributes<CardIdentityAttributes>(
                new CardIdentityAttributes(
                    new StringName("card.iron_sword"),
                    "铁剑",
                    new StringName("wayfarer"),
                    CardSize.Small,
                    [GameElements.General]),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.AttackDamage] = 25,
                    [GameAttributeKeys.CooldownTicks] = 10,
                })),
            new TagSet([GameTags.Weapon]),
            [
                new AbilityDefinition(
                    new StringName("ability.basic_attack"),
                    AbilityActivation.Active,
                    AbilityTarget.EnemyHero,
                    0,
                    10,
                    [new DamageEffectDefinition(25)]),
            ])
    {
    }
}

public sealed class ForestPathEncounterDefinition : EncounterDefinition
{
    public ForestPathEncounterDefinition()
        : base(
            new EntityAttributes<EncounterIdentityAttributes>(
                new EncounterIdentityAttributes(new StringName("encounter.forest_path"), "林间岔路")),
            1,
            8)
    {
    }
}

public sealed class VillageMarketEncounterDefinition : EncounterDefinition
{
    public VillageMarketEncounterDefinition() : base(
        new EntityAttributes<EncounterIdentityAttributes>(
            new EncounterIdentityAttributes(new StringName("encounter.village_market"), "村庄集市")),
        1, 99, EncounterKind.Shop, 2) { }
}

public sealed class TravelingMerchantEncounterDefinition : EncounterDefinition
{
    public TravelingMerchantEncounterDefinition() : base(
        new EntityAttributes<EncounterIdentityAttributes>(
            new EncounterIdentityAttributes(new StringName("encounter.traveling_merchant"), "旅行商人")),
        1, 99, EncounterKind.Shop, 1) { }
}

public sealed class WolfPackEncounterDefinition : EncounterDefinition
{
    public WolfPackEncounterDefinition() : base(
        new EntityAttributes<EncounterIdentityAttributes>(
            new EncounterIdentityAttributes(new StringName("encounter.wolf_pack"), "狼群")),
        1, 99, EncounterKind.Monster) { }
}

public sealed class GoblinRaidEncounterDefinition : EncounterDefinition
{
    public GoblinRaidEncounterDefinition() : base(
        new EntityAttributes<EncounterIdentityAttributes>(
            new EncounterIdentityAttributes(new StringName("encounter.goblin_raid"), "哥布林袭击")),
        1, 99, EncounterKind.Monster) { }
}

public sealed class StoneGolemEncounterDefinition : EncounterDefinition
{
    public StoneGolemEncounterDefinition() : base(
        new EntityAttributes<EncounterIdentityAttributes>(
            new EncounterIdentityAttributes(new StringName("encounter.stone_golem"), "石魔像")),
        1, 99, EncounterKind.Monster) { }
}

public sealed class LocalPvpEncounterDefinition : EncounterDefinition
{
    public LocalPvpEncounterDefinition() : base(
        new EntityAttributes<EncounterIdentityAttributes>(
            new EncounterIdentityAttributes(new StringName("encounter.local_pvp"), "异步挑战")),
        1, 99, EncounterKind.Pvp) { }
}
