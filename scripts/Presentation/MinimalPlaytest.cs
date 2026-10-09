using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Content;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Encounters;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Application.Mentors;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Infrastructure.Encounters;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;


namespace Project_Star.Presentation;

// 试玩入口仅组装应用服务，将页面快照交给界面骨架。
public sealed partial class MinimalPlaytest : Control
{
    private MatchPresenter _presenter = null!;
    private MatchShell _shell = null!;
    // 真实时间仅用于推进回放游标，不驱动战斗计算。
    public override void _Process(double delta) => _presenter?.AdvancePlayback(delta);
    public override void _Ready()
    {
        GetWindow().MinSize = new Vector2I(1280, 720);
        var registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        var factory = new EntityFactory();
        var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
        var economy = new CardEconomyService(factory, board, registry.Cards.Values);
        var rewards = new MonsterRewardClaimService(registry, economy, new SkillAcquisitionService(factory), board);
        var events = new ResolveEncounterOptionService(factory, board, registry.Cards.Values, registry.Monsters.Values);
        var game = new GameCoordinator(new CreateMatchService(factory),
            new EncounterScheduler(registry, allowIncompleteMonsterChoices: true, encounterLevelOverride: 4),
            new StartBattleService(new BattleSetupFactory(registry.Sets), new CombatSimulator()),
            new MatchResultService(board));
        _presenter = new MatchPresenter(registry, board, economy, new ShopCardPoolService(), events,
            new MentorService(registry, new SkillAcquisitionService(factory)), rewards, game, new LocalTestOpponentProvider(registry));
        _shell = GetNode<MatchShell>("MatchShell");
        _shell.SetCatalog(CardCatalogQuery.Capture(registry));
        _shell.ChoiceSelected += Choose;
        _shell.BuyRequested += _presenter.BuyCard;
        _shell.ActionRequested += Act;
        _shell.BoardSlotPressed += _presenter.OnBoardSlot;
        _shell.MoveRequested += Move;
        _shell.MovePreviewRequested += Preview;
        _shell.CancelRequested += _presenter.CancelSelection;
        _shell.SellRequested += _presenter.SellCard;
        _shell.DragSellRequested += _presenter.SellDraggedCard;
        _shell.SellPreviewRequested += PreviewSale;
        _shell.ClaimRequested += _presenter.ClaimReward;
        _presenter.ViewChanged += Render;
        _presenter.Reset();
    }
    public override void _ExitTree()
    {
        if (_presenter is not null) _presenter.ViewChanged -= Render;
        if (_shell is not null && _presenter is not null)
        {
            _shell.ChoiceSelected -= Choose;
            _shell.BuyRequested -= _presenter.BuyCard;
            _shell.ActionRequested -= Act;
            _shell.BoardSlotPressed -= _presenter.OnBoardSlot;
            _shell.MoveRequested -= Move;
            _shell.MovePreviewRequested -= Preview;
            _shell.CancelRequested -= _presenter.CancelSelection;
            _shell.SellRequested -= _presenter.SellCard;
            _shell.DragSellRequested -= _presenter.SellDraggedCard;
            _shell.SellPreviewRequested -= PreviewSale;
            _shell.ClaimRequested -= _presenter.ClaimReward;
        }
    }
    private void Render(MatchPageViewModel view) => _shell.Render(view);
    private Project_Star.Application.Common.Result<int> PreviewSale(BoardDragData data) =>
        _presenter.PreviewSale(data.MatchId, data.CardId);
    private void Move(BoardDragData data, BoardZone zone, int start) => _presenter.MoveCard(data.MatchId, data.CardId, zone, start);
    private Project_Star.Application.Common.Result<BoardPlacementResult> Preview(BoardDragData data, BoardZone zone, int start) =>
        _presenter.PreviewMove(data.MatchId, data.CardId, zone, start);

    private void Choose(MatchPage page, StringName key, long revision)
    {
        if (_presenter.View.Page != page) return;
        switch (page)
        {
            case MatchPage.HeroSelection: _presenter.SelectHero(key); break;
            case MatchPage.EncounterChoice: _presenter.ChooseEncounter(key); break;
            case MatchPage.Event: _presenter.ResolveEventOption(key, revision); break;
        }
    }

    private void Act(MatchAction action)
    {
        switch (action)
        {
            case MatchAction.Log: _shell.ToggleLog(); break;
            case MatchAction.Refresh: _presenter.RefreshShop(); break;
            case MatchAction.Battle: _presenter.StartBattle(); break;
            case MatchAction.Continue: _presenter.ContinueMatch(); break;
            case MatchAction.Reward: _presenter.ClaimMonsterReward(); break;
            case MatchAction.Reset: _presenter.Reset(); break;
            case MatchAction.Verification: GetTree().ChangeSceneToFile("res://Main.tscn"); break;
            case MatchAction.Showcase: GetTree().ChangeSceneToFile("res://scripts/Presentation/Playtest/ComponentShowcase.tscn"); break;
            case MatchAction.Pause: _presenter.PausePlayback(); break;
            case MatchAction.Speed: _presenter.SpeedPlayback(); break;
            case MatchAction.Skip: _presenter.SkipPlayback(); break;
        }
    }
}
