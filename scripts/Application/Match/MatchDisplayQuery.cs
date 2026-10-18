using System;
using System.Linq;
using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Domain.Common;
using Project_Star.Application.Combat;
using Project_Star.Domain.Combat;

namespace Project_Star.Application.Match;

/// <summary>Captures a shop card without creating an instance or advancing random state.</summary>
public static class MatchDisplayQuery
{
    public static MatchSnapshot Capture(MatchSession session, IReadOnlyDictionary<StringName, CardSetDefinition> sets,
        MatchSession? opponent = null)
    {
        var snapshot = MatchSnapshot.From(session);
        var active = new CardSetEvaluator().Evaluate(session, sets);
        var placed = snapshot.BoardPlacements.Where(item => item.Zone == BoardZone.Battlefield)
            .Select(item => item.CardId).ToHashSet();
        var preview = session.Player.Hero is null ? null : new CombatSimulator().Preview(
            new BattleSetupFactory(sets).CreatePreview(session, opponent), opponent is not null);
        return snapshot with
        {
            Hero = snapshot.Hero is { } hero && preview is not null ? hero with
            {
                CombatValues = new System.Collections.ObjectModel.ReadOnlyDictionary<StringName, int>(
                    new Dictionary<StringName, int>(hero.CombatValues) { [GameAttributeKeys.MaxHealth] = preview.Player.MaxHealth }),
            } : snapshot.Hero,
            Cards = Array.AsReadOnly(snapshot.Cards.Select(card =>
            {
                var state = preview?.Cards.FirstOrDefault(item => item.Id == card.Id && item.Side == SideId.Player);
                return state is null || !placed.Contains(card.Id) ? card
                    : card with { CurrentValues = state.Values, EffectAmounts = state.EffectAmounts, Tags = state.QuestTags,
                        CurrentCooldownSeconds = state.CooldownDurationUnits.Count > 0 ? state.CooldownDurationUnits.Max() / 20m : null };
            }).ToArray()),
            Sets = Array.AsReadOnly(sets.Values.OrderBy(set => set.Attributes.Identity.Key.ToString(), StringComparer.Ordinal)
                .Select(set => new CardSetSnapshot(set.Attributes.Identity.Key, set.Attributes.Identity.DisplayName,
                    snapshot.Cards.Where(card => placed.Contains(card.Id) && card.SetKey == set.Attributes.Identity.Key)
                        .Select(card => card.Key).Distinct().Count(),
                    Array.AsReadOnly(set.Thresholds.Select(threshold => new CardSetThresholdSnapshot(
                        threshold.RequiredDistinctCards, active.Any(item => item.SetKey == set.Attributes.Identity.Key
                            && item.Threshold.RequiredDistinctCards == threshold.RequiredDistinctCards),
                        MatchSnapshot.CopyAbilities(threshold.Abilities))).ToArray()))).ToArray()),
        };
    }

    public static (bool CanMerge, string Reason) PurchaseCondition(MatchSession session, ShopOffer offer,
        Project_Star.Application.Economy.CardEconomyService economy,
        Project_Star.Application.Board.BoardService board)
    {
        var merge = economy.FindMergeTarget(session, offer.Definition, offer.Level) is not null;
        var check = economy.CheckPurchase(session, offer);
        return (merge, check.Failure?.Message ?? "");
    }

    // 技能奖励展示使用正式等级配置，并复制可变集合。
    public static SkillSnapshot FromSkill(SkillDefinition definition, int level)
    {
        var values = definition.Attributes.BaseCombat.CreateMutableCopy();
        var configured = definition.GetLevel(level);
        if (configured is not null)
            foreach (var (key, value) in configured.BaseCombatValues) values.SetBaseValue(key, value);
        var identity = definition.Attributes.Identity;
        return new SkillSnapshot(default, identity.Key, identity.DisplayName, level, identity.FactionKey)
        {
            Illustration = identity.Illustration,
            Abilities = MatchSnapshot.CopyAbilities(configured?.Abilities ?? definition.Abilities),
            CurrentValues = values.SnapshotFinalValues(),
        };
    }

    public static CardSnapshot FromOffer(ShopOffer offer)
    {
        var definition = offer.Definition;
        var identity = definition.Attributes.Identity;
        var values = definition.Attributes.BaseCombat.CreateMutableCopy();
        var level = definition.GetLevel(offer.Level);
        if (level is not null)
            foreach (var (key, value) in level.BaseCombatValues) values.SetBaseValue(key, value);
        return new CardSnapshot(default, identity.Key, identity.DisplayName, offer.Level,
            CardValueCalculator.CalculateAcquiredValue(offer.Price) + (level?.AcquiredValueBonus ?? 0), identity.Size, identity.FactionKey,
            Array.AsReadOnly(identity.ElementKeys.ToArray()), Array.Empty<QuestProgressSnapshot>())
        {
            SetKey = identity.SetKey,
            OnSellReward = level?.OnSellReward ?? definition.OnSellReward,
            SaleAttributeBonuses = level?.SaleAttributeBonuses ?? Array.Empty<TaggedCardSaleAttributeBonus>(),
            BattleVictoryBonuses = level?.BattleVictoryBonuses ?? Array.Empty<BattleVictoryAttributeBonus>(),
            BattleValueBonus = level?.BattleValueBonus ?? 0,
            AcquisitionAttributeBonuses = level?.AcquisitionAttributeBonuses ?? Array.Empty<TaggedCardAcquisitionAttributeBonus>(),
            OwnValues = values.SnapshotFinalValues(),
            Illustration = identity.Illustration,
            GemSockets = Array.AsReadOnly(new GemSnapshot?[identity.GemSocketCount]),
            DescriptionEntries = Array.AsReadOnly(identity.DescriptionEntries.ToArray()),
            Tags = Array.AsReadOnly(definition.Tags.ToArray()),
            BaseValues = values.SnapshotFinalValues(), CurrentValues = values.SnapshotFinalValues(),
            Abilities = MatchSnapshot.CopyAbilities(level?.Abilities ?? definition.Abilities),
            QuestDefinitions = Array.AsReadOnly(definition.Quests.ToArray()),
        };
    }
}
