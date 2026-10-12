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
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 弗兰肯特材料出售成长、多重完整发动和升级保留验证（表现层）。
internal static class FrankentChecks
{
    internal static bool SaleGrowthAndBattle()
    {
        var definition = new FrankentCardDefinition(); var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.DisplayName != "弗兰肯特" || identity.FactionKey != new StringName("harla")
            || identity.Size != CardSize.Large || definition.InitialLevel != 3
            || definition.SupportsLevel(2) || definition.SupportsLevel(5)
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !definition.Tags.Contains(GameTags.Mechanical) || texture is null
            || texture.GetWidth() * 2 != texture.GetHeight() * 3
            || !DefinitionRegistry.Scan(typeof(FrankentChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        foreach (var (level, damage, status, bonus) in new[] { (3, 50, 2, 10), (4, 100, 4, 20) })
        {
            var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
            var economy = new CardEconomyService(factory, board);
            var match = new CreateMatchService(factory).Create(42, 100, new HarlaHeroDefinition());
            var enemy = new CreateMatchService(factory).Create(43, 100, new HarlaHeroDefinition());
            enemy.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, 10000);
            var field = factory.CreateCard(definition, level); var bench = factory.CreateCard(definition, level);
            foreach (var card in new[] { field, bench }) match.Player.Inventory.Add(card);
            board.PlaceCard(match, field.Id, BoardZone.Battlefield, 0);
            board.PlaceCard(match, bench.Id, BoardZone.Bench, 0);
            var frozen = new BattleSetupFactory().Create(match, enemy, 42, new BattleTick(65));
            var nonmaterial = factory.CreateCard(new SmallRedPotionCardDefinition());
            match.Player.Inventory.Add(nonmaterial);
            if (economy.SellCard(match, nonmaterial.Id).IsFailure) return false;
            if (field.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != damage) return false;
            for (var sale = 0; sale < 2; sale++)
            {
                var material = factory.CreateCard(new BeastHideCardDefinition());
                match.Player.Inventory.Add(material);
                if (economy.SellCard(match, material.Id).IsFailure
                    || economy.SellCard(match, material.Id).IsSuccess) return false;
            }
            foreach (var card in new[] { field, bench })
                if (card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != damage + bonus * 2
                    || card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Burn) != status * 3
                    || card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Poison) != status * 3
                    || card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Multicast) != 2
                    || card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != 60) return false;
            foreach (var (setup, expectedDamage, expectedStatus) in new[]
                { (frozen, damage, status),
                  (new BattleSetupFactory().Create(match, enemy, 42, new BattleTick(65)), damage + bonus * 2, status * 3) })
            {
                var result = new CombatSimulator().Simulate(setup);
                var hits = result.Events.OfType<DamageDealtEvent>().Where(item => item.SourceCardId == field.Id).ToArray();
                if (hits.Length != 3 || hits.Any(item => item.RawDamage != expectedDamage)
                    || result.Events.OfType<AbilityActivatedEvent>().Count(item => item.SourceCardId == field.Id) != 3
                    || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
                foreach (var kind in new[] { BattleStatus.Burn, BattleStatus.Poison })
                {
                    var statuses = result.Events.OfType<StatusChangedEvent>().Where(item => item.Status == kind).ToArray();
                    if (statuses.Length != 3 || statuses.Any(item => item.Amount != expectedStatus)) return false;
                }
            }
            factory.ApplyCardLevel(field, definition, 4);
            var nextMaterial = factory.CreateCard(new BeastHideCardDefinition()); match.Player.Inventory.Add(nextMaterial);
            if (economy.SellCard(match, nextMaterial.Id).IsFailure
                || field.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != 120 + bonus * 2
                || field.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Burn) != 8 + status * 2
                || field.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Poison) != 8 + status * 2) return false;
        }
        return true;
    }
}
