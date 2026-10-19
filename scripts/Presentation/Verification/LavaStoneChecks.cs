using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Content;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 熔岩石等级、出售实际灼伤成长与原生卡面验证（表现层）。
internal static class LavaStoneChecks
{
    internal static bool LevelsAndSales()
    {
        var definition = new LavaStoneCardDefinition(); var identity = definition.Attributes.Identity;
        using var image = GD.Load<Texture2D>(identity.Illustration.ToString()).GetImage();
        if (!DefinitionRegistry.Scan(typeof(LavaStoneCardDefinition).Assembly).Cards.ContainsKey(identity.Key)
            || identity.Key != new StringName("card.lava_stone") || identity.DisplayName != "熔岩石"
            || identity.FactionKey != new StringName("neutral") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Fire }) || definition.InitialLevel != 1
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Material)
            || definition.SupportsLevel(5) || image.GetHeight() != image.GetWidth() * 2 || !image.HasMipmaps()) return false;
        GD.Print($"熔岩石原画 {image.GetWidth()}×{image.GetHeight()} mipmaps={image.GetMipmapCount()}");
        foreach (var level in new[] { 1, 2, 3, 4 })
        foreach (var zone in new[] { BoardZone.Battlefield, BoardZone.Bench })
        {
            var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
            var economy = new CardEconomyService(factory, board);
            var session = new CreateMatchService(factory).Create(1, 50, new MonaHeroDefinition());
            var enemy = new CreateMatchService(factory).Create(2, 0, new MonaHeroDefinition());
            var target = Add(new FireglowJewelBeetleCardDefinition(), 1, BoardZone.Battlefield, 3);
            var right = Add(new FireglowJewelBeetleCardDefinition(), 1, BoardZone.Battlefield, 6);
            var bench = Add(new FireglowJewelBeetleCardDefinition(), 1, BoardZone.Bench, 0);
            Add(new WhetstoneCardDefinition(), 1, BoardZone.Battlefield, 1);
            var sold = Add(definition, level, zone, 2);
            if (sold.Abilities.Count != 0 || sold.OnSellReward is not IncreaseLeftmostBurnCardOnSellDefinition { Amount: var amount }
                || amount != level) return false;
            var before = new BattleSetupFactory().Create(session, enemy, 9, new BattleTick(40));
            var wealth = session.Player.Wealth; var value = sold.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value);
            if (economy.SellFromBoard(session, sold.Id).IsFailure || session.Player.Wealth != wealth + value
                || session.Player.Inventory.Find(sold.Id) is not null || economy.SellCard(session, sold.Id).IsSuccess) return false;
            var setup = new BattleSetupFactory().Create(session, enemy, 9, new BattleTick(40));
            var simulator = new CombatSimulator(); var result = simulator.Simulate(setup);
            var burns = result.Events.OfType<StatusChangedEvent>().Where(item => item.Status == BattleStatus.Burn).ToArray();
            if (burns.Length != 2 || burns[0].Amount != 3 + level || burns[1].Amount != 3
                || !result.Events.SequenceEqual(simulator.Simulate(setup).Events)
                || simulator.Simulate(before).Events.OfType<StatusChangedEvent>()
                    .Where(item => item.Status == BattleStatus.Burn).Any(item => item.Amount != 3)
                || CardDisplayAdapter.FaceEffects(MatchSnapshot.From(session).Cards.Single(card => card.Id == bench.Id))
                    .Single(effect => effect.Kind == CardFaceEffectKind.Burn).Value != "3") return false;

            CardInstance Add(CardDefinition content, int cardLevel, BoardZone location, int start)
            {
                var card = factory.CreateCard(content, cardLevel); session.Player.Inventory.Add(card);
                if (board.PlaceCard(session, card.Id, location, start).IsFailure)
                    throw new InvalidOperationException("熔岩石验证放置失败。");
                return card;
            }
        }
        return true;
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(620, 450), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(620, 450) });
            foreach (var (level, x, width) in new[] { (1, 20, 200), (4, 240, 200), (1, 470, 100) })
            {
                var face = new CardItemView { Position = new Vector2(x, 20), Size = new Vector2(width, width * 2) };
                viewport.AddChild(face);
                face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new LavaStoneCardDefinition(), level)), new CardDisplayAdapter());
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw(false);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/lava-stone/card-face.png") != Error.Ok)
                throw new InvalidOperationException("熔岩石截图失败。");
            var preview = new CardPreviewView { Size = owner.GetViewport().GetVisibleRect().Size, Theme = MatchTheme.Create() };
            owner.AddChild(preview);
            try
            {
                preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(LavaStoneChecks).Assembly)));
                preview.Open(MatchDisplayQuery.FromOffer(ShopOffer.Create(new LavaStoneCardDefinition())), null, null);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var large = owner.GetViewport().GetTexture().GetImage();
                if (large.SavePng("res://output/lava-stone/preview.png") != Error.Ok)
                    throw new InvalidOperationException("熔岩石预览截图失败。");
                preview.Close();
            }
            finally { owner.RemoveChild(preview); preview.Free(); }
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }
}
