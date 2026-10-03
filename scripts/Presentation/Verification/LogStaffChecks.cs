using System.Collections.Generic;
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

namespace Project_Star.Presentation.Verification;

// 原木法杖拾取任务、合并计数、统一发动与升级验证（表现层验证模块）。
internal static class LogStaffChecks
{
    // 多元素拾取同时累计两个任务，已有卡牌不补计，新卡牌不计自身。
    internal static bool AcquisitionAndActivation()
    {
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
        var economy = new CardEconomyService(factory, board);
        var staffDefinition = new LogStaffCardDefinition();
        var elementDefinition = new ElementFixture("dual", [GameElements.Fire, GameElements.Wood]);
        var neutralDefinition = new ElementFixture("general", [GameElements.General]);
        int[] damages = [10, 20, 40, 80]; int[] statuses = [8, 12, 20, 32];
        for (var level = 1; level <= 4; level++)
        {
            var match = new CreateMatchService(factory).Create(42, 999, new MonaHeroDefinition());
            economy.AcquireCard(match, elementDefinition, 1, CardAcquisitionSource.Reward);
            var staff = economy.AcquireAndPlace(match, staffDefinition, level, CardAcquisitionSource.Reward).Value!.Card;
            var quests = staff.Quests;
            if (staff.Attributes.Identity.Size != CardSize.Medium || !staff.Tags.Contains(GameTags.Equipment)
                || quests.Count != 3 || quests.Any(quest => staff.GetQuestProgress(quest.Key) != 0)) return false;
            board.PlaceCard(match, staff.Id, BoardZone.Bench, 0);
            var offer = ShopOffer.Create(elementDefinition, 1);
            var purchase = economy.BuyCard(match, offer);
            if (purchase.IsFailure || !purchase.Value!.WasUpgraded || economy.BuyCard(match, offer).IsSuccess
                || staff.GetQuestProgress(quests[0].Key) != 1 || staff.GetQuestProgress(quests[1].Key) != 1) return false;
            for (var index = 0; index < 3; index++)
                economy.AcquireCard(match, elementDefinition, 1, CardAcquisitionSource.Drop);
            if (staff.IsQuestUnlocked(quests[0]) || staff.IsQuestUnlocked(quests[1])) return false;
            economy.AcquireCard(match, elementDefinition, 1, CardAcquisitionSource.Reward);
            for (var index = 0; index < 9; index++)
                economy.AcquireCard(match, neutralDefinition, 1, CardAcquisitionSource.Reward);
            if (staff.IsQuestUnlocked(quests[2]) || staff.GetQuestProgress(quests[2].Key) != 9) return false;
            economy.AcquireCard(match, neutralDefinition, 1, CardAcquisitionSource.Reward);
            if (quests.Any(quest => !staff.IsQuestUnlocked(quest))
                || staff.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != 80) return false;
            board.PlaceCard(match, staff.Id, BoardZone.Battlefield, 0);
            if (staff.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != 50) return false;
            match.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.Mana, 10);
            match.Player.Hero.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.ManaRegen, 0);
            var enemy = new CreateMatchService(factory).Create(43, 100, new MonaHeroDefinition());
            enemy.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, 10000);
            var setup = new BattleSetupFactory().Create(match, enemy, 42, new BattleTick(101));
            var result = new CombatSimulator().Simulate(setup);
            var activations = result.Events.OfType<AbilityActivatedEvent>().Where(item => item.SourceCardId == staff.Id).ToArray();
            var payments = result.Events.OfType<ManaChangedEvent>().Where(item => item.Amount < 0).ToArray();
            var applied = result.Events.OfType<StatusChangedEvent>().ToArray();
            if (activations.Length != 1 || activations[0].Tick.Value != 50 || activations[0].IsEcho
                || payments.Length != 1 || payments[0].Amount != -10
                || applied.Length != 2 || applied.Any(item => item.Amount != statuses[level - 1])
                || result.Events.OfType<DamageDealtEvent>().Single(item => item.SourceCardId == staff.Id).RawDamage != damages[level - 1]) return false;
            var snapshot = MatchSnapshot.From(match).Cards.Single(card => card.Id == staff.Id);
            if (snapshot.Abilities.Count != 2 || snapshot.Abilities.Single(ability => ability.Activation == AbilityActivation.Active).Effects.Count != 3
                || snapshot.Quests.Any(quest => !quest.Unlocked)) return false;
            board.PlaceCard(match, staff.Id, BoardZone.Bench, 0);
            if (staff.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != 80) return false;
            if (level < 4)
            {
                var merged = economy.AcquireCard(match, staffDefinition, level, CardAcquisitionSource.Reward);
                if (merged.IsFailure || merged.Value!.Card.Id != staff.Id || !merged.Value.WasUpgraded
                    || staff.Quests.Any(quest => !staff.IsQuestUnlocked(quest))) return false;
                board.PlaceCard(match, staff.Id, BoardZone.Battlefield, 0);
                var upgraded = new BattleSetupFactory().Create(match, enemy, 42, new BattleTick(50));
                if (upgraded.Player.Cards.Single(card => card.EntityId == staff.Id).Abilities!.Single().CooldownTicks != 50
                    || staff.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Burn) != statuses[level]) return false;
            }
        }
        return true;
    }

    // 仅供拾取任务验证的通用元素内容，不进入正式注册表。
    private sealed class ElementFixture : CardDefinition
    {
        public ElementFixture(string suffix, IReadOnlyList<StringName> elements)
            : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
                new StringName("verification.staff." + suffix), "任务材料", GameFactions.Neutral,
                CardSize.Small, elements)), new TagSet()) { }
    }
}
