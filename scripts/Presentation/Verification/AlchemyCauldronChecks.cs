using System.Linq;
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
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Verification;

// 炼金釜出售被动、永久成长及战斗冻结输入验证（表现层验证模块）。
internal static class AlchemyCauldronChecks
{
    internal static bool SaleGrowthAndBattle()
    {
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
        var economy = new CardEconomyService(factory, board);
        var definition = new AlchemyCauldronCardDefinition();
        if (definition.InitialLevel != 2 || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || definition.Attributes.Identity.FactionKey != new StringName("mona")
            || definition.Attributes.Identity.Size != CardSize.Medium
            || !definition.Attributes.Identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.Tags.Count != 1) return false;
        foreach (var (level, bonus) in new[] { (2, 6), (3, 12), (4, 20) })
        {
            var match = new CreateMatchService(factory).Create(42, 100, new MonaHeroDefinition());
            var enemy = new CreateMatchService(factory).Create(43, 100, new MonaHeroDefinition());
            var field = factory.CreateCard(definition, level); var bench = factory.CreateCard(definition, level);
            var loose = factory.CreateCard(definition, level);
            foreach (var card in new[] { field, bench, loose }) match.Player.Inventory.Add(card);
            board.PlaceCard(match, field.Id, BoardZone.Battlefield, 0);
            board.PlaceCard(match, bench.Id, BoardZone.Bench, 0);
            var original = new BattleSetupFactory().Create(match, enemy, 42, new BattleTick(60));
            for (var sale = 0; sale < 2; sale++)
            {
                var potion = factory.CreateCard(new SmallRedPotionCardDefinition());
                match.Player.Inventory.Add(potion);
                board.PlaceCard(match, potion.Id, BoardZone.Bench, 2);
                if (economy.SellFromBoard(match, potion.Id).IsFailure
                    || economy.SellFromBoard(match, potion.Id).IsSuccess) return false;
            }
            if (loose.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Poison) != 10
                || new CombatSimulator().Simulate(original).Events.OfType<StatusChangedEvent>().Any(item => item.Amount != 10)) return false;
            foreach (var card in new[] { field, bench })
                if (card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Poison) != 10 + 2 * bonus
                    || card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Burn) != 10 + 2 * bonus) return false;
            var snapshot = MatchSnapshot.From(match);
            var effects = CardDisplayAdapter.FaceEffects(snapshot.Cards.Single(card => card.Id == field.Id))
                .Where(effect => effect.Kind is CardFaceEffectKind.Poison or CardFaceEffectKind.Burn).ToArray();
            if (effects.Length != 2 || effects.Any(effect => effect.Value != (10 + 2 * bonus).ToString())) return false;
            var material = factory.CreateCard(new BeastHideCardDefinition());
            match.Player.Inventory.Add(material);
            if (economy.SellCard(match, material.Id).IsFailure
                || field.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Poison) != 10 + 2 * bonus) return false;
            var setup = new BattleSetupFactory().Create(match, enemy, 42, new BattleTick(60));
            var result = new CombatSimulator().Simulate(setup);
            if (result.Events.OfType<StatusChangedEvent>().Count() != 2
                || result.Events.OfType<StatusChangedEvent>().Any(item => item.Amount != 10 + 2 * bonus)
                || result.Events.OfType<AbilityActivatedEvent>().Any(item => item.IsEcho)
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            factory.ApplyCardLevel(field, definition, 4);
            if (field.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Burn) != 10 + 2 * bonus
                || field.SaleAttributeBonuses.Any(item => item.Amount != 20)) return false;
            var manaPotion = factory.CreateCard(new SmallManaPotionCardDefinition());
            match.Player.Inventory.Add(manaPotion);
            if (economy.SellCard(match, manaPotion.Id).IsFailure
                || field.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Burn) != 30 + 2 * bonus
                || bench.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Burn) != 10 + 3 * bonus) return false;
            var overflowPotion = factory.CreateCard(new SmallRedPotionCardDefinition());
            match.Player.Inventory.Add(overflowPotion);
            field.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.Poison, int.MaxValue - 2 * bonus - 20);
            var wealth = match.Player.Wealth;
            if (economy.SellCard(match, overflowPotion.Id).IsSuccess
                || match.Player.Wealth != wealth || match.Player.Inventory.Find(overflowPotion.Id) is null) return false;
        }
        return true;
    }
}
