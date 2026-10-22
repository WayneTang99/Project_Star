using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Project_Star.Application.Content;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Content.Sets;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 巫师装备的召唤筛选、回响、套装与战斗隔离专项（表现验证层）。
internal static class WizardSummoningChecks
{
    internal static bool Combat()
    {
        foreach (var level in new[] { 2, 3, 4 })
        foreach (var reverse in new[] { false, true })
        {
            var crown = Card(new MagisterCrownCardDefinition(), level, 3);
            var boots = Card(new MagisterBootsCardDefinition(), level, 0);
            var wand = Card(new MagisterScepterCardDefinition(), Math.Max(3, level), 1);
            var set = new WizardSetDefinition();
            var sets = set.Thresholds.Select(threshold => new CardSetBattleSetup(EntityId.New(), threshold.Abilities)).ToArray();
            var own = Side([boots, wand, crown], [new SkeletonCardDefinition()], sets);
            var setup = Setup(own, reverse, 1);
            var result = new CombatSimulator().Simulate(setup);
            var summon = result.Events.OfType<CardSummonedEvent>().Single();
            Check(summon.Side == (reverse ? SideId.Opponent : SideId.Player) && summon.BoardStart == 4
                && summon.CardKey == new StringName("card.skeleton") && summon.Level == level, "same level summon at any free slot");
            var state = result.States.Last(frame => frame.Tick.Value == 0);
            var summoned = state.Cards.Single(card => card.Id == summon.SummonedCardId);
            Check(summoned.Haste == (level - 1) * 10, "dark summon haste");
            Check(state.Cards.Single(card => card.Id == wand.EntityId).Values[GameAttributeKeys.AttackDamage]
                == (level == 4 ? 360 : 180), "one attack growth per summon");
            Check(state.Cards.Single(card => card.Id == wand.EntityId).CooldownDurationUnits.Single() == 95m, "five percent dark cooldown");
            Check(summoned.CooldownDurationUnits.Single() == 57m, "aura includes summoned dark cards");
            Check(result.Events.OfType<CardChargedEvent>().Count() == 1, "one set charge per summon");
            Check(result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events), "deterministic summon replay");
            Check(result.PermanentAttributeBonuses.Count == 0, "no persistent attack growth");
            Check(new EntityFactory().CreateCard(new MagisterScepterCardDefinition(), Math.Max(3, level))
                .Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) == (level == 4 ? 200 : 100), "definition isolated");

            var benchBoots = boots with { IsOnBench = true };
            var benchWand = wand with { IsOnBench = true };
            var benchSetup = Setup(Side([benchBoots, benchWand, crown], [new SkeletonCardDefinition()]), reverse, 1);
            var benchResult = new CombatSimulator().Simulate(benchSetup);
            var benchState = benchResult.States.Last(frame => frame.Tick.Value == 0);
            var benchSummon = benchResult.Events.OfType<CardSummonedEvent>().Single();
            Check(benchState.Cards.Single(card => card.Id == benchSummon.SummonedCardId).Haste == 0, "bench boots excluded");
            Check(!benchResult.Events.OfType<CardAttributeChangedEvent>().Any(change => change.CardId == wand.EntityId), "bench growth excluded");
            Check(!new CombatSimulator().Simulate(Setup(Side([crown with { IsOnBench = true }], [new SkeletonCardDefinition()]), reverse, 1))
                .Events.OfType<CardSummonedEvent>().Any(), "bench crown excluded");
        }
        FiltersAndFailure();
        SummonKinds();
        SetAndCatalog();
        DestructionOrder();
        WandDamage();
        return true;
    }

    private static void FiltersAndFailure()
    {
        var crown = Card(new MagisterCrownCardDefinition(), 2, 0);
        var valid = new SkeletonCardDefinition();
        var pool = new CardDefinition[] { valid, new DarkSlimeCardDefinition(), new BoneBarrowCardDefinition(), new SmallManaPotionCardDefinition() };
        var first = new CombatSimulator().Simulate(Setup(Side([crown], pool), false, 1));
        Check(first.Events.OfType<CardSummonedEvent>().Single().CardKey == valid.Attributes.Identity.Key, "size element and level pool filters");
        foreach (var full in new[] { false, true })
        {
            var own = Side([crown], full ? [valid] : []);
            if (full) own = new BattleSideSetup(own.Hero, own.Cards) { BattlefieldCapacity = 1, CardDefinitions = [valid] };
            var setup = Setup(own, false, 1);
            var runtime = new BattleRuntime(setup); var control = new BattleRuntime(setup);
            var source = runtime.Cards.Single();
            BattleEffectResolver.ApplyEffect(runtime, new PendingAbility(source, source.Abilities.Single(), false, false, runtime.Tick),
                source.Abilities.Single().Definition.Effects.Single());
            Check(!runtime.Events.OfType<CardSummonedEvent>().Any() && runtime.NextRandomIndex(10000) == control.NextRandomIndex(10000), "failed summon preserves random state");
        }
        var registry = DefinitionRegistry.Scan(typeof(WizardSetDefinition).Assembly);
        var bootPool = registry.Cards.Values.Where(card => card.SupportsLevel(3) && card.Attributes.Identity.Size == CardSize.Small
            && card.Attributes.Identity.ElementKeys.Contains(GameElements.Dark)
            && (card.Attributes.Identity.FactionKey == new StringName("mona") || card.Attributes.Identity.FactionKey == GameFactions.Neutral)).ToArray();
        Check(bootPool.Any(card => card is MagisterBootsCardDefinition) && bootPool.Any(card => card is MagisterCrownCardDefinition)
            && bootPool.Any(card => card is DarkSlimeCardDefinition), "formal pool includes dark equipment and neutral cards");
        foreach (var reverse in new[] { false, true })
        {
            var ownBoots = Card(new MagisterBootsCardDefinition(), 4, 0);
            var otherBoots = Card(new MagisterBootsCardDefinition(), 4, 0);
            var enemyCrown = Card(new MagisterCrownCardDefinition(), 4, 1);
            var setup = new BattleSetup(Side([ownBoots], []), Side([otherBoots, enemyCrown], [valid]), 42, new BattleTick(1), new BattleTick(100));
            if (reverse) setup = new BattleSetup(setup.Opponent, setup.Player, 42, new BattleTick(1), new BattleTick(100));
            var result = new CombatSimulator().Simulate(setup);
            var summonedId = result.Events.OfType<CardSummonedEvent>().Single().SummonedCardId;
            Check(result.States.Last(frame => frame.Tick.Value == 0).Cards.Single(card => card.Id == summonedId).Haste == 30, "enemy boots cannot double haste");
        }
    }

    private static void SummonKinds()
    {
        foreach (var dark in new[] { false, true })
        foreach (var echo in new[] { false, true })
        {
            CardDefinition template = dark ? new SkeletonCardDefinition() : new WaterSlimeCardDefinition();
            var summoner = Card(new BoneBarrowCardDefinition(), 3, 1) with
            {
                Abilities = [new AbilityDefinition(new StringName("ability.verify_summon"), AbilityActivation.PassiveOnBattleStart,
                    AbilityTarget.SelfCard, 0, 0, [new SummonAdjacentCardEffectDefinition(template, AdjacentCardSide.Left),
                        new SummonAdjacentCardEffectDefinition(template, AdjacentCardSide.Right)])],
            };
            var boots = Card(new MagisterBootsCardDefinition(), 3, 4);
            var wand = Card(new MagisterScepterCardDefinition(), 3, 5);
            var own = Side([summoner, boots, wand], []);
            if (!echo)
            {
                var result = new CombatSimulator().Simulate(Setup(own, false, 1));
                var summoned = result.Events.OfType<CardSummonedEvent>().ToArray();
                Check(summoned.Length == 2, "multiple successful summons");
                var frame = result.States.Last(state => state.Tick.Value == 0);
                Check(frame.Cards.Single(card => card.Id == wand.EntityId).Values[GameAttributeKeys.AttackDamage] == 260, "growth includes non-dark summons and stacks");
                Check(summoned.All(item => frame.Cards.Single(card => card.Id == item.SummonedCardId).Haste == (dark ? 20 : 0)), "haste only dark summons");
            }
            else
            {
                var runtime = new BattleRuntime(Setup(own, false, 1));
                var source = runtime.Cards.Single(card => card.EntityId == summoner.EntityId);
                var pending = new PendingAbility(source, source.Abilities.Single(), true, false, runtime.Tick);
                foreach (var effect in pending.Ability.Definition.Effects) BattleEffectResolver.ApplyEffect(runtime, pending, effect);
                Check(runtime.Events.OfType<CardSummonedEvent>().Count() == 2 && runtime.Queue.Count == 0, "echo summons cannot chain echoes");
            }
        }
    }

    private static void SetAndCatalog()
    {
        var registry = DefinitionRegistry.Scan(typeof(WizardSetDefinition).Assembly);
        var factory = new EntityFactory();
        var match = new CreateMatchService(factory, definitions: registry).Create(42, 0, new MonaHeroDefinition());
        var enemy = new CreateMatchService(factory, definitions: registry).Create(43, 0, new MonaHeroDefinition());
        var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
        var economy = new CardEconomyService(factory, board, registry.Cards.Values);
        foreach (var definition in new CardDefinition[] { new MagisterCrownCardDefinition(), new MagisterBootsCardDefinition(), new MagisterScepterCardDefinition() })
        {
            Check(definition.Attributes.Identity.ElementKeys.SequenceEqual(new[] { GameElements.Dark })
                && definition.Attributes.Identity.SetKey == new StringName("set.wizard"), "dark set identity");
            Check(economy.AcquireAndPlace(match, definition, definition.InitialLevel, CardAcquisitionSource.Reward).IsSuccess, "equip");
        }
        Check(new CardSetEvaluator().Evaluate(match, registry.Sets).Count == 2, "cumulative set thresholds");
        var setup = new BattleSetupFactory(registry.Sets).Create(match, enemy, 42, new BattleTick(1), new BattleTick(100));
        Check(setup.Player.CardDefinitions.Count == registry.Cards.Count && setup.Player.Sets.Count == 2, "frozen catalog and set abilities");
        Check(new CombatSimulator().Simulate(setup).Events.OfType<CardSummonedEvent>().Count() == 1, "catalog random summon in real setup");
    }

    private static void WandDamage()
    {
        foreach (var level in new[] { 3, 4 })
        {
            var wand = Card(new MagisterScepterCardDefinition(), level, 0);
            var result = new CombatSimulator().Simulate(Setup(Side([wand], []), false, 51));
            var hit = result.Events.OfType<DamageDealtEvent>().Single(item => item.SourceCardId == wand.EntityId);
            Check(hit.Tick.Value == 50 && hit.RawDamage == (level == 3 ? 100 : 200), "five second zero mana attribute attack");
        }
    }

    private static void DestructionOrder()
    {
        foreach (var reverse in new[] { false, true })
        foreach (var swap in new[] { false, true })
        {
            CardDefinition first = swap ? new WaterSlimeCardDefinition() : new SkeletonCardDefinition();
            CardDefinition second = swap ? new SkeletonCardDefinition() : new WaterSlimeCardDefinition();
            var victim = new CardBattleSetup(EntityId.New(), 0, 0, 10,
                [new AbilityDefinition(new StringName("verification.order.victim"), AbilityActivation.Active,
                    AbilityTarget.SelfCard, 0, 10, [new DestroyCardEffectDefinition(false)])], UseLegacyAttack: false) { Level = 3 };
            var listener = new CardBattleSetup(EntityId.New(), 1, 0, 0,
                [new AbilityDefinition(new StringName("verification.order.first_echo"), AbilityActivation.EchoOnAlliedCardDestroyed,
                    AbilityTarget.AlliedHero, 0, 0, [new SummonAtDestroyedCardEffectDefinition(first)], triggerCardSize: CardSize.Small)],
                UseLegacyAttack: false, OccupiedSlots: 2) { Level = 3 };
            var later = listener with { EntityId = EntityId.New(), BoardStart = 3,
                Abilities = [new AbilityDefinition(new StringName("verification.order.second_echo"), AbilityActivation.EchoOnAlliedCardDestroyed,
                    AbilityTarget.AlliedHero, 0, 0, [new SummonAtDestroyedCardEffectDefinition(second)], triggerCardSize: CardSize.Small)] };
            var echoed = new CombatSimulator().Simulate(Setup(Side([victim, listener, later], []), reverse, 11));
            var replacement = echoed.Events.OfType<CardSummonedEvent>().Single();
            Check(replacement.SourceCardId == listener.EntityId && replacement.CardKey == first.Attributes.Identity.Key
                && replacement.BoardStart == 0, "same destruction event resolves left to right and never overwrites first summon");

            var passiveVictim = victim with { BoardStart = 2, Abilities = [], CooldownTicks = 0 };
            var left = listener with { BoardStart = 0, Abilities = [new AbilityDefinition(new StringName("verification.order.left_replace"),
                AbilityActivation.Active, AbilityTarget.SelfCard, 0, 10,
                [new DestroyBattlefieldCardsEffectDefinition(CardSize.Small), new SummonAdjacentCardEffectDefinition(first, AdjacentCardSide.Right)])] };
            var right = later with { BoardStart = 3, Abilities = [new AbilityDefinition(new StringName("verification.order.right_replace"),
                AbilityActivation.Active, AbilityTarget.SelfCard, 0, 10,
                [new DestroyBattlefieldCardsEffectDefinition(CardSize.Small), new SummonAdjacentCardEffectDefinition(second, AdjacentCardSide.Left)])] };
            var setup = Setup(Side([left, passiveVictim, right], []), reverse, 11);
            var chained = new CombatSimulator().Simulate(setup);
            var summons = chained.Events.OfType<CardSummonedEvent>().ToArray();
            Check(summons.Length == 2 && summons[0].CardKey == first.Attributes.Identity.Key
                && summons[1].CardKey == second.Attributes.Identity.Key && summons.All(item => item.BoardStart == 2 && item.Tick.Value == 10),
                "active replacements re-read current position: A to B to C");
            var destruction = chained.Events.OfType<CardDestroyedEvent>().ToArray();
            Check(destruction.Length == 2, "both A and newly summoned B are destroyed");
            var state = chained.States.Last(frame => frame.Tick.Value == 10);
            Check(state.Cards.Single(card => card.Id == summons[0].SummonedCardId).Destroyed
                && !state.Cards.Single(card => card.Id == summons[1].SummonedCardId).Destroyed, "only final replacement survives");
            Check(chained.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events), "replacement order replay");
        }
    }

    private static CardBattleSetup Card(CardDefinition definition, int level, int start)
    {
        var card = new EntityFactory().CreateCard(definition, level); var identity = card.Attributes.Identity;
        return new CardBattleSetup(card.Id, start, card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
            card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks), card.Abilities, UseLegacyAttack: false,
            Tags: card.Tags, OccupiedSlots: identity.OccupiedSlots, ElementKeys: identity.ElementKeys)
            { Identity = identity, Level = level, CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues() };
    }
    private static BattleSideSetup Side(CardBattleSetup[] cards, CardDefinition[] pool, CardSetBattleSetup[]? sets = null) =>
        new(new HeroBattleSetup(EntityId.New(), 100000, 0, ManaRegen: 0), cards, sets: sets)
            { BattlefieldCapacity = 10, CardDefinitions = Array.AsReadOnly(pool) };
    private static BattleSetup Setup(BattleSideSetup own, bool reverse, int ticks)
    {
        var enemy = Side([], []);
        return new BattleSetup(reverse ? enemy : own, reverse ? own : enemy, 42, new BattleTick(ticks), new BattleTick(10000));
    }
    internal static async Task Capture(Control owner)
    {
        var registry = DefinitionRegistry.Scan(typeof(WizardSetDefinition).Assembly);
        var details = new CardDetailsView { Theme = MatchTheme.Create() }; owner.AddChild(details);
        details.SetEntries(CardCatalogQuery.Capture(registry));
        var index = 0;
        foreach (var definition in new CardDefinition[] { new MagisterCrownCardDefinition(), new MagisterBootsCardDefinition(), new MagisterScepterCardDefinition() })
        {
            var snapshot = new CardPreviewQuery(registry).Describe(MatchDisplayQuery.FromOffer(ShopOffer.Create(definition)), null);
            var face = new CardItemView { Position = new Vector2(30, 40), Size = new Vector2(index == 2 ? 200 : 100, 200) };
            owner.AddChild(face); face.Render(snapshot, new CardDisplayAdapter());
            details.ShowCard(snapshot, new Vector2(270, 40), owner.GetViewportRect().Size);
            for (var frame = 0; frame < 12; frame++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw(); using var image = owner.GetViewport().GetTexture().GetImage();
            image.SavePng($"res://output/wizard-summoning/card-{index++}.png");
            face.QueueFree();
        }
        details.QueueFree();
    }

    private static void Check(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("巫师装备验证失败：" + message); }
}
