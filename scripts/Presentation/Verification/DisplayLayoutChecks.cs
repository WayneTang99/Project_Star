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
        if (DirAccess.MakeDirRecursiveAbsolute("res://output/ui-16x9") != Error.Ok) return false;
        var window = new Window { Size = new Vector2I(1280, 720), Position = new Vector2I(20, 30), Visible = false };
        owner.AddChild(window);
        var settings = new DisplaySettingsView { TargetWindow = window, ConfigurationPath = path, Size = new Vector2(1600, 900) };
        owner.AddChild(settings);
        try
        {
            settings.Open();
            var choices = settings.FindChildren("Resolution", "OptionButton", true, false).OfType<OptionButton>().Single();
            Button Button(string name) => settings.FindChildren(name, "Button", true, false).OfType<Button>().Single();
            var sizes = Enumerable.Range(0, choices.ItemCount).Select(index =>
            {
                var parts = choices.GetItemText(index).Split('×');
                return new Vector2I(int.Parse(parts[0]), int.Parse(parts[1]));
            }).ToArray();
            var available = DisplayServer.GetName() == "headless" ? new Vector2I(int.MaxValue, int.MaxValue)
                : DisplayServer.ScreenGetUsableRect(window.CurrentScreen).Size;
            bool Fits(Vector2I size) => size.X <= available.X && size.Y <= available.Y;
            var previewIndex = Array.FindLastIndex(sizes, Fits);
            if (previewIndex < 0) throw new InvalidOperationException($"屏幕可用范围{available}不支持任何候选分辨率。");
            var confirmedSize = sizes[previewIndex];
            GD.Print($"显示设置验证：可用范围{available}，预览{confirmedSize}。");
            if (Button("Cancel").FindNextValidFocus() != choices || choices.FindPrevValidFocus() != Button("Cancel")) return false;
            choices.Select(previewIndex); Button("Apply").EmitSignal(Godot.Button.SignalName.Pressed);
            if (window.Size != confirmedSize || !Button("Confirm").Visible) return false;
            settings.Open();
            if (Button("Cancel").FindNextValidFocus() != Button("Confirm") || Button("Confirm").FindNextValidFocus() != Button("Cancel")) return false;
            Button("Cancel").EmitSignal(Godot.Button.SignalName.Pressed);
            if (window.Size != new Vector2I(1280, 720) || window.Position != new Vector2I(20, 30) || settings.Visible) return false;
            settings.Open(); choices.Select(previewIndex); Button("Apply").EmitSignal(Godot.Button.SignalName.Pressed);
            Button("Confirm").EmitSignal(Godot.Button.SignalName.Pressed);
            var config = new ConfigFile();
            if (config.Load(path) != Error.Ok || (int)config.GetValue("display", "width") != confirmedSize.X || settings.Visible) return false;
            window.Size = new Vector2I(1280, 720); settings.LoadSaved();
            if (window.Size != confirmedSize) return false;
            settings.Open(); choices.Select(0); Button("Apply").EmitSignal(Godot.Button.SignalName.Pressed); settings._Process(16);
            if (window.Size != confirmedSize || settings.Visible) return false;
            settings.Open(); choices.Select(0); Button("Apply").EmitSignal(Godot.Button.SignalName.Pressed);
            settings._Input(new InputEventKey { Keycode = Key.Escape, Pressed = true });
            if (window.Size != confirmedSize || settings.Visible) return false;
            settings.Open(); choices.Select(0); Button("Apply").EmitSignal(Godot.Button.SignalName.Pressed); settings.Hide();
            if (window.Size != confirmedSize || config.Load(path) != Error.Ok || (int)config.GetValue("display", "width") != confirmedSize.X) return false;
            var rejectedIndex = Array.FindLastIndex(sizes, size => !Fits(size));
            if (rejectedIndex >= 0)
            {
                settings.Open(); choices.Select(rejectedIndex); Button("Apply").EmitSignal(Godot.Button.SignalName.Pressed);
                var message = settings.FindChildren("Message", "Label", true, false).OfType<Label>().Single();
                if (window.Size != confirmedSize || Button("Confirm").Visible || !message.Text.Contains("超出")) return false;
                Button("Cancel").EmitSignal(Godot.Button.SignalName.Pressed);
            }
            return settings.FindChildren("Palette", "OptionButton", true, false).Count == 0;
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
                if (summary.End.Y >= board.GetGlobalRect().Position.Y || board.GetGlobalRect().End.Y >= own.Position.Y
                    || own.End.Y >= hero.Position.Y || own.End.Y > 900 || shell.GetNode<VScrollBar>("DesktopScroll").Visible
                    || !originalItems.SequenceEqual(board.GetChildren().OfType<CardItemView>())) return false;
                if (board.ShowHeading || Mathf.Abs(board.GlobalPosition.Y - 250) > .05f
                    || Mathf.Abs(own.Position.Y - 500) > .05f || Mathf.Abs(own.Size.Y - 230) > .05f
                    || Mathf.Abs(board.GetNode<Button>("Slot0").Size.Y - 204) > .05f) return false;
                shell.SetBenchOpen(true);
                if (!board.IsVisibleInTree() || shell.GetNode<Control>("BenchRow/Content").IsVisibleInTree()
                    || !shell.GetNode<Button>("BenchToggle").Disabled) return false;
            }
            shell.Render(view with { Page = MatchPage.Shop, EnemyVisible = false });
            var title = shell.GetNode<Control>("ContextRow/Portrait").GetGlobalRect();
            var shop = shell.GetNode<Control>("ContextRow/ContextHost").GetGlobalRect();
            var playerHero = shell.GetNode<Control>("BenchRow/Hero/PlayerHeroPanel").GetGlobalRect();
            if (board.Visible || Mathf.Abs(title.End.X - shop.Position.X) > .1f || title.Position.Y != shop.Position.Y + 1
                || playerHero.Position.Y != 750 || shell.GetNode<Control>("BenchRow/Content").IsVisibleInTree()) return false;
            shell.SetBenchOpen(true);
            if (!shell.GetNode<Control>("BenchRow/Content").IsVisibleInTree()
                || shell.GetNode<Control>("ContextRow/ContextHost").IsVisibleInTree()
                || shell.GetNode<Control>("BattlefieldRow/Content").GlobalPosition.Y != 500) return false;
            shell.SetBenchOpen(false);
            var nodes = new[] { "BattlefieldRow/Content", "BenchRow/Content", "BenchRow/Hero/PlayerHeroPanel", "BenchToggle", "Economy", "ContextRow/ContextHost" }
                .Select(path => shell.GetNode<Control>(path)).ToArray();
            var geometry = nodes.Select(node => node.GetGlobalRect()).ToArray();
            var previousSkin = MatchTheme.Skin;
            var variant = (BoardSkin)previousSkin.Duplicate();
            variant.TableSurface = new GradientTexture2D { Gradient = new Gradient { Colors = new[] { Colors.White, Colors.Gray } } };
            variant.SurfaceEffect = (ShaderMaterial)previousSkin.SurfaceEffect.Duplicate();
            variant.SurfaceEffect.SetShaderParameter("grain_opacity", .12f);
            shell.ApplyBoardSkin(variant);
            var geometryStable = nodes.Select(node => node.GetGlobalRect()).SequenceEqual(geometry)
                && MatchTheme.Skin.SurfaceEffect != previousSkin.SurfaceEffect
                && shell.GetNode<TextureRect>("TableMaterial").Texture == variant.TableSurface
                && shell.GetNode<Control>("TableMaterial").MouseFilter == Control.MouseFilterEnum.Ignore;
            shell.ApplyBoardSkin(previousSkin);
            return geometryStable && shell.GetNode<Control>("ContextRow/ContextHost").IsVisibleInTree()
                && session.Random.State == random && opponent.Random.State == enemyRandom
                && enemy.BoardPlacements.SequenceEqual(MatchSnapshot.From(opponent).BoardPlacements);
        }
        finally { owner.RemoveChild(shell); shell.Free(); }
    }
}
