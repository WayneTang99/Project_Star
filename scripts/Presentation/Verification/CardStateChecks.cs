using System;
using System.Linq;
using Godot;
using Project_Star.Application.Combat;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

internal static class CardStateChecks
{
    private static AbilityDefinition Ability(string key, AbilityActivation activation, int cooldown,
        params EffectDefinition[] effects) => new(new StringName("verification.card_state." + key), activation,
            AbilityTarget.SelfCard, 0, cooldown, effects);

    private static BattleSetup Setup(EntityId id) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 100, 0),
            [new CardBattleSetup(id, 0, 0, 2,
                [Ability("enable", AbilityActivation.PassiveOnBattleStart, 0,
                    new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, true),
                    new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, true),
                    new SetSourceCardStateEffectDefinition(GameAttributeKeys.Berserk, true)),
                 Ability("disable", AbilityActivation.Active, 2,
                    new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, false),
                    new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, false),
                    new SetSourceCardStateEffectDefinition(GameAttributeKeys.Berserk, false))], UseLegacyAttack: false)]),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 100, 0), []), 42, new BattleTick(4));

    internal static bool BooleanLifecycle()
    {
        var id = EntityId.New();
        var setup = Setup(id);
        var simulator = new CombatSimulator();
        var result = simulator.Simulate(setup);
        var initial = result.States[0].Cards.Single();
        var enabled = result.States.Last(frame => frame.Tick.Value == 1).Cards.Single();
        var final = result.States[^1].Cards.Single();
        if (initial.IsFlying || initial.IsBerserk || !enabled.IsFlying || !enabled.IsBerserk
            || final.IsFlying || final.IsBerserk || enabled.Values.ContainsKey(GameAttributeKeys.Flying)
            || enabled.Values.ContainsKey(GameAttributeKeys.Berserk)) return false;
        var events = result.Events.OfType<CardStateChangedEvent>().ToArray();
        if (events.Length != 4 || events[0].StateKey != GameAttributeKeys.Flying || !events[0].Enabled
            || events[1].StateKey != GameAttributeKeys.Berserk || !events[1].Enabled
            || events[2].Enabled || events[3].Enabled) return false;
        if (!result.Events.SequenceEqual(simulator.Simulate(setup).Events)) return false;
        try
        {
            _ = Ability("invalid", AbilityActivation.PassiveOnBattleStart, 0,
                new SetSourceCardStateEffectDefinition(new StringName("Unknown"), true));
            return false;
        }
        catch (ArgumentException) { }
        try
        {
            _ = Ability("invalid_aura", AbilityActivation.PassiveAura, 0,
                new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, true));
            return false;
        }
        catch (ArgumentException) { }
        try
        {
            SkillDefinition.ValidateNonCardAbilities([Ability("skill", AbilityActivation.PassiveOnBattleStart, 0,
                new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, true))]);
            return false;
        }
        catch (ArgumentException) { }
        var fresh = simulator.Simulate(new BattleSetup(
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 100, 0),
                [new CardBattleSetup(id, 0, 0, 1, [], UseLegacyAttack: false)]), setup.Opponent, 42, new BattleTick(1)));
        return fresh.States.All(frame => !frame.Cards.Single().IsFlying && !frame.Cards.Single().IsBerserk);
    }

    internal static bool PlaybackAndDisplay(Control owner)
    {
        var id = EntityId.New();
        var result = new CombatSimulator().Simulate(Setup(id));
        var before = MatchSnapshot.From(new CreateMatchService(new EntityFactory()).Create(42, 100, new PaladinHeroDefinition()))
            with { Cards = Array.AsReadOnly(new[] { new CardSnapshot(id, new StringName("card.state_fixture"), "状态夹具",
                1, 0, CardSize.Small, GameFactions.Neutral, [GameElements.General], []) }) };
        var playback = new BattlePlaybackPresenter(new BattleResolution(before, before, result, before));
        var initial = playback.Project(SideId.Player).Cards.Single();
        playback.Advance(.1);
        var frozen = playback.Capture().State.Cards.Single();
        var displayed = playback.Project(SideId.Player).Cards.Single();
        if (initial.IsFlying || initial.IsBerserk || !displayed.IsFlying || !displayed.IsBerserk
            || !CardDisplayAdapter.Details(displayed).Contains("状态：飞行、狂暴")) return false;
        var item = new CardItemView(); owner.AddChild(item);
        try
        {
            item.Render(displayed, new CardDisplayAdapter()); item.RenderBattle(frozen, false);
            var label = item.GetNode<Label>("BattleStatus");
            if (!label.Text.Contains("飞行") || !label.Text.Contains("狂暴")) return false;
            playback.Skip();
            var cleared = playback.Project(SideId.Player).Cards.Single();
            item.Render(cleared, new CardDisplayAdapter()); item.RenderBattle(playback.Capture().State.Cards.Single(), false);
            return !cleared.IsFlying && !cleared.IsBerserk && !CardDisplayAdapter.Details(cleared).Contains("状态：")
                && !label.Text.Contains("飞行") && !label.Text.Contains("狂暴") && frozen.IsFlying && frozen.IsBerserk;
        }
        finally { owner.RemoveChild(item); item.Free(); }
    }

    internal static bool FlyingDurations()
    {
        var id = EntityId.New();
        var direct = new CombatSimulator().Simulate(Battle([
            new CardBattleSetup(id, 0, 0, 1, [Ability("flight_duration", AbilityActivation.PassiveOnBattleStart, 0,
                new ApplyStatusEffectDefinition(BattleStatus.SlowDuration, 3),
                new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, true),
                new ApplyStatusEffectDefinition(BattleStatus.SlowDuration, 3),
                new ApplyStatusEffectDefinition(BattleStatus.ImmobilizeDuration, 3),
                new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 3),
                new ApplyStatusEffectDefinition(BattleStatus.ImmobilizeDuration, 1),
                new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, false),
                new ApplyStatusEffectDefinition(BattleStatus.SlowDuration, 3),
                new ApplyStatusEffectDefinition(BattleStatus.ImmobilizeDuration, 3))], UseLegacyAttack: false)], 1));
        if (!direct.Events.OfType<StatusChangedEvent>().Select(item => item.Amount).SequenceEqual(new[] { 3, 1, 1, 3, 0, 3, 3 })) return false;
        var card = direct.States.Last(frame => frame.Tick.Value == 0).Cards.Single();
        if (card.IsFlying || card.Slow != 7 || card.Immobilize != 4 || card.Haste != 3) return false;

        var flying = EntityId.New(); var source = EntityId.New(); var grounded = EntityId.New();
        var adjacent = new CombatSimulator().Simulate(Battle([
            new CardBattleSetup(flying, 0, 0, 1, [Ability("flight", AbilityActivation.PassiveOnBattleStart, 0,
                new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, true))], UseLegacyAttack: false, Tags: new TagSet([GameTags.Human])),
            new CardBattleSetup(source, 1, 0, 1, [Ability("adjacent", AbilityActivation.PassiveOnBattleStart, 0,
                new ApplyStatusToAdjacentAlliedCardsEffectDefinition(BattleStatus.SlowDuration, 3, GameTags.Human),
                new ApplyStatusToAdjacentAlliedCardsEffectDefinition(BattleStatus.ImmobilizeDuration, 3, GameTags.Beast),
                new ApplyStatusToAdjacentAlliedCardsEffectDefinition(BattleStatus.HasteDuration, 3, GameTags.Human))], UseLegacyAttack: false),
            new CardBattleSetup(grounded, 2, 0, 1, [], UseLegacyAttack: false, Tags: new TagSet([GameTags.Human]))], 1));
        var frame = adjacent.States.Last(state => state.Tick.Value == 0);
        var air = frame.Cards.Single(item => item.Id == flying); var ground = frame.Cards.Single(item => item.Id == grounded);
        return air.IsFlying && air.Slow == 3 && air.Immobilize == 1 && air.Haste == 6
            && !ground.IsFlying && ground.Slow == 6 && ground.Immobilize == 3 && ground.Haste == 6;
    }

    internal static bool BerserkDamageAndStatus()
    {
        var card = EntityId.New();
        var attack = new AbilityDefinition(new StringName("verification.berserk.damage"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 1, [
                new DamageEffectDefinition(7), new SourceHeroLevelScaledDamageEffectDefinition(7),
                new MaxHealthPercentDamageEffectDefinition(1), new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                new SourceHeroHealthScaledAttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                new SourceHeroArmorDamageEffectDefinition(), new ApplyStatusEffectDefinition(BattleStatus.Poison, 7),
                new ApplyStatusEffectDefinition(BattleStatus.Burn, 7),
                new SetSourceCardStateEffectDefinition(GameAttributeKeys.Berserk, false), new DamageEffectDefinition(7),
                new ApplyStatusEffectDefinition(BattleStatus.Poison, 7), new ApplyStatusEffectDefinition(BattleStatus.Burn, 7)]);
        var result = new CombatSimulator().Simulate(Battle([
            new CardBattleSetup(card, 0, 7, 1, [Ability("rage", AbilityActivation.PassiveOnBattleStart, 0,
                new SetSourceCardStateEffectDefinition(GameAttributeKeys.Berserk, true)), attack], UseLegacyAttack: false)], 1, enemyArmor: 5));
        var damage = result.Events.OfType<DamageDealtEvent>().Where(item => item.SourceCardId == card).ToArray();
        var final = result.States[^1];
        return damage.Select(item => item.RawDamage).SequenceEqual(new[] { 8, 8, 12, 8, 8, 8, 7 })
            && damage[0].ArmorAbsorbed == 5 && damage[0].HealthDamage == 3
            && result.Events.OfType<StatusChangedEvent>().Select(item => item.Amount).SequenceEqual(new[] { 8, 8, 7, 7 })
            && final.Opponent.Poison == 15 && final.Opponent.Burn == 15
            && !final.Cards.Single().IsBerserk && final.Cards.Single().Values[GameAttributeKeys.AttackDamage] == 7;
    }

    internal static bool BerserkActiveOnly()
    {
        var card = EntityId.New();
        EffectDefinition[] hit = [new DamageEffectDefinition(7), new ApplyStatusEffectDefinition(BattleStatus.Poison, 7),
            new ApplyStatusEffectDefinition(BattleStatus.Burn, 7)];
        var opening = new AbilityDefinition(new StringName("verification.berserk.opening"), AbilityActivation.PassiveOnBattleStart,
            AbilityTarget.EnemyHero, 0, 0, new EffectDefinition[] { new SetSourceCardStateEffectDefinition(GameAttributeKeys.Berserk, true) }.Concat(hit).ToArray());
        var attack = new AbilityDefinition(new StringName("verification.berserk.active"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 10, hit);
        var result = new CombatSimulator().Simulate(Battle([
            new CardBattleSetup(card, 0, 7, 10, [opening, attack], UseLegacyAttack: false, Multicast: 1)], 11));
        var direct = result.Events.OfType<DamageDealtEvent>().Where(item => item.SourceCardId == card).ToArray();
        // 当前多重也重复战斗开始被动；两次7点中毒在周期结算时应造成14，而不是再次增加20%。
        if (!direct.Select(item => item.RawDamage).SequenceEqual(new[] { 7, 7, 8, 8 })
            || !result.Events.OfType<StatusChangedEvent>().Select(item => item.Amount).SequenceEqual(new[] { 7, 7, 7, 7, 8, 8, 8, 8 })
            || !result.Events.OfType<DamageDealtEvent>().Any(item => item.SourceKind == DamageSourceKind.Status && item.RawDamage == 14)
            || result.Events.OfType<DamageDealtEvent>().Any(item => item.SourceKind == DamageSourceKind.Status && item.RawDamage == 16)) return false;
        var echo = new AbilityDefinition(new StringName("verification.berserk.echo"), AbilityActivation.EchoOnAbilityActivated,
            AbilityTarget.EnemyHero, 0, 0, hit);
        var echoed = new CombatSimulator().Simulate(Battle([
            new CardBattleSetup(card, 0, 7, 10, [Ability("echo_enable", AbilityActivation.PassiveOnBattleStart, 0,
                new SetSourceCardStateEffectDefinition(GameAttributeKeys.Berserk, true)), attack, echo], UseLegacyAttack: false)], 11));
        return echoed.Events.OfType<DamageDealtEvent>().Where(item => item.SourceCardId == card)
            .Select(item => item.RawDamage).SequenceEqual(new[] { 7, 8, 7 })
            && echoed.Events.OfType<StatusChangedEvent>().Select(item => item.Amount).SequenceEqual(new[] { 7, 7, 8, 8, 7, 7 });
    }

    private static BattleSetup Battle(CardBattleSetup[] cards, int timeout, int enemyArmor = 0) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 100, 7), cards),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, enemyArmor), []), 42, new BattleTick(timeout));
}
