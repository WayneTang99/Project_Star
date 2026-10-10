using System;
using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Infrastructure.Encounters;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 显示设置回退与真实怪物阵容的只读排版验收（表现层验证模块）。
internal static class DisplayLayoutChecks
{
    internal static bool Settings(Control owner)
    {
        const string path = "res://output/ui-16x9/settings-check.cfg";
        var window = new Window { Size = new Vector2I(1280, 720), Position = new Vector2I(20, 30), Visible = false };
        owner.AddChild(window);
        var settings = new DisplaySettingsView { TargetWindow = window, ConfigurationPath = path, Size = new Vector2(1600, 900) };
        owner.AddChild(settings);
        try
        {
            settings.Open();
            var choices = settings.FindChildren("Resolution", "OptionButton", true, false).OfType<OptionButton>().Single();
            Button Button(string name) => settings.FindChildren(name, "Button", true, false).OfType<Button>().Single();
            if (Button("Cancel").FindNextValidFocus() != choices || choices.FindPrevValidFocus() != Button("Cancel")) return false;
            choices.Select(1); Button("Apply").EmitSignal(Godot.Button.SignalName.Pressed);
            if (window.Size != new Vector2I(1600, 900) || !Button("Confirm").Visible) return false;
            settings.Open();
            if (Button("Cancel").FindNextValidFocus() != Button("Confirm") || Button("Confirm").FindNextValidFocus() != Button("Cancel")) return false;
            Button("Cancel").EmitSignal(Godot.Button.SignalName.Pressed);
            if (window.Size != new Vector2I(1280, 720) || window.Position != new Vector2I(20, 30) || settings.Visible) return false;
            settings.Open(); choices.Select(1); Button("Apply").EmitSignal(Godot.Button.SignalName.Pressed);
            Button("Confirm").EmitSignal(Godot.Button.SignalName.Pressed);
            var config = new ConfigFile();
            if (config.Load(path) != Error.Ok || (int)config.GetValue("display", "width") != 1600 || settings.Visible) return false;
            window.Size = new Vector2I(1280, 720); settings.LoadSaved();
            if (window.Size != new Vector2I(1600, 900)) return false;
            settings.Open(); choices.Select(2); Button("Apply").EmitSignal(Godot.Button.SignalName.Pressed); settings._Process(16);
            if (window.Size != new Vector2I(1600, 900) || settings.Visible) return false;
            settings.Open(); choices.Select(0); Button("Apply").EmitSignal(Godot.Button.SignalName.Pressed);
            settings._Input(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            if (window.Size != new Vector2I(1600, 900) || settings.Visible) return false;
            settings.Open(); choices.Select(0); Button("Apply").EmitSignal(Godot.Button.SignalName.Pressed); settings.Hide();
            if (window.Size != new Vector2I(1600, 900) || config.Load(path) != Error.Ok || (int)config.GetValue("display", "width") != 1600) return false;
            var originalPalette = MatchTheme.BluePalette;
            var palette = settings.FindChildren("Palette", "OptionButton", true, false).OfType<OptionButton>().Single();
            settings.Open(); palette.Select(originalPalette ? 0 : 1); Button("Apply").EmitSignal(Godot.Button.SignalName.Pressed);
            if (MatchTheme.BluePalette == originalPalette) return false;
            Button("Cancel").EmitSignal(Godot.Button.SignalName.Pressed);
            return MatchTheme.BluePalette == originalPalette;
        }
        finally
        { owner.RemoveChild(settings); settings.Free(); owner.RemoveChild(window); window.Free(); DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path)); }
    }

    internal static bool MonsterBoard(Control owner)
    {
        var root = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<MinimalPlaytest>();
        var shell = root.GetNode<MatchShell>("MatchShell"); root.RemoveChild(shell); root.Free();
        shell.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft); shell.Size = new Vector2(1600, 900); owner.AddChild(shell);
        try
        {
            var registry = DefinitionRegistry.Scan(typeof(DisplayLayoutChecks).Assembly);
            var session = new CreateMatchService(new EntityFactory()).Create(42, 34, new PaladinHeroDefinition());
            var player = MatchSnapshot.From(session);
            var opponent = new LocalTestOpponentProvider(registry).CreateMonsterOpponent(42, registry.Monsters["monster.boar"]);
            var enemy = MatchSnapshot.From(opponent); var random = session.Random.State; var enemyRandom = opponent.Random.State;
            var view = new MatchPageViewModel(MatchPage.Preparation, "战斗准备", "确认你的阵容", player, enemy, null, true, true,
                [], [], [], [], 1, new UiAction("刷新", false), new UiAction("开始战斗"), new UiAction("继续", false), new UiAction("奖励", false));
            shell.Render(view);
            var board = shell.GetNode<BoardZoneView>("ContextRow/ContextHost/EnemyBoard");
            var originalItems = board.GetChildren().OfType<CardItemView>().ToArray();
            if (!board.Visible || originalItems.Length != 3 || board.GetChildren().OfType<Button>().Count(button => button.Name.ToString().StartsWith("Slot")) != 10) return false;
            foreach (var placement in enemy.BoardPlacements)
            {
                var card = board.GetNode<CardItemView>($"Card_{placement.CardId.Value:N}");
                var slot = board.GetNode<Button>($"Slot{placement.Start}");
                if (!card.Disabled || card.Position.DistanceTo(slot.Position) > .1f) return false;
            }
            foreach (var page in new[] { MatchPage.Preparation, MatchPage.BattlePlayback, MatchPage.Preparation })
            {
                shell.Render(view with { Page = page });
                var summary = shell.GetNode<Control>("ContextRow/ContextHost/BattleStage").GetGlobalRect();
                var hero = shell.GetNode<Control>("BenchRow/Hero/PlayerHeroPanel").GetGlobalRect();
                var own = shell.GetNode<Control>("BattlefieldRow/Content/Board").GetGlobalRect();
                var bench = shell.GetNode<Control>("BenchRow/Content/Board").GetGlobalRect();
                if (summary.End.Y >= hero.Position.Y || board.GetGlobalRect().End.Y >= own.Position.Y || own.End.Y >= bench.Position.Y
                    || bench.End.Y > 900 || board.GetGlobalRect().Position.X <= summary.End.X || shell.GetNode<VScrollBar>("DesktopScroll").Visible
                    || !originalItems.SequenceEqual(board.GetChildren().OfType<CardItemView>())) return false;
                // 同宽棋盘遵循原型的自然比例与间距，怪物包装不占己方标题行。
                if (board.ShowHeading || Mathf.Abs(board.GlobalPosition.Y - 137) > .05f
                    || Mathf.Abs(own.Position.Y - 372.4f) > .05f || Mathf.Abs(bench.Position.Y - 634.2f) > .05f
                    || Mathf.Abs(own.Size.Y - 249.8f) > .05f || Mathf.Abs(board.GetNode<Button>("Slot0").Size.Y - 202.4f) > .05f)
                    return false;
            }
            shell.Render(view with { Page = MatchPage.Shop, EnemyVisible = false });
            var title = shell.GetNode<Control>("ContextRow/Portrait").GetGlobalRect();
            var shop = shell.GetNode<Control>("ContextRow/ContextHost").GetGlobalRect();
            var playerHero = shell.GetNode<Control>("BenchRow/Hero/PlayerHeroPanel").GetGlobalRect();
            return !board.Visible && Mathf.Abs(title.End.X - shop.Position.X) < .1f && title.Position.Y == shop.Position.Y + 1
                && Mathf.Abs(shell.GetNode<Control>("BattlefieldRow/Content").Position.Y - 362) < .05f
                && Mathf.Abs(shell.GetNode<Control>("BenchRow/Content").Position.Y - 623.8f) < .05f
                && playerHero.Position.X < title.Position.X && playerHero.Position.Y < 170
                && session.Random.State == random && opponent.Random.State == enemyRandom
                && enemy.BoardPlacements.SequenceEqual(MatchSnapshot.From(opponent).BoardPlacements);
        }
        finally { owner.RemoveChild(shell); shell.Free(); }
    }
}
