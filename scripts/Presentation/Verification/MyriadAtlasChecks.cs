using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Content;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

internal static class MyriadAtlasChecks
{
    internal static async Task Capture(Node owner)
    {
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new MyriadAtlasCardDefinition()));
        var viewport = new SubViewport { Size = new Vector2I(650, 440), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        var preview = new CardPreviewView { Size = owner.GetViewport().GetVisibleRect().Size, Theme = MatchTheme.Create() };
        owner.AddChild(preview);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(650, 440) });
            foreach (var (position, size) in new[] { (new Vector2(20, 20), new Vector2(400, 400)),
                (new Vector2(460, 20), new Vector2(100, 100)) })
            {
                var face = new CardItemView { Position = position, Size = size };
                viewport.AddChild(face); face.Render(snapshot, new CardDisplayAdapter());
            }
            using var artwork = GD.Load<Texture2D>(snapshot.Illustration.ToString()).GetImage();
            GD.Print($"万象舆图原画 {artwork.GetWidth()}×{artwork.GetHeight()} mipmap={artwork.GetMipmapCount()}");
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(MyriadAtlasChecks).Assembly)));
            preview.Open(snapshot, null, null);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw();
            using var faceImage = viewport.GetTexture().GetImage();
            using var detailImage = owner.GetViewport().GetTexture().GetImage();
            faceImage.SavePng("res://output/myriad-atlas/card-face.png");
            detailImage.SavePng("res://output/myriad-atlas/preview.png");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); owner.RemoveChild(preview); preview.Free(); }
    }

    private static bool Fail([System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    { GD.Print($"万象舆图检查失败，行号：{line}"); return false; }

    internal static bool GrowthAndQuest()
    {
        var definition = new MyriadAtlasCardDefinition();
        var identity = definition.Attributes.Identity;
        using var image = GD.Load<Texture2D>(identity.Illustration.ToString()).GetImage();
        if (identity.DisplayName != "万象舆图" || identity.FactionKey != new StringName("robin")
            || identity.Size != CardSize.Medium || definition.InitialLevel != 1 || definition.SupportsLevel(5)
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !DefinitionRegistry.Scan(typeof(MyriadAtlasChecks).Assembly).Cards.ContainsKey(identity.Key)
            || image.GetWidth() != image.GetHeight() || !image.HasMipmaps()) return Fail();
        foreach (var level in Enumerable.Range(1, 4))
        {
            var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
            var economy = new CardEconomyService(factory, board); var matches = new CreateMatchService(factory);
            var session = matches.Create(42, 100, new RobinHeroDefinition());
            var opponent = matches.Create(43, 100, new RobinHeroDefinition());
            var source = economy.AcquireAndPlace(session, definition, level, CardAcquisitionSource.Reward).Value!.Card;
            var bench = factory.CreateCard(definition, level); session.Player.Inventory.Add(bench);
            if (board.PlaceCard(session, bench.Id, BoardZone.Bench, 0).IsFailure) return Fail();
            int Value() => source.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value);
            int Progress() => source.GetQuestProgress(source.Quests.Single().Key);
            BattleSetup Setup() => new BattleSetupFactory().Create(session, opponent, 42, new BattleTick(1), new BattleTick(100));
            var initial = Value(); var benchInitial = bench.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value);
            var frozenLocked = Setup();
            // 非地域不成长、不推进；成功地域获得及出售各计一次，重复出售不计。
            var other = economy.AcquireCard(session, new BeastHideCardDefinition(), 1, CardAcquisitionSource.Reward).Value!.Card;
            if (economy.SellCard(session, other.Id).IsFailure || Value() != initial || Progress() != 0) return Fail();
            for (var i = 1; i <= 5; i++)
            {
                var location = economy.AcquireCard(session, new WheatFieldCardDefinition(), 2, CardAcquisitionSource.Reward).Value!.Card;
                if (Value() != initial + i * level || economy.SellCard(session, location.Id).IsFailure
                    || economy.SellCard(session, location.Id).IsSuccess || Progress() != i) return Fail();
                var health = new CombatSimulator().Preview(Setup()).Player;
                if (health.MaxHealth != 200 + (i == 5 ? Value() * 20 : 0)) return Fail();
            }
            if (bench.GetQuestProgress(bench.Quests.Single().Key) != 5
                || bench.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value) != benchInitial + 5 * level
                || new CombatSimulator().Preview(frozenLocked).Player.MaxHealth != 200) return Fail();
            var frozen = Setup(); var frozenValue = Value();
            factory.ApplyCardLevel(source, definition, 4);
            if (Progress() != 5) return Fail();
            var before = Value();
            _ = economy.AcquireCard(session, new WheatFieldCardDefinition(), 2, CardAcquisitionSource.Reward);
            if (Value() != before + 4 || new CombatSimulator().Preview(frozen).Player.MaxHealth != 200 + frozenValue * 20
                || new CombatSimulator().Preview(Setup()).Player.MaxHealth != 200 + Value() * 20) return Fail();
            if (board.PlaceCard(session, source.Id, BoardZone.Bench, 2).IsFailure
                || new CombatSimulator().Preview(Setup()).Player.MaxHealth != 200) return Fail();
            if (board.PlaceCard(session, source.Id, BoardZone.Battlefield, 0).IsFailure) return Fail();
            var setup = Setup();
            var sourceSetup = setup.Player.Cards.Single(card => card.EntityId == source.Id);
            var destroy = new AbilityDefinition(new StringName("ability.verify_atlas_destroy"),
                AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)]);
            var destroyed = new BattleSetup(new BattleSideSetup(setup.Player.Hero, setup.Player.Cards.Select(card =>
                card.EntityId == source.Id ? sourceSetup with { Abilities = sourceSetup.Abilities!.Append(destroy).ToArray() } : card).ToArray()),
                setup.Opponent, 42, new BattleTick(1), new BattleTick(100));
            var result = new CombatSimulator().Simulate(destroyed);
            var afterOpening = result.States.Last(state => state.Tick.Value == 0).Player;
            if (afterOpening.MaxHealth != 200 || afterOpening.Health != 200
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(destroyed).Events)) return Fail();
        }
        return true;
    }
}
