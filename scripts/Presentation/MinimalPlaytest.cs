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
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;
using CardFaceControl = Project_Star.Presentation.CardFace.CardFace;

namespace Project_Star.Presentation;

/// <summary>Composes the playtest and renders one captured page; flow belongs to MatchPresenter.</summary>
public sealed partial class MinimalPlaytest : Control
{
    private MatchPresenter _presenter = null!;
    private readonly CardDisplayAdapter _cards = new();
    private readonly Button[] _choices = new Button[3];
    private VBoxContainer _heroButtons = null!;
    private Button _reset = null!;
    private Button _verification = null!;
    private readonly List<(Button Button, Action Handler)> _bindings = new();
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
    private readonly CardFaceControl?[] _battlefieldFaces = new CardFaceControl?[10];
    private readonly CardFaceControl?[] _benchFaces = new CardFaceControl?[10];
    private readonly CardFaceControl?[] _enemyBattlefieldFaces = new CardFaceControl?[10];
    private PackedScene _cardFaceScene = null!;
    public override void _Ready()
    {
        var registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        var factory = new EntityFactory();
        var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
        var economy = new CardEconomyService(factory, board, registry.Cards.Values);
        var rewards = new MonsterRewardClaimService(registry, economy, new SkillAcquisitionService(factory), board);
        var events = new ResolveEncounterOptionService(factory, board, registry.Cards.Values);
        var game = new GameCoordinator(new CreateMatchService(factory),
            new EncounterScheduler(registry, allowIncompleteMonsterChoices: true),
            new StartBattleService(new BattleSetupFactory(registry.Sets), new CombatSimulator()),
            new MatchResultService(board));
        _presenter = new MatchPresenter(registry, board, economy, new ShopCardPoolService(), events, rewards, game);
        _cardFaceScene = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardFace.tscn");
        const string root = "Margin/Frame/Margin/Content";
        const string main = root + "/MainPanel/Margin/MainContent";
        _title = GetNode<Label>($"{root}/Header/HeaderPanel/Margin/Title");
        _state = GetNode<Label>($"{root}/State");
        _log = GetNode<RichTextLabel>($"{main}/Log");
        _hero = GetNode<Button>($"{main}/Actions/Create");
        _hero.Visible = false;
        _heroButtons = new VBoxContainer();
        GetNode<Control>(main).AddChild(_heroButtons);
        for (var index = 0; index < _buyButtons.Length; index++)
            _buyButtons[index] = GetNode<Button>($"{main}/Actions/Buy{index + 1}");
        _refreshShop = GetNode<Button>($"{main}/Actions/Refresh");
        _battleButton = GetNode<Button>($"{main}/Actions/Battle");
        _continue = GetNode<Button>($"{main}/EncounterActions/Generate");
        _rewardButton = new Button();
        GetNode<HBoxContainer>($"{main}/EncounterActions").AddChild(_rewardButton);
        _eventOptionButtons = GetNode<VBoxContainer>($"{main}/EventOptions");
        _battlefieldPanel = GetNode<Control>($"{root}/BattlefieldPanel");
        _benchPanel = GetNode<Control>($"{root}/BenchPanel");
        _enemyBattlefieldPanel = GetNode<Control>($"{root}/EnemyBattlefieldPanel");
        _battlefieldGrid = GetNode<GridContainer>($"{root}/BattlefieldPanel/Margin/Area/BattlefieldSlots");
        _benchGrid = GetNode<GridContainer>($"{root}/BenchPanel/Margin/Area/BenchSlots");
        _enemyBattlefieldGrid = GetNode<GridContainer>($"{root}/EnemyBattlefieldPanel/Margin/Area/EnemyBattlefieldSlots");
        for (var index = 0; index < _choices.Length; index++)
            _choices[index] = GetNode<Button>($"{main}/Choices/Choice{index + 1}");
        _reset = GetNode<Button>($"{root}/Footer/Reset");
        _verification = GetNode<Button>($"{root}/Footer/Verification");
        BuildBoardSlots();
        _presenter.ViewChanged += Render;
        _presenter.Reset();
    }

    public override void _ExitTree()
    {
        if (_presenter is not null) _presenter.ViewChanged -= Render;
        DisconnectBindings();
    }

    private void Bind(Button button, Action handler)
    {
        button.Pressed += handler;
        _bindings.Add((button, handler));
    }

    private void DisconnectBindings()
    {
        foreach (var (button, handler) in _bindings)
            if (GodotObject.IsInstanceValid(button)) button.Pressed -= handler;
        _bindings.Clear();
    }

    private static void ClearButtons(VBoxContainer container)
    {
        foreach (var child in container.GetChildren())
        {
            container.RemoveChild(child);
            child.QueueFree();
        }
    }

    private static void RenderAction(Button button, UiAction action)
    {
        button.Text = action.Text;
        button.Visible = action.Visible;
        button.Disabled = !action.Enabled;
        button.TooltipText = action.Reason;
    }

    private void Render(MatchPageViewModel view)
    {
        DisconnectBindings();
        _title.Text = view.Title;
        _log.Text = view.Message;
        var player = view.Player;
        _state.Text = player is null ? "尚未开始对局"
            : $"轮次 {player.Round}-{player.Turn}　金钱 {player.Wealth}　收入 {player.Income}　"
                + $"声望 {player.Reputation}　经验 {player.Experience}　等级 {player.Hero?.Level ?? 0}　PvP {player.PvpWins}/10　"
                + $"战场 {UsedSlots(player, BoardZone.Battlefield)}/{player.BattlefieldCapacity}格　"
                + $"备战 {UsedSlots(player, BoardZone.Bench)}/{player.BenchCapacity}格";
        ClearButtons(_heroButtons);
        _heroButtons.Visible = view.Page == MatchPage.HeroSelection;
        if (_heroButtons.Visible)
        {
            foreach (var hero in view.Heroes)
            {
                var button = new Button();
                RenderAction(button, hero.Action);
                _heroButtons.AddChild(button);
                Bind(button, () => _presenter.SelectHero(hero.Key));
            }
        }
        for (var index = 0; index < _choices.Length; index++)
        {
            var choice = index < view.Choices.Count ? view.Choices[index] : null;
            _choices[index].Visible = choice is not null;
            if (choice is null) continue;
            RenderAction(_choices[index], choice.Action);
            Bind(_choices[index], () => _presenter.ChooseEncounter(choice.Key));
        }
        for (var index = 0; index < _buyButtons.Length; index++)
        {
            var offer = index < view.Offers.Count ? view.Offers[index] : null;
            _buyButtons[index].Visible = offer is not null && offer.Action.Visible;
            if (offer is null) continue;
            RenderAction(_buyButtons[index], offer.Action with
            {
                Text = $"{PlaytestText.FormatCardFace(offer.Card)}\n{offer.Action.Text}",
            });
            _buyButtons[index].TooltipText = CardDisplayAdapter.Details(offer.Card) + "\n" + offer.Action.Reason;
            Bind(_buyButtons[index], () => _presenter.BuyCard(offer.Index, offer.Revision));
        }
        ClearButtons(_eventOptionButtons);
        _eventOptionButtons.Visible = view.EventOptions.Count > 0;
        foreach (var option in view.EventOptions)
        {
            var button = new Button();
            RenderAction(button, option.Action);
            _eventOptionButtons.AddChild(button);
            Bind(button, () => _presenter.ResolveEventOption(option.Key, view.EventRevision));
        }
        RenderAction(_refreshShop, view.Refresh);
        RenderAction(_battleButton, view.Battle);
        RenderAction(_continue, view.Continue);
        RenderAction(_rewardButton, view.Reward);
        Bind(_refreshShop, _presenter.RefreshShop);
        Bind(_battleButton, _presenter.StartBattle);
        Bind(_continue, _presenter.ContinueMatch);
        Bind(_rewardButton, _presenter.ClaimMonsterReward);
        Bind(_reset, _presenter.Reset);
        Bind(_verification, () => GetTree().ChangeSceneToFile("res://Main.tscn"));
        _battlefieldPanel.Visible = _benchPanel.Visible = player is not null;
        _enemyBattlefieldPanel.Visible = view.EnemyVisible;
        RenderZone(player, BoardZone.Battlefield, _battlefieldSlots, _battlefieldFaces, view);
        RenderZone(player, BoardZone.Bench, _benchSlots, _benchFaces, view);
        RenderZone(view.Enemy, BoardZone.Battlefield, _enemyBattlefieldSlots, _enemyBattlefieldFaces, view, enemy: true);
    }

    private static int UsedSlots(MatchSnapshot snapshot, BoardZone zone) => snapshot.BoardPlacements
        .Where(item => item.Zone == zone).Sum(item => item.EndExclusive - item.Start);

    private void RenderZone(MatchSnapshot? snapshot, BoardZone zone, IReadOnlyList<Button> buttons,
        IReadOnlyList<CardFaceControl?> faces, MatchPageViewModel view, bool enemy = false)
    {
        for (var slot = 0; slot < buttons.Count; slot++)
        {
            var placement = snapshot?.BoardPlacements.FirstOrDefault(item => item.Zone == zone
                && item.Start <= slot && slot < item.EndExclusive);
            var card = placement?.Start == slot ? snapshot!.Cards.First(item => item.Id == placement.CardId) : null;
            var button = buttons[slot];
            button.Text = card is not null ? string.Empty : placement is not null ? "■" : $"{slot + 1}\n·";
            button.Disabled = enemy || !view.BoardEnabled;
            button.TooltipText = placement is null ? "" : CardDisplayAdapter.Details(snapshot!.Cards.First(item => item.Id == placement.CardId));
            button.Modulate = !enemy && placement is not null && placement.CardId == view.SelectedCardId
                ? new Color("75d69c") : Colors.White;
            if (!enemy)
            {
                var targetSlot = slot;
                Bind(button, () => _presenter.OnBoardSlot(zone, targetSlot));
            }
            var face = faces[slot];
            if (face is null) continue;
            face.Visible = card is not null;
            if (card is null) continue;
            face.Position = new Vector2((button.Size.X - 200 * (int)card.Size * face.Scale.X) * 0.5f,
                (button.Size.Y - 400 * face.Scale.Y) * 0.5f);
            face.SetCard(_cards.Build(card));
        }
    }
    private void BuildBoardSlots()
    {
        for (var index = 0; index < 10; index++)
        {
            _battlefieldSlots[index] = new Button { Text = "·", CustomMinimumSize = new Vector2(110, 96) };
            _battlefieldGrid.AddChild(_battlefieldSlots[index]);
            _battlefieldFaces[index] = CreateBoardFace(_battlefieldSlots[index]);
            _benchSlots[index] = new Button { Text = "·", CustomMinimumSize = new Vector2(110, 96) };
            _benchGrid.AddChild(_benchSlots[index]);
            _benchFaces[index] = CreateBoardFace(_benchSlots[index]);
            _enemyBattlefieldSlots[index] = new Button
            {
                Text = "·",
                CustomMinimumSize = new Vector2(110, 96),
                Disabled = true,
            };
            _enemyBattlefieldGrid.AddChild(_enemyBattlefieldSlots[index]);
            _enemyBattlefieldFaces[index] = CreateBoardFace(_enemyBattlefieldSlots[index]);
        }
    }

    private CardFaceControl CreateBoardFace(Button slot)
    {
        var face = _cardFaceScene.Instantiate<CardFaceControl>();
        face.Scale = new Vector2(0.17f, 0.17f);
        face.MouseFilter = Control.MouseFilterEnum.Ignore;
        slot.AddChild(face);
        return face;
    }

}
