using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using Godot;
using Project_Star.Application.Economy;
using Project_Star.Application.Encounters;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Infrastructure.Server;

// 显式对局存档编解码（基础设施层）；不把展示 Snapshot 或内容 Definition 整体序列化。
public sealed class MatchSaveCodec(IDefinitionCatalog definitions, string rulesVersion)
{
    private readonly EntityFactory _factory = new();
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new KeyConverter() },
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    };

    // 捕获当前闭环状态；战斗重复结算由持久化阶段和数据库回执阻止，不保存对象引用缓存。
    public string Encode(ServerMatchSession run)
    {
        var match = run.Match;
        if (match.ActiveMentorVisit is not null || match.ActiveGemShopVisit is not null)
            throw new NotSupportedException("Mentor and gem visits require their durable protocol before saving.");
        var hero = match.Player.Hero ?? throw new InvalidOperationException("Hero is required.");
        var saved = new Save(4, rulesVersion, match.Id, match.Random.Seed.ToString(CultureInfo.InvariantCulture),
            match.Random.State.ToString(CultureInfo.InvariantCulture), match.Board.Battlefield.Capacity,
            match.Progress.Round, match.Progress.Turn, match.Progress.PvpWins,
            match.Progress.IncomeSettledThroughRound, match.Progress.ExperienceSettledThroughTurn,
            match.Status, match.Summary, Capture(match.Player.Resources),
            new HeroData(hero.Id.Value, hero.Attributes.Identity.Key, Capture(hero.Attributes.Persistent), Capture(hero.Attributes.BaseCombat)),
            match.Player.Inventory.Cards.Select(card => new CardData(card.Id.Value, card.Attributes.Identity.Key,
                card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level), Capture(card.Attributes.Persistent), Capture(card.Attributes.BaseCombat),
                card.CooldownMultiplier, card.Quests.Select(quest => new QuestData(quest.Key, card.GetQuestProgress(quest.Key))).ToArray(),
                card.GemSockets.Select(gem => gem?.Attributes.Identity.Key).ToArray())).ToArray(),
            match.Player.Skills.Items.Select(skill => new SkillData(skill.Id.Value, skill.Attributes.Identity.Key,
                skill.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level), Capture(skill.Attributes.Persistent), Capture(skill.Attributes.BaseCombat))).ToArray(),
            match.Board.Battlefield.Placements.Select(placement => new PlacementData(BoardZone.Battlefield, placement)).Concat(
                match.Board.Bench.Placements.Select(placement => new PlacementData(BoardZone.Bench, placement))).ToArray(),
            match.EncounterSchedule.SeenKeys.OrderBy(key => key.ToString(), StringComparer.Ordinal).ToArray(),
            match.EncounterSchedule.CurrentChoices.ToArray(), match.PendingMonsterRewards.ToArray(), match.PendingBattleMaxHealthBonus,
            match.GemShopRevision, match.AppliedCardSetModifiers.ToArray(), match.AppliedCardQuestModifiers.ToArray(),
            run.Phase, run.ShopKey, run.Shop is null ? null : new ShopData(run.Shop.ShopLevel, run.Shop.RefreshCost, run.Shop.HasRefreshed,
                run.Shop.EligibleCards.Select(card => card.Attributes.Identity.Key).ToArray(),
                run.Shop.Offers.Select(offer => new OfferData(offer.Definition.Attributes.Identity.Key, offer.Level, offer.Price, offer.IsSold)).ToArray()),
            run.Battle is not { } battle ? null : new BattleData(battle.Id, battle.MonsterKey, battle.Round, battle.Turn,
                battle.Seed.ToString(CultureInfo.InvariantCulture), Encode(new ServerMatchSession(battle.Opponent)), battle.Result, battle.SourceEncounterKey),
            run.Event is not { } visit ? null : new EventData(visit.Id, visit.EncounterKey, visit.Round, visit.Turn,
                visit.Options.Level, visit.Options.Options.Select(option => option.Key).ToArray(), visit.Options.IsResolved, visit.Result),
            run.Pvp is not { } pvp ? null : new PvpData(pvp.Id, pvp.Round, pvp.Seed.ToString(CultureInfo.InvariantCulture),
                Encode(new ServerMatchSession(pvp.Opponent)), pvp.OpponentSnapshotId, pvp.IsTestOpponent, pvp.Result));
        return JsonSerializer.Serialize(saved, Json);
    }

    // 重建独立实体与可撤销贡献，不抽取随机数、不执行收入、任务或购买效果。
    public ServerMatchSession Decode(string text) => Decode(text, null);

    private ServerMatchSession Decode(string text, StringName? monsterKey, bool isOpponent = false)
    {
        var saved = JsonSerializer.Deserialize<Save>(text, Json) ?? throw new JsonException("Save is required.");
        if (saved.FormatVersion != 4 || saved.RulesVersion != rulesVersion) throw new InvalidOperationException("Save version is incompatible.");
        if (isOpponent && (saved.Phase != ServerMatchPhase.EncounterChoice || saved.Shop is not null
            || saved.Battle is not null || saved.Event is not null || saved.Pvp is not null))
            throw new InvalidOperationException("Opponent must be an independent frozen snapshot.");
        if (saved.MatchId == Guid.Empty || saved.Round < 1 || saved.Turn is < 1 or > 8
            || saved.BoardCapacity != BoardState.DefaultCapacity || !Enum.IsDefined(saved.Status)
            || !Enum.IsDefined(saved.Phase) || saved.Cards.Length > 64 || saved.Skills.Length > 256)
            throw new InvalidOperationException("Invalid save header.");
        var seed = ulong.Parse(saved.Seed, CultureInfo.InvariantCulture);
        var match = new MatchSession(saved.MatchId, seed, saved.BoardCapacity, definitions);
        match.Random.State = ulong.Parse(saved.RandomState, CultureInfo.InvariantCulture);
        match.Progress.Round = saved.Round;
        match.Progress.Turn = saved.Turn;
        match.Progress.PvpWins = saved.PvpWins;
        match.Progress.IncomeSettledThroughRound = saved.IncomeSettledThroughRound;
        match.Progress.ExperienceSettledThroughTurn = saved.ExperienceSettledThroughTurn;
        match.Status = saved.Status;
        match.Summary = saved.Summary;
        RestoreInto(match.Player.Resources, saved.Resources);
        if (monsterKey is not null && (saved.Hero.Key != monsterKey || saved.Battle is not null || saved.Event is not null))
            throw new InvalidOperationException("Invalid monster opponent.");
        var heroTemplate = monsterKey is null ? _factory.CreateHero(definitions.Heroes[saved.Hero.Key])
            : _factory.CreateMonsterCombatant(definitions.Monsters[monsterKey]);
        var ids = new HashSet<Guid>();
        AddId(ids, saved.Hero.Id);
        match.Player.SelectHero(new HeroInstance(new EntityId(saved.Hero.Id), new EntityAttributes<HeroIdentityAttributes>(
            heroTemplate.Attributes.Identity, Restore(saved.Hero.Persistent), Restore(saved.Hero.Combat))));
        foreach (var item in saved.Cards)
        {
            AddId(ids, item.Id);
            var definition = definitions.Cards[item.Key];
            var template = _factory.CreateCard(definition, item.Level);
            var persistent = Restore(item.Persistent);
            if (persistent.GetBaseValue(GameAttributeKeys.Level) != item.Level) throw new InvalidOperationException("Card level mismatch.");
            var card = new CardInstance(new EntityId(item.Id), new EntityAttributes<CardIdentityAttributes>(
                template.Attributes.Identity, persistent, Restore(item.Combat)), template.Tags, template.Abilities,
                template.OnSellReward, template.Quests, template.SaleAttributeBonuses, template.BattleVictoryBonuses,
                template.BattleValueBonus, template.AcquisitionAttributeBonuses) { Definition = definition };
            card.RestoreCooldownMultiplier(item.CooldownMultiplier);
            if (item.Quests.Select(quest => quest.Key).Distinct().Count() != item.Quests.Length) throw new InvalidOperationException("Duplicate quest.");
            foreach (var quest in item.Quests)
            {
                var rule = card.Quests.Single(value => value.Key == quest.Key);
                if (quest.Progress < 0 || quest.Progress > rule.RequiredCount) throw new InvalidOperationException("Invalid quest progress.");
                card.SetQuestProgress(quest.Key, quest.Progress);
            }
            if (item.Gems.Length != card.GemSockets.Count) throw new InvalidOperationException("Gem socket mismatch.");
            for (var index = 0; index < item.Gems.Length; index++)
                if (item.Gems[index] is { } key) card.SocketGem(index, definitions.Gems[key]);
            match.Player.Inventory.Add(card);
        }
        foreach (var item in saved.Skills)
        {
            AddId(ids, item.Id);
            var template = _factory.CreateSkill(definitions.Skills[item.Key], item.Level);
            var persistent = Restore(item.Persistent);
            if (persistent.GetBaseValue(GameAttributeKeys.Level) != item.Level) throw new InvalidOperationException("Skill level mismatch.");
            match.Player.Skills.Add(new SkillInstance(new EntityId(item.Id), new EntityAttributes<SkillIdentityAttributes>(
                template.Attributes.Identity, persistent, Restore(item.Combat)), template.Abilities));
        }
        var placed = new HashSet<EntityId>();
        foreach (var item in saved.Placements)
        {
            var placement = item.Placement;
            var card = match.Player.Inventory.Find(placement.CardId) ?? throw new InvalidOperationException("Placement references missing card.");
            var zone = match.Board.GetZone(item.Zone);
            if (!placed.Add(card.Id) || placement.Size != card.Attributes.Identity.OccupiedSlots || placement.Start < 0
                || placement.EndExclusive > zone.Capacity || zone.Placements.Any(other => placement.Start < other.EndExclusive && other.Start < placement.EndExclusive))
                throw new InvalidOperationException("Invalid placement.");
            zone.Apply(card.Id, placement.Size, placement.Start, placement.IsPushable,
                PlacementPlan.Success(placement.Start, PushDirection.None, 0, Array.Empty<PlacementShift>()));
        }
        match.EncounterSchedule.Restore(saved.SeenEncounters, saved.Choices);
        foreach (var reward in saved.PendingRewards) match.AddPendingMonsterReward(reward);
        match.AddPendingBattleMaxHealthBonus(saved.PendingBattleMaxHealthBonus);
        match.GemShopRevision = saved.GemShopRevision;
        match.AppliedCardSetModifiers.AddRange(saved.SetModifiers);
        match.AppliedCardQuestModifiers.AddRange(saved.QuestModifiers);
        var run = new ServerMatchSession(match) { Phase = saved.Phase, ShopKey = saved.ShopKey };
        if (saved.Shop is { } shop)
        {
            if (saved.Phase != ServerMatchPhase.Shop || saved.ShopKey is null
                || definitions.Encounters[saved.ShopKey] is not CardShopEncounterDefinition || shop.Offers.Length > ShopCardPoolService.OfferCount)
                throw new InvalidOperationException("Invalid shop state.");
            var offers = shop.Offers.Select(item =>
            {
                var offer = ShopOffer.Create(definitions.Cards[item.Key], item.Level);
                if (offer.Price != item.Price) throw new InvalidOperationException("Shop price does not match pinned rules.");
                if (item.Sold) offer.MarkSold();
                return offer;
            }).ToArray();
            run.Shop = new ShopStock(shop.EligibleCards.Select(key => definitions.Cards[key]).ToArray(), offers, shop.RefreshCost, shop.Level);
            if (shop.Refreshed) run.Shop.ReplaceOffers(offers);
        }
        else if (saved.Phase == ServerMatchPhase.Shop || saved.ShopKey is not null)
            throw new InvalidOperationException("Missing shop state.");
        if (saved.Battle is { } battle)
        {
            if (battle.Id == Guid.Empty || !definitions.Monsters.ContainsKey(battle.MonsterKey)
                || battle.Round != saved.Round || battle.Turn is < 1 or > 7 || saved.Turn != battle.Turn + 1 || saved.Shop is not null
                || saved.Event is not null || saved.Pvp is not null
                || (battle.SourceEncounterKey is null ? battle.Turn != 4
                    : !definitions.Encounters.TryGetValue(battle.SourceEncounterKey, out var source) || source is not ChoiceEncounterDefinition)
                || saved.Choices.Length != 0
                || (saved.Phase != ServerMatchPhase.MonsterPreparation && saved.Phase != ServerMatchPhase.MonsterResult)
                || (saved.Phase == ServerMatchPhase.MonsterPreparation) != (battle.Result is null)
                || (battle.Result is { } result && (!Enum.IsDefined(result.Outcome) || !Enum.IsDefined(result.EndReason)
                    || result.EndedAt < 0 || result.EndedAt > 600 || result.PlayerRemainingHealth < 0 || result.OpponentRemainingHealth < 0)))
                throw new InvalidOperationException("Invalid monster battle state.");
            run.Battle = new ServerMonsterBattle(battle.Id, battle.MonsterKey, battle.Round, battle.Turn,
                ulong.Parse(battle.Seed, CultureInfo.InvariantCulture), Decode(battle.OpponentSave, battle.MonsterKey, true).Match,
                battle.Result, battle.SourceEncounterKey);
        }
        else if (saved.Phase is ServerMatchPhase.MonsterPreparation or ServerMatchPhase.MonsterResult)
            throw new InvalidOperationException("Missing monster battle state.");
        if (saved.Event is { } visit)
        {
            if (saved.Phase != ServerMatchPhase.Event || saved.Shop is not null || saved.Battle is not null || saved.Pvp is not null
                || saved.Choices.Length != 0 || visit.Id == Guid.Empty || visit.Round != saved.Round
                || visit.Turn is < 1 or > 7 || saved.Turn != visit.Turn + 1 || visit.Level is < 1 or > 5
                || !definitions.Encounters.TryGetValue(visit.EncounterKey, out var definition) || definition is not ChoiceEncounterDefinition encounter
                || visit.Options.Length != encounter.OptionSlots.Count || visit.Resolved != (visit.Result is not null))
                throw new InvalidOperationException("Invalid event state.");
            for (var index = 0; index < visit.Options.Length; index++)
                if (!encounter.OptionSlots[index].Candidates.Any(candidate => candidate.OptionKey == visit.Options[index]))
                    throw new InvalidOperationException("Event option is outside its issued slot.");
            if (visit.Result is { } outcome && !visit.Options.Contains(outcome.OptionKey))
                throw new InvalidOperationException("Resolved event option was not offered.");
            var options = new EncounterOptionSet(encounter, visit.Options.Select(key => encounter.Options.Single(option => option.Key == key)).ToArray(),
                visit.Level, visit.Round);
            if (visit.Resolved) options.MarkResolved();
            run.Event = new ServerEncounterVisit(visit.Id, visit.EncounterKey, visit.Round, visit.Turn, options, visit.Result);
        }
        else if (saved.Phase == ServerMatchPhase.Event)
            throw new InvalidOperationException("Missing event state.");
        if (saved.Pvp is { } pvp)
        {
            if (pvp.Id == Guid.Empty || pvp.Round < 1 || saved.Round != pvp.Round + 1 || saved.Turn != 1
                || saved.Shop is not null || saved.Battle is not null || saved.Event is not null || saved.Choices.Length != 0
                || saved.Phase is not (ServerMatchPhase.PvpPending or ServerMatchPhase.PvpResult)
                || (saved.Phase == ServerMatchPhase.PvpPending) != (pvp.Result is null)
                || (saved.Phase == ServerMatchPhase.PvpPending && saved.Status != MatchStatus.InProgress)
                || pvp.IsTestOpponent != (pvp.OpponentSnapshotId is null)
                || pvp.OpponentSnapshotId == Guid.Empty
                || (pvp.Result is { } result && (!Enum.IsDefined(result.Outcome) || !Enum.IsDefined(result.EndReason)
                    || result.EndedAt < 0 || result.EndedAt > 600 || result.PlayerRemainingHealth < 0 || result.OpponentRemainingHealth < 0)))
                throw new InvalidOperationException("Invalid PvP state.");
            var opponent = Decode(pvp.OpponentSave, null, true).Match;
            if (opponent.Id == saved.MatchId || opponent.Progress.Round != pvp.Round || opponent.Progress.Turn != 8)
                throw new InvalidOperationException("Invalid PvP opponent snapshot.");
            run.Pvp = new ServerPvpBattle(pvp.Id, pvp.Round, ulong.Parse(pvp.Seed, CultureInfo.InvariantCulture), opponent,
                pvp.OpponentSnapshotId, pvp.IsTestOpponent, pvp.Result);
        }
        else if (saved.Phase is ServerMatchPhase.PvpPending or ServerMatchPhase.PvpResult)
            throw new InvalidOperationException("Missing PvP state.");
        return run;
    }

    private static AttributeData Capture(ModifiableAttributeSet attributes) => new(
        attributes.CaptureBaseValues().OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal).Select(pair => new BaseData(pair.Key, pair.Value)).ToArray(),
        attributes.CaptureModifiers().ToArray());

    private static ModifiableAttributeSet Restore(AttributeData data)
    {
        var result = new ModifiableAttributeSet();
        RestoreInto(result, data);
        return result;
    }

    private static void RestoreInto(ModifiableAttributeSet target, AttributeData data)
    {
        var bases = new ModifiableAttributeSet(data.Bases.ToDictionary(item => item.Key, item => item.Value));
        target.ReplaceBaseValues(bases);
        foreach (var modifier in data.Modifiers) target.ApplyModifier(modifier);
    }

    private static void AddId(HashSet<Guid> ids, Guid id)
    {
        if (id == Guid.Empty || !ids.Add(id)) throw new InvalidOperationException("Invalid or duplicate entity ID.");
    }

    // 显式 StringName 文本编码，避免保存引擎原生句柄。
    private sealed class KeyConverter : JsonConverter<StringName>
    {
        public override StringName Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) =>
            new(reader.GetString() ?? throw new JsonException("Key is required."));
        public override void Write(Utf8JsonWriter writer, StringName value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString());
    }

    // 存档头与聚合状态；内容与能力通过固定规则版本的 Definition 重建。
    private sealed record Save(int FormatVersion, string RulesVersion, Guid MatchId, string Seed, string RandomState,
        int BoardCapacity, int Round, int Turn, int PvpWins, int IncomeSettledThroughRound, int ExperienceSettledThroughTurn,
        MatchStatus Status, MatchSummary? Summary, AttributeData Resources, HeroData Hero, CardData[] Cards, SkillData[] Skills,
        PlacementData[] Placements, StringName[] SeenEncounters, EncounterChoice[] Choices, PendingMonsterReward[] PendingRewards,
        int PendingBattleMaxHealthBonus, long GemShopRevision, AppliedCardSetModifier[] SetModifiers,
        AppliedCardQuestModifier[] QuestModifiers, ServerMatchPhase Phase, StringName? ShopKey, ShopData? Shop, BattleData? Battle, EventData? Event, PvpData? Pvp);
    // 待执行及已完成PvP的固定对手／种子；双方原始输入另由数据库任务不可变保存。
    private sealed record PvpData(Guid Id, int Round, string Seed, string OpponentSave, Guid? OpponentSnapshotId,
        bool IsTestOpponent, ServerBattleSummary? Result);
    // 对手完整实例存档保留身份、布局、成长与随机状态，读取不重新生成怪物。
    private sealed record BattleData(Guid Id, StringName MonsterKey, int Round, int Turn, string Seed,
        string OpponentSave, ServerBattleSummary? Result, StringName? SourceEncounterKey);
    // 只保存已展示选项key、访问等级／边界、完成标记与小型结果，不重抽展示槽。
    private sealed record EventData(Guid Id, StringName EncounterKey, int Round, int Turn, int Level,
        StringName[] Options, bool Resolved, ServerEncounterOutcome? Result);
    // 基础属性与每条贡献分开保存。
    private sealed record AttributeData(BaseData[] Bases, StatModifier[] Modifiers);
    // 单个基础属性。
    private sealed record BaseData(StringName Key, int Value);
    // 英雄实例身份与成长。
    private sealed record HeroData(Guid Id, StringName Key, AttributeData Persistent, AttributeData Combat);
    // 卡牌实例及非派生状态。
    private sealed record CardData(Guid Id, StringName Key, int Level, AttributeData Persistent, AttributeData Combat,
        decimal CooldownMultiplier, QuestData[] Quests, StringName?[] Gems);
    // 技能实例及成长。
    private sealed record SkillData(Guid Id, StringName Key, int Level, AttributeData Persistent, AttributeData Combat);
    // 单个任务的持久进度；身份奖励恢复时由同版本任务定义重建。
    private sealed record QuestData(StringName Key, int Progress);
    // 棋盘位置及可推挤状态。
    private sealed record PlacementData(BoardZone Zone, BoardPlacement Placement);
    // 商品与刷新池必须保存，不能在恢复时重抽。
    private sealed record ShopData(int Level, int RefreshCost, bool Refreshed, StringName[] EligibleCards, OfferData[] Offers);
    // 单件已定价商品及售罄状态。
    private sealed record OfferData(StringName Key, int Level, int Price, bool Sold);
}
