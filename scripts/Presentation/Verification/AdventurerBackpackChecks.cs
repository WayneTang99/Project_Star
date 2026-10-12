using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
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

namespace Project_Star.Presentation.Verification;

// 背包等级光环、有效来源变化与正式冒险者套装组合验证（表现层）。
internal static class AdventurerBackpackChecks
{
    internal static bool IdentityAndScaling()
    {
        var definition = new AdventurerBackpackCardDefinition(); var identity = definition.Attributes.Identity;
        var registry = DefinitionRegistry.Scan(typeof(RobinHeroDefinition).Assembly);
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (!registry.Cards.ContainsKey(identity.Key) || identity.DisplayName != "冒险者背包"
            || identity.FactionKey != new StringName("robin") || identity.SetKey != new StringName("set.adventurer")
            || identity.Size != CardSize.Medium || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.InitialLevel != 2 || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Equipment)
            || texture is null || texture.GetWidth() != texture.GetHeight()) return false;
        for (var level = 2; level <= 4; level++)
        {
            var ability = definition.GetLevel(level)!.Abilities.Single();
            var multiplier = (level - 1) * 10;
            if (ability.Activation != AbilityActivation.PassiveAura || ability.CooldownTicks != 0 || ability.ManaCost != 0
                || ability.Effects.Single() is not IncreaseAlliedCardAttributeAuraEffectDefinition
                    { Amount: 0 } aura || aura.AttributeKey != GameAttributeKeys.AttackDamage
                || aura.HeroLevelMultiplier != multiplier) return false;
            foreach (var heroLevel in new[] { 1, 2, 7 })
            {
                var bow = Card(new AdventurerHuntingBowCardDefinition(), 1, 0);
                var backpack = Card(definition, level, 2);
                var setup = Battle([bow, backpack], [], heroLevel, 1, 60);
                var result = new CombatSimulator().Simulate(setup);
                var hit = result.Events.OfType<DamageDealtEvent>().Single();
                if (hit.RawDamage != 40 + heroLevel * multiplier || hit.Tick.Value != 60
                    || result.States[0].Cards.Single(card => card.Id == bow.EntityId).Values[GameAttributeKeys.AttackDamage] != hit.RawDamage
                    || result.PermanentAttributeBonuses.Count != 0 || bow.AttackDamage != 40
                    || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            }
            var factory = new EntityFactory(); var session = new CreateMatchService(factory).Create(1, 0, new RobinHeroDefinition());
            session.Player.Inventory.Add(factory.CreateCard(definition, level));
            var face = CardDisplayAdapter.FaceEffects(MatchSnapshot.From(session).Cards.Single()).Single();
            if (face.Kind != CardFaceEffectKind.Damage || face.Value != $"+{multiplier}"
                || !CardDisplayAdapter.AbilityDetails([ability]).Contains($"英雄当前等级×{multiplier}")) return false;
        }
        return true;
    }

    internal static bool StackingAndRemoval()
    {
        var bow = Card(new AdventurerHuntingBowCardDefinition(), 1, 0);
        var first = Card(new AdventurerBackpackCardDefinition(), 2, 2);
        var second = Card(new AdventurerBackpackCardDefinition(), 3, 4);
        second = second with { Abilities = second.Abilities!.Append(new AbilityDefinition(
            new StringName("verification.backpack.destroy"), AbilityActivation.Active, AbilityTarget.SelfCard,
            0, 75, [new DestroyCardEffectDefinition(false)])).ToArray() };
        var standard = Card(new BattleStandardCardDefinition(), 2, 6);
        var nun = Card(new NunCardDefinition(), 1, 7);
        var bench = Card(new AdventurerBackpackCardDefinition(), 4, 0) with { IsOnBench = true };
        var benchBow = Card(new AdventurerHuntingBowCardDefinition(), 1, 2) with { IsOnBench = true };
        var enemyBow = Card(new AdventurerHuntingBowCardDefinition(), 1, 0);
        var enemyBackpack = Card(new AdventurerBackpackCardDefinition(), 4, 2);
        var setup = Battle([bow, first, second, standard, nun, bench, benchBow], [enemyBow, enemyBackpack], 2, 3, 120);
        var result = new CombatSimulator().Simulate(setup);
        if (!result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == bow.EntityId)
            .Select(hit => hit.RawDamage).SequenceEqual(new[] { 120, 80 })
            || result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == enemyBow.EntityId).Any(hit => hit.RawDamage != 130)
            || result.States[0].Cards.Single(card => card.Id == benchBow.EntityId).Values[GameAttributeKeys.AttackDamage] != 40
            || result.States[0].Cards.Single(card => card.Id == nun.EntityId).Values[GameAttributeKeys.AttackDamage] != 0
            || result.States[0].Cards.Single(card => card.Id == first.EntityId).Values[GameAttributeKeys.AttackDamage] != 0
            || result.PermanentAttributeBonuses.Count != 0 || bow.AttackDamage != 40) return false;
        // 无攻击卡牌的来源不会凭空得到攻击，零攻击但有攻击能力的卡牌仍获得光环。
        var zeroBow = bow with { AttackDamage = 0 };
        var zero = new CombatSimulator().Simulate(Battle([zeroBow, first], [], 2, 1, 60));
        if (zero.Events.OfType<DamageDealtEvent>().Single().RawDamage != 20) return false;
        var knight = Card(new TemplarKnightCardDefinition(), 3, 2);
        var summoned = new CombatSimulator().Simulate(Battle([first with { BoardStart = 6 }, knight], [], 2, 1, 40));
        return summoned.Events.OfType<CardSummonedEvent>().Count() == 2
            && summoned.Events.OfType<CardSummonedEvent>().All(card => summoned.States[^1].Cards.Single(
                snapshot => snapshot.Id == card.SummonedCardId).Values[GameAttributeKeys.AttackDamage] == 40)
            && result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events);
    }

    internal static bool HeroGrowthAndSet()
    {
        var registry = DefinitionRegistry.Scan(typeof(RobinHeroDefinition).Assembly); var factory = new EntityFactory();
        var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
        var matches = new CreateMatchService(factory); var match = matches.Create(1, 0, new RobinHeroDefinition());
        var enemy = matches.Create(2, 0, new RobinHeroDefinition());
        enemy.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, 1000);
        var bow = factory.CreateCard(new AdventurerHuntingBowCardDefinition(), 1);
        var backpack = factory.CreateCard(new AdventurerBackpackCardDefinition(), 2);
        match.Player.Inventory.Add(bow); match.Player.Inventory.Add(backpack);
        if (board.PlaceCard(match, bow.Id, BoardZone.Battlefield, 0).IsFailure
            || board.PlaceCard(match, backpack.Id, BoardZone.Battlefield, 2).IsFailure) return false;
        var set = MatchDisplayQuery.Capture(match, registry.Sets).Sets.Single(item => item.Key == new StringName("set.adventurer"));
        match.Progress.Round = 2;
        if (set.DistinctCardCount != 2 || set.Thresholds.Count(threshold => threshold.Active) != 1
            || new RoundIncomeService(registry.Sets).SettleCurrentRound(match).IsFailure || match.Player.Experience != 2) return false;
        var setups = new BattleSetupFactory(registry.Sets); var frozen = setups.Create(match, enemy, 4, new BattleTick(60));
        match.Player.AddExperience(8);
        if (match.Player.Hero!.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) != 2
            || Damage(frozen) != 50 || Damage(setups.Create(match, enemy, 4, new BattleTick(60))) != 60) return false;
        if (board.PlaceCard(match, backpack.Id, BoardZone.Bench, 0).IsFailure
            || Damage(setups.Create(match, enemy, 4, new BattleTick(60))) != 40) return false;
        set = MatchDisplayQuery.Capture(match, registry.Sets).Sets.Single(item => item.Key == new StringName("set.adventurer"));
        return set.DistinctCardCount == 1 && set.Thresholds.All(threshold => !threshold.Active)
            && bow.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) == 40;

        int Damage(BattleSetup setup) => new CombatSimulator().Simulate(setup).Events.OfType<DamageDealtEvent>().Single().RawDamage;
    }

    private static CardBattleSetup Card(CardDefinition definition, int level, int start)
    {
        var card = new EntityFactory().CreateCard(definition, level);
        return new CardBattleSetup(card.Id, start, card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
            0, definition.GetLevel(level)!.Abilities, UseLegacyAttack: false,
            Tags: card.Tags, OccupiedSlots: (int)definition.Attributes.Identity.Size,
            ElementKeys: definition.Attributes.Identity.ElementKeys) { Level = level };
    }

    private static BattleSetup Battle(CardBattleSetup[] cards, CardBattleSetup[] enemies, int level, int enemyLevel, int ticks) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0, Level: level), cards),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0, Level: enemyLevel), enemies),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
