using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Economy;
using Project_Star.Application.Encounters;
using Project_Star.Application.Match;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Playtest;

/// <summary>Owns UI flow and calls application use cases; never owns controls or renders game state.</summary>
public sealed class MatchPresenter
{
    private readonly DefinitionRegistry _registry;
    private readonly BoardService _board;
    private readonly CardEconomyService _economy;
    private readonly ShopCardPoolService _shops;
    private readonly ResolveEncounterOptionService _events;
    private readonly MonsterRewardClaimService _rewards;
    private readonly GameCoordinator _game;
    private MatchSession? _player;
    private MatchSession? _enemy;
    private ShopStock? _stock;
    private EncounterOptionSet? _options;
    private MatchBattleKind _battleKind;
    private int _battleRound;
    private MatchPage _page;
    private string _title = "选择英雄";
    private string _message = "选择英雄后，对局会从第 1 轮第 1 回合正式开始。";
    private bool _eventCompleted;
    private bool _executing;
    private long _shopRevision;
    private long _eventRevision;
    private EntityId? _selected;

    public MatchPresenter(DefinitionRegistry registry, BoardService board, CardEconomyService economy,
        ShopCardPoolService shops, ResolveEncounterOptionService events,
        MonsterRewardClaimService rewards, GameCoordinator game)
    {
        _registry = registry; _board = board; _economy = economy;
        _shops = shops; _events = events; _rewards = rewards; _game = game;
    }

    public event Action<MatchPageViewModel>? ViewChanged;
    public MatchPageViewModel View { get; private set; } = null!;

    private MatchSnapshot Snapshot(MatchSession session) => MatchDisplayQuery.Capture(session, _registry.Sets);

    public void Reset() => Execute(() =>
    {
        _player = null; _enemy = null; _stock = null; _options = null; _selected = null;
        _shopRevision++; _eventRevision++; _eventCompleted = false;
        _page = MatchPage.HeroSelection; _title = "选择英雄";
        _message = _registry.Heroes.Count == 0 ? "尚未配置正式英雄内容。"
            : "选择英雄后，对局会从第 1 轮第 1 回合正式开始。";
    });

    public void SelectHero(StringName key) => Execute(() =>
    {
        if (_page != MatchPage.HeroSelection || !_registry.Heroes.TryGetValue(key, out var hero)) return;
        _player = _game.CreateMatch(42,
            999 - hero.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Income), hero);
        GenerateChoices();
    });

    private void GenerateChoices()
    {
        if (_player is null) return;
        _enemy = null; _stock = null; _options = null; _selected = null;
        _shopRevision++; _eventRevision++;
        _page = MatchPage.EncounterChoice;
        var result = _game.GenerateEncounterChoices(_player);
        var snapshot = Snapshot(_player);
        _title = $"第 {snapshot.Round} 轮 · 第 {snapshot.Turn} 回合";
        _message = result.IsFailure ? result.Failure!.Message : "选择本回合要进入的遭遇。";
    }

    public void ChooseEncounter(StringName key) => Execute(() =>
    {
        if (_player is null || _page != MatchPage.EncounterChoice) return;
        var snapshot = Snapshot(_player);
        var choice = snapshot.EncounterChoices.FirstOrDefault(item => item.Key == key);
        if (choice is null) return;
        var selected = _game.SelectEncounter(_player, key);
        if (selected.IsFailure) { _message = selected.Failure!.Message; return; }
        _selected = null; _battleRound = snapshot.Round;
        _title = choice.DisplayName;
        switch (choice.Kind)
        {
            case EncounterKind.Shop:
                _page = MatchPage.Shop;
                _stock = _registry.Encounters[key] is ShopEncounterDefinition shop
                    ? _shops.CreateStock(_player, shop, _registry.Cards.Values) : null;
                _shopRevision++;
                _message = _stock is null || _stock.Offers.Count == 0 ? "尚未配置正式卡牌内容。"
                    : "每件商品只能购买一次；购买价与持有价值分别显示。";
                break;
            case EncounterKind.Monster:
            case EncounterKind.Pvp:
                _page = MatchPage.Preparation;
                var opponents = new LocalTestOpponentProvider(_registry);
                _enemy = choice.Kind == EncounterKind.Monster
                    ? opponents.CreateMonsterOpponent(_player, _registry.Monsters[key])
                    : opponents.CreateOpponent(_player);
                _battleKind = choice.Kind == EncounterKind.Pvp ? MatchBattleKind.Pvp : MatchBattleKind.Monster;
                _message = "可以调整双棋盘，然后开始战斗；空战场也允许开始。";
                break;
            default:
                _page = MatchPage.Event;
                _options = _registry.Encounters[key] is ChoiceEncounterDefinition encounter
                    ? _events.CreateOptionSet(_player, encounter) : null;
                _eventRevision++; _eventCompleted = _options is null;
                _message = _eventCompleted ? "本次事件没有额外奖励。" : "选择一项。";
                break;
        }
    });

    public void BuyCard(int index, long revision) => Execute(() =>
    {
        if (_player is null || _page != MatchPage.Shop || _stock is null
            || revision != _shopRevision || index < 0 || index >= _stock.Offers.Count) return;
        var offer = _stock.Offers[index];
        var merge = _economy.FindMergeTarget(_player, offer.Definition, offer.Level);
        var target = merge is null ? _board.FindFirstAvailableTarget(_player,
            offer.Definition.Attributes.Identity.OccupiedSlots) : null;
        if (merge is null && target is null)
        { _message = "战场区和备战区都没有足够空间，无法购买。"; return; }
        var result = _economy.BuyCard(_player, offer);
        if (result.IsFailure) { _message = result.Failure!.Message; return; }
        // P4 moves this existing orchestration into the complete economy transaction.
        if (result.Value!.WasCreated)
        {
            var placement = _board.PlaceCard(_player, result.Value.Card.Id, target!.Zone, target.Start);
            if (placement.IsFailure) { _message = placement.Failure!.Message; return; }
        }
        _message = result.Value.WasUpgraded ? $"已合并升级至 {result.Value.CurrentLevel} 级。" : "购买成功，卡牌已放入棋盘。";
    });

    public void RefreshShop() => Execute(() =>
    {
        if (_player is null || _page != MatchPage.Shop || _stock is null) return;
        var result = _shops.Refresh(_player, _stock);
        if (result.IsSuccess) _shopRevision++;
        _message = result.IsSuccess ? $"商店刷新成功，花费 {_stock.RefreshCost} 金钱。" : result.Failure!.Message;
    });

    public void ResolveEventOption(StringName key, long revision) => Execute(() =>
    {
        if (_player is null || _page != MatchPage.Event || _eventCompleted
            || _options is null || revision != _eventRevision) return;
        var result = _events.Resolve(_player, _options, key);
        if (result.IsFailure) { _message = result.Failure!.Message; return; }
        _eventCompleted = true;
        _message = PlaytestText.FormatEventResult(result.Value!);
    });

    public void OnBoardSlot(BoardZone zone, int slot) => Execute(() =>
    {
        if (_player is null || _page is MatchPage.HeroSelection or MatchPage.MatchEnded) return;
        var snapshot = Snapshot(_player);
        if (_selected is null)
        {
            _selected = snapshot.BoardPlacements.FirstOrDefault(item => item.Zone == zone
                && item.Start <= slot && slot < item.EndExclusive)?.CardId;
            _message = _selected is null ? "请选择棋盘卡牌。" : "已选中卡牌，请点击目标格移动。";
            return;
        }
        var result = _board.PlaceCard(_player, _selected.Value, zone, slot);
        _message = result.IsSuccess ? $"移动成功，推挤 {result.Value!.AffectedCards} 张卡牌。" : result.Failure!.Message;
        if (result.IsSuccess) _selected = null;
    });

    public void StartBattle() => Execute(() =>
    {
        if (_player is null || _enemy is null || _page != MatchPage.Preparation) return;
        var before = Snapshot(_player);
        var result = _game.ResolveBattle(_player, _enemy, _battleKind, _battleRound);
        if (result.IsFailure) { _message = result.Failure!.Message; return; }
        var after = Snapshot(_player);
        _page = after.Status == MatchStatus.InProgress ? MatchPage.BattleResult : MatchPage.MatchEnded;
        _title = after.Status == MatchStatus.InProgress ? "战斗结算"
            : after.Status == MatchStatus.Won ? "对局胜利" : "对局失败";
        _selected = null;
        _message = PlaytestText.FormatBattleLog(result.Value!);
        if (_battleKind == MatchBattleKind.Monster)
            _message += $"\n金钱 +{after.Wealth - before.Wealth}，经验 +{after.Experience - before.Experience}。";
    });

    public void ClaimMonsterReward() => Execute(() =>
    {
        if (_player is null || _page == MatchPage.HeroSelection) return;
        var result = _rewards.ClaimFirst(_player);
        _message = result.IsFailure ? result.Failure!.Message
            : $"已领取 {result.Value!.Reward.DisplayName}（{result.Value.CurrentLevel}级）。";
    });

    public void ContinueMatch()
    {
        if (_page == MatchPage.MatchEnded) { Reset(); return; }
        Execute(() =>
        {
            if (_page == MatchPage.Shop || _page == MatchPage.BattleResult
                || _page == MatchPage.Event && _eventCompleted) GenerateChoices();
        });
    }

    private void Execute(Action command)
    {
        if (_executing) return;
        _executing = true;
        try { command(); }
        finally
        {
            try { RefreshView(); }
            finally { _executing = false; }
        }
    }

    /// <summary>Captures a complete page without generating content, spending resources, or settling battles.</summary>
    public void RefreshView()
    {
        var player = _player is null ? null : Snapshot(_player);
        var enemy = _enemy is null ? null : Snapshot(_enemy);
        if (_selected is not null && (player is null || !player.Cards.Any(card => card.Id == _selected))) _selected = null;
        var heroes = _registry.Heroes.Values.OrderBy(hero => hero.Attributes.Identity.Key.ToString(), StringComparer.Ordinal)
            .Select(hero => new KeyedAction(hero.Attributes.Identity.Key, new UiAction($"选择：{hero.Attributes.Identity.DisplayName}"))).ToArray();
        var choices = _page != MatchPage.EncounterChoice || player is null ? Array.Empty<KeyedAction>()
            : player.EncounterChoices.Select(choice => new KeyedAction(choice.Key,
                new UiAction($"{choice.DisplayName}\n{PlaytestText.KindName(choice.Kind)}"))).ToArray();
        var offers = new List<ShopItemViewModel>();
        if (_page == MatchPage.Shop && _stock is not null && _player is not null)
            for (var index = 0; index < _stock.Offers.Count; index++)
            {
                var offer = _stock.Offers[index];
                var (merge, reason) = MatchDisplayQuery.PurchaseCondition(_player, offer, _economy, _board);
                offers.Add(new ShopItemViewModel(index, _shopRevision,
                    new UiAction($"购买价 {offer.Price}{(!merge ? "" : " · 可合并")}", !offer.IsSold, reason.Length == 0, reason),
                    MatchDisplayQuery.FromOffer(offer)));
            }
        var options = _page != MatchPage.Event || _eventCompleted || _options is null ? Array.Empty<KeyedAction>()
            : _options.Options.Select(option => new KeyedAction(option.Key, new UiAction(option.DisplayName))).ToArray();
        var canContinue = _page is MatchPage.Shop or MatchPage.BattleResult or MatchPage.MatchEnded
            || _page == MatchPage.Event && _eventCompleted;
        var refreshReason = _stock is null || !_stock.CanRefresh ? "本次商店不可刷新"
            : player!.Wealth < _stock.RefreshCost ? "金钱不足" : "";
        View = new MatchPageViewModel(_page, _title, _message, player, enemy, _selected,
            player is not null && _page != MatchPage.MatchEnded,
            enemy is not null && _page is MatchPage.Preparation or MatchPage.BattleResult or MatchPage.MatchEnded,
            Array.AsReadOnly(heroes), Array.AsReadOnly(choices), offers.AsReadOnly(), Array.AsReadOnly(options), _eventRevision,
            new UiAction($"刷新（{_stock?.RefreshCost ?? 0}）", _page == MatchPage.Shop && _stock is not null,
                refreshReason.Length == 0, refreshReason),
            new UiAction("开始战斗", _page == MatchPage.Preparation),
            new UiAction(_page == MatchPage.Shop ? "离开商店" : _page == MatchPage.MatchEnded ? "返回英雄选择"
                : _page == MatchPage.Event ? "继续旅程" : "进入下一回合", canContinue),
            new UiAction(player?.PendingMonsterRewards.Count > 0
                ? $"领取战利品：{player.PendingMonsterRewards[0].DisplayName}" : "领取战利品",
                player?.PendingMonsterRewards.Count > 0));
        ViewChanged?.Invoke(View);
    }
}
