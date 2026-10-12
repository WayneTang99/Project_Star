using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Content;

// 应用层只读卡牌预览；冻结目录并复用战斗预览，不创建对局实体或执行交易。
public sealed class CardPreviewQuery
{
    private readonly IReadOnlyList<CardCatalogEntry> _cards;
    private readonly IReadOnlyList<CardSetSnapshot> _sets;
    public IReadOnlyList<GemSnapshot> Gems { get; }

    public CardPreviewQuery(IDefinitionCatalog catalog)
    {
        _cards = CardCatalogQuery.Capture(catalog);
        _sets = Array.AsReadOnly(catalog.Sets.Values.Select(set => new CardSetSnapshot(set.Attributes.Identity.Key,
            set.Attributes.Identity.DisplayName, 0, Array.AsReadOnly(set.Thresholds.Select(threshold =>
                new CardSetThresholdSnapshot(threshold.RequiredDistinctCards, false,
                    MatchSnapshot.CopyAbilities(threshold.Abilities))).ToArray()))).ToArray());
        Gems = Array.AsReadOnly(catalog.Gems.Values.Select(gem => new GemSnapshot(gem.Attributes.Identity.Key,
            gem.Attributes.Identity.DisplayName, gem.Attributes.Identity.Description)
            { ActiveEffects = Array.AsReadOnly(gem.ActiveEffects.ToArray()), Color = gem.Color }).ToArray());
    }

    // 只返回正式定义支持的等级。
    public IReadOnlyList<int> Levels(CardSnapshot card) => Array.AsReadOnly(
        (_cards.FirstOrDefault(item => item.InitialCard.Key == card.Key)?.Levels.Select(item => item.Card.Level)
            ?? new[] { card.Level }).ToArray());

    // 商品与图鉴补套装说明，实例使用同批战场套装件数与英雄等级。
    public CardSnapshot Describe(CardSnapshot card, MatchSnapshot? context) => card with
    {
        Set = context?.Sets.FirstOrDefault(set => set.Key == card.SetKey)
            ?? _sets.FirstOrDefault(set => set.Key == card.SetKey),
        HeroLevel = context?.Hero?.Level ?? card.HeroLevel,
    };

    // 保留实例成长、任务与冷却倍率；等级和宝石仅作用于本次查询的副本。
    public CardSnapshot Preview(CardSnapshot source, int level, IReadOnlyList<StringName?> sockets,
        MatchSnapshot? context = null, MatchSnapshot? enemy = null)
    {
        var entry = _cards.FirstOrDefault(item => item.InitialCard.Key == source.Key);
        var target = entry?.Levels.FirstOrDefault(item => item.Card.Level == level)?.Card;
        if (target is null || sockets.Count != source.GemSockets.Count) return Describe(source, context);
        var selected = sockets.Select(key => key is null ? null : Gems.FirstOrDefault(gem => gem.Key == key)).ToArray();
        if (sockets.Where(key => key is not null).Any(key => !Gems.Any(gem => gem.Key == key))) return Describe(source, context);
        if (!target.Abilities.Any(ability => ability.Activation == AbilityActivation.Active)) selected = source.GemSockets.ToArray();
        if (level == source.Level && selected.Select(gem => gem?.Key).SequenceEqual(source.GemSockets.Select(gem => gem?.Key)))
            return Describe(source, context);
        var own = source.OwnValues.Count > 0 ? source.OwnValues : source.CurrentValues;
        var values = own.Keys.Concat(target.BaseValues.Keys).Distinct().ToDictionary(key => key,
            key => checked(own.GetValueOrDefault(key) + target.BaseValues.GetValueOrDefault(key) - source.BaseValues.GetValueOrDefault(key)));
        var quests = source.QuestDefinitions.Where(quest => source.Quests.Any(progress => progress.Key == quest.Key && progress.Unlocked)).ToArray();
        var gemEffects = selected.Where(gem => gem is not null).SelectMany(gem => gem!.ActiveEffects).ToArray();
        var originalGemCount = source.GemSockets.Where(gem => gem is not null).Sum(gem => gem!.ActiveEffects.Count);
        var abilities = level == source.Level ? source.Abilities : target.Abilities.Concat(quests.SelectMany(quest => quest.Abilities)).ToArray();
        var composed = abilities.Select(ability => new AbilityDefinition(ability.Key, ability.Activation, ability.Target,
            ability.ManaCost, ability.Activation == AbilityActivation.Active && level != source.Level
                ? Math.Max(1, checked(ability.CooldownTicks + own.GetValueOrDefault(GameAttributeKeys.CooldownTicks) - source.BaseValues.GetValueOrDefault(GameAttributeKeys.CooldownTicks)))
                : ability.CooldownTicks,
            ability.Activation != AbilityActivation.Active ? ability.Effects : Array.AsReadOnly(
                (level == source.Level ? ability.Effects.Take(Math.Max(0, ability.Effects.Count - originalGemCount))
                    : ability.Effects.Concat(quests.SelectMany(quest => quest.AdditionalActiveEffects))).Concat(gemEffects).ToArray()),
            ability.AllowsBench, ability.TriggerStateKey, ability.TriggerCardTag, ability.TriggerCardElement,
            ability.TriggerCardSide, ability.TriggerCardSize, ability.TriggerStatus, ability.TriggerHeroHealthPercent)).ToArray();
        var preview = Describe(source with
        {
            Level = level, Value = checked(source.Value + target.Value - (entry!.Levels.First(item => item.Card.Level == source.Level).Card.Value)),
            BaseValues = target.BaseValues, OwnValues = new ReadOnlyDictionary<StringName, int>(values),
            CurrentValues = new ReadOnlyDictionary<StringName, int>(values), Abilities = Array.AsReadOnly(composed),
            EffectAmounts = Array.Empty<int?>(), GemSockets = Array.AsReadOnly(selected), OnSellReward = target.OnSellReward,
            SaleAttributeBonuses = target.SaleAttributeBonuses, BattleVictoryBonuses = target.BattleVictoryBonuses,
            BattleValueBonus = target.BattleValueBonus,
        }, context);
        var id = preview.Id == default ? new EntityId(new Guid("00000000-0000-0000-0000-000000000001")) : preview.Id;
        var player = Side(context, preview with { Id = id }, 2);
        var opponent = Side(enemy, null, 3);
        var state = new CombatSimulator().Preview(new BattleSetup(player, opponent, 1, new BattleTick(1)), enemy is not null)
            .Cards.FirstOrDefault(card => card.Id == id && card.Side == SideId.Player);
        return state is null ? preview : preview with { CurrentValues = state.Values, EffectAmounts = state.EffectAmounts,
            CurrentCooldownSeconds = state.CooldownDurationUnits.Count > 0 ? state.CooldownDurationUnits.Max() / 20m : null };
    }

    private static BattleSideSetup Side(MatchSnapshot? context, CardSnapshot? replacement, int heroId)
    {
        var hero = context?.Hero;
        var stats = hero?.CombatValues;
        int Value(StringName key, int fallback = 0) => stats?.GetValueOrDefault(key, fallback) ?? fallback;
        var cards = context?.Cards.ToList() ?? new List<CardSnapshot>();
        if (replacement is not null)
        {
            cards.RemoveAll(card => card.Id == replacement.Id); cards.Add(replacement);
        }
        var inputs = cards.Select(card =>
        {
            var placement = context?.BoardPlacements.FirstOrDefault(item => item.CardId == card.Id);
            var own = card.OwnValues.Count > 0 ? card.OwnValues : card.CurrentValues;
            return new CardBattleSetup(card.Id, placement?.Start ?? 0, own.GetValueOrDefault(GameAttributeKeys.AttackDamage),
                own.GetValueOrDefault(GameAttributeKeys.CooldownTicks), card.Abilities,
                IsOnBench: context is not null && (placement is null || placement.Zone == BoardZone.Bench), UseLegacyAttack: false,
                Tags: new TagSet(card.Tags), Multicast: own.GetValueOrDefault(GameAttributeKeys.Multicast),
                OccupiedSlots: (int)card.Size, ElementKeys: card.ElementKeys,
                ArmorAmount: own.GetValueOrDefault(GameAttributeKeys.Armor))
                { Level = card.Level, CombatValues = own, CooldownMultiplier = card.CooldownMultiplier };
        }).ToArray();
        return new BattleSideSetup(new HeroBattleSetup(hero?.Id ?? new EntityId(new Guid($"00000000-0000-0000-0000-{heroId:000000000000}")),
            Value(GameAttributeKeys.MaxHealth, 200), Value(GameAttributeKeys.Armor), Value(GameAttributeKeys.MaxMana, 100),
            Value(GameAttributeKeys.Mana), Value(GameAttributeKeys.ManaRegen), Value(GameAttributeKeys.HealthRegen), Level: hero?.Level ?? 1),
            inputs, context?.Skills.Select(skill => new SkillBattleSetup(skill.Id, skill.Abilities, skill.CurrentValues)).ToArray(),
            context?.Sets.SelectMany(set => set.Thresholds.Where(threshold => threshold.Active)).Select((threshold, index) =>
                new CardSetBattleSetup(new EntityId(new Guid($"00000000-0000-0000-{heroId:0000}-{index + 10:000000000000}")), threshold.Abilities)).ToArray())
            { BattlefieldCapacity = context?.BattlefieldCapacity ?? 10 };
    }
}
