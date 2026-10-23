using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

internal static class SecondaryModalChecks
{
    internal static async Task Capture(Control owner)
    {
        if (!SkillCatalogChecks.ArtworkAndLevels(owner) || !CardInteractionChecks.DirectAcquisition(owner))
            throw new InvalidOperationException("技能与奖励浮层回归失败。");
        var root = GD.Load<PackedScene>("res://Main.tscn").Instantiate<GameMain>();
        owner.AddChild(root); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        try
        {
            var presenter = (MatchPresenter)typeof(GameMain).GetField("_presenter",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(root)!;
            presenter.SelectHero(new StringName("hero.paladin"));
            var shell = root.GetNode<MatchShell>("MatchShell");
            var modal = shell.GetNode<HeroDetailsView>("HeroDetails");
            var registry = DefinitionRegistry.Scan(typeof(SecondaryModalChecks).Assembly);
            var skill = MatchDisplayQuery.FromSkill(registry.Skills.Values.Single(s => s.Attributes.Identity.DisplayName == "放逐"), 2);
            foreach (var size in new[] { new Vector2I(1600, 900), new Vector2I(2240, 1260) })
            {
                owner.GetWindow().Size = size; owner.GetTree().Root.ContentScaleSize = size;
                await Frame(); await Frame();
                shell.GetNode<Button>("OpenGameMenu").GrabFocus();
                typeof(MatchShell).GetMethod("ShowSkill", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(shell, [skill]);
                await Frame(); await Frame();
                var menuPoint = shell.GetNode<Button>("OpenGameMenu").GetGlobalRect().GetCenter();
                Click(menuPoint); await Frame();
                if (shell.GetNode<Control>("GameMenu").Visible || !modal.Visible)
                    throw new InvalidOperationException("技能详情遮罩未阻断底层菜单。");
                Key(Godot.Key.Tab); await Frame();
                if (!modal.IsAncestorOf(owner.GetViewport().GuiGetFocusOwner()))
                    throw new InvalidOperationException("技能详情Tab焦点泄漏。");
                var point = modal.GetNode<Button>("Content/Close").GetGlobalRect().GetCenter();
                owner.GetViewport().PushInput(new InputEventMouseMotion { Position = point, GlobalPosition = point }, true);
                await Frame();
                GD.Print($"技能详情鼠标命中 {owner.GetViewport().GuiGetHoveredControl()?.GetPath()}");
                DirAccess.MakeDirRecursiveAbsolute("res://output/secondary-modal");
                await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using (var image = owner.GetViewport().GetTexture().GetImage()) image.SavePng($"res://output/secondary-modal/skill-{size.X}x{size.Y}.png");
                owner.GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }, true);
                owner.GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, true);
                await Frame();
                if (modal.Visible || !shell.GetNode<Button>("OpenGameMenu").HasFocus())
                    throw new InvalidOperationException("技能详情关闭按钮被拦截或焦点未恢复。");
                typeof(MatchShell).GetMethod("ShowSection", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(shell, [HeroSection.Skills]);
                await Frame(); Key(Godot.Key.Escape); await Frame();
                if (modal.Visible) throw new InvalidOperationException("二级浮层Escape无法关闭。");
            }
            GD.Print("技能详情两个分辨率真实鼠标关闭通过。");
        }
        finally { owner.RemoveChild(root); root.Free(); }
        void Click(Vector2 point)
        {
            owner.GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }, true);
            owner.GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, true);
        }
        void Key(Godot.Key code)
        {
            owner.GetViewport().PushInput(new InputEventKey { Keycode = code, Pressed = true }, true);
            owner.GetViewport().PushInput(new InputEventKey { Keycode = code, Pressed = false }, true);
        }
        async Task Frame() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
    }
}
