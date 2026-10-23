using Project_Star.Domain.Match;
using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Combat;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

internal static class IceSpikeWormChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new IceSpikeWormCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.Key != new StringName("card.ice_spike_worm") || identity.DisplayName != "冰晶蝶"
            || identity.FactionKey != new StringName("mona") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Ice })
            || !definition.Tags.Contains(GameTags.Insect) || definition.InitialLevel != 1 || definition.SupportsLevel(5)) return false;
        for (var level = 1; level <= 4; level++)
        foreach (var reverse in new[] { false, true })
        {
            var card = new EntityFactory().CreateCard(definition, level);
            if (card.OnSellReward is not ReduceLeftmostElementCardCooldownOnSellDefinition { Percent: var percent, ElementKey: var element }
                || percent != level || element != GameElements.Ice || card.Abilities.Single().CooldownTicks != 40
                || card.Abilities.Single().ManaCost != 0) return false;
            var source = new CardBattleSetup(card.Id, 0, 0, 40, card.Abilities, UseLegacyAttack: false);
            var target = new CardBattleSetup(EntityId.New(), 0, 0, 0, [], UseLegacyAttack: false);
            var ally = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0), [source]);
            var enemy = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0), [target, target with { EntityId = EntityId.New(), BoardStart = 1, IsOnBench = true }]);
            var setup = new BattleSetup(reverse ? enemy : ally, reverse ? ally : enemy, 42, new BattleTick(50));
            var result = new CombatSimulator().Simulate(setup);
            var slow = result.Events.OfType<StatusChangedEvent>().Where(e => e.Status == BattleStatus.SlowDuration && e.Amount > 0).ToArray();
            if (slow.Length != 1 || slow[0].Amount != 10 || slow[0].Tick.Value != 40
                || result.States.Last(s => s.Tick.Value == 40).Cards.Single(c => c.Id == target.EntityId).Slow != 10
                || result.States[^1].Cards.Single(c => c.Id == target.EntityId).Slow != 0
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
        }
        return BlackRhinocerosBeetleChecks.SaleTargetsAndPersistence(definition, GameElements.Ice)
            && BlackRhinocerosBeetleChecks.LevelsAndSlow() && CombatTraceChecks.PreservesTraces();
    }

    internal static async Task Capture(Control owner)
    {
        var details = new CardDetailsView { Theme = MatchTheme.Create() };
        owner.AddChild(details);
        try
        {
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            {
                owner.GetWindow().Size = size;
                owner.GetTree().Root.ContentScaleSize = size;
                var card = MatchDisplayQuery.FromOffer(ShopOffer.Create(new LogStaffCardDefinition()));
                details.SetPinned(true);
                details.ShowNear(card, new Rect2(560, 240, 90, 140), size);
                await Frames();
                var scroll = details.GetNode<ScrollContainer>("Content");
                var content = details.GetNode<CardDetailsContent>("Content/CardContent");
                var body = content.GetNode<ScrollContainer>("BodyScroll");
                var quests = content.GetNode<VBoxContainer>("BodyScroll/Paragraphs/Quests");
                if (!content.ExpandContent || body.VerticalScrollMode != ScrollContainer.ScrollMode.Disabled
                    || quests.GetChildren().OfType<PanelContainer>().Count() != 3
                    || quests.GetChildren().OfType<PanelContainer>().Any(row => row.Size.Y < 90)
                    || details.GetGlobalRect().End.Y > size.Y - 10) throw new InvalidOperationException("任务展开或详情边界失败。");
                await Save($"quest-{size.X}-top");
                var close = details.GetNode<Button>("Close").GlobalPosition;
                scroll.ScrollVertical = (int)scroll.GetVScrollBar().MaxValue;
                await Frames();
                if (scroll.GetVScrollBar().MaxValue > scroll.GetVScrollBar().Page && scroll.ScrollVertical == 0
                    || details.GetNode<Button>("Close").GlobalPosition != close) throw new InvalidOperationException("整体滚动或固定关闭按钮失败。");
                var last = quests.GetChildren().OfType<PanelContainer>().Last();
                if (!scroll.GetGlobalRect().Intersects(last.GetGlobalRect())) throw new InvalidOperationException("末项任务无法滚动到。");
                await Save($"quest-{size.X}-bottom");
                var previousScroll = scroll.ScrollVertical;
                details.RefreshCard(card, size); await Frames();
                if (previousScroll > 0 && scroll.ScrollVertical == 0) throw new InvalidOperationException($"刷新丢失滚动位置 {size}，之前{previousScroll}，最大{scroll.GetVScrollBar().MaxValue}，视口{scroll.GetVScrollBar().Page}。");
                var closePoint = details.GetNode<Button>("Close").GetGlobalRect().GetCenter();
                if (size.X == 1280)
                {
                    owner.GetViewport().PushInput(new InputEventMouseMotion { Position = closePoint, GlobalPosition = closePoint });
                    owner.GetViewport().PushInput(new InputEventMouseButton { Position = closePoint, GlobalPosition = closePoint, ButtonIndex = MouseButton.Left, Pressed = true });
                    owner.GetViewport().PushInput(new InputEventMouseButton { Position = closePoint, GlobalPosition = closePoint, ButtonIndex = MouseButton.Left, Pressed = false });
                }
                else
                {
                    details.GetNode<Button>("Close").GrabFocus();
                    owner.GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = true });
                    owner.GetViewport().PushInput(new InputEventKey { Keycode = Key.Enter, Pressed = false });
                }
                await Frames();
                if (details.Visible) throw new InvalidOperationException("关闭失败。");
            }
            var worm = MatchDisplayQuery.FromOffer(ShopOffer.Create(new IceSpikeWormCardDefinition()));
            using var art = GD.Load<Texture2D>(worm.Illustration.ToString()).GetImage();
            if (art.GetWidth() * 2 != art.GetHeight() || !art.HasMipmaps()) throw new InvalidOperationException("原画比例或mipmap失败。");
            GD.Print($"冰晶蝶原画 {art.GetWidth()}×{art.GetHeight()} mipmap={art.GetMipmapCount()}");
            var face = new CardItemView { Position = new Vector2(30, 40), Size = new Vector2(200, 400) }; owner.AddChild(face);
            face.Render(worm, new CardDisplayAdapter());
            details.ShowCard(worm, new Vector2(270, 40), owner.GetViewportRect().Size);
            await Frames(); await Save("worm-card");
            face.QueueFree();
            GD.Print("整体详情滚动、三项任务展开、边界、刷新和关闭验证通过。");
        }
        finally { owner.RemoveChild(details); details.Free(); }
        async Task Frames() { for (var i = 0; i < 18; i++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame); RenderingServer.ForceDraw(); }
        Task Save(string name) { using var image = owner.GetViewport().GetTexture().GetImage(); image.SavePng($"res://output/quest-whole-scroll/{name}.png"); return Task.CompletedTask; }
    }
}
