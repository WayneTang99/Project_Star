using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Verification;

// 遗迹石像的分级、伤害护甲目标、禁锢时序与属性光环组合验证（表现层）。
internal static class RuinStoneStatueChecks
{
    internal static bool LevelsAndTiming()
    {
        var definition = new RuinStoneStatueCardDefinition(); var identity = definition.Attributes.Identity;
        var registry = DefinitionRegistry.Scan(typeof(RobinHeroDefinition).Assembly);
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (!registry.Cards.ContainsKey(identity.Key) || identity.DisplayName != "遗迹石像"
            || identity.FactionKey != new StringName("robin") || identity.Size != CardSize.Large || identity.SetKey is not null
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Earth })
            || !definition.Tags.ToArray().ToHashSet().SetEquals(new[] { GameTags.Large, GameTags.Vehicle })
            || definition.InitialLevel != 3 || definition.SupportsLevel(2) || definition.SupportsLevel(5)
            || texture is null || texture.GetWidth() * 2 != texture.GetHeight() * 3) return false;
        foreach (var (level, amount) in new[] { (3, 100), (4, 200) })
        {
            var factory = new EntityFactory(); var instance = factory.CreateCard(definition, level);
            var card = Card(definition, level, 0);
            var abilities = definition.GetLevel(level)!.Abilities;
            if (abilities.Count != 2 || abilities[0].Activation != AbilityActivation.Active
                || abilities[0].CooldownTicks != 60 || abilities[0].ManaCost != 0 || abilities[0].Effects.Count != 2
                || abilities[0].Effects[0] is not AttributeDamageEffectDefinition
                || abilities[0].Effects[1] is not GainSourceHeroArmorFromAttributeEffectDefinition armor || armor.AttributeKey != GameAttributeKeys.Armor
                || instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Armor) != amount
                || abilities[1].Activation != AbilityActivation.PassiveOnBattleStart || abilities[1].Target != AbilityTarget.SelfCard
                || abilities[1].Effects.Single() is not ApplyStatusEffectDefinition { Status: BattleStatus.ImmobilizeDuration, Amount: 20 }
                || instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != 60) return false;
            var setup = Battle([card], [OpeningDamage(500)], 140, enemyArmor: 20);
            var result = new CombatSimulator().Simulate(setup);
            var hits = result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == card.EntityId).ToArray();
            if (!hits.Select(hit => hit.Tick.Value).SequenceEqual(new long[] { 80, 140 })
                || hits.Any(hit => hit.RawDamage != amount || hit.TargetSide != SideId.Opponent)
                || hits[0].ArmorAbsorbed != 20 || hits[1].ArmorAbsorbed != 0
                || result.States.Last(frame => frame.Tick.Value == 0).Cards.Single(item => item.Id == card.EntityId).Immobilize != 20
                || result.States.Last(frame => frame.Tick.Value == 20).Cards.Single(item => item.Id == card.EntityId).Immobilize != 0
                || result.States.Last(frame => frame.Tick.Value == 20).Cards.Single(item => item.Id == card.EntityId).CooldownUnits[0] != 120
                || LiveFrame(result, 80).Player.Health != 500 || LiveFrame(result, 140).Player.Health != 500
                || LiveFrame(result, 80).Player.Armor != amount || LiveFrame(result, 140).Player.Armor != amount * 2
                || LiveFrame(result, 140).Opponent.Health != 1000 - amount * 2 + 20
                || result.Events.OfType<StatusChangedEvent>().Count(item => item.Status == BattleStatus.ImmobilizeDuration) != 1
                || result.Events.OfType<ManaChangedEvent>().Any() || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            var session = new CreateMatchService(factory).Create(1, 0, new RobinHeroDefinition()); session.Player.Inventory.Add(instance);
            var snapshot = MatchSnapshot.From(session).Cards.Single(); var face = CardDisplayAdapter.FaceEffects(snapshot);
            if (!snapshot.Tags.ToHashSet().SetEquals(new[] { GameTags.Large, GameTags.Vehicle })
                || !face.Any(effect => effect.Kind == CardFaceEffectKind.Damage && effect.Value == amount.ToString())
                || !face.Any(effect => effect.Kind == CardFaceEffectKind.Armor && effect.Value == amount.ToString())
                || face.Any(effect => effect.Kind == CardFaceEffectKind.Healing)
                || !CardDisplayAdapter.Details(snapshot).Contains("己方英雄获得100/200护甲")
                || !CardDisplayAdapter.Details(snapshot).Contains("2秒禁锢")) return false;
        }
        return true;
    }

    internal static bool TargetsBonusesAndLifecycle()
    {
        var card = Card(new RuinStoneStatueCardDefinition(), 3, 0) with
        {
            AttackDamage = 120,
            ArmorAmount = 125,
        };
        var guardian = Card(new GuardianStandardCardDefinition(), 2, 3);
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        {
            var setup = side == SideId.Player ? Battle([card, guardian], [OpeningDamage(500)], 80)
                : Battle([OpeningDamage(500)], [card, guardian], 80);
            var result = new CombatSimulator().Simulate(setup); var frame = LiveFrame(result, 80);
            var own = side == SideId.Player ? frame.Player : frame.Opponent;
            var enemy = side == SideId.Player ? frame.Opponent : frame.Player;
            if (own.Health != 500 || own.Armor != 145 || enemy.Health != 880 || enemy.Armor != 0
                || result.PermanentAttributeBonuses.Count != 0) return false;
        }
        var fullHealth = new CombatSimulator().Simulate(Battle([card, guardian], [], 80));
        if (LiveFrame(fullHealth, 80).Player.Health != 1000 || LiveFrame(fullHealth, 80).Player.Armor != 145) return false;
        var bench = new CombatSimulator().Simulate(Battle([card with { IsOnBench = true }], [OpeningDamage(500)], 140));
        if (bench.Events.OfType<AbilityActivatedEvent>().Any(item => item.SourceCardId == card.EntityId)
            || bench.States.Any(frame => frame.Cards.Single(item => item.Id == card.EntityId).Immobilize != 0)) return false;
        var destroyed = card with { Abilities = card.Abilities!.Append(new AbilityDefinition(
            new StringName("verification.statue.destroy"), AbilityActivation.Active, AbilityTarget.SelfCard, 0, 30,
            [new DestroyCardEffectDefinition(false)])).ToArray() };
        var removed = new CombatSimulator().Simulate(Battle([destroyed], [OpeningDamage(500)], 140));
        if (removed.Events.OfType<CardDestroyedEvent>().Single().Tick.Value != 50
            || removed.Events.OfType<DamageDealtEvent>().Any(hit => hit.SourceCardId == card.EntityId)) return false;
        // 同一通用组合的多重完整重复伤害和己方护甲，并共用一次冷却。
        var activeOnly = card with { Abilities = card.Abilities!.Where(ability => ability.Activation == AbilityActivation.Active).ToArray(), Multicast = 1 };
        var repeated = new CombatSimulator().Simulate(Battle([activeOnly, guardian], [OpeningDamage(500)], 62));
        return LiveFrame(repeated, 62).Player.Health == 500 && LiveFrame(repeated, 62).Player.Armor == 290
            && LiveFrame(repeated, 62).Opponent.Health == 760
            && repeated.Events.OfType<DamageDealtEvent>().Count(hit => hit.SourceCardId == card.EntityId) == 2
            && LiveFrame(repeated, 62).Cards.Single(item => item.Id == card.EntityId).CooldownUnits[0] == 116
            && new CombatSimulator().Simulate(Battle([card], [], 1)).States.Last(frame => frame.Tick.Value == 0)
                .Cards.Single().Immobilize == 20;
    }

    private static BattleStateSnapshot LiveFrame(BattleResult result, int tick) => result.States.Last(
        frame => frame.Tick.Value == tick && frame.Player.Health > 0 && frame.Opponent.Health > 0);

    private static CardBattleSetup Card(CardDefinition definition, int level, int start)
    {
        var instance = new EntityFactory().CreateCard(definition, level);
        return new CardBattleSetup(instance.Id, start, instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
            instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks), definition.GetLevel(level)!.Abilities,
            UseLegacyAttack: false, Tags: instance.Tags, OccupiedSlots: (int)definition.Attributes.Identity.Size,
            ElementKeys: definition.Attributes.Identity.ElementKeys,
            ArmorAmount: instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Armor)) { Level = level };
    }

    private static CardBattleSetup OpeningDamage(int damage) => new(EntityId.New(), 0, 0, 0,
        [new AbilityDefinition(new StringName("verification.statue.opening_damage"), AbilityActivation.PassiveOnBattleStart,
            AbilityTarget.EnemyHero, 0, 0, [new DamageEffectDefinition(damage)])], UseLegacyAttack: false);

    private static BattleSetup Battle(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks, int enemyArmor = 0) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), cards),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, enemyArmor, ManaRegen: 0), enemies),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
