using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Content.Monsters;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Infrastructure.Encounters;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

internal static class WindmillDaisyChecks
{
    internal static bool LevelsHealingAndSales()
    {
        var definition = new WindmillDaisyCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.FactionKey != GameFactions.Neutral || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Wind, GameElements.Wood })
            || !definition.Tags.Contains(GameTags.Plant) || definition.InitialLevel != 1 || definition.SupportsLevel(5)) return false;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (texture is null || texture.GetHeight() != texture.GetWidth() * 2) return false;
        foreach (var level in Enumerable.Range(1, 4))
        {
            var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
            var economy = new CardEconomyService(factory, board);
            var matches = new CreateMatchService(factory);
            var session = matches.Create(1, 50, new PaladinHeroDefinition());
            var opponent = matches.Create(2, 0, new PaladinHeroDefinition());
            var card = economy.AcquireCard(session, definition, level, CardAcquisitionSource.Reward).Value!.Card;
            if (board.PlaceCard(session, card.Id, BoardZone.Battlefield, 0).IsFailure) return false;
            var ability = card.Abilities.Single();
            var healing = 10 << (level - 1);
            if (ability.ManaCost != 0 || ability.CooldownTicks != 40
                || ability.Effects.Single() is not HealEffectDefinition { Amount: var amount } || amount != healing) return false;
            var frozen = new BattleSetupFactory().Create(session, opponent, 42, new BattleTick(41), new BattleTick(42));
            var injured = new BattleSetup(new BattleSideSetup(frozen.Player.Hero with { Poison = 30 }, frozen.Player.Cards),
                frozen.Opponent, frozen.Seed, frozen.Timeout, frozen.EclipseTime);
            var battle = new CombatSimulator().Simulate(injured);
            if (battle.States.Last(frame => frame.Tick.Value == 40).Player.Health != 80 + healing
                || battle.Events.OfType<AbilityActivatedEvent>().Count(item => item.SourceCardId == card.Id) != 1) return false;
            var before = session.Player.Hero!.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth);
            if (economy.SellFromBoard(session, card.Id).IsFailure
                || session.Player.Hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth) != before + level * 10
                || economy.SellFromBoard(session, card.Id).IsSuccess
                || frozen.Player.Hero.MaxHealth != before
                || new BattleSetupFactory().Create(session, opponent, 42, new BattleTick(1)).Player.Hero.MaxHealth != before + level * 10)
                return false;
        }
        var registry = DefinitionRegistry.Scan(typeof(WindmillDaisyCardDefinition).Assembly);
        var baa = new LocalTestOpponentProvider(registry).CreateMonsterOpponent(1, registry.Monsters[new StringName("monster.baa")]);
        var player = new CreateMatchService(new EntityFactory()).Create(1, 0, new PaladinHeroDefinition());
        var setup = new BattleSetupFactory().Create(player, baa, 42, new BattleTick(41), new BattleTick(42));
        var damaged = new BattleSetup(setup.Player, new BattleSideSetup(setup.Opponent.Hero with { Poison = 10 },
            setup.Opponent.Cards, setup.Opponent.Skills), setup.Seed, setup.Timeout, setup.EclipseTime);
        return new CombatSimulator().Simulate(damaged).States.Last(frame => frame.Tick.Value == 40).Opponent.Health == 80;
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(820, 500), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            var backdrop = new ColorRect { Size = viewport.Size, Color = new Color("182b24") }; viewport.AddChild(backdrop);
            var face = new CardItemView { Position = new Vector2(30, 40), Size = new Vector2(200, 400) }; viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new WindmillDaisyCardDefinition())), new CardDisplayAdapter());
            var elements = new[] { GameElements.General, GameElements.Fire, GameElements.Water, GameElements.Wind,
                GameElements.Earth, GameElements.Lightning, GameElements.Wood, GameElements.Ice, GameElements.Light, GameElements.Dark };
            var names = new[] { "无", "火", "水", "风", "土", "雷", "木", "冰", "光", "暗" };
            for (var index = 0; index < elements.Length; index++)
            {
                var point = new Vector2(280 + index % 5 * 100, 90 + index / 5 * 190);
                var large = new CardElementBadge { Position = point, Size = new Vector2(23, 23), Scale = Vector2.One * 2.8f };
                viewport.AddChild(large); large.SetElement(elements[index]);
                var small = new CardElementBadge { Position = point + new Vector2(20, 90), Size = new Vector2(23, 23) };
                viewport.AddChild(small); small.SetElement(elements[index]);
                viewport.AddChild(new Label { Position = point + new Vector2(20, 125), Text = names[index] });
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/windmill-daisy/card-and-elements.png") != Error.Ok)
                throw new InvalidOperationException("Windmill daisy capture failed.");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }
}
