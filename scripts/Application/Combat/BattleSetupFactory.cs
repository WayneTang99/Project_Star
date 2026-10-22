using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Combat;

/// <summary>Copies match state into an immutable battle input.</summary>
public sealed class BattleSetupFactory
{
    private readonly IReadOnlyDictionary<StringName, CardSetDefinition> _sets;
    private readonly CardSetEvaluator _setEvaluator = new();

    public BattleSetupFactory(IReadOnlyDictionary<StringName, CardSetDefinition>? sets = null) =>
        _sets = sets ?? new Dictionary<StringName, CardSetDefinition>();

    public BattleSetup Create(
        MatchSession playerSession,
        MatchSession opponentSession,
        ulong seed,
        BattleTick timeout,
        BattleTick? eclipseTime = null)
    {
        ArgumentNullException.ThrowIfNull(playerSession);
        ArgumentNullException.ThrowIfNull(opponentSession);
        return new BattleSetup(
            CreateSide(playerSession),
            CreateSide(opponentSession),
            seed,
            timeout,
            eclipseTime ?? new BattleTick(300));
    }

    // 展示预览复用同一冻结输入；尚未选择对手时仅用空对侧承载自身光环查询。
    public BattleSetup CreatePreview(MatchSession session, MatchSession? opponent = null) => new(
        CreateSide(session), opponent is null
            ? new BattleSideSetup(new HeroBattleSetup(default, 1, 0, MaxMana: 0, ManaRegen: 0), [])
            : CreateSide(opponent), 0, new BattleTick(0));

    private BattleSideSetup CreateSide(MatchSession session)
    {
        var hero = session.Player.Hero
            ?? throw new InvalidOperationException("A hero must be selected before battle.");
        var maxHealth = checked(
            hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth)
            + session.PendingBattleMaxHealthBonus);
        var armor = hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Armor);
        var maxMana = hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxMana);
        var mana = hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Mana);
        var manaRegen = hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.ManaRegen);
        var healthRegen = hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.HealthRegen);
        var burn = hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Burn);
        var poison = hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Poison);
        var level = hero.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Level);
        if (maxHealth < 1 || armor < 0 || maxMana < 0 || mana < 0 || mana > maxMana
            || manaRegen < 0 || healthRegen < 0 || burn < 0 || poison < 0 || level < 1)
        {
            throw new InvalidOperationException("Hero battle attributes are invalid.");
        }

        var cards = new List<CardBattleSetup>();
        foreach (var placement in session.Board.Battlefield.Placements)
        {
            var card = session.Player.Inventory.Find(placement.CardId)
                ?? throw new InvalidOperationException($"Battlefield card '{placement.CardId}' is not owned by the player.");
            var damage = card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage);
            var cooldown = card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks);
            var multicast = card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Multicast);
            var armorAmount = card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Armor);
            if (damage < 0 || cooldown < 0 || multicast < 0 || armorAmount < 0)
            {
                throw new InvalidOperationException($"Card '{placement.CardId}' has invalid battle attributes.");
            }

            var abilities = CardAbilityComposer.Compose(card);
            var combatValues = new Dictionary<StringName, int>(card.Attributes.BaseCombat.SnapshotFinalValues())
            {
                [GameAttributeKeys.Value] = card.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value),
            };
            cards.Add(new CardBattleSetup(
                card.Id,
                placement.Start,
                damage,
                cooldown,
                abilities,
                UseLegacyAttack: false,
                Tags: card.Tags,
                Multicast: multicast,
                OccupiedSlots: card.Attributes.Identity.OccupiedSlots,
                ElementKeys: card.Attributes.Identity.ElementKeys,
                ArmorAmount: armorAmount)
                { UpgradeLevels = BattleCardLevels.Capture(card.Definition, card), Identity = card.Attributes.Identity, CombatValues = new System.Collections.ObjectModel.ReadOnlyDictionary<StringName, int>(combatValues), CooldownMultiplier = card.CooldownMultiplier,
                    Level = card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level), Quests = card.Quests,
                    QuestProgress = new System.Collections.ObjectModel.ReadOnlyDictionary<StringName, int>(
                        card.Quests.ToDictionary(quest => quest.Key, quest => card.GetQuestProgress(quest.Key))) });
        }

        foreach (var placement in session.Board.Bench.Placements)
        {
            var card = session.Player.Inventory.Find(placement.CardId)
                ?? throw new InvalidOperationException($"Bench card '{placement.CardId}' is not owned by the player.");
            var damage = card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage);
            var cooldown = card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks);
            var multicast = card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Multicast);
            var armorAmount = card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Armor);
            if (damage < 0 || cooldown < 0 || multicast < 0 || armorAmount < 0)
                throw new InvalidOperationException($"Card '{placement.CardId}' has invalid battle attributes.");
            cards.Add(new CardBattleSetup(
                card.Id,
                placement.Start,
                damage,
                cooldown,
                CardAbilityComposer.Compose(card),
                IsOnBench: true,
                UseLegacyAttack: false,
                Tags: card.Tags,
                Multicast: multicast,
                OccupiedSlots: card.Attributes.Identity.OccupiedSlots,
                ElementKeys: card.Attributes.Identity.ElementKeys,
                ArmorAmount: armorAmount) { UpgradeLevels = BattleCardLevels.Capture(card.Definition, card), Identity = card.Attributes.Identity, CooldownMultiplier = card.CooldownMultiplier,
                    CombatValues = new System.Collections.ObjectModel.ReadOnlyDictionary<StringName, int>(
                        new Dictionary<StringName, int>(card.Attributes.BaseCombat.SnapshotFinalValues())
                        { [GameAttributeKeys.Value] = card.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value) }),
                    Level = card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level), Quests = card.Quests,
                    QuestProgress = new System.Collections.ObjectModel.ReadOnlyDictionary<StringName, int>(
                        card.Quests.ToDictionary(quest => quest.Key, quest => card.GetQuestProgress(quest.Key))) });
        }

        var skills = new List<SkillBattleSetup>();
        foreach (var skill in session.Player.Skills.Items)
            skills.Add(new SkillBattleSetup(
                skill.Id,
                skill.Abilities,
                skill.Attributes.BaseCombat.SnapshotFinalValues()));

        var sets = new List<CardSetBattleSetup>();
        foreach (var active in _setEvaluator.Evaluate(session, _sets))
        {
            var battleAbilities = new List<AbilityDefinition>();
            foreach (var ability in active.Threshold.Abilities)
                if (ability.Activation != AbilityActivation.PassiveWhileEnabled)
                    battleAbilities.Add(ability);
            if (battleAbilities.Count > 0)
                sets.Add(new CardSetBattleSetup(
                    CardSetEvaluator.SourceId(active.SetKey, active.Threshold.RequiredDistinctCards),
                    battleAbilities.AsReadOnly()));
        }

        return new BattleSideSetup(
            new HeroBattleSetup(hero.Id, maxHealth, armor, maxMana, mana, manaRegen, healthRegen, burn, poison, level)
                { Identity = hero.Attributes.Identity },
            cards.AsReadOnly(),
            skills.AsReadOnly(),
            sets.AsReadOnly()) { BattlefieldCapacity = session.Board.Battlefield.Capacity,
                CardDefinitions = Array.AsReadOnly(session.Definitions?.Cards.Values.OrderBy(card => card.Attributes.Identity.Key.ToString(), StringComparer.Ordinal).ToArray()
                    ?? Array.Empty<CardDefinition>()) };
    }
}
