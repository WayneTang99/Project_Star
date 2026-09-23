using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Combat;

internal sealed class HeroBattleState
{
    public HeroBattleState(HeroBattleSetup setup)
    {
        EntityId = setup.EntityId; MaxHealth = setup.MaxHealth; Health = setup.MaxHealth; Armor = setup.Armor;
        MaxMana = setup.MaxMana; Mana = setup.InitialMana; ManaRegen = setup.ManaRegen;
        HealthRegen = setup.HealthRegen; Burn = setup.Burn; Poison = setup.Poison;
    }
    public EntityId EntityId { get; }
    public int MaxHealth { get; }
    public int Health { get; set; }
    public int Armor { get; set; }
    public int MaxMana { get; }
    public int Mana { get; set; }
    public int ManaSpent { get; set; }
    public int Burn { get; set; }
    public int Poison { get; set; }
    public int HealthRegen { get; set; }
    public int ManaRegen { get; set; }
}

internal sealed class BattleAbilityState
{
    public BattleAbilityState(AbilityDefinition definition)
    {
        Definition = definition; RemainingCooldownUnits = definition.CooldownTicks * 2;
    }
    public AbilityDefinition Definition { get; }
    public int RemainingCooldownUnits { get; set; }
}

internal interface IBattleAbilitySource
{
    EntityId EntityId { get; }
    SideId Side { get; }
    bool Destroyed { get; }
    bool IsOnBench { get; }
    int ActivationCount { get; set; }
    int CooldownBonusTicks { get; set; }
    List<BattleAbilityState> Abilities { get; }
    int GetCombatAttribute(StringName key);
}

internal sealed class CardBattleState : IBattleAbilitySource
{
    public CardBattleState(CardBattleSetup setup, SideId side)
    {
        EntityId = setup.EntityId; Side = side; BoardStart = setup.BoardStart; IsOnBench = setup.IsOnBench;
        OccupiedSlots = setup.OccupiedSlots;
        Tags = setup.Tags ?? new TagSet();
        ElementKeys = setup.ElementKeys is null ? [] : new HashSet<StringName>(setup.ElementKeys);
        CombatAttributes[GameAttributeKeys.AttackDamage] = setup.AttackDamage;
        CombatAttributes[GameAttributeKeys.Armor] = setup.ArmorAmount;
        CombatAttributes[GameAttributeKeys.Multicast] = setup.Multicast;
        var definitions = setup.Abilities is { Count: > 0 }
            ? setup.Abilities
            : setup.UseLegacyAttack
                ? [new AbilityDefinition(new StringName("ability.legacy_attack"), AbilityActivation.Active,
                    AbilityTarget.EnemyHero, 0, setup.CooldownTicks, [new DamageEffectDefinition(setup.AttackDamage)])]
                : Array.Empty<AbilityDefinition>();
        foreach (var definition in definitions) Abilities.Add(new BattleAbilityState(definition));
        foreach (var definition in definitions)
        foreach (var effect in definition.Effects)
            switch (effect)
            {
                case AttributeDamageEffectDefinition attributeDamage:
                    SupportedCombatAttributes.Add(attributeDamage.AttributeKey);
                    break;
                case GainSourceHeroArmorFromAttributeEffectDefinition attributeArmor:
                    SupportedCombatAttributes.Add(attributeArmor.AttributeKey);
                    break;
            }
        if (definitions.Count > 0) SupportedCombatAttributes.Add(GameAttributeKeys.Multicast);
    }
    public EntityId EntityId { get; }
    public SideId Side { get; }
    public int BoardStart { get; }
    public int OccupiedSlots { get; }
    public bool IsOnBench { get; }
    public TagSet Tags { get; }
    public HashSet<StringName> DestroyedTags { get; } = [];
    public IReadOnlySet<StringName> ElementKeys { get; }
    public bool Destroyed { get; set; }
    public int ActivationCount { get; set; }
    public int CooldownBonusTicks { get; set; }
    public int HasteDuration { get; set; }
    public int SlowDuration { get; set; }
    public int ImmobilizeDuration { get; set; }
    public List<BattleAbilityState> Abilities { get; } = [];
    public Dictionary<StringName, int> CombatAttributes { get; } = [];
    public HashSet<StringName> SupportedCombatAttributes { get; } = [];

    public bool SupportsCombatAttribute(StringName key) => SupportedCombatAttributes.Contains(key);

    public int GetCombatAttribute(StringName key) => CombatAttributes.TryGetValue(key, out var value) ? value : 0;

    public int AddCombatAttribute(StringName key, int amount)
    {
        var value = checked(GetCombatAttribute(key) + amount);
        CombatAttributes[key] = value;
        return value;
    }
}

internal sealed class SkillBattleState : IBattleAbilitySource
{
    private readonly IReadOnlyDictionary<StringName, int> _combatAttributes;

    public SkillBattleState(SkillBattleSetup setup, SideId side)
    {
        EntityId = setup.EntityId;
        Side = side;
        _combatAttributes = setup.CombatAttributes;
        foreach (var definition in setup.Abilities) Abilities.Add(new BattleAbilityState(definition));
    }

    public EntityId EntityId { get; }
    public SideId Side { get; }
    public bool Destroyed => false;
    public bool IsOnBench => false;
    public int ActivationCount { get; set; }
    public int CooldownBonusTicks { get; set; }
    public List<BattleAbilityState> Abilities { get; } = [];

    public int GetCombatAttribute(StringName key) =>
        _combatAttributes.TryGetValue(key, out var value) ? value : 0;
}

internal sealed record PendingAbility(
    IBattleAbilitySource Source,
    BattleAbilityState Ability,
    bool IsEcho,
    bool IsMulticast,
    BattleTick EnqueuedAt);

internal sealed class AbilityQueue
{
    private readonly Queue<PendingAbility> _items = new();
    public int Count => _items.Count;
    public void Enqueue(PendingAbility ability) => _items.Enqueue(ability);
    public PendingAbility Dequeue() => _items.Dequeue();
}

internal sealed class BattleRuntime
{
    public BattleRuntime(BattleSetup setup)
    {
        RandomState = setup.Seed;
        PlayerHero = new HeroBattleState(setup.Player.Hero); OpponentHero = new HeroBattleState(setup.Opponent.Hero);
        AddCards(setup.Player.Cards, SideId.Player); AddCards(setup.Opponent.Cards, SideId.Opponent); Cards.Sort(CompareCards);
        AddSkills(setup.Player.Skills, SideId.Player); AddSkills(setup.Opponent.Skills, SideId.Opponent);
    }
    public BattleTick Tick { get; set; } = BattleTick.Zero;
    public HeroBattleState PlayerHero { get; }
    public HeroBattleState OpponentHero { get; }
    public List<CardBattleState> Cards { get; } = [];
    public List<SkillBattleState> Skills { get; } = [];
    public IEnumerable<IBattleAbilitySource> AbilitySources
    {
        get
        {
            foreach (var side in new[] { SideId.Player, SideId.Opponent })
            {
                foreach (var card in Cards)
                    if (card.Side == side) yield return card;
                foreach (var skill in Skills)
                    if (skill.Side == side) yield return skill;
            }
        }
    }
    public AbilityQueue Queue { get; } = new();
    public List<BattleEvent> Events { get; } = [];
    public List<PermanentChange> PermanentChanges { get; } = [];
    private ulong RandomState { get; set; }
    public HeroBattleState GetHero(SideId side) => side == SideId.Player ? PlayerHero : OpponentHero;
    public int NextRandomIndex(int count)
    {
        if (count < 1) throw new ArgumentOutOfRangeException(nameof(count));
        RandomState += 0x9E3779B97F4A7C15UL;
        var value = RandomState;
        value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
        value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
        value ^= value >> 31;
        return (int)(value % (ulong)count);
    }
    private void AddCards(IReadOnlyList<CardBattleSetup> cards, SideId side) { foreach (var card in cards) Cards.Add(new(card, side)); }
    private void AddSkills(IReadOnlyList<SkillBattleSetup> skills, SideId side)
    {
        foreach (var skill in skills) Skills.Add(new SkillBattleState(skill, side));
        Skills.Sort((left, right) => left.Side != right.Side
            ? left.Side.CompareTo(right.Side)
            : left.EntityId.Value.CompareTo(right.EntityId.Value));
    }
    private static int CompareCards(CardBattleState left, CardBattleState right)
    {
        var side = left.Side.CompareTo(right.Side); if (side != 0) return side;
        var position = left.BoardStart.CompareTo(right.BoardStart);
        return position != 0 ? position : left.EntityId.Value.CompareTo(right.EntityId.Value);
    }
}
