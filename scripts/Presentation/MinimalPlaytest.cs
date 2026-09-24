using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Encounters;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation;

/// <summary>Playable match flow using application services rather than direct model mutation.</summary>
public sealed partial class MinimalPlaytest : Control
{
    private enum Screen { HeroSelection, EncounterChoice, Shop, Event, Preparation, BattleResult }

    private readonly EntityFactory _factory = new();
    private readonly BoardService _board = new(new BoardPlacementSolver());
    private readonly ShopCardPoolService _shopCardPool = new();
    private ResolveEncounterOptionService _encounterOptions = null!;
    private readonly Button[] _choices = new Button[3];
    private DefinitionRegistry _registry = null!;
    private CardEconomyService _economy = null!;
    private MonsterRewardClaimService _monsterRewards = null!;
    private GameCoordinator _game = null!;
    private MatchSession? _player;
    private MatchSession? _enemy;
    private Screen _screen;
    private MatchBattleKind _battleKind;
    private int _battleRound;
    private Label _title = null!;
    private Label _state = null!;
    private RichTextLabel _log = null!;
    private Button _hero = null!;
    private readonly Button[] _buyButtons = new Button[ShopCardPoolService.OfferCount];
    private Button _refreshShop = null!;
    private Button _battleButton = null!;
    private Button _continue = null!;
    private Button _rewardButton = null!;
    private VBoxContainer _eventOptionButtons = null!;
    private Control _battlefieldPanel = null!;
    private Control _benchPanel = null!;
    private Control _enemyBattlefieldPanel = null!;
    private GridContainer _battlefieldGrid = null!;
    private GridContainer _benchGrid = null!;
    private GridContainer _enemyBattlefieldGrid = null!;
    private readonly Button[] _battlefieldSlots = new Button[10];
    private readonly Button[] _benchSlots = new Button[10];
    private readonly Button[] _enemyBattlefieldSlots = new Button[10];
    private EntityId? _selectedCardId;
    private ShopStock? _currentStock;
    private EncounterOptionSet? _currentEncounterOptions;

    public override void _Ready()
    {
        _registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        _economy = new CardEconomyService(_factory, _board, _registry.Cards.Values);
        _monsterRewards = new MonsterRewardClaimService(
            _registry, _economy, new SkillAcquisitionService(_factory), _board);
        _encounterOptions = new ResolveEncounterOptionService(_factory, _board, _registry.Cards.Values);
        _game = new GameCoordinator(
            new CreateMatchService(_factory),
            new EncounterScheduler(_registry, allowIncompleteMonsterChoices: true),
            new StartBattleService(new BattleSetupFactory(), new CombatSimulator()),
            new MatchResultService(_board));
        const string root = "Margin/Frame/Margin/Content";
        _title = GetNode<Label>($"{root}/Header/HeaderPanel/Margin/Title");
        _state = GetNode<Label>($"{root}/State");
        _log = GetNode<RichTextLabel>($"{root}/MainPanel/Margin/MainContent/Log");
        _hero = GetNode<Button>($"{root}/MainPanel/Margin/MainContent/Actions/Create");
        for (var index = 0; index < _buyButtons.Length; index++)
        {
            var captured = index;
            _buyButtons[index] = GetNode<Button>($"{root}/MainPanel/Margin/MainContent/Actions/Buy{index + 1}");
            _buyButtons[index].Pressed += () => BuyCard(captured);
        }
        _refreshShop = GetNode<Button>($"{root}/MainPanel/Margin/MainContent/Actions/Refresh");
        _battleButton = GetNode<Button>($"{root}/MainPanel/Margin/MainContent/Actions/Battle");
        _continue = GetNode<Button>($"{root}/MainPanel/Margin/MainContent/EncounterActions/Generate");
        _rewardButton = new Button { Visible = false };
        GetNode<HBoxContainer>($"{root}/MainPanel/Margin/MainContent/EncounterActions").AddChild(_rewardButton);
        _rewardButton.Pressed += ClaimMonsterReward;
        _eventOptionButtons = GetNode<VBoxContainer>($"{root}/MainPanel/Margin/MainContent/EventOptions");
        _battlefieldPanel = GetNode<Control>($"{root}/BattlefieldPanel");
        _benchPanel = GetNode<Control>($"{root}/BenchPanel");
        _enemyBattlefieldPanel = GetNode<Control>($"{root}/EnemyBattlefieldPanel");
        _battlefieldGrid = GetNode<GridContainer>($"{root}/BattlefieldPanel/Margin/Area/BattlefieldSlots");
        _benchGrid = GetNode<GridContainer>($"{root}/BenchPanel/Margin/Area/BenchSlots");
        _enemyBattlefieldGrid = GetNode<GridContainer>($"{root}/EnemyBattlefieldPanel/Margin/Area/EnemyBattlefieldSlots");
        BuildBoardSlots();
        for (var index = 0; index < _choices.Length; index++)
        {
            var captured = index;
            _choices[index] = GetNode<Button>($"{root}/MainPanel/Margin/MainContent/Choices/Choice{index + 1}");
            _choices[index].Pressed += () => ChooseEncounter(captured);
        }
        _hero.Pressed += SelectHero;
        _refreshShop.Pressed += RefreshShop;
        _battleButton.Pressed += StartBattle;
        _continue.Pressed += ContinueMatch;
        GetNode<Button>($"{root}/Footer/Reset").Pressed += ShowHeroSelection;
        GetNode<Button>($"{root}/Footer/Verification").Pressed +=
            () => GetTree().ChangeSceneToFile("res://Main.tscn");
        ShowHeroSelection();
    }

    private void ShowHeroSelection()
    {
        _player = null; _enemy = null; _screen = Screen.HeroSelection;
        _selectedCardId = null;
        _title.Text = "选择英雄";
        HideAllActions();
        var hero = FirstHero();
        _log.Text = hero is null
            ? "尚未配置正式英雄内容。"
            : "选择英雄后，对局会从第 1 轮第 1 回合正式开始。";
        _hero.Text = hero is null ? "暂无可选英雄" : $"选择：{hero.Attributes.Identity.DisplayName}";
        _hero.Disabled = hero is null;
        _hero.Visible = true;
        UpdateState();
    }

    private void SelectHero()
    {
        var hero = FirstHero();
        if (hero is null) return;
        const int testInitialWealth = 999;
        _player = _game.CreateMatch(
            42,
            testInitialWealth - hero.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Income),
            hero);
        ShowEncounterChoices();
    }

    private HeroDefinition? FirstHero() => _registry.Heroes.Values
        .OrderBy(definition => definition.Attributes.Identity.Key.ToString(), StringComparer.Ordinal)
        .FirstOrDefault();

    private void ShowEncounterChoices()
    {
        if (_player is null) return;
        _enemy = null;
        _screen = Screen.EncounterChoice; HideAllActions();
        var generated = _game.GenerateEncounterChoices(_player);
        var snapshot = _game.GetSnapshot(_player);
        _title.Text = $"第 {snapshot.Round} 轮 · 第 {snapshot.Turn} 回合";
        if (generated.IsFailure) { _log.Text = generated.Failure!.Message; return; }
        _log.Text = "选择本回合要进入的遭遇。";
        for (var index = 0; index < generated.Value!.Count; index++)
        {
            var choice = generated.Value[index];
            _choices[index].Text = $"{choice.DisplayName}\n{KindName(choice.Kind)}";
            _choices[index].Visible = true;
        }
        UpdateState();
    }

    private void ChooseEncounter(int index)
    {
        if (_player is null) return;
        var snapshot = _game.GetSnapshot(_player);
        if (index >= snapshot.EncounterChoices.Count) return;
        var choice = snapshot.EncounterChoices[index];
        _battleRound = snapshot.Round;
        var selected = _game.SelectEncounter(_player, choice.Key);
        if (selected.IsFailure) { _log.Text = selected.Failure!.Message; return; }
        HideAllActions();
        switch (choice.Kind)
        {
            case EncounterKind.Shop: ShowShop(choice); break;
            case EncounterKind.Monster:
            case EncounterKind.Pvp: ShowPreparation(choice); break;
            default: ShowEvent(choice); break;
        }
    }

    private void ShowShop(EncounterChoice choice)
    {
        _screen = Screen.Shop;
        var shop = _registry.Encounters[choice.Key] as ShopEncounterDefinition;
        _title.Text = shop is null ? choice.DisplayName : $"{choice.DisplayName} · {shop.Level}级";
        _currentStock = shop is null || _player is null
            ? null
            : _shopCardPool.CreateStock(_player, shop, _registry.Cards.Values);
        _log.Text = _currentStock is null || _currentStock.Offers.Count == 0
            ? "尚未配置正式卡牌内容。"
            : "本次商店提供以下商品；每件商品只能购买一次。";
        RefreshShopButtons();
        _continue.Text = "离开商店"; _continue.Visible = true;
        UpdateState();
    }

    private void BuyCard(int index)
    {
        if (_player is null) return;
        if (_currentStock is null || index >= _currentStock.Offers.Count) return;
        var offer = _currentStock.Offers[index];
        var mergeTarget = _economy.FindMergeTarget(_player, offer.Definition, offer.Level);
        var target = mergeTarget is null
            ? _board.FindFirstAvailableTarget(_player, offer.Definition.Attributes.Identity.OccupiedSlots)
            : null;
        if (mergeTarget is null && target is null)
        {
            _log.Text = "战场区和备战区都没有足够空间，无法购买。";
            return;
        }

        var result = _economy.BuyCard(_player, offer);
        if (result.IsSuccess)
        {
            var displayName = offer.Definition.Attributes.Identity.DisplayName;
            if (result.Value!.WasCreated)
            {
                var placement = _board.PlaceCard(_player, result.Value.Card.Id, target!.Zone, target.Start);
                if (placement.IsFailure)
                {
                    _log.Text = placement.Failure!.Message;
                    return;
                }
            }

            _buyButtons[index].Visible = false;
            _log.Text = result.Value.WasUpgraded
                ? $"购买成功，{displayName}已合并升级至{result.Value.CurrentLevel}级。"
                : target!.Zone == BoardZone.Battlefield
                    ? $"购买成功，{displayName}已自动放入战场区。"
                    : $"战场区空间不足，{displayName}已自动放入备战区。";
        }
        else _log.Text = result.Failure!.Message;
        RefreshBoard();
        RefreshShopButtons();
        UpdateState();
    }

    private void RefreshShop()
    {
        if (_player is null || _currentStock is null) return;
        var result = _shopCardPool.Refresh(_player, _currentStock);
        _log.Text = result.IsSuccess
            ? $"商店刷新成功，花费 {_currentStock.RefreshCost} 金钱。"
            : result.Failure!.Message;
        RefreshShopButtons();
        UpdateState();
    }

    private void RefreshShopButtons()
    {
        for (var index = 0; index < _buyButtons.Length; index++)
        {
            var button = _buyButtons[index];
            var offer = _currentStock is not null && index < _currentStock.Offers.Count
                ? _currentStock.Offers[index]
                : null;
            button.Visible = offer is not null && !offer.IsSold;
            button.Disabled = offer is null;
            if (offer is not null)
            {
                var card = offer.Definition;
                button.Text = $"{FormatCardFace(card.Attributes.Identity, offer.Level)}\n价格 {offer.Price}";
            }
        }
        _refreshShop.Visible = _currentStock is not null;
        _refreshShop.Disabled = _currentStock is null || !_currentStock.CanRefresh;
        _refreshShop.Text = _currentStock is null
            ? "刷新"
            : _currentStock.CanRefresh
                ? $"刷新一次（{_currentStock.RefreshCost}）"
                : "不可刷新";
    }

    private void ShowEvent(EncounterChoice choice)
    {
        _screen = Screen.Event;
        var definition = _registry.Encounters[choice.Key] as ChoiceEncounterDefinition;
        _title.Text = definition is null ? choice.DisplayName : $"{choice.DisplayName} · {definition.Level}级";
        if (definition is null)
        {
            _log.Text = "你沿着林间道路继续前进。本次事件没有额外奖励。";
            _continue.Text = "继续旅程"; _continue.Visible = true;
            UpdateState();
            return;
        }

        _log.Text = "选择一项训练。";
        _currentEncounterOptions = _encounterOptions.CreateOptionSet(_player!, definition);
        _eventOptionButtons.Visible = true;
        foreach (var option in _currentEncounterOptions.Options)
        {
            var captured = option;
            var button = new Button { Text = option.DisplayName };
            button.Pressed += () => ResolveEventOption(definition, captured);
            _eventOptionButtons.AddChild(button);
        }
        UpdateState();
    }

    private void ResolveEventOption(ChoiceEncounterDefinition encounter, EncounterOptionDefinition option)
    {
        if (_player is null) return;
        if (_currentEncounterOptions is null || _currentEncounterOptions.Encounter != encounter) return;
        var result = _encounterOptions.Resolve(_player, _currentEncounterOptions, option.Key);
        if (result.IsFailure)
        {
            _log.Text = result.Failure!.Message;
            return;
        }

        _eventOptionButtons.Visible = false;
        var changes = new List<string>();
        foreach (var change in result.Value!.Changes)
            changes.Add($"{change.AttributeKey} +{change.Amount}（当前 {change.CurrentValue}）");
        if (result.Value.WealthGained > 0) changes.Add($"金币 +{result.Value.WealthGained}");
        if (result.Value.GrantedCard is not null)
            changes.Add($"获得 {result.Value.GrantedCard.Attributes.Identity.DisplayName}");
        if (result.Value.CardRewardSkipped) changes.Add("双棋盘已满，未生成卡牌");
        _log.Text = $"{option.DisplayName}完成：{string.Join("，", changes)}。";
        _continue.Text = "继续旅程";
        _continue.Visible = true;
        UpdateState();
    }

    private void ShowPreparation(EncounterChoice choice)
    {
        if (_player is null) return;
        _screen = Screen.Preparation; _title.Text = choice.Kind == EncounterKind.Pvp ? "PvP 战斗准备" : $"迎战：{choice.DisplayName}";
        var opponents = new LocalTestOpponentProvider(_registry);
        _enemy = choice.Kind == EncounterKind.Monster
            ? opponents.CreateMonsterOpponent(_player, _registry.Monsters[choice.Key])
            : opponents.CreateOpponent(_player);
        _battleKind = choice.Kind == EncounterKind.Pvp ? MatchBattleKind.Pvp : MatchBattleKind.Monster;
        _log.Text = _game.GetSnapshot(_player).BattlefieldCount == 0
            ? "你没有卡牌。仍可开始战斗，但几乎无法获胜。"
            : "你可以继续调整战场区与备战区，然后开始战斗。";
        _battleButton.Text = "开始战斗"; _battleButton.Visible = true;
        _selectedCardId = null;
        RefreshBoard();
        UpdateState();
    }

    private void BuildBoardSlots()
    {
        for (var index = 0; index < 10; index++)
        {
            var slot = index;
            _battlefieldSlots[index] = new Button { Text = "·", CustomMinimumSize = new Vector2(110, 96) };
            _battlefieldSlots[index].Pressed += () => OnBoardSlot(BoardZone.Battlefield, slot);
            _battlefieldGrid.AddChild(_battlefieldSlots[index]);
            _benchSlots[index] = new Button { Text = "·", CustomMinimumSize = new Vector2(110, 96) };
            _benchSlots[index].Pressed += () => OnBoardSlot(BoardZone.Bench, slot);
            _benchGrid.AddChild(_benchSlots[index]);
            _enemyBattlefieldSlots[index] = new Button
            {
                Text = "·",
                CustomMinimumSize = new Vector2(110, 96),
                Disabled = true,
            };
            _enemyBattlefieldGrid.AddChild(_enemyBattlefieldSlots[index]);
        }
    }

    private void OnBoardSlot(BoardZone zone, int slot)
    {
        if (_player is null) return;
        var snapshot = _game.GetSnapshot(_player);
        var occupying = FindAt(snapshot.BoardPlacements, zone, slot);
        if (_selectedCardId is null)
        {
            if (occupying is not null)
            {
                _selectedCardId = occupying.CardId;
                _log.Text = "已选中棋盘卡牌，请点击目标格移动。";
                RefreshBoard();
            }
            return;
        }

        var result = _board.PlaceCard(_player, _selectedCardId.Value, zone, slot);
        _log.Text = result.IsSuccess
            ? $"棋盘操作成功：方向 {result.Value!.Direction}，推挤 {result.Value.AffectedCards} 张卡牌。"
            : result.Failure!.Message;
        if (result.IsSuccess) _selectedCardId = null;
        RefreshBoard();
    }

    private void RefreshBoard()
    {
        if (_player is null) return;
        var snapshot = _game.GetSnapshot(_player);
        RefreshZone(snapshot, BoardZone.Battlefield, _battlefieldSlots);
        RefreshZone(snapshot, BoardZone.Bench, _benchSlots);
        RefreshEnemyBattlefield(_enemy is null ? null : _game.GetSnapshot(_enemy));
    }

    private void RefreshEnemyBattlefield(MatchSnapshot? snapshot)
    {
        for (var slot = 0; slot < _enemyBattlefieldSlots.Length; slot++)
        {
            var placement = snapshot is null ? null : FindAt(snapshot.BoardPlacements, BoardZone.Battlefield, slot);
            _enemyBattlefieldSlots[slot].Text = placement is null ? $"{slot + 1}\n·"
                : placement.Start == slot
                    ? $"{slot + 1}\n{FormatCardFace(snapshot!.Cards.First(card => card.Id == placement.CardId))}"
                    : $"{slot + 1}\n■";
        }
    }

    private void RefreshZone(MatchSnapshot snapshot, BoardZone zone, IReadOnlyList<Button> buttons)
    {
        for (var slot = 0; slot < buttons.Count; slot++)
        {
            var placement = FindAt(snapshot.BoardPlacements, zone, slot);
            buttons[slot].Text = placement is null ? $"{slot + 1}\n·"
                : placement.Start == slot
                    ? $"{slot + 1}\n{FormatCardFace(snapshot.Cards.First(card => card.Id == placement.CardId))}"
                : $"{slot + 1}\n■";
            buttons[slot].Modulate = placement?.CardId == _selectedCardId ? new Color("75d69c") : Colors.White;
        }
    }

    private static BoardPlacementSnapshot? FindAt(
        IReadOnlyList<BoardPlacementSnapshot> placements,
        BoardZone zone,
        int slot)
    {
        foreach (var placement in placements)
            if (placement.Zone == zone && placement.Start <= slot && slot < placement.EndExclusive) return placement;
        return null;
    }

    private static string FormatCardFace(CardSnapshot card) =>
        FormatCardFace(card.DisplayName, card.Level, card.Size, card.FactionKey, card.ElementKeys);

    private static string FormatCardFace(CardIdentityAttributes identity, int level)
        => FormatCardFace(identity.DisplayName, level, identity.Size, identity.FactionKey, identity.ElementKeys);

    private static string FormatCardFace(
        string displayName,
        int level,
        CardSize cardSize,
        StringName factionKey,
        IReadOnlyList<StringName> elementKeys)
    {
        var size = cardSize switch
        {
            CardSize.Small => "小型",
            CardSize.Medium => "中型",
            CardSize.Large => "大型",
            _ => cardSize.ToString(),
        };
        var faction = factionKey == GameFactions.Neutral
            ? "无阵营"
            : factionKey == new StringName("paladin") ? "帕拉帝恩" : factionKey.ToString();
        var elements = string.Join("、", elementKeys.Select(element => element switch
        {
            var key when key == GameElements.General => "通用",
            var key when key == GameElements.Fire => "火",
            var key when key == GameElements.Water => "水",
            var key when key == GameElements.Wind => "风",
            var key when key == GameElements.Earth => "土",
            var key when key == GameElements.Lightning => "雷",
            var key when key == GameElements.Wood => "木",
            var key when key == GameElements.Ice => "冰",
            var key when key == GameElements.Light => "光",
            var key when key == GameElements.Dark => "暗",
            _ => element.ToString(),
        }));
        return $"{level}级 {displayName}\n{size} · {faction}\n{elements}";
    }

    private void StartBattle()
    {
        if (_player is null || _enemy is null) return;
        var before = _game.GetSnapshot(_player);
        var result = _game.ResolveBattle(_player, _enemy, _battleKind, _battleRound);
        if (result.IsFailure) { _log.Text = result.Failure!.Message; return; }
        var snapshot = _game.GetSnapshot(_player);
        _screen = Screen.BattleResult; HideAllActions();
        _title.Text = snapshot.Status == MatchStatus.InProgress ? "战斗结算"
            : snapshot.Status == MatchStatus.Won ? "对局胜利" : "对局失败";
        _log.Text = FormatBattleLog(result.Value!);
        if (_battleKind == MatchBattleKind.Monster)
            _log.Text += $"\n金币 +{snapshot.Wealth - before.Wealth}，经验 +{snapshot.Experience - before.Experience}。";
        _continue.Text = snapshot.Status == MatchStatus.InProgress ? "进入下一回合" : "返回英雄选择";
        _continue.Visible = true;
        UpdateState();
    }

    private void ClaimMonsterReward()
    {
        if (_player is null) return;
        var claimed = _monsterRewards.ClaimFirst(_player);
        if (claimed.IsFailure) { _log.Text = claimed.Failure!.Message; return; }
        _log.Text += $"\n已领取 {claimed.Value!.Reward.DisplayName}（{claimed.Value.CurrentLevel}级）。";
        RefreshBoard();
        UpdateState();
    }

    private void ContinueMatch()
    {
        if (_player is not null && _game.GetSnapshot(_player).Status == MatchStatus.InProgress) ShowEncounterChoices();
        else ShowHeroSelection();
    }

    private void HideAllActions()
    {
        _currentStock = null;
        _currentEncounterOptions = null;
        foreach (var child in _eventOptionButtons.GetChildren()) child.QueueFree();
        _eventOptionButtons.Visible = false;
        _hero.Visible = _battleButton.Visible = _continue.Visible = _refreshShop.Visible = false;
        _rewardButton.Visible = false;
        foreach (var button in _buyButtons) if (button is not null) button.Visible = false;
        _battlefieldPanel.Visible = _player is not null;
        _benchPanel.Visible = _player is not null;
        _enemyBattlefieldPanel.Visible = _enemy is not null;
        if (_player is not null) RefreshBoard();
        foreach (var choice in _choices) if (choice is not null) choice.Visible = false;
    }

    private void UpdateState()
    {
        if (_player is null) { _state.Text = "尚未开始对局"; return; }
        var snapshot = _game.GetSnapshot(_player);
        _state.Text = $"轮次 {snapshot.Round}-{snapshot.Turn}　财富 {snapshot.Wealth}　经验 {snapshot.Experience}　收入 {snapshot.Income}　"
            + $"声望 {snapshot.Reputation}　PvP胜场 {snapshot.PvpWins}/10　"
            + $"战场 {snapshot.BattlefieldCount}/10　备战 {snapshot.BenchCount}/10";
        _rewardButton.Visible = snapshot.PendingMonsterRewards.Count > 0;
        if (_rewardButton.Visible)
            _rewardButton.Text = $"领取战利品：{snapshot.PendingMonsterRewards[0].DisplayName}";
    }

    private static string FormatBattleLog(BattleResult result)
    {
        var lines = new List<string>();
        foreach (var battleEvent in result.Events)
            if (battleEvent is DamageDealtEvent damage)
            {
                var source = damage.SourceKind switch
                {
                    DamageSourceKind.Eclipse => "日蚀",
                    DamageSourceKind.Status => "状态",
                    DamageSourceKind.Skill => "技能",
                    _ => "卡牌",
                };
                lines.Add($"{damage.Tick.ToSeconds(),4:0.0}s　{source}：{(damage.TargetSide == SideId.Player ? "玩家" : "敌方")}受到 {damage.HealthDamage} 点伤害，生命 {damage.RemainingHealth}");
            }
        var outcome = result.Outcome == BattleOutcome.PlayerVictory ? "玩家胜利"
            : result.Outcome == BattleOutcome.OpponentVictory ? "战斗失败" : "平局";
        lines.Add($"\n结果：{outcome}　耗时 {result.EndedAt.ToSeconds():0.0}s");
        return string.Join("\n", lines);
    }

    private static string KindName(EncounterKind kind) => kind switch
    {
        EncounterKind.Shop => "商店",
        EncounterKind.Monster => "怪物战",
        EncounterKind.Pvp => "PvP",
        _ => "事件",
    };
}
