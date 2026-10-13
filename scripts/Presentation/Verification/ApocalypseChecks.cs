using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Board;
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
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 天启内容、全场日蚀边界、套装生命周期和冻结回放验证。
internal static class ApocalypseChecks
{
    private static CardDefinition[] Definitions() =>
        [new DamosCardDefinition(), new IgnisCardDefinition(), new VirisCardDefinition(), new DebilisCardDefinition()];

    internal static bool LevelsAndNaturalEntry()
    {
        var catalog = DefinitionRegistry.Scan(typeof(ApocalypseChecks).Assembly);
        var definitions = Definitions();
        var elements = new[] { GameElements.General, GameElements.Fire, GameElements.Wood, GameElements.Dark };
        int[][] amounts = [[20, 40, 80, 160], [12, 24, 48], [24, 48], []];
        var attributes = new[] { GameAttributeKeys.AttackDamage, GameAttributeKeys.Burn, GameAttributeKeys.Poison };
        for (var index = 0; index < definitions.Length; index++)
        {
            var definition = definitions[index]; var identity = definition.Attributes.Identity;
            var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
            if (!catalog.Cards.ContainsKey(identity.Key) || definition.InitialLevel != index + 1
                || identity.FactionKey != new StringName("neutral") || identity.Size != CardSize.Medium
                || identity.SetKey != new StringName("set.apocalypse") || !definition.Tags.Contains(GameTags.Human)
                || !identity.ElementKeys.SequenceEqual(new[] { elements[index] })
                || texture is null || texture.GetWidth() != texture.GetHeight()
                || definition.SupportsLevel(5) || index > 0 && definition.SupportsLevel(index)) return false;
            for (var level = index + 1; level <= 4; level++)
            {
                var source = Card(definition, level, 0);
                if (source.CooldownTicks != (index == 3 ? 120 : 50)
                    || source.Abilities!.Any(ability => ability.ManaCost != 0)) return false;
                var rules = CardDisplayAdapter.Rules(MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level)));
                if (!rules.Any(rule => rule.Contains("日蚀"))) return false;
                if (index == 3) continue;
                var setup = Battle([source], [], 150, 100);
                var result = new CombatSimulator().Simulate(setup);
                var expected = amounts[index][level - index - 1];
                var growth = result.Events.OfType<CardAttributeChangedEvent>().Single();
                if (growth.Tick.Value != 100 || growth.AttributeKey != attributes[index]
                    || growth.Amount != expected || growth.CurrentValue != expected * 2
                    || result.States.First(frame => frame.Eclipse).Tick.Value != 100
                    || result.States.Last(frame => frame.Tick.Value == 99).Eclipse
                    || !result.States.Last(frame => frame.Tick.Value == 100).Eclipse) return false;
                if (index == 0)
                {
                    var hits = result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceKind == DamageSourceKind.Card).ToArray();
                    if (!hits.Select(hit => hit.RawDamage).SequenceEqual(new[] { expected, expected * 2, expected * 2 })) return false;
                }
                else
                {
                    var statuses = result.Events.OfType<StatusChangedEvent>().ToArray();
                    if (!statuses.Select(status => status.Amount).SequenceEqual(new[] { expected, expected * 2, expected * 2 })) return false;
                }
                if (result.PermanentAttributeBonuses.Count != 0
                    || source.CombatValues[attributes[index]] != expected
                    || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            }
        }
        return catalog.Sets.ContainsKey(new StringName("set.apocalypse"));
    }

    internal static bool EarlyEntryAndSetBattle()
    {
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        {
            var definitions = Definitions();
            var cards = definitions.Select((definition, index) => Card(definition, definition.InitialLevel, index * 2)).ToArray();
            var sets = new ApocalypseSetDefinition().Thresholds.Where(threshold => threshold.RequiredDistinctCards >= 3)
                .Select(threshold => new CardSetBattleSetup(EntityId.New(), threshold.Abilities)).ToArray();
            var setup = Battle(cards, [], 250, 300, side, sets);
            var result = new CombatSimulator().Simulate(setup);
            var replay = new CombatSimulator().Simulate(setup);
            if (result.States.First(frame => frame.Eclipse).Tick.Value != 120
                || result.Events.OfType<CardAttributeChangedEvent>().Count() != 3
                || !result.Events.SequenceEqual(replay.Events)
                || !result.States.Select(frame => (frame.Tick, frame.EventCount, frame.Eclipse, frame.Player, frame.Opponent))
                    .SequenceEqual(replay.States.Select(frame => (frame.Tick, frame.EventCount, frame.Eclipse, frame.Player, frame.Opponent)))) return false;
            var frame = result.States.Last(state => state.Tick.Value == 120);
            var own = side == SideId.Player ? frame.Player : frame.Opponent;
            if (own.MaxHealth != 1500000 || own.Health != 1500000) return false;
            var damages = result.Events.OfType<DamageDealtEvent>()
                .Where(hit => hit.SourceKind == DamageSourceKind.Eclipse && hit.Tick.Value == 130).ToArray();
            if (damages.Single(hit => hit.TargetSide == side).RawDamage != 2
                || damages.Single(hit => hit.TargetSide != side).RawDamage != 4) return false;
            if (result.Events.OfType<DamageDealtEvent>().Single(hit => hit.SourceKind == DamageSourceKind.Card && hit.Tick.Value == 150).RawDamage != 40
                || !result.Events.OfType<StatusChangedEvent>().Where(status => status.Tick.Value == 150)
                    .Select(status => status.Amount).SequenceEqual(new[] { 24, 48 })) return false;
            // 24秒再次发动不会重新进入、重置曲线或重复加生命。
            var later = result.States.Last(state => state.Tick.Value == 240);
            if ((side == SideId.Player ? later.Player : later.Opponent).MaxHealth != 1500000
                || !later.Eclipse || result.PermanentAttributeBonuses.Count != 0) return false;
        }
        // 双方死亡之手同Tick发动，全场只进入一次。
        var death = Card(new DebilisCardDefinition(), 4, 0);
        var simultaneous = new CombatSimulator().Simulate(Battle([death], [death with { EntityId = EntityId.New() }], 240, 300));
        var entry = simultaneous.States.First(frame => frame.Eclipse);
        return entry.Tick.Value == 120 && simultaneous.States.SkipWhile(frame => !frame.Eclipse).All(frame => frame.Eclipse)
            && !new CombatSimulator().Simulate(Battle([death with { IsOnBench = true }], [], 150, 200)).States.Any(frame => frame.Eclipse);
    }

    internal static bool EntryBoundaries()
    {
        var attack = Card(new DamosCardDefinition(), 1, 0);
        var bench = attack with { EntityId = EntityId.New(), IsOnBench = true };
        var destroyed = attack with { EntityId = EntityId.New(), BoardStart = 4 };
        var sets = new ApocalypseSetDefinition().Thresholds.Where(threshold => threshold.RequiredDistinctCards >= 3)
            .Select(threshold => new CardSetBattleSetup(EntityId.New(), threshold.Abilities)).ToArray();
        var runtime = new BattleRuntime(Battle([attack, bench, destroyed], [], 10, 5, sets: sets));
        runtime.Cards.Single(card => card.EntityId == destroyed.EntityId).Destroyed = true;
        runtime.PlayerHero.MaxHealth = 101; runtime.PlayerHero.Health = 60;
        runtime.OpponentHero.Armor = 10;
        runtime.Tick = new BattleTick(5); CombatSimulator.EnterEclipse(runtime);
        if (runtime.PlayerHero.MaxHealth != 151 || runtime.PlayerHero.Health != 110
            || runtime.Events.OfType<CardAttributeChangedEvent>().Count() != 1) return false;
        CombatSimulator.EnterEclipse(runtime);
        runtime.Tick = new BattleTick(10); BattleStatusResolver.ApplyEclipse(runtime, Battle([], [], 10, 5));
        var hits = runtime.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceKind == DamageSourceKind.Eclipse).ToArray();
        if (hits.Single(hit => hit.TargetSide == SideId.Opponent).ArmorAbsorbed != 2
            || runtime.PlayerHero.MaxHealth != 151 || runtime.EclipseTime.Value != 5) return false;
        // 进入后召唤／修复不补发进入增益；新场恢复。
        runtime.Cards.Single(card => card.EntityId == destroyed.EntityId).Destroyed = false;
        CombatSimulator.EnterEclipse(runtime);
        if (runtime.Cards.Single(card => card.EntityId == destroyed.EntityId).GetCombatAttribute(GameAttributeKeys.AttackDamage) != 20) return false;
        var fresh = new BattleRuntime(Battle([attack], [], 1, 2));
        return !fresh.EclipseEntered && fresh.Cards.Single().GetCombatAttribute(GameAttributeKeys.AttackDamage) == 20;
    }

    internal static bool RoundSetCooldowns()
    {
        var catalog = DefinitionRegistry.Scan(typeof(ApocalypseChecks).Assembly);
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver(), catalog.Sets);
        var session = new CreateMatchService(factory).Create(42, 0, new PaladinHeroDefinition());
        CardInstance Add(CardDefinition definition, BoardZone zone, int start)
        {
            var card = factory.CreateCard(definition); session.Player.Inventory.Add(card);
            if (board.PlaceCard(session, card.Id, zone, start).IsFailure) throw new InvalidOperationException("天启验证放置失败。");
            return card;
        }
        var damos = Add(new DamosCardDefinition(), BoardZone.Battlefield, 0);
        var duplicate = Add(new DamosCardDefinition(), BoardZone.Battlefield, 2);
        var rounds = new RoundIncomeService(catalog.Sets, catalog);
        session.Progress.Round = 2; rounds.SettleCurrentRound(session);
        if (damos.CooldownMultiplier != 1m || duplicate.CooldownMultiplier != 1m) return false;
        var ignis = Add(new IgnisCardDefinition(), BoardZone.Battlefield, 4);
        var bench = Add(new VirisCardDefinition(), BoardZone.Bench, 0);
        var unrelated = Add(new ShieldBearerCardDefinition(), BoardZone.Battlefield, 6);
        rounds.SettleCurrentRound(session);
        if (ignis.CooldownMultiplier != 1m) return false;
        session.Progress.Round = 3; rounds.SettleCurrentRound(session); rounds.SettleCurrentRound(session);
        if (damos.CooldownMultiplier != .98m || duplicate.CooldownMultiplier != .98m || ignis.CooldownMultiplier != .98m
            || bench.CooldownMultiplier != 1m || unrelated.CooldownMultiplier != 1m) return false;
        session.Progress.Round = 4; rounds.SettleCurrentRound(session);
        if (damos.CooldownMultiplier != .9604m) return false;
        factory.ApplyCardLevel(damos, new DamosCardDefinition(), 2);
        if (damos.CooldownMultiplier != .9604m) return false;
        board.PlaceCard(session, ignis.Id, BoardZone.Bench, 2);
        session.Progress.Round = 5; rounds.SettleCurrentRound(session);
        return damos.CooldownMultiplier == .9604m && duplicate.CooldownMultiplier == .9604m
            && new CardSetEvaluator().Evaluate(session, catalog.Sets).All(active => active.SetKey != new StringName("set.apocalypse"));
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(1368, 382), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(1368, 382) });
            var definitions = Definitions();
            var set = new ApocalypseSetDefinition();
            var setSnapshot = new CardSetSnapshot(set.Attributes.Identity.Key, set.Attributes.Identity.DisplayName, 0,
                set.Thresholds.Select(threshold => new CardSetThresholdSnapshot(threshold.RequiredDistinctCards, false, threshold.Abilities)).ToArray());
            for (var i = 0; i < definitions.Length; i++)
            {
                var face = new CardItemView { Position = new Vector2(20 + i * 337, 20), Size = new Vector2(317, 317) };
                viewport.AddChild(face);
                face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(definitions[i])) with { Set = setSnapshot }, new CardDisplayAdapter());
                var label = new Label { Text = definitions[i].Attributes.Identity.DisplayName, Position = new Vector2(20 + i * 337, 344) };
                viewport.AddChild(label);
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/apocalypse/card-faces.png") != Error.Ok) throw new InvalidOperationException("天启卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardBattleSetup Card(CardDefinition definition, int level, int start)
    {
        var card = new EntityFactory().CreateCard(definition, level);
        return new CardBattleSetup(card.Id, start, card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
            card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks), card.Abilities,
            UseLegacyAttack: false, Tags: card.Tags, OccupiedSlots: 2, ElementKeys: card.Attributes.Identity.ElementKeys)
        { Level = level, CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues() };
    }

    private static BattleSetup Battle(IReadOnlyList<CardBattleSetup> cards, IReadOnlyList<CardBattleSetup> enemies,
        int timeout, int eclipse, SideId side = SideId.Player, IReadOnlyList<CardSetBattleSetup>? sets = null)
    {
        var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000000, 0), cards, sets: sets);
        var other = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000000, 0), enemies);
        return new BattleSetup(side == SideId.Player ? own : other, side == SideId.Player ? other : own,
            42, new BattleTick(timeout), new BattleTick(eclipse));
    }
}
