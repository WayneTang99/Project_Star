using System;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Verification;

// 宝石孔、镶嵌事务、合并保留与卡面展示的机制验证（表现层验证模块）。
internal static class GemSocketChecks
{
    internal static bool Lifecycle(Control owner)
    {
        var factory = new EntityFactory();
        var session = new MatchSession(42);
        var service = new SocketGemService();
        var definition = new SocketCard(1);
        var card = factory.CreateCard(definition);
        session.Player.Inventory.Add(card);
        var gem = new TestGem("red", "测试红宝石");
        var command = new SocketGemCommand(session.Id, card.Id, 0, gem);
        var before = MatchSnapshot.From(session);
        if (card.GemSockets.Count != 1 || card.GemSockets[0] is not null
            || MatchDisplayQuery.FromOffer(ShopOffer.Create(definition)).GemSockets.Count != 1
            || service.Execute(session, command with { MatchId = Guid.NewGuid() }).IsSuccess
            || service.Execute(session, command with { CardId = EntityId.New() }).IsSuccess
            || service.Execute(session, command with { SocketIndex = -1 }).IsSuccess
            || service.Execute(session, command with { SocketIndex = 1 }).IsSuccess
            || service.Execute(session, command).IsFailure) return false;
        var filled = MatchSnapshot.From(session).Cards.Single();
        if (before.Cards.Single().GemSockets[0] is not null || filled.GemSockets[0]?.DisplayName != "测试红宝石"
            || service.Execute(session, command with { Gem = new TestGem("blue", "测试蓝宝石") }).IsSuccess
            || card.GemSockets[0]?.Key != gem.Attributes.Identity.Key) return false;
        var material = factory.CreateCard(definition, 2);
        session.Player.Inventory.Add(material);
        if (service.Execute(session, command with { CardId = material.Id, Gem = new TestGem("blue", "测试蓝宝石") }).IsFailure) return false;
        var economy = new CardEconomyService(factory, new BoardService(new BoardPlacementSolver()));
        var merged = economy.AcquireCard(session, definition, 1, CardAcquisitionSource.Reward);
        if (merged.IsFailure || merged.Value!.Card.Id != material.Id || session.Player.Inventory.Cards.Count != 1
            || material.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 3
            || material.GemSockets[0]?.Key != new StringName("verification.gem.blue")
            || session.Player.Inventory.Find(card.Id) is not null) return false;
        if (economy.SellCard(session, material.Id).IsFailure || session.Player.Inventory.Cards.Count != 0
            || service.Execute(session, command).IsSuccess || filled.GemSockets[0]?.DisplayName != "测试红宝石") return false;
        var multi = factory.CreateCard(new SocketCard(3)); session.Player.Inventory.Add(multi);
        if (service.Execute(session, command with { CardId = multi.Id, SocketIndex = 2 }).IsFailure) return false;
        var snapshot = MatchSnapshot.From(session).Cards.Single();
        var face = GD.Load<PackedScene>("res://scripts/Presentation/CardFace/CardFace.tscn")
            .Instantiate<Project_Star.Presentation.CardFace.CardFace>();
        owner.AddChild(face);
        try
        {
            face.SetCard(new CardDisplayAdapter().Build(snapshot));
            var sockets = face.GetNode<Control>("GemSockets");
            var details = CardDisplayAdapter.Details(snapshot);
            if (sockets.GetChildCount() != 3 || sockets.GetChildren().Any(child => child is not Panel)
                || sockets.GetChild<Panel>(0).GetThemeStylebox("panel") is not StyleBoxTexture empty
                || sockets.GetChild<Panel>(2).GetThemeStylebox("panel") is not StyleBoxTexture filledStyle
                || empty.Texture == filledStyle.Texture
                || sockets.GetChildren().Cast<Panel>().Any(socket => socket.RotationDegrees != 45)
                || !details.Contains("孔1：空孔") || !details.Contains("孔3：测试红宝石")) return false;
            face.SetCard(new CardDisplayAdapter().Build(MatchDisplayQuery.FromOffer(ShopOffer.Create(new SocketCard(0)))));
            if (sockets.GetChildCount() != 0 || sockets.Visible) return false;
            foreach (var level in Enumerable.Range(1, 5))
            {
                face.SetCard(new CardDisplayAdapter().Build(snapshot with { Level = level }));
                if (face.HasNode("LevelGem") || face.GetNode<Panel>("Frame").GetThemeStylebox("panel") is not StyleBoxFlat frame
                    || frame.BorderColor != CardLevelGem.LevelColor(level)) return false;
            }
        }
        finally { owner.RemoveChild(face); face.Free(); }
        session.Status = MatchStatus.Won;
        if (service.Execute(session, command with { CardId = multi.Id }).IsSuccess) return false;
        var registry = DefinitionRegistry.Create([gem]);
        if (!registry.Gems.ContainsKey(gem.Attributes.Identity.Key)) return false;
        try { DefinitionRegistry.Create([gem, new TestGem("red", "重复")]); return false; }
        catch (DefinitionValidationException) { }
        try { _ = new SocketCard(-1); return false; }
        catch (ArgumentOutOfRangeException) { }
        return factory.CreateCard(new SocketCard(1)).GemSockets.All(item => item is null);
    }

    // 仅验证孔数配置与合并，不注册为正式内容。
    private sealed class SocketCard : CardDefinition
    {
        internal SocketCard(int count) : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("verification.socket_card"), "宝石孔测试卡", GameFactions.Neutral, CardSize.Medium,
            [GameElements.General], gemSocketCount: count)), new TagSet(),
            levels: Enumerable.Range(1, 4).Select(level => new CardLevelDefinition(level, null, [])).ToArray()) { }
    }

    // 无玩法效果的宝石夹具，不进入正式宝石目录。
    private sealed class TestGem : GemDefinition
    {
        internal TestGem(string suffix, string name) : base(new EntityAttributes<GemIdentityAttributes>(
            new GemIdentityAttributes(new StringName("verification.gem." + suffix), name, "仅用于机制验证"))) { }
    }
}
