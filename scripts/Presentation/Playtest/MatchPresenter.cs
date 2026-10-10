using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Economy;
using Project_Star.Application.Encounters;
using Project_Star.Application.Match;
using Project_Star.Application.Mentors;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

/// <summary>Owns UI flow and calls application use cases; never owns controls or renders game state.</summary>
public sealed class MatchPresenter
{
    private readonly IDefinitionCatalog _registry;
    private readonly BoardService _board;
    private readonly CardEconomyService _economy;
    private readonly ShopCardPoolService _shops;
    private readonly ResolveEncounterOptionService _events;
    private readonly MentorService _mentors;
    private readonly MonsterRewardClaimService _rewards;
    private readonly GameCoordinator _game;
    private readonly IOpponentProvider _opponents;
    private MatchSession? _player;
    private MatchSession? _enemy;
    private ShopStock? _stock;
    private EncounterOptionSet? _options;
    private MentorVisit? _mentorVisit;
    private MatchBattleKind _battleKind;
    private int _battleRound;
    private int _encounterTurn;
    private MatchPage _page;
    private string _title = "选择英雄";
    private string _message = "选择英雄后，对局会从第 1 轮第 1 回合正式开始。";
    private bool _eventCompleted;
    private bool _executing;
    private long _shopRevision;
    private int _shopLevel;
    private StringName _encounterIllustration = new("");
    private int _encounterLevel;
    private long _eventRevision;
    private EntityId? _selected;
    private long _rewardRevision;
    private string _battleLog = "";
    private BattlePlaybackPresenter? _playback;

    public MatchPresenter(IDefinitionCatalog registry, BoardService board, CardEconomyService economy,
        ShopCardPoolService shops, ResolveEncounterOptionService events, MentorService mentors,
        MonsterRewardClaimService rewards, GameCoordinator game, IOpponentProvider opponents)
    {
        _registry = registry; _board = board; _economy = economy;
        _shops = shops; _events = events; _rewards = rewards; _game = game;
        _mentors = mentors;
        _opponents = opponents ?? throw new ArgumentNullException(nameof(opponents));
    }

    public event Action<MatchPageViewModel>? ViewChanged;
    public MatchPageViewModel View { get; private set; } = null!;

    private MatchSnapshot Snapshot(MatchSession session) => MatchDisplayQuery.Capture(session, _registry.Sets);

    public void Reset() => Execute(ResetFlow);

    private void ResetFlow()
    {
        _player = null; _enemy = null; _stock = null; _options = null; _selected = null;
        _mentorVisit = null;
        _playback = null;
        _shopRevision++; _eventRevision++; _rewardRevision++; _eventCompleted = false; _battleLog = "";
        _encounterIllustration = new StringName("");
        _encounterLevel = 0;
        _page = MatchPage.HeroSelection; _title = "选择英雄";
        _message = _registry.Heroes.Count == 0 ? "尚未配置正式英雄内容。"
            : "选择英雄后，对局会从第 1 轮第 1 回合正式开始。";
    }

    // 确认绑定打开弹窗时的对局，旧确认不能放弃新局。
    public void AbandonMatch(Guid matchId) => Execute(() =>
    {
        if (_player is null || _player.Id != matchId || _player.Status != MatchStatus.InProgress) return;
        var result = _game.AbandonMatch(_player);
        if (result.IsFailure) { _message = result.Failure!.Message; return; }
        ResetFlow(); _message = "已放弃对局。选择英雄开始新的旅程。";
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
        _mentorVisit = null;
        _playback = null;
        _shopRevision++; _eventRevision++; _battleLog = "";
        _page = MatchPage.EncounterChoice;
        _encounterIllustration = new StringName("");
        _encounterLevel = 0;
        var result = _game.GenerateEncounterChoices(_player);
        var snapshot = Snapshot(_player);
        _title = "选择遭遇";
        _message = result.IsFailure ? result.Failure!.Message : "选择一处前往";
    }

    public void ChooseEncounter(StringName key) => Execute(() =>
    {
        if (_player is null || _page != MatchPage.EncounterChoice) return;
        var snapshot = Snapshot(_player);
        var choice = snapshot.EncounterChoices.FirstOrDefault(item => item.Key == key);
        if (choice is null) return;
        var selected = _game.SelectEncounter(_player, key);
        if (selected.IsFailure) { _message = selected.Failure!.Message; return; }
        _selected = null; _battleRound = snapshot.Round; _encounterTurn = snapshot.Turn;
        _title = choice.DisplayName;
        _shopLevel = choice.ShopLevel;
        _encounterIllustration = choice.Illustration;
        _encounterLevel = choice.Level;
        switch (choice.Kind)
        {
            case EncounterKind.Shop:
                _page = MatchPage.Shop;
                _stock = _registry.Encounters[key] is ShopEncounterDefinition shop
                    ? _shops.CreateStock(_player, shop, _registry.Cards.Values, choice.ShopLevel) : null;
                _shopRevision++;
                _message = _stock is null || _stock.Offers.Count == 0 ? "尚未配置正式卡牌内容。"
                    : "选择商品购买";
                break;
            case EncounterKind.Monster:
            case EncounterKind.Pvp:
                _page = MatchPage.Preparation;
                _enemy = choice.Kind == EncounterKind.Monster
                    ? _opponents.CreateMonsterOpponent(_player.Random.State, _registry.Monsters[key])
                    : _opponents.CreateOpponent(_player.Random.State);
                _battleKind = choice.Kind == EncounterKind.Pvp ? MatchBattleKind.Pvp : MatchBattleKind.Monster;
                _message = "可以调整双棋盘，然后开始战斗；空战场也允许开始。";
                break;
            default:
                _page = MatchPage.Event;
                if (_registry.Encounters[key] is MentorEncounterDefinition mentorEncounter)
                {
                    var opened = _mentors.Open(_player, mentorEncounter.MentorKey, choice.Level);
                    _mentorVisit = opened.IsSuccess ? opened.Value : null;
                    _eventRevision++; _eventCompleted = _mentorVisit is null || _mentorVisit.IsResolved;
                    _message = opened.IsFailure ? opened.Failure!.Message
                        : _eventCompleted ? "导师目前没有可传授的技能。" : "选择一个技能。";
                    break;
                }
                _options = _registry.Encounters[key] is ChoiceEncounterDefinition encounter
                    ? _events.CreateOptionSet(_player, encounter, choice.Level) : null;
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
        var result = _economy.BuyAndPlace(_player, offer);
        if (result.IsFailure) { _message = result.Failure!.Message; return; }
        _message = result.Value!.WasUpgraded ? $"已合并升级至 {result.Value.CurrentLevel} 级。" : "购买成功，卡牌已放入棋盘。";
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
            || revision != _eventRevision) return;
        if (_mentorVisit is not null)
        {
            var acquired = _mentors.ChooseSkill(_player, _mentorVisit, key);
            if (acquired.IsFailure) { _message = acquired.Failure!.Message; return; }
            _eventCompleted = true;
            _message = $"已获得 {acquired.Value!.Skill.Attributes.Identity.DisplayName}（{acquired.Value.CurrentLevel}级）。";
            return;
        }
        if (_options is null) return;
        var result = _events.Resolve(_player, _options, key);
        if (result.IsFailure) { _message = result.Failure!.Message; return; }
        _eventCompleted = true;
        if (result.Value!.MonsterEncounter is { } monster)
        {
            _enemy = _opponents.CreateMonsterOpponent(_player.Random.State, _registry.Monsters[monster.Key]);
            _battleKind = MatchBattleKind.Monster;
            _encounterLevel = monster.Level; _encounterIllustration = monster.Illustration; _title = monster.DisplayName;
            _page = MatchPage.Preparation;
            _message = "遇见怪物，可以调整双棋盘后开始战斗。";
            return;
        }
        _message = $"{_options.Options.First(option => option.Key == key).DisplayName}：{PlaytestText.FormatEventResult(result.Value!)}";
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

    // 拖拽期间只查询当前棋盘，不刷新页面或改变选择。
    public Project_Star.Application.Common.Result<BoardPlacementResult> PreviewMove(Guid matchId, EntityId cardId, BoardZone zone, int start)
    {
        if (_player is null || _player.Id != matchId || !View.BoardEnabled)
            return Project_Star.Application.Common.Result<BoardPlacementResult>.Fail(
                new Project_Star.Application.Common.Failure(new StringName("ui.invalid_drag"), "本次拖拽已失效。"));
        return _board.PreviewPlaceCard(_player, cardId, zone, start);
    }

    // 松开时重新校验当前对局，失败保留原位置和选择。
    public void MoveCard(Guid matchId, EntityId cardId, BoardZone zone, int start) => Execute(() =>
    {
        if (_player is null || _player.Id != matchId || !View.BoardEnabled) return;
        var result = _board.PlaceCard(_player, cardId, zone, start);
        _message = result.IsSuccess ? $"移动成功，推挤 {result.Value!.AffectedCards} 张卡牌。" : result.Failure!.Message;
        if (result.IsSuccess) _selected = null;
    });

    // Escape 只取消界面选择，不修改棋盘。
    public void CancelSelection()
    {
        if (_selected is not null) Execute(() => _selected = null);
    }

    public void StartBattle() => Execute(() =>
    {
        if (_player is null || _enemy is null || _page != MatchPage.Preparation) return;
        var before = Snapshot(_player);
        var result = _game.ResolveBattleForPlayback(_player, _enemy, _battleKind, _battleRound);
        if (result.IsFailure) { _message = result.Failure!.Message; return; }
        var after = Snapshot(_player);
        _page = after.Status == MatchStatus.InProgress ? MatchPage.BattleResult : MatchPage.MatchEnded;
        _title = after.Status == MatchStatus.InProgress ? "战斗结算"
            : after.Status == MatchStatus.Won ? "对局胜利" : "对局失败";
        _selected = null;
        _battleLog = PlaytestText.FormatBattleLog(result.Value!.Result);
        _rewardRevision++;
        _message = _battleLog.Split('\n').Last();
        if (_battleKind == MatchBattleKind.Monster)
            _message += $"\n金钱 +{after.Wealth - before.Wealth}，经验 +{after.Experience - before.Experience + 10L * ((after.Hero?.Level ?? 0) - (before.Hero?.Level ?? 0))}。";
        _playback = new BattlePlaybackPresenter(result.Value);
        _page = MatchPage.BattlePlayback;
    });

    // 帧更新仅消费冻结回放；不重新调用战斗或结算用例。
    public void AdvancePlayback(double seconds)
    {
        if (_page != MatchPage.BattlePlayback || _playback is null || !_playback.Advance(seconds)) return;
        CompletePlayback(); RefreshView();
    }

    // 播放控制不接触对局状态。
    public void PausePlayback() => Execute(() => _playback?.TogglePause());
    public void SpeedPlayback() => Execute(() => _playback?.ToggleSpeed());
    public void SkipPlayback() => Execute(() => { _playback?.Skip(); CompletePlayback(); });

    private void CompletePlayback()
    {
        if (_page == MatchPage.BattlePlayback && _playback?.Completed == true)
            _page = _playback.Source.PlayerAfter.Status == MatchStatus.InProgress ? MatchPage.BattleResult : MatchPage.MatchEnded;
    }

    public void ClaimMonsterReward() => Execute(() =>
    {
        if (_player is null || _page is MatchPage.HeroSelection or MatchPage.BattlePlayback) return;
        var result = _rewards.ClaimFirst(_player);
        if (result.IsSuccess) _rewardRevision++;
        _message = result.IsFailure ? result.Failure!.Message
            : $"已领取 {result.Value!.Reward.DisplayName}（{result.Value.CurrentLevel}级）。";
    });

    // 浮层只提交当前捕获的奖励版本，旧列表不能误领新的条目。
    public void ClaimReward(Guid matchId, int index, long revision) => Execute(() =>
    {
        if (_player is null || _page == MatchPage.BattlePlayback || _player.Id != matchId || revision != _rewardRevision) return;
        var result = _rewards.Claim(_player, index);
        if (result.IsSuccess) _rewardRevision++;
        _message = result.IsFailure ? result.Failure!.Message
            : $"已领取 {result.Value!.Reward.DisplayName}（{result.Value.CurrentLevel}级）。";
    });

    // 出售确认带上卡牌、等级与金额，刷新后过期的确认不能出售其他卡牌。
    public void SellCard(Guid matchId, EntityId cardId, int level, int value) =>
        Execute(() => SellCard(matchId, cardId, level, value, requireSelection: true));

    // 拖入出售区无需预先点击选中，松开时仍核对身份、等级与金额。
    public void SellDraggedCard(Guid matchId, EntityId cardId, int level, int value) =>
        Execute(() => SellCard(matchId, cardId, level, value, requireSelection: false));

    public Project_Star.Application.Common.Result<int> PreviewSale(Guid matchId, EntityId cardId)
    {
        if (_player is null || _player.Id != matchId || !View.BoardEnabled)
            return Project_Star.Application.Common.Result<int>.Fail(
                new Project_Star.Application.Common.Failure(new StringName("ui.invalid_sale"), "本次出售拖拽已失效。"));
        return _economy.CheckSale(_player, cardId);
    }

    private void SellCard(Guid matchId, EntityId cardId, int level, int value, bool requireSelection)
    {
        if (_player is null || _player.Id != matchId || !View.BoardEnabled || requireSelection && _selected != cardId) return;
        var card = Snapshot(_player).Cards.FirstOrDefault(item => item.Id == cardId);
        if (card is null || card.Level != level || card.Value != value) { _message = "卡牌信息已变化，请重新确认出售。"; return; }
        var result = _economy.SellFromBoard(_player, cardId);
        if (result.IsFailure) { _message = result.Failure!.Message; return; }
        _selected = null; _message = $"已出售 {card.DisplayName}，金钱 +{result.Value}。";
    }

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
        var playing = _page == MatchPage.BattlePlayback && _playback is not null;
        if (playing) { player = _playback!.Project(SideId.Player); enemy = _playback.Project(SideId.Opponent); }
        if (_selected is not null && (player is null || !player.Cards.Any(card => card.Id == _selected))) _selected = null;
        var heroes = _registry.Heroes.Values.OrderBy(hero => hero.Attributes.Identity.Key.ToString(), StringComparer.Ordinal)
            .Select(hero => new KeyedAction(hero.Attributes.Identity.Key, new UiAction($"选择：{PlaytestText.FormatHeroName(hero.Attributes.Identity.DisplayName, hero.Attributes.Identity.Title)}"))
                { Illustration = hero.Attributes.Identity.Illustration, Hero = HeroSelectionDetails.From(hero) }).ToArray();
        var choices = _page != MatchPage.EncounterChoice || player is null ? Array.Empty<KeyedAction>()
            : player.EncounterChoices.Select(choice => new KeyedAction(choice.Key,
                new UiAction(choice.DisplayName))
                { Illustration = choice.Illustration, ShopLevel = choice.ShopLevel, Level = choice.Level,
                    Subtitle = choice.Summary.Length > 0 ? choice.Summary : PlaytestText.KindName(choice.Kind) }).ToArray();
        var offers = new List<ShopItemViewModel>();
        if (_page == MatchPage.Shop && _stock is not null && _player is not null)
            for (var index = 0; index < _stock.Offers.Count; index++)
            {
                var offer = _stock.Offers[index];
                var (merge, reason) = MatchDisplayQuery.PurchaseCondition(_player, offer, _economy, _board);
                offers.Add(new ShopItemViewModel(index, _shopRevision,
                    new UiAction(offer.IsSold ? "已售罄" : $"购买{(!merge ? "" : " · 可合并")}", true, reason.Length == 0, reason),
                    MatchDisplayQuery.FromOffer(offer)) { Price = offer.Price, MergeLevel = merge ? offer.Level + 1 : 0 });
            }
        var options = _page != MatchPage.Event || _eventCompleted || _options is null ? Array.Empty<KeyedAction>()
            : _options.Options.Select(option => new KeyedAction(option.Key, new UiAction(option.DisplayName))
                { Subtitle = PlaytestText.FormatOption(option, player?.Hero?.Level ?? 1, _options.Level) }).ToArray();
        if (_page == MatchPage.Event && !_eventCompleted && player?.MentorVisit is { IsResolved: false } mentorVisit)
            options = mentorVisit.Offers.Select(offer =>
            {
                var skill = MatchDisplayQuery.FromSkill(_registry.Skills[offer.SkillKey], offer.Level);
                return new KeyedAction(offer.SkillKey, new UiAction($"{offer.DisplayName} · {offer.Level}级"))
                    { Level = offer.Level, Subtitle = CardDisplayAdapter.AbilityDetails(skill.Abilities, skill.CurrentValues).Replace("\n", " · ") };
            }).ToArray();
        var canContinue = _page is MatchPage.Shop or MatchPage.BattleResult or MatchPage.MatchEnded
            || _page == MatchPage.Event && _eventCompleted;
        var refreshReason = _stock is null || _player is null ? "本次商店不可刷新"
            : _shops.CheckRefresh(_player, _stock).Failure?.Message ?? "";
        View = new MatchPageViewModel(_page, playing ? "战斗回放" : _title,
            playing ? $"{_playback!.Tick / 10m:0.0}秒" : _message, player, enemy, _selected,
            player is not null && _page is not (MatchPage.MatchEnded or MatchPage.BattlePlayback),
            enemy is not null && _page is MatchPage.Preparation or MatchPage.BattleResult or MatchPage.MatchEnded or MatchPage.BattlePlayback,
            Array.AsReadOnly(heroes), Array.AsReadOnly(choices), offers.AsReadOnly(), Array.AsReadOnly(options), _eventRevision,
            new UiAction($"刷新 · {_stock?.RefreshCost ?? 0} 金币", _page == MatchPage.Shop && _stock is not null,
                refreshReason.Length == 0, refreshReason),
            new UiAction("开始战斗", _page == MatchPage.Preparation),
            new UiAction(_page == MatchPage.Shop ? "离开商店" : _page == MatchPage.MatchEnded ? "返回英雄选择"
                : _page == MatchPage.Event ? "继续旅程" : "进入下一回合", canContinue),
            new UiAction(player?.PendingMonsterRewards.Count > 0
                ? $"领取战利品：{player.PendingMonsterRewards[0].DisplayName}" : "领取战利品",
                player?.PendingMonsterRewards.Count > 0 && !playing))
        {
            BattleLog = _battleLog,
            ShopLevel = _page == MatchPage.Shop ? _shopLevel : 0,
            ContextIllustration = _encounterIllustration,
            EncounterLevel = _encounterLevel,
            DisplayRound = player is null ? 0 : _page == MatchPage.EncounterChoice ? player.Round : _battleRound,
            DisplayTurn = player is null ? 0 : _page == MatchPage.EncounterChoice ? player.Turn : _encounterTurn,
            Playback = playing ? _playback!.Capture() : null,
            Sell = _selected is null || _player is null || _page == MatchPage.MatchEnded ? new UiAction("出售", false)
                : new UiAction("确认出售", true, _economy.CheckSale(_player, _selected.Value).IsSuccess,
                    _economy.CheckSale(_player, _selected.Value).Failure?.Message ?? ""),
            Rewards = player is null ? Array.Empty<RewardItemViewModel>() : Array.AsReadOnly(player.PendingMonsterRewards
                .Select((reward, index) => new RewardItemViewModel(index, _rewardRevision,
                    $"{(reward.Kind == MonsterRewardKind.Card ? "卡牌" : "技能")} · {reward.DisplayName} · {reward.Level}级",
                    reward.Kind == MonsterRewardKind.Card ? MatchDisplayQuery.FromOffer(
                        ShopOffer.Create(_registry.Cards[reward.Key], reward.Level)) : null,
                    reward.Kind == MonsterRewardKind.Skill ? CardDisplayAdapter.SkillDetails(
                        MatchDisplayQuery.FromSkill(_registry.Skills[reward.Key], reward.Level)) : "") { Level = reward.Level })
                .ToArray()),
        };
        ViewChanged?.Invoke(View);
    }
}
