using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 英雄身份、初始属性及选角和头像纹理的真实控件验证（表现层）。
internal static class HeroArtworkChecks
{
    internal static bool CheckVoice(Control owner)
    {
        var registry = DefinitionRegistry.Scan(typeof(HarlaHeroDefinition).Assembly);
        var choices = new[] { "paladin", "mona" }.Select(key =>
        {
            var hero = registry.Heroes[new StringName("hero." + key)];
            return new KeyedAction(hero.Attributes.Identity.Key, new UiAction(hero.Attributes.Identity.DisplayName))
                { Illustration = hero.Attributes.Identity.Illustration, Hero = HeroSelectionDetails.From(hero) };
        }).ToArray();
        var host = new Control(); owner.AddChild(host);
        var selection = new HeroSelectionView(); host.AddChild(selection);
        try
        {
            selection.Render("配音", choices);
            var player = selection.GetNode<AudioStreamPlayer>("SelectionVoice");
            var entries = selection.GetNode<VBoxContainer>("Roster/Entries");
            var submitted = 0; selection.Selected += (_, _) => submitted++;
            if (player.Playing) return false;
            entries.GetChild<Button>(0).EmitSignal(Button.SignalName.Pressed);
            if (!player.Playing || player.Stream is not AudioStreamMP3 stream || stream.Loop
                || stream.GetLength() is < 0.5 or > 10 || player.MaxPolyphony != 1) return false;
            using var decoder = stream.InstantiatePlayback();
            decoder.Start();
            var samples = decoder.MixAudio(1f, (int)(AudioServer.GetMixRate() * stream.GetLength()));
            decoder.Stop();
            if (!samples.Any(sample => sample.LengthSquared() > .0001f)) return false;
            GD.Print($"圣骑士选角配音：{stream.GetLength():F2}秒，解码{samples.Length}帧，包含有效语音。");
            var first = player.GetStreamPlayback();
            selection.Render("刷新", choices, 9);
            if (!player.Playing || player.GetStreamPlayback() != first) return false;
            entries.GetChild<Button>(0).EmitSignal(Button.SignalName.Pressed);
            if (!player.Playing || player.GetStreamPlayback() == first) return false;
            selection._Input(new InputEventKey { Keycode = Key.Right, Pressed = true });
            if (player.Playing) return false;
            selection._Input(new InputEventKey { Keycode = Key.Left, Pressed = true });
            if (!player.Playing || submitted != 0) return false;
            host.Hide();
            entries.GetChild<Button>(0).EmitSignal(Button.SignalName.Pressed);
            if (player.Playing) return false;
            host.Show();
            if (player.Playing) return false;
            entries.GetChild<Button>(0).EmitSignal(Button.SignalName.Pressed);
            if (!player.Playing) return false;
            selection.Hide();
            if (player.Playing) return false;
            selection.Show();
            entries.GetChild<Button>(0).EmitSignal(Button.SignalName.Pressed);
            selection.Render("空名单", []);
            return !player.Playing && submitted == 0;
        }
        finally { owner.RemoveChild(host); host.Free(); }
    }

    internal static bool CheckMotion(Control owner)
    {
        var registry = DefinitionRegistry.Scan(typeof(HarlaHeroDefinition).Assembly);
        KeyedAction Choice(string key)
        {
            var hero = registry.Heroes[new StringName("hero." + key)];
            return new KeyedAction(hero.Attributes.Identity.Key, new UiAction(hero.Attributes.Identity.DisplayName))
                { Illustration = hero.Attributes.Identity.Illustration, Hero = HeroSelectionDetails.From(hero) };
        }
        var host = new Control(); owner.AddChild(host);
        var selection = new HeroSelectionView(); host.AddChild(selection);
        var second = new HeroSelectionView(); host.AddChild(second);
        try
        {
            var paladin = Choice("paladin");
            selection.Render("动效", [paladin]); second.Render("独立时钟", [paladin]);
            var portrait = selection.GetNode<TextureRect>("CurrentPortrait");
            if (portrait.Material is not ShaderMaterial material || !selection.IsProcessing()
                || material == second.GetNode<TextureRect>("CurrentPortrait").Material
                || portrait.Texture?.ResourcePath != paladin.Illustration.ToString()) return false;
            var submitted = 0; selection.Selected += (_, _) => submitted++;
            float Clock() => material.GetShaderParameter("motion_time").AsSingle();
            selection._Process(1.25);
            selection.Render("刷新", [paladin], 4);
            if (Math.Abs(Clock() - 1.25f) > .001f) return false;
            host.Hide(); selection._Process(2);
            if (selection.IsProcessing() || Math.Abs(Clock() - 1.25f) > .001f) return false;
            host.Show(); selection._Process(7);
            if (!selection.IsProcessing() || Math.Abs(Clock() - .25f) > .001f) return false;
            selection.Hide(); selection._Process(2);
            if (selection.IsProcessing() || Math.Abs(Clock() - .25f) > .001f) return false;
            selection.Show();
            selection.Render("其他英雄", [Choice("mona")]);
            if (portrait.Material is not null || selection.IsProcessing()) return false;
            selection.Render("返回", [paladin]);
            if (!selection.IsProcessing() || Clock() != 0 || submitted != 0) return false;
            var thumbnail = selection.GetNode<VBoxContainer>("Roster/Entries").GetChild<Button>(0)
                .GetNode<HBoxContainer>("Content").GetChild<TextureRect>(0);
            if (thumbnail.Material is not null) return false;
            selection.Render("空名单", []);
            return portrait.Material is null && !selection.IsProcessing();
        }
        finally { owner.RemoveChild(host); host.Free(); }
    }

    // 实际选角页捕获八秒循环；同时检查图鉴暂停、返回继续和英雄切换。
    internal static async Task CaptureMotion(Control owner)
    {
        const string directory = "res://output/paladin-motion";
        DirAccess.MakeDirRecursiveAbsolute(directory);
        var root = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<MinimalPlaytest>();
        owner.AddChild(root); root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        try
        {
            var shell = root.GetNode<MatchShell>("MatchShell");
            var selection = shell.GetNode<HeroSelectionView>("ContextRow/ContextHost/HeroSelectionView");
            var entries = selection.GetNode<VBoxContainer>("Roster/Entries");
            entries.GetChild<Button>(3).EmitSignal(Button.SignalName.Pressed);
            await owner.ToSignal(owner.GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
            var portrait = selection.GetNode<TextureRect>("CurrentPortrait");
            if (portrait.Material is not ShaderMaterial material) throw new InvalidOperationException("圣骑士原画没有动效材质。");
            float Clock() => material.GetShaderParameter("motion_time").AsSingle();
            var before = Clock(); await Frame(); await Frame();
            if (Clock() == before) throw new InvalidOperationException("圣骑士动效时钟未推进。");
            shell.GetNode<Button>("GameMenu/ItemsPanel/Items/OpenCardCatalog").EmitSignal(Button.SignalName.Pressed);
            var paused = Clock(); await Frame(); await Frame();
            if (selection.IsProcessing() || Clock() != paused) throw new InvalidOperationException("图鉴打开后动效未暂停。");
            owner.GetViewport().PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = true }, true);
            owner.GetViewport().PushInput(new InputEventKey { Keycode = Key.Escape, Pressed = false }, true);
            await Frame(); await Frame();
            if (!selection.IsProcessing() || Clock() == paused) throw new InvalidOperationException("图鉴返回后动效未继续。");
            entries.GetChild<Button>(2).EmitSignal(Button.SignalName.Pressed);
            if (portrait.Material is not null || selection.IsProcessing()) throw new InvalidOperationException("其他英雄没有停用动效。");
            entries.GetChild<Button>(3).EmitSignal(Button.SignalName.Pressed);
            await owner.ToSignal(owner.GetTree().CreateTimer(.3), SceneTreeTimer.SignalName.Timeout);
            selection.SetProcess(false);
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            {
                owner.GetWindow().Size = size; owner.GetTree().Root.ContentScaleSize = size;
                await Frame(); await Frame();
                var rect = portrait.GetGlobalRect();
                if (Math.Abs(rect.Size.X - rect.Size.Y) > 1 || rect.Position.X < 0 || rect.End.X > size.X || rect.End.Y > size.Y)
                    throw new InvalidOperationException("选角原画非方形或越界。");
                var count = size.X == 1280 ? 129 : 1;
                for (var index = 0; index < count; index++)
                {
                    material.SetShaderParameter("motion_time", index / 16f);
                    await Frame();
                    await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                    using var image = owner.GetViewport().GetTexture().GetImage();
                    if (owner.GetViewport().UseHdr2D)
                        for (var y = 0; y < image.GetHeight(); y++)
                            for (var x = 0; x < image.GetWidth(); x++) image.SetPixel(x, y, image.GetPixel(x, y).LinearToSrgb());
                    image.Convert(Image.Format.Rgba8);
                    if (image.SavePng($"{directory}/frame-{size.X}-{index:D2}.png") != Error.Ok)
                        throw new InvalidOperationException("圣骑士动效截图保存失败。");
                }
                GD.Print($"圣骑士原画 {size.X}x{size.Y}：{rect}");
            }
            GD.Print("圣骑士动效时钟、图鉴暂停与恢复、英雄切换及两种窗口捕获通过。");
        }
        finally { owner.RemoveChild(root); root.Free(); }
        async Task Frame() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    internal static bool Check(Control owner)
    {
        var registry = DefinitionRegistry.Scan(typeof(HarlaHeroDefinition).Assembly);
        var panel = new PlayerHeroPanel(); owner.AddChild(panel);
        var selection = new HeroSelectionView { Size = new Vector2(1200, 600) }; owner.AddChild(selection);
        try
        {
            foreach (var (key, name, title) in new[] { ("harla", "哈尔拉", "机械师"), ("jiyun", "极云", "熊猫人"),
                ("mona", "莫娜", "小魔女"), ("paladin", "帕拉帝恩", "圣骑士"),
                ("robin", "罗宾", "冒险家"), ("valos", "瓦洛斯", "潜行者") })
            {
                var definition = registry.Heroes[new StringName("hero." + key)];
                var identity = definition.Attributes.Identity;
                var session = new CreateMatchService(new EntityFactory()).Create(42, 100, definition);
                var snapshot = MatchSnapshot.From(session);
                var hero = snapshot.Hero!;
                if (identity.DisplayName != name || identity.Title != title || identity.FactionKey != new StringName(key)
                    || identity.Illustration.IsEmpty || hero.Illustration != identity.Illustration || hero.Level != 1 || snapshot.Income != 5
                    || hero.CombatValues[GameAttributeKeys.MaxHealth] != 200 || hero.CombatValues[GameAttributeKeys.MaxMana] != 100
                    || hero.CombatValues[GameAttributeKeys.Mana] != 0 || hero.CombatValues[GameAttributeKeys.Armor] != 0
                    || hero.CombatValues[GameAttributeKeys.ManaRegen] != 10 || hero.CombatValues[GameAttributeKeys.HealthRegen] != 0) return false;
                panel.Render(hero);
                var portrait = panel.GetNode<TextureRect>("Identity/Portrait");
                if (!portrait.Visible || portrait.Texture is not AtlasTexture cropped
                    || cropped.Atlas.ResourcePath != identity.Illustration.ToString()
                    || !Mathf.IsEqualApprox(cropped.Region.Size.X, cropped.Region.Size.Y)
                    || cropped.Region.Size.X >= cropped.Atlas.GetWidth() / 2f
                    || !new Rect2(Vector2.Zero, cropped.Atlas.GetSize()).Encloses(cropped.Region)) return false;
                selection.Render("选角", [new KeyedAction(identity.Key, new UiAction(name))
                    { Illustration = hero.Illustration, Hero = HeroSelectionDetails.From(definition) }]);
                if (selection.GetNode<TextureRect>("CurrentPortrait").Texture?.ResourcePath != identity.Illustration.ToString()) return false;
                if (selection.GetNode<Label>("HeroName").Text != name || selection.GetNode<Label>("HeroTitle").Text != title
                    || selection.GetNode<GridContainer>("Stats").GetChild<Label>(1).Text != "200") return false;
            }
            var choices = registry.Heroes.Values.OrderBy(hero => hero.Attributes.Identity.Key.ToString())
                .Select(hero => new KeyedAction(hero.Attributes.Identity.Key, new UiAction(hero.Attributes.Identity.DisplayName))
                    { Illustration = hero.Attributes.Identity.Illustration, Hero = HeroSelectionDetails.From(hero) }).ToArray();
            selection.Render("选择", choices);
            var entries = selection.GetNode<VBoxContainer>("Roster/Entries");
            if (entries.GetChildCount() != choices.Length) return false;
            entries.GetChild<Button>(0).EmitSignal(Button.SignalName.Pressed);
            var initial = selection.GetNode<TextureRect>("CurrentPortrait").Texture;
            var submitted = 0;
            StringName submittedKey = new(""); long submittedRevision = 0;
            selection.Selected += (key, revision) => { submitted++; submittedKey = key; submittedRevision = revision; };
            var second = entries.GetChild<Button>(1);
            second.EmitSignal(Button.SignalName.Pressed); second.GrabFocus();
            var next = selection.GetNode<TextureRect>("CurrentPortrait").Texture;
            selection.Render("刷新", choices, 7);
            if (next == initial || submitted != 0 || selection.GetNode<TextureRect>("CurrentPortrait").Texture != next
                || entries.GetChild<Button>(1) != second || !second.HasFocus()) return false;
            selection._Input(new InputEventKey { Keycode = Key.Left, Pressed = true });
            if (selection.GetNode<TextureRect>("CurrentPortrait").Texture != initial) return false;
            selection._Input(new InputEventKey { Keycode = Key.Enter, Pressed = true });
            if (submitted != 1 || submittedKey != choices[0].Key || submittedRevision != 7) return false;
            var custom = choices[1] with { Hero = choices[1].Hero! with { MaxHealth = 321, Income = 9 } };
            selection.Render("属性副本", [custom]);
            if (selection.GetNode<GridContainer>("Stats").GetChild<Label>(1).Text != "321"
                || selection.GetNode<GridContainer>("Stats").GetChild<Label>(5).Text != "9") return false;
            second.EmitSignal(Button.SignalName.Pressed);
            if (submitted != 1) return false;
            var toggle = selection.GetNode<Button>("Attributes");
            toggle.ButtonPressed = false;
            if (selection.GetNode<GridContainer>("Stats").Visible) return false;
            toggle.ButtonPressed = true;
            selection.Render("不可选", [custom with { Action = new UiAction("不可选", Enabled: false, Reason: "验证禁用") }]);
            selection.GetNode<Button>("Choose").EmitSignal(Button.SignalName.Pressed);
            if (submitted != 1 || !selection.GetNode<Button>("Choose").Disabled) return false;
            selection.Render("空名单", []);
            if (entries.GetChildCount() != 0 || selection.GetNode<TextureRect>("CurrentPortrait").Texture is not null
                || selection.GetNode<GridContainer>("Stats").Visible) return false;
            panel.Render(null);
            return panel.GetNode<TextureRect>("Identity/Portrait").Texture is null
                && typeof(HeroIdentityAttributes).GetProperty(nameof(HeroIdentityAttributes.Illustration))!.SetMethod is null;
        }
        finally { owner.RemoveChild(panel); panel.Free(); owner.RemoveChild(selection); selection.Free(); }
    }
}
