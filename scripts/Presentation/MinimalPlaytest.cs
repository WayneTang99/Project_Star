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
    private readonly StartBattleService _battle = new(new BattleSetupFactory(), new CombatSimulator());
    private readonly ShopCardPoolService _shopCardPool = new();
    private readonly Button[] _choices = new Button[3];
    private DefinitionRegistry _registry = null!;
    private CardEconomyService _economy = null!;
    private CreateMatchService _matches = null!;
    private EncounterScheduler _encounters = null!;
    private MatchResultService _matchResults = null!;
    private MatchSession? _player;
    private MatchSession? _enemy;
    private Screen _screen;
    private MatchBattleKind _battleKind;
    private int _battleRound;
    private Label _title = null!;
    private Label _state = null!;
    private RichTextLabel _log = null!;
    private Button _hero = null!;
    private Button _buy = null!;
    private Button _battleButton = null!;
    private Button _continue = null!;
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
    private ShopOffer? _currentOffer;

    public override void _Ready()
    {
        _registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        _economy = new CardEconomyService(_factory, _board, _registry.Cards.Values);
        _matches = new CreateMatchService(_factory);
        _encounters = new EncounterScheduler(_registry, allowIncompleteMonsterChoices: true);
        _matchResults = new MatchResultService(_board);
        const string root = "Margin/Frame/Margin/Content";
        _title = GetNode<Label>($"{root}/Header/HeaderPanel/Margin/Title");
        _state = GetNode<Label>($"{root}/State");
        _log = GetNode<RichTextLabel>($"{root}/MainPanel/Margin/MainContent/Log");
        _hero = GetNode<Button>($"{root}/MainPanel/Margin/MainContent/Actions/Create");
        _buy = GetNode<Button>($"{root}/MainPanel/Margin/MainContent/Actions/Buy");
        _battleButton = GetNode<Button>($"{root}/MainPanel/Margin/MainContent/Actions/Battle");
        _continue = GetNode<Button>($"{root}/MainPanel/Margin/MainContent/EncounterActions/Generate");
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
        _buy.Pressed += BuyCard;
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
        _player = _matches.Create(42, 10, hero);
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
        var generated = _encounters.Generate(_player);
        _title.Text = $"第 {_player.Progress.Round} 轮 · 第 {_player.Progress.Turn} 回合";
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
        if (_player is null || index >= _player.EncounterSchedule.CurrentChoices.Count) return;
        var choice = _player.EncounterSchedule.CurrentChoices[index];
        _battleRound = _player.Progress.Round;
        var selected = _encounters.Select(_player, choice.Key);
        if (selected.IsFailure) { _log.Text = selected.Failure!.Message; return; }
        HideAllActions();
        switch (choice.Kind)
        {
            case EncounterKind.Shop: ShowShop(choice); break;
            case EncounterKind.Monster:
            case EncounterKind.Pvp: ShowPreparation(choice); break;
            default: ShowEvent(choice.DisplayName); break;
        }
    }

    private void ShowShop(EncounterChoice choice)
    {
        _screen = Screen.Shop;
        var shop = _registry.Encounters[choice.Key] as ShopEncounterDefinition;
        _title.Text = shop is null ? choice.DisplayName : $"{choice.DisplayName} · {shop.Level}级";
        _currentOffer = shop is null || _player is null
            ? null
            : _shopCardPool.CreateOffer(_player, shop, _registry.Cards.Values);
        var card = _currentOffer?.Definition;
        _log.Text = _currentOffer is null
            ? "尚未配置正式卡牌内容。"
            : $"{card!.Attributes.Identity.DisplayName}　价格 {_currentOffer.Price}　购买后价值 {_currentOffer.Price / 2}\n本次商店只有这一件商品。";
        _buy.Text = _currentOffer is null ? "暂无商品" : $"购买{card!.Attributes.Identity.DisplayName}";
        _buy.Disabled = _currentOffer is null;
        _buy.Visible = true;
        _continue.Text = "离开商店"; _continue.Visible = true;
        UpdateState();
    }

    private void BuyCard()
    {
        if (_player is null) return;
        if (_currentOffer is null) return;
        var target = _board.FindFirstAvailableTarget(
            _player,
            _currentOffer.Definition.Attributes.Identity.OccupiedSlots);
        if (target is null)
        {
            _log.Text = "战场区和备战区都没有足够空间，无法购买。";
            return;
        }

        var result = _economy.BuyCard(_player, _currentOffer);
        if (result.IsSuccess)
        {
            var displayName = _currentOffer.Definition.Attributes.Identity.DisplayName;
            var placement = _board.PlaceCard(_player, result.Value!.Id, target.Zone, target.Start);
            if (placement.IsFailure)
            {
                _log.Text = placement.Failure!.Message;
                return;
            }

            _buy.Visible = false;
            _log.Text = target.Zone == BoardZone.Battlefield
                ? $"购买成功，{displayName}已自动放入战场区。"
                : $"战场区空间不足，{displayName}已自动放入备战区。";
        }
        else _log.Text = result.Failure!.Message;
        RefreshBoard();
        UpdateState();
    }

    private void ShowEvent(string name)
    {
        _screen = Screen.Event; _title.Text = name;
        _log.Text = "你沿着林间道路继续前进。本次事件没有额外奖励。";
        _continue.Text = "继续旅程"; _continue.Visible = true;
        UpdateState();
    }

    private void ShowPreparation(EncounterChoice choice)
    {
        if (_player is null) return;
        _screen = Screen.Preparation; _title.Text = choice.Kind == EncounterKind.Pvp ? "PvP 战斗准备" : $"迎战：{choice.DisplayName}";
        var opponents = new LocalTestOpponentProvider(_registry);
        _enemy = choice.Kind == EncounterKind.Monster
            ? opponents.CreateMonsterOpponent(_player.Random.State, _registry.Monsters[choice.Key])
            : opponents.CreateOpponent(_player.Random.State);
        _battleKind = choice.Kind == EncounterKind.Pvp ? MatchBattleKind.Pvp : MatchBattleKind.Monster;
        _log.Text = _player.Board.Battlefield.Count == 0
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
            _battlefieldSlots[index] = new Button { Text = "·", CustomMinimumSize = new Vector2(64, 48) };
            _battlefieldSlots[index].Pressed += () => OnBoardSlot(BoardZone.Battlefield, slot);
            _battlefieldGrid.AddChild(_battlefieldSlots[index]);
            _benchSlots[index] = new Button { Text = "·", CustomMinimumSize = new Vector2(64, 48) };
            _benchSlots[index].Pressed += () => OnBoardSlot(BoardZone.Bench, slot);
            _benchGrid.AddChild(_benchSlots[index]);
            _enemyBattlefieldSlots[index] = new Button
            {
                Text = "·",
                CustomMinimumSize = new Vector2(64, 48),
                Disabled = true,
            };
            _enemyBattlefieldGrid.AddChild(_enemyBattlefieldSlots[index]);
        }
    }

    private void OnBoardSlot(BoardZone zone, int slot)
    {
        if (_player is null) return;
        var zoneState = _player.Board.GetZone(zone);
        var occupying = FindAt(zoneState, slot);
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
        RefreshZone(_player.Board.Battlefield, _battlefieldSlots);
        RefreshZone(_player.Board.Bench, _benchSlots);
        RefreshEnemyBattlefield();
    }

    private void RefreshEnemyBattlefield()
    {
        for (var slot = 0; slot < _enemyBattlefieldSlots.Length; slot++)
        {
            var placement = _enemy is null ? null : FindAt(_enemy.Board.Battlefield, slot);
            _enemyBattlefieldSlots[slot].Text = placement is null ? $"{slot + 1}\n·"
                : placement.Start == slot
                    ? $"{slot + 1}\n{_enemy!.Player.Inventory.Find(placement.CardId)!.Attributes.Identity.DisplayName}"
                    : $"{slot + 1}\n■";
        }
    }

    private void RefreshZone(BoardZoneState zone, IReadOnlyList<Button> buttons)
    {
        for (var slot = 0; slot < buttons.Count; slot++)
        {
            var placement = FindAt(zone, slot);
            buttons[slot].Text = placement is null ? $"{slot + 1}\n·"
                : placement.Start == slot ? $"{slot + 1}\n{_player!.Player.Inventory.Find(placement.CardId)!.Attributes.Identity.DisplayName}"
                : $"{slot + 1}\n■";
            buttons[slot].Modulate = placement?.CardId == _selectedCardId ? new Color("75d69c") : Colors.White;
        }
    }

    private static BoardPlacement? FindAt(BoardZoneState zone, int slot)
    {
        foreach (var placement in zone.Placements)
            if (placement.Start <= slot && slot < placement.EndExclusive) return placement;
        return null;
    }

    private void StartBattle()
    {
        if (_player is null || _enemy is null) return;
        var result = _battle.StartBattle(_player, _enemy, _player.Random.State);
        if (result.IsFailure) { _log.Text = result.Failure!.Message; return; }
        _ = _matchResults.Apply(_player, result.Value!, _battleKind, _battleRound);
        _screen = Screen.BattleResult; HideAllActions();
        _title.Text = _player.Status == MatchStatus.InProgress ? "战斗结算"
            : _player.Status == MatchStatus.Won ? "对局胜利" : "对局失败";
        _log.Text = FormatBattleLog(result.Value!);
        _continue.Text = _player.Status == MatchStatus.InProgress ? "进入下一回合" : "返回英雄选择";
        _continue.Visible = true;
        UpdateState();
    }

    private void ContinueMatch()
    {
        if (_player?.Status == MatchStatus.InProgress) ShowEncounterChoices();
        else ShowHeroSelection();
    }

    private void HideAllActions()
    {
        _currentOffer = null;
        _hero.Visible = _buy.Visible = _battleButton.Visible = _continue.Visible = false;
        _battlefieldPanel.Visible = _player is not null;
        _benchPanel.Visible = _player is not null;
        _enemyBattlefieldPanel.Visible = _enemy is not null;
        if (_player is not null) RefreshBoard();
        foreach (var choice in _choices) if (choice is not null) choice.Visible = false;
    }

    private void UpdateState()
    {
        if (_player is null) { _state.Text = "尚未开始对局"; return; }
        _state.Text = $"轮次 {_player.Progress.Round}-{_player.Progress.Turn}　财富 {_player.Player.Wealth}　"
            + $"声望 {_player.Player.Reputation}　PvP胜场 {_player.Progress.PvpWins}/10　"
            + $"战场 {_player.Board.Battlefield.Count}/10　备战 {_player.Board.Bench.Count}/10";
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
