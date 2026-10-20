using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Combat;

internal sealed class HeroBattleState
{
    public HeroBattleState(HeroBattleSetup setup)
    {
        EntityId = setup.EntityId; MaxHealth = setup.MaxHealth; Health = setup.MaxHealth; Armor = setup.Armor;
        Level = setup.Level;
        Identity = setup.Identity;
        MaxMana = setup.MaxMana; Mana = setup.InitialMana; ManaRegen = setup.ManaRegen;
        HealthRegen = setup.HealthRegen; Burn = setup.Burn; Poison = setup.Poison;
    }
    public EntityId EntityId { get; }
    public HeroIdentityAttributes? Identity { get; }
    public int MaxHealth { get; set; }
    public int MaxHealthAuraBonus { get; set; }
    public int Level { get; }
    public int Health { get; set; }
    public int Armor { get; set; }
    public int MaxMana { get; }
    public int Mana { get; set; }
    public int ManaSpent { get; set; }
    public int Burn { get; set; }
    public int Poison { get; set; }
    public int HealthRegen { get; set; }
    public int ManaRegen { get; set; }
    public int StealthDurationTicks { get; set; }
    public bool FirstLethalDamageReceived { get; set; }
}

internal sealed class BattleAbilityState
{
    public BattleAbilityState(AbilityDefinition definition, decimal cooldownMultiplier = 1m)
    {
        Definition = definition; RemainingCooldownUnits = definition.Activation == AbilityActivation.Active
            ? BattleCooldownRules.DurationUnits(definition.CooldownTicks, multiplier: cooldownMultiplier) : 0m;
    }
    public AbilityDefinition Definition { get; set; }
    public decimal RemainingCooldownUnits { get; set; }
    public bool Triggered { get; set; }
    public long SequenceExpiresAt { get; set; } = -1;
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
        if (setup.CooldownMultiplier is < 0m or > 1m)
            throw new ArgumentOutOfRangeException(nameof(setup), "Cooldown multiplier must be between zero and one.");
        CooldownMultiplier = setup.CooldownMultiplier;
        Identity = setup.Identity;
        Level = setup.Level;
        Quests = Array.AsReadOnly(System.Linq.Enumerable.ToArray(setup.Quests));
        foreach (var quest in Quests)
            QuestProgress[quest.Key] = setup.QuestProgress.TryGetValue(quest.Key, out var progress)
                ? Math.Clamp(progress, 0, quest.RequiredCount) : 0;
        EntityId = setup.EntityId; Side = side; BoardStart = setup.BoardStart; IsOnBench = setup.IsOnBench;
        OccupiedSlots = setup.OccupiedSlots;
        Tags = setup.Tags ?? new TagSet();
        ElementKeys = setup.ElementKeys is null ? [] : new HashSet<StringName>(setup.ElementKeys);
        CombatAttributes[GameAttributeKeys.AttackDamage] = setup.AttackDamage;
        CombatAttributes[GameAttributeKeys.Armor] = setup.ArmorAmount;
        CombatAttributes[GameAttributeKeys.Multicast] = setup.Multicast;
        FrozenValue = setup.CombatValues.TryGetValue(GameAttributeKeys.Value, out var value) ? value : 0;
        var definitions = setup.Abilities is { Count: > 0 }
            ? setup.Abilities
            : setup.UseLegacyAttack
                ? [new AbilityDefinition(new StringName("ability.legacy_attack"), AbilityActivation.Active,
                    AbilityTarget.EnemyHero, 0, setup.CooldownTicks, [new DamageEffectDefinition(setup.AttackDamage)])]
                : Array.Empty<AbilityDefinition>();
        definitions = definitions.Where(definition => !MatchAbilityRules.IsMatchTrigger(definition.Activation)).ToArray();
        foreach (var definition in definitions) Abilities.Add(new BattleAbilityState(definition, CooldownMultiplier));
        if (definitions.Any(definition => definition.Effects.Any(BurnEffectRules.AppliesBurn)))
        {
            SupportedCombatAttributes.Add(GameAttributeKeys.BurnBonus);
            if (setup.CombatValues.TryGetValue(GameAttributeKeys.BurnBonus, out var burnBonus))
                CombatAttributes[GameAttributeKeys.BurnBonus] = burnBonus;
        }
        if (definitions.Any(definition => definition.Effects.Any(effect => effect is
            ApplyStatusEffectDefinition { Status: BattleStatus.Poison }
            or ApplyAttributeStatusEffectDefinition { Status: BattleStatus.Poison }
            or ApplySourceAttributePercentStatusEffectDefinition { Status: BattleStatus.Poison }
            or ApplyPrimaryEffectPercentStatusEffectDefinition { Status: BattleStatus.Poison })))
        {
            SupportedCombatAttributes.Add(GameAttributeKeys.PoisonBonus);
            if (setup.CombatValues.TryGetValue(GameAttributeKeys.PoisonBonus, out var poisonBonus))
                CombatAttributes[GameAttributeKeys.PoisonBonus] = poisonBonus;
        }
        if (definitions.Any(definition => definition.Effects.Any(HasteEffectRules.AppliesHaste)))
        {
            SupportedCombatAttributes.Add(GameAttributeKeys.HasteDurationBonus);
            if (setup.CombatValues.TryGetValue(GameAttributeKeys.HasteDurationBonus, out var hasteBonus))
                CombatAttributes[GameAttributeKeys.HasteDurationBonus] = hasteBonus;
        }
        foreach (var definition in definitions)
        foreach (var effect in definition.Effects)
            switch (effect)
            {
                case HealEffectDefinition:
                case HealMaxHealthPercentEffectDefinition:
                    SupportedCombatAttributes.Add(GameAttributeKeys.HealingBonus);
                    if (setup.CombatValues.TryGetValue(GameAttributeKeys.HealingBonus, out var healingBonus))
                        CombatAttributes[GameAttributeKeys.HealingBonus] = healingBonus;
                    break;
                case ApplyAttributeStatusEffectDefinition attributeStatus:
                    SupportedCombatAttributes.Add(attributeStatus.AttributeKey);
                    CombatAttributes[attributeStatus.AttributeKey] = setup.CombatValues.TryGetValue(attributeStatus.AttributeKey, out var amount) ? amount : 0;
                    break;
                case ApplySourceAttributePercentStatusEffectDefinition percentStatus:
                    SupportedCombatAttributes.Add(percentStatus.AttributeKey);
                    if (setup.CombatValues.TryGetValue(percentStatus.AttributeKey, out var percentAmount))
                        CombatAttributes[percentStatus.AttributeKey] = percentAmount;
                    break;
                case AttributeDamageEffectDefinition attributeDamage:
                    SupportedCombatAttributes.Add(attributeDamage.AttributeKey);
                    break;
                case DestroyAlliedCardsAndDamageEffectDefinition sacrifice:
                    SupportedCombatAttributes.Add(sacrifice.DamageAttributeKey);
                    break;
                case SourceHeroHealthScaledAttributeDamageEffectDefinition scaledDamage:
                    SupportedCombatAttributes.Add(scaledDamage.AttributeKey);
                    break;
                case GainSourceHeroArmorFromAttributeEffectDefinition attributeArmor:
                    SupportedCombatAttributes.Add(attributeArmor.AttributeKey);
                    break;
                case SourceHeroArmorDamageEffectDefinition { BonusAttributeKey: { IsEmpty: false } bonusAttributeKey }:
                    SupportedCombatAttributes.Add(bonusAttributeKey);
                    break;
            }
        if (definitions.Count > 0) SupportedCombatAttributes.Add(GameAttributeKeys.Multicast);
    }
    public EntityId EntityId { get; }
    public SideId Side { get; }
    public int BoardStart { get; }
    public int Level { get; }
    public int FrozenValue { get; }
    public CardIdentityAttributes? TransformedIdentity { get; init; }
    public CardIdentityAttributes? Identity { get; }
    public SummonedCardSnapshot? SummonedCard { get; init; }
    public int OccupiedSlots { get; }
    public bool IsOnBench { get; }
    public TagSet Tags { get; set; }
    public HashSet<StringName> DestroyedTags { get; } = [];
    public IReadOnlySet<StringName> ElementKeys { get; set; }
    public IReadOnlyList<CardQuestDefinition> Quests { get; }
    public Dictionary<StringName, int> QuestProgress { get; } = [];
    public bool Destroyed { get; set; }
    public int ActivationCount { get; set; }
    public int ActiveActivationCount { get; set; }
    public int CooldownBonusTicks { get; set; }
    public int HasteDuration { get; set; }
    public decimal CooldownMultiplier { get; }
    public int CooldownAuraReductionPercent { get; set; }
    public decimal EffectiveCooldownMultiplier => CooldownMultiplier * (1m - CooldownAuraReductionPercent / 100m);
    public int CooldownAuraBonusTicks { get; set; }
    public int SlowDuration { get; set; }
    public int ImmobilizeDuration { get; set; }
    public bool IsFlying { get; set; }
    public bool IsBerserk { get; set; }
    public List<BattleAbilityState> Abilities { get; } = [];
    public Dictionary<StringName, int> CombatAttributes { get; } = [];
    public HashSet<StringName> SupportedCombatAttributes { get; } = [];

    public bool SupportsCombatAttribute(StringName key) => SupportedCombatAttributes.Contains(key);

    // 攻击卡牌按伤害能力分类，零攻击数值不改变分类。
    public bool IsAttackCard => Abilities.Any(item => item.Definition.Effects.Any(effect => effect is
        DamageEffectDefinition or AttributeDamageEffectDefinition or DestroyAlliedCardsAndDamageEffectDefinition
        or MaxHealthPercentDamageEffectDefinition or SourceHeroLevelScaledDamageEffectDefinition
        or SourceHeroHealthScaledAttributeDamageEffectDefinition or SourceHeroArmorDamageEffectDefinition));

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

// 套装阈值战斗来源不占棋盘，也不会因成员卡牌摧毁而失效（领域战斗层）。
internal sealed class CardSetBattleState : IBattleAbilitySource
{
    public CardSetBattleState(CardSetBattleSetup setup, SideId side)
    {
        EntityId = setup.EntityId;
        Side = side;
        foreach (var definition in setup.Abilities) Abilities.Add(new BattleAbilityState(definition));
    }

    public EntityId EntityId { get; }
    public SideId Side { get; }
    public bool Destroyed => false;
    public bool IsOnBench => false;
    public int ActivationCount { get; set; }
    public int CooldownBonusTicks { get; set; }
    public List<BattleAbilityState> Abilities { get; } = [];
    public int GetCombatAttribute(StringName key) => 0;
}

internal sealed record PendingAbility(
    IBattleAbilitySource Source,
    BattleAbilityState Ability,
    bool IsEcho,
    bool IsMulticast,
    BattleTick EnqueuedAt,
    CardBattleState? EventCard = null)
{
    public int PrimaryValue { get; init; }
    public decimal DamageMultiplier { get; init; } = 1m;
    public IReadOnlyList<(IBattleAbilitySource Source, BattleAbilityState Ability)> SequenceFollowups { get; init; } =
        Array.Empty<(IBattleAbilitySource, BattleAbilityState)>();
    public bool SuppressEchoes { get; init; }
    public bool IsTriggeredActivation { get; init; }
    public bool AllowDestroyedSource { get; init; }
    public HashSet<EntityId>? TriggerChain { get; init; }
}

internal sealed class AbilityQueue
{
    private readonly Queue<PendingAbility> _items = new();
    public int Count => _items.Count;
    public void Enqueue(PendingAbility ability) => _items.Enqueue(ability);
    public PendingAbility Dequeue() => _items.Dequeue();
    // 摧毁时取消该来源旧队列，防止同Tick修复后执行摧毁前的发动。
    public void CancelSource(IBattleAbilitySource source)
    {
        for (var remaining = _items.Count; remaining > 0; remaining--)
        {
            var item = _items.Dequeue();
            if (!ReferenceEquals(item.Source, source)) _items.Enqueue(item);
        }
    }
}

internal sealed class BattleRuntime
{
    public BattleRuntime(BattleSetup setup)
    {
        MatchRewardSources = Array.AsReadOnly(setup.Player.Cards.Where(card => !card.IsOnBench)
            .OrderBy(card => card.BoardStart).Select(card => new MatchRewardSource(card.EntityId, card.Level, true,
                Array.AsReadOnly((card.Abilities ?? []).Where(ability => MatchAbilityRules.IsMatchTrigger(ability.Activation)).ToArray())))
            .Concat(setup.Player.Sets.Select(set => new MatchRewardSource(set.EntityId, 1, false,
                Array.AsReadOnly(set.Abilities.Where(ability => MatchAbilityRules.IsMatchTrigger(ability.Activation)).ToArray()))))
            .Concat(setup.Player.Skills.Select(skill => new MatchRewardSource(skill.EntityId, 1, false,
                Array.AsReadOnly(skill.Abilities.Where(ability => MatchAbilityRules.IsMatchTrigger(ability.Activation)).ToArray()))))
            .Where(source => source.Abilities.Count > 0).ToArray());
        PlayerBattlefieldCapacity = setup.Player.BattlefieldCapacity;
        OpponentBattlefieldCapacity = setup.Opponent.BattlefieldCapacity;
        EclipseTime = setup.EclipseTime;
        RandomState = setup.Seed;
        PlayerHero = new HeroBattleState(setup.Player.Hero); OpponentHero = new HeroBattleState(setup.Opponent.Hero);
        AddCards(setup.Player.Cards, SideId.Player); AddCards(setup.Opponent.Cards, SideId.Opponent); Cards.Sort(CompareCards);
        AddSkills(setup.Player.Skills, SideId.Player); AddSkills(setup.Opponent.Skills, SideId.Opponent);
        AddSets(setup.Player.Sets, SideId.Player); AddSets(setup.Opponent.Sets, SideId.Opponent);
    }
    public BattleTick Tick { get; set; } = BattleTick.Zero;
    public PendingAbility? ResolvingActivation { get; set; }
    public IReadOnlyList<MatchRewardSource> MatchRewardSources { get; }
    public HeroBattleState PlayerHero { get; }
    public HeroBattleState OpponentHero { get; }
    public List<CardBattleState> Cards { get; } = [];
    public List<PermanentCardAttributeBonus> PermanentAttributeBonuses { get; } = [];
    public List<SkillBattleState> Skills { get; } = [];
    public List<CardSetBattleState> Sets { get; } = [];
    private int PlayerBattlefieldCapacity { get; }
    private int OpponentBattlefieldCapacity { get; }
    private int SummonSequence { get; set; }
    public int GetBattlefieldCapacity(SideId side) => side == SideId.Player ? PlayerBattlefieldCapacity : OpponentBattlefieldCapacity;
    public void AddSummonedCard(CardBattleState card) { Cards.Add(card); Cards.Sort(CompareCards); }
    // 由来源及战斗内顺序派生 ID，复算保持一致且不消耗战斗随机数。
    public EntityId NewSummonedCardId(EntityId source)
    {
        EntityId id;
        do
        {
            var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(
                FormattableString.Invariant($"summon:{source.Value:D}:{++SummonSequence}")));
            id = new EntityId(new Guid(hash.AsSpan(0, 16)));
        } while (id == PlayerHero.EntityId || id == OpponentHero.EntityId
            || System.Linq.Enumerable.Any(AbilitySources, item => item.EntityId == id));
        return id;
    }
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
                foreach (var set in Sets)
                    if (set.Side == side) yield return set;
            }
        }
    }
    public AbilityQueue Queue { get; } = new();
    public List<(BattleTick Due, PendingAbility Ability)> ScheduledMulticasts { get; } = [];
    public List<BattleEvent> Events { get; } = [];
    public BattleTick EclipseTime { get; set; }
    public bool EclipseEntered { get; set; }
    public List<BattleStateSnapshot> States { get; } = [];
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
    private void AddSets(IReadOnlyList<CardSetBattleSetup> sets, SideId side)
    {
        foreach (var set in sets) Sets.Add(new CardSetBattleState(set, side));
    }
    private static int CompareCards(CardBattleState left, CardBattleState right)
    {
        var side = left.Side.CompareTo(right.Side); if (side != 0) return side;
        var position = left.BoardStart.CompareTo(right.BoardStart);
        return position != 0 ? position : left.EntityId.Value.CompareTo(right.EntityId.Value);
    }
}
