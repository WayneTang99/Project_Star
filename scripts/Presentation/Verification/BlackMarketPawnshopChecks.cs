using System.Linq;
using System;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
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
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 黑市典当铺分级交易、价值光环、来源失效与冻结回放验证（表现层）。
internal static class BlackMarketPawnshopChecks
{
    internal static bool SaleGrowthAndAura()
    {
        var definition = new BlackMarketPawnshopCardDefinition();
        var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.DisplayName != "黑市典当铺" || identity.FactionKey != new StringName("valos")
            || identity.Size != CardSize.Large || definition.InitialLevel != 1 || definition.SupportsLevel(5)
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !definition.Tags.Contains(GameTags.Location) || texture is null
            || texture.GetWidth() * 2 != texture.GetHeight() * 3
            || !DefinitionRegistry.Scan(typeof(BlackMarketPawnshopChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        foreach (var (level, percent) in new[] { (1, 10), (2, 15), (3, 20), (4, 30) })
        {
            var factory = new EntityFactory();
            var board = new BoardService(new BoardPlacementSolver());
            var economy = new CardEconomyService(factory, board);
            var match = new CreateMatchService(factory).Create(42, 100, new ValosHeroDefinition());
            var enemy = new CreateMatchService(factory).Create(43, 100, new ValosHeroDefinition());
            enemy.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, 10000);
            var field = factory.CreateCard(definition, level);
            var bench = factory.CreateCard(definition, level);
            var attack = factory.CreateCard(new FlangedMaceCardDefinition());
            var idle = factory.CreateCard(new SmallRedPotionCardDefinition());
            foreach (var card in new[] { field, bench, attack, idle }) match.Player.Inventory.Add(card);
            if (board.PlaceCard(match, field.Id, BoardZone.Battlefield, 0).IsFailure
                || board.PlaceCard(match, attack.Id, BoardZone.Battlefield, 3).IsFailure
                || board.PlaceCard(match, idle.Id, BoardZone.Battlefield, 4).IsFailure
                || board.PlaceCard(match, bench.Id, BoardZone.Bench, 0).IsFailure) return false;
            field.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, 7);
            bench.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, 7);
            var frozen = new BattleSetupFactory().Create(match, enemy, 42, new BattleTick(50));
            for (var sale = 0; sale < 2; sale++)
            {
                var sold = factory.CreateCard(new BeastHideCardDefinition());
                sold.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, 17);
                match.Player.Inventory.Add(sold);
                if (economy.SellCard(match, sold.Id).IsFailure || economy.SellCard(match, sold.Id).IsSuccess) return false;
            }
            var grown = 7 + 2 * (17 * percent / 100 + 1);
            if (field.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value) != grown
                || bench.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value) != grown) return false;
            foreach (var (setup, expected) in new[] { (frozen, 17),
                (new BattleSetupFactory().Create(match, enemy, 42, new BattleTick(50)), 10 + grown) })
            {
                var result = new CombatSimulator().Simulate(setup);
                if (result.Events.OfType<DamageDealtEvent>().Single(hit => hit.SourceCardId == attack.Id).RawDamage != expected
                    || result.States[0].Cards.Single(card => card.Id == idle.Id).Values[GameAttributeKeys.AttackDamage] != 0
                    || result.PermanentChanges.Count != 0
                    || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
                var source = setup.Player.Cards.Single(card => card.EntityId == field.Id);
                var destroyed = source with { Abilities = source.Abilities!.Concat(new[] {
                    new AbilityDefinition(new StringName("ability.verify_destroy_source"), AbilityActivation.PassiveOnBattleStart,
                        AbilityTarget.SelfCard, 0, 0, new EffectDefinition[] { new DestroyCardEffectDefinition(false) }) }).ToArray() };
                var removed = new BattleSetup(new BattleSideSetup(setup.Player.Hero,
                    setup.Player.Cards.Select(card => card.EntityId == field.Id ? destroyed : card).ToArray()),
                    setup.Opponent, setup.Seed, setup.Timeout, setup.EclipseTime);
                if (new CombatSimulator().Simulate(removed).Events.OfType<DamageDealtEvent>()
                    .Single(hit => hit.SourceCardId == attack.Id).RawDamage != 10) return false;
            }
            factory.ApplyCardLevel(field, definition, 4);
            if (field.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value) != grown) return false;
            var zero = factory.CreateCard(new BeastHideCardDefinition()); match.Player.Inventory.Add(zero);
            zero.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, 0);
            if (economy.SellCard(match, zero.Id).IsFailure
                || field.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value) != grown + 1) return false;
            field.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, int.MaxValue - (grown + 1 - 7));
            var overflow = factory.CreateCard(new BeastHideCardDefinition()); match.Player.Inventory.Add(overflow);
            var wealth = match.Player.Wealth;
            if (economy.SellCard(match, overflow.Id).IsSuccess || match.Player.Wealth != wealth
                || match.Player.Inventory.Find(overflow.Id) is null) return false;
        }
        return true;
    }

    internal static async Task Capture(Control owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(648, 448), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(648, 448) });
            var face = new CardItemView { Position = new Vector2(24, 24), Size = new Vector2(600, 400) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new BlackMarketPawnshopCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/black-market-pawnshop/card-face.png") != Error.Ok)
                throw new InvalidOperationException("黑市典当铺卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }
}
