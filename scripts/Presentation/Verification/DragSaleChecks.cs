using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

internal static class DragSaleChecks
{
    private static T Field<T>(object target, string name) =>
        (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target)!;

    private static (MatchPresenter Presenter, MatchSession Session, CardEconomyService Economy, BoardService Board) Setup(Control scene)
    {
        var presenter = Field<MatchPresenter>(scene, "_presenter");
        presenter.SelectHero(new StringName("hero.paladin"));
        var session = Field<MatchSession>(presenter, "_player");
        var registry = DefinitionRegistry.Scan(typeof(DragSaleChecks).Assembly);
        var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
        return (presenter, session, new CardEconomyService(new EntityFactory(), board, registry.Cards.Values), board);
    }

    // 真实入口连接出售区，覆盖双棋盘、奖励、旧身份和最新金额校验。
    internal static bool Transactions(Control owner)
    {
        var scene = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<Control>();
        owner.AddChild(scene);
        try
        {
            var (presenter, session, economy, board) = Setup(scene);
            var boar = economy.AcquireAndPlace(session, new BoarCardDefinition(), 1, CardAcquisitionSource.Reward).Value!.Card;
            var bag = economy.AcquireAndPlace(session, new JewelryBagCardDefinition(), 2, CardAcquisitionSource.Reward).Value!.Card;
            board.PlaceCard(session, bag.Id, BoardZone.Bench, 0);
            boar.Attributes.Persistent.ApplyModifier(new StatModifier(ModifierId.New(), boar.Id, GameAttributeKeys.Value, 7));
            presenter.RefreshView();
            var drop = scene.GetNode<SellDropZone>("MatchShell/SellDropZone");
            var card = presenter.View.Player!.Cards.Single(item => item.Id == boar.Id);
            using var drag = new BoardDragData(session.Id, boar.Id, 1, 2);
            var wealth = session.Player.Wealth;
            var random = session.Random.State;
            if (!drop._CanDropData(Vector2.Zero, drag) || session.Player.Wealth != wealth || session.Random.State != random) return false;
            presenter.SellDraggedCard(session.Id, boar.Id, card.Level, card.Value + 1);
            if (!session.Board.Contains(boar.Id) || session.Player.Wealth != wealth) return false;
            drop._DropData(Vector2.Zero, drag);
            if (session.Board.Contains(boar.Id) || session.Player.Wealth != wealth + card.Value
                || presenter.View.Player!.Cards.Any(item => item.Id == boar.Id)) return false;
            drop._DropData(Vector2.Zero, drag);
            if (session.Player.Wealth != wealth + card.Value) return false;
            using var foreign = new BoardDragData(Guid.NewGuid(), bag.Id, 0, 1);
            using var enemy = new BoardDragData(session.Id, EntityId.New(), 0, 1);
            if (drop._CanDropData(Vector2.Zero, foreign) || drop._CanDropData(Vector2.Zero, enemy)) return false;
            using var bagDrag = new BoardDragData(session.Id, bag.Id, 0, 1);
            var bagValue = presenter.View.Player.Cards.Single(item => item.Id == bag.Id).Value;
            if (!drop._CanDropData(Vector2.Zero, bagDrag)) return false;
            drop._DropData(Vector2.Zero, bagDrag);
            if (session.Board.Contains(bag.Id) || session.Player.Wealth != wealth + card.Value + bagValue
                || presenter.View.Player!.Cards.Count != 1
                || !presenter.View.Player.Cards.Single().Tags.Contains(GameTags.Material)) return false;

            // 到第4回合真实进入战斗，播放期间不能预览或提交出售。
            while (presenter.View.Player!.Turn < 4)
            {
                var choice = presenter.View.Choices.First();
                presenter.ChooseEncounter(choice.Key);
                if (presenter.View.Page == MatchPage.Event)
                {
                    var option = presenter.View.EventOptions.FirstOrDefault();
                    if (option is not null) presenter.ResolveEventOption(option.Key, presenter.View.EventRevision);
                }
                presenter.ContinueMatch();
            }
            presenter.ChooseEncounter(presenter.View.Choices.First().Key);
            presenter.StartBattle();
            if (presenter.View.Page != MatchPage.BattlePlayback) return false;
            var remaining = session.Player.Inventory.Cards.First();
            var remainingValue = remaining.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value);
            var before = session.Player.Wealth;
            using var blocked = new BoardDragData(session.Id, remaining.Id, 0, 1);
            if (drop._CanDropData(Vector2.Zero, blocked) || presenter.PreviewSale(session.Id, remaining.Id).IsSuccess) return false;
            presenter.SellDraggedCard(session.Id, remaining.Id, remaining.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level), remainingValue);
            if (!session.Board.Contains(remaining.Id) || session.Player.Wealth != before) return false;
            presenter.Reset(); presenter.SelectHero(new StringName("hero.paladin"));
            return !drop._CanDropData(Vector2.Zero, bagDrag) && presenter.PreviewSale(session.Id, bag.Id).IsFailure;
        }
        finally { owner.RemoveChild(scene); scene.Free(); }
    }

    // 原生GUI从真实卡牌启动拖拽，确认上方遮罩接管输入、双棋盘可出售且取消无副作用。
    internal static async Task<bool> Native(Control owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(1280, 720), Disable3D = true, HandleInputLocally = true };
        var capture = OS.GetCmdlineUserArgs().Contains("--capture-drag-sale");
        if (capture) viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
        owner.AddChild(viewport);
        var scene = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<Control>();
        viewport.AddChild(scene);
        async Task Send(InputEvent input)
        {
            if (input is InputEventKey) { Input.ParseInputEvent(input); Input.FlushBufferedEvents(); }
            viewport.PushInput(input, true);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        }
        InputEventMouseMotion Motion(Vector2 point, Vector2 relative, bool down) => new()
            { Position = point, GlobalPosition = point, Relative = relative, ButtonMask = down ? MouseButtonMask.Left : 0 };
        InputEventMouseButton Button(Vector2 point, bool down) => new()
            { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = down,
                ButtonMask = down ? MouseButtonMask.Left : 0 };
        try
        {
            var (presenter, session, economy, board) = Setup(scene);
            var boar = economy.AcquireAndPlace(session, new BoarCardDefinition(), 1, CardAcquisitionSource.Reward).Value!.Card;
            var armor = economy.AcquireAndPlace(session, new ArmguardCardDefinition(), 1, CardAcquisitionSource.Reward).Value!.Card;
            board.PlaceCard(session, armor.Id, BoardZone.Bench, 0);
            presenter.RefreshView(); viewport.NotifyMouseEntered();
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            var drop = scene.GetNode<SellDropZone>("MatchShell/SellDropZone");
            foreach (var (card, path) in new[] { (boar, "MatchShell/BattlefieldRow/Content/Board"), (armor, "MatchShell/BenchRow/Content/Board") })
            {
                var item = scene.GetNode<CardItemView>(path + "/Card_" + card.Id.Value.ToString("N"));
                var press = item.GlobalPosition + item.Size / 2;
                var target = new Vector2(650, 140);
                var wealth = session.Player.Wealth;
                var value = card.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value);
                var placements = presenter.View.Player!.BoardPlacements.ToArray();
                await Send(Motion(press, Vector2.Zero, false)); await Send(Button(press, true));
                await Send(Motion(target, target - press, true));
                if (!viewport.GuiIsDragging() || !drop.Visible || !session.Board.Contains(card.Id)) return false;
                // 第一次拖拽先取消，确认未出售，然后再次拖到上方松开。
                if (card == boar)
                {
                    await Send(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = true });
                    await Send(Button(target, false));
                    if (drop.Visible || !session.Board.Contains(card.Id) || session.Player.Wealth != wealth
                        || !placements.SequenceEqual(presenter.View.Player!.BoardPlacements)) return false;
                    await Send(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = false });
                    await Send(Motion(press, press - target, false)); await Send(Button(press, true));
                    await Send(Motion(target, target - press, true));
                }
                await Send(Motion(target + Vector2.Right, Vector2.Right, true));
                if (capture && card == boar)
                {
                    await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var image = viewport.GetTexture().GetImage();
                    if (image.SavePng("res://output/drag-sale-preview.png") != Error.Ok) return false;
                }
                await Send(Button(target, false));
                if (drop.Visible || viewport.GuiIsDragging() || session.Board.Contains(card.Id)
                    || session.Player.Wealth != wealth + value || presenter.View.SelectedCardId is not null) return false;
            }
            return presenter.View.Player!.Cards.Count == 0;
        }
        finally
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = Key.Escape, PhysicalKeycode = Key.Escape, Pressed = false });
            Input.FlushBufferedEvents(); owner.RemoveChild(viewport); viewport.Free();
        }
    }
}
