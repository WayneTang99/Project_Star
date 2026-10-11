using System;
using System.Linq;
using Godot;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Verification;

// 见习剑士分级攻击、中型人类充能回响及冻结筛选验证（表现层验证模块）。
internal static class ApprenticeSwordsmanChecks
{
    internal static bool LevelsAndArtwork()
    {
        var definition = new ApprenticeSwordsmanCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.Key != new StringName("card.apprentice_swordsman") || identity.DisplayName != "见习剑士"
            || identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Human)
            || definition.InitialLevel != 1 || definition.SupportsLevel(0) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(ApprenticeSwordsmanChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (texture is null || texture.GetWidth() * 2 != texture.GetHeight()) return false;
        for (var level = 1; level <= 4; level++)
        {
            var damage = 10 << (level - 1);
            var sword = Swordsman(level);
            var result = new CombatSimulator().Simulate(Battle([sword], [], 140));
            var hits = result.Events.OfType<DamageDealtEvent>().ToArray();
            if (hits.Length != 2 || !hits.Select(hit => hit.Tick.Value).SequenceEqual(new long[] { 70, 140 })
                || hits.Any(hit => hit.RawDamage != damage || hit.HealthDamage != damage)
                || result.Events.OfType<CardChargedEvent>().Any() || result.PermanentChanges.Any()) return false;
            var enhanced = new CombatSimulator().Simulate(Battle([sword with { AttackDamage = damage + 7 }], [], 70));
            if (enhanced.Events.OfType<DamageDealtEvent>().Single().RawDamage != damage + 7) return false;
            var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            var echo = snapshot.Abilities.Single(ability => ability.Activation == AbilityActivation.EchoOnMatchingAlliedCardActivated);
            if (echo.TriggerCardSize != CardSize.Medium || echo.TriggerCardTag != GameTags.Human
                || echo.TriggerCardElement is not null
                || !CardDisplayAdapter.FaceEffects(snapshot).Any(effect => effect.Kind == CardFaceEffectKind.Damage
                    && effect.Value == damage.ToString())
                || !CardDisplayAdapter.Details(snapshot).Contains("1秒充能")) return false;
        }
        return true;
    }

    internal static bool MatchingAndImmediateCharge()
    {
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        foreach (var level in new[] { 1, 2, 3, 4 })
        {
            var sword = Swordsman(level);
            var human = Activator(1, CardSize.Medium, true, 20) with { Multicast = 1 };
            var setup = side == SideId.Player ? Battle([sword, human], [], 40) : Battle([], [sword, human], 40);
            var result = new CombatSimulator().Simulate(setup);
            var charges = result.Events.OfType<CardChargedEvent>().ToArray();
            var hits = result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == sword.EntityId).ToArray();
            if (charges.Length != 4 || charges.Any(item => item.SourceCardId != sword.EntityId
                    || item.TargetCardId != sword.EntityId || item.AmountTicks != 10)
                || Echoes(result, sword) != 4 || hits.Length != 1 || hits[0].Tick.Value != 40
                || hits[0].RawDamage != 10 << (level - 1) || hits[0].TargetSide == side
                || LiveFrame(result, 20).Cards.Single(card => card.Id == sword.EntityId).CooldownUnits[0] != 60
                || LiveFrame(result, 40).Cards.Single(card => card.Id == sword.EntityId).CooldownUnits[0] != 140
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                || result.PermanentChanges.Any()) return false;
        }
        // 一个回响把剩余1秒扣至零，当前Tick发动，下一次仍以7秒基础冷却重置。
        var source = Swordsman(1);
        var one = new CombatSimulator().Simulate(Battle([source, Activator(1, CardSize.Medium, true, 60)], [], 60));
        if (one.Events.OfType<DamageDealtEvent>().Single().Tick.Value != 60
            || one.Events.OfType<CardChargedEvent>().Single().AmountTicks != 10
            || LiveFrame(one, 60).Cards.Single(card => card.Id == source.EntityId).CooldownUnits[0] != 140) return false;
        foreach (var rejected in new[]
        {
            Activator(1, CardSize.Small, true, 20),
            Activator(1, CardSize.Large, true, 20),
            Activator(1, CardSize.Medium, false, 20),
            Activator(1, CardSize.Medium, true, 20) with { IsOnBench = true },
            Activator(1, CardSize.Medium, true, 20) with { Abilities = [Opening(new DestroyCardEffectDefinition(false))] },
            Activator(1, CardSize.Medium, true, 20) with { Abilities = [Opening(new GainSourceHeroArmorEffectDefinition(1))] },
        })
        {
            var result = new CombatSimulator().Simulate(Battle([source, rejected], [], 70));
            if (Echoes(result, source) != 0 || result.Events.OfType<DamageDealtEvent>().Single().Tick.Value != 70) return false;
        }
        var enemy = new CombatSimulator().Simulate(Battle([source], [Activator(0, CardSize.Medium, true, 20)], 70));
        var bench = new CombatSimulator().Simulate(Battle([source with { IsOnBench = true }, Activator(1, CardSize.Medium, true, 20)], [], 70));
        var dead = source with { Abilities = source.Abilities!.Append(Opening(new DestroyCardEffectDefinition(false))).ToArray() };
        var destroyed = new CombatSimulator().Simulate(Battle([dead, Activator(1, CardSize.Medium, true, 20)], [], 70));
        return Echoes(enemy, source) == 0 && Echoes(bench, source) == 0 && Echoes(destroyed, source) == 0
            && !bench.Events.OfType<DamageDealtEvent>().Any() && !destroyed.Events.OfType<DamageDealtEvent>().Any();
    }

    internal static bool SizeFilterComposition()
    {
        var source = Swordsman(1) with { Abilities = [FilteredEcho(GameTags.Human, GameElements.Light)] };
        foreach (var (size, human, element, expected) in new[]
        {
            (CardSize.Small, true, GameElements.Light, 0),
            (CardSize.Medium, false, GameElements.Dark, 0),
            (CardSize.Medium, false, GameElements.Light, 1),
            (CardSize.Medium, true, GameElements.Dark, 1),
            (CardSize.Medium, true, GameElements.Light, 1),
        })
        {
            var trigger = Activator(1, size, human, 20) with { ElementKeys = [element] };
            if (Echoes(new CombatSimulator().Simulate(Battle([source, trigger], [], 20)), source) != expected) return false;
        }
        var sizeOnly = source with { Abilities = [FilteredEcho(null, null)] };
        if (Echoes(new CombatSimulator().Simulate(Battle([sizeOnly, Activator(1, CardSize.Medium, false, 20)], [], 20)), source) != 1)
            return false;
        foreach (var activation in new[] { AbilityActivation.Active, AbilityActivation.EchoOnMatchingAlliedCardActivated })
        {
            try
            {
                _ = new AbilityDefinition(new StringName("verification.swordsman.invalid_size"), activation,
                    AbilityTarget.SelfCard, 0, activation == AbilityActivation.Active ? 20 : 0,
                    [new ChargeSourceCardEffectDefinition(10)], triggerCardSize: activation == AbilityActivation.Active
                        ? CardSize.Medium : (CardSize)99);
                return false;
            }
            catch (ArgumentException) { }
        }
        return true;
    }

    private static AbilityDefinition FilteredEcho(StringName? tag, StringName? element) => new(
        new StringName("verification.swordsman.filtered_echo"), AbilityActivation.EchoOnMatchingAlliedCardActivated,
        AbilityTarget.SelfCard, 0, 0, [new ChargeSourceCardEffectDefinition(10)], triggerCardTag: tag,
        triggerCardElement: element, triggerCardSize: CardSize.Medium);
    private static CardBattleSetup Swordsman(int level)
    {
        var definition = new ApprenticeSwordsmanCardDefinition();
        var card = new EntityFactory().CreateCard(definition, level);
        return new CardBattleSetup(card.Id, 0, card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
            card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks), definition.GetLevel(level)!.Abilities,
            UseLegacyAttack: false, Tags: card.Tags, ElementKeys: definition.Attributes.Identity.ElementKeys) { Level = level };
    }
    private static CardBattleSetup Activator(int start, CardSize size, bool human, int ticks) => new(EntityId.New(), start, 0, ticks,
        [new AbilityDefinition(new StringName("verification.swordsman.activation"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, ticks, [new GainSourceHeroArmorEffectDefinition(1)])], UseLegacyAttack: false,
        Tags: TagSet.ForCard(size, human ? [GameTags.Human] : [GameTags.Beast]), OccupiedSlots: (int)size,
        ElementKeys: [GameElements.General]);
    private static AbilityDefinition Opening(EffectDefinition effect) => new(new StringName("verification.swordsman.opening"),
        AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [effect]);
    private static int Echoes(BattleResult result, CardBattleSetup sword) => result.Events.OfType<AbilityActivatedEvent>()
        .Count(item => item.SourceCardId == sword.EntityId && item.IsEcho);
    private static BattleStateSnapshot LiveFrame(BattleResult result, int tick) => result.States.Last(
        frame => frame.Tick.Value == tick && frame.Player.Health > 0 && frame.Opponent.Health > 0);
    private static BattleSetup Battle(CardBattleSetup[] player, CardBattleSetup[] opponent, int ticks) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), player),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), opponent),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
