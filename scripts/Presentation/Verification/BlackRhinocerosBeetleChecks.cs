using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Project_Star.Application.Content;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.Playtest;
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

// 麻痹针蜂迟缓、黑犀金龟护甲、出售与永久冷却倍率的集成验证（表现层验证模块）。
internal static class BlackRhinocerosBeetleChecks
{
    internal static bool LevelsAndSlow()
    {
        var definition = new ParalyzingNeedleWaspCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.Key != new StringName("card.paralyzing_needle_wasp") || identity.DisplayName != "麻痹针蜂"
            || identity.FactionKey != new StringName("mona") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Lightning })
            || !definition.Tags.Contains(GameTags.Insect) || definition.InitialLevel != 1
            || definition.SupportsLevel(5) || TagDisplayNames.Get(GameTags.Insect) != "虫族") return false;
        var factory = new EntityFactory();
        for (var level = 1; level <= 4; level++)
        {
            var card = factory.CreateCard(definition, level);
            if (card.CooldownMultiplier != 1m || card.Abilities.Single().CooldownTicks != 40
                || card.Abilities.Single().ManaCost != 0
                || card.OnSellReward is not ReduceLeftmostElementCardCooldownOnSellDefinition { Percent: var percent, ElementKey: var element }
                || percent != level || element != GameElements.Lightning) return false;
        }
        var source = EntityId.New(); var first = EntityId.New(); var second = EntityId.New();
        var bench = EntityId.New(); var destroyed = EntityId.New(); var ally = EntityId.New();
        var openingDestruction = Ability("destroy", AbilityActivation.PassiveOnBattleStart, 0,
            new DestroyCardEffectDefinition(false));
        var selections = new HashSet<EntityId>();
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var setup = Battle([
                new CardBattleSetup(source, 0, 0, 40, definition.GetLevel(1)!.Abilities, UseLegacyAttack: false),
                new CardBattleSetup(ally, 1, 0, 0, [], UseLegacyAttack: false)], [
                new CardBattleSetup(first, 0, 0, 0, [], UseLegacyAttack: false),
                new CardBattleSetup(second, 1, 0, 0, [], UseLegacyAttack: false),
                new CardBattleSetup(bench, 2, 0, 0, [], IsOnBench: true, UseLegacyAttack: false),
                new CardBattleSetup(destroyed, 3, 0, 0, [openingDestruction], UseLegacyAttack: false)], 40, seed);
            var result = new CombatSimulator().Simulate(setup);
            var affected = result.States[^1].Cards.Where(card => card.Slow > 0).ToArray();
            if (affected.Length != 1 || affected[0].Slow != 10
                || affected[0].Id != first && affected[0].Id != second
                || result.Events.OfType<AbilityActivatedEvent>().Single(item => item.SourceCardId == source).Tick.Value != 40
                || result.Events.OfType<ManaChangedEvent>().Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            selections.Add(affected[0].Id);
        }
        if (selections.Count != 2) return false;
        var beetle = new CardBattleSetup(source, 0, 0, 40, definition.GetLevel(1)!.Abilities, UseLegacyAttack: false);
        var enemy = new CardBattleSetup(first, 0, 0, 45,
            [Ability("enemy", AbilityActivation.Active, 45, new DamageEffectDefinition(1))], UseLegacyAttack: false);
        var delayed = new CombatSimulator().Simulate(Battle([beetle], [enemy], 50));
        if (delayed.Events.OfType<AbilityActivatedEvent>().Single(item => item.SourceCardId == first).Tick.Value != 50
            || delayed.States.Last(frame => frame.Tick.Value == 49).Cards.Single(card => card.Id == first).Slow != 1
            || delayed.States[^1].Cards.Single(card => card.Id == first).Slow != 0) return false;
        var repeated = new CombatSimulator().Simulate(Battle([beetle, beetle with { EntityId = ally, BoardStart = 1 }], [enemy], 40));
        if (repeated.States[^1].Cards.Single(card => card.Id == first).Slow != 20) return false;
        var flying = enemy with { Abilities = [Ability("flying", AbilityActivation.PassiveOnBattleStart, 0,
            new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, true))] };
        var halved = new CombatSimulator().Simulate(Battle([beetle], [flying], 40));
        if (halved.States[^1].Cards.Single(card => card.Id == first).Slow != 5) return false;
        var empty = new CombatSimulator().Simulate(Battle([beetle], [enemy with { IsOnBench = true }], 40));
        return !empty.Events.OfType<StatusChangedEvent>().Any();
    }

    internal static bool LevelsAndArmor()
    {
        var definition = new BlackRhinocerosBeetleCardDefinition();
        for (var level = 1; level <= 4; level++)
        foreach (var reverse in new[] { false, true })
        {
            var card = new EntityFactory().CreateCard(definition, level);
            var amount = 10 << (level - 1);
            var source = new CardBattleSetup(card.Id, 0, 0, 40, card.Abilities,
                UseLegacyAttack: false, ArmorAmount: amount)
                { CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues(), Level = level };
            var sides = reverse ? Battle([], [source], 80) : Battle([source], [], 80);
            var setup = new BattleSetup(sides.Player, sides.Opponent, 42, new BattleTick(80), new BattleTick(81));
            var result = new CombatSimulator().Simulate(setup);
            var armor = reverse ? result.States[^1].Opponent.Armor : result.States[^1].Player.Armor;
            if (armor != amount * 2 || result.Events.OfType<StatusChangedEvent>().Any()
                || !result.Events.OfType<AbilityActivatedEvent>().Select(e => e.Tick.Value)
                    .SequenceEqual(new long[] { 40, 80 })
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            var benched = new CombatSimulator().Simulate(reverse
                ? Battle([], [source with { IsOnBench = true }], 80)
                : Battle([source with { IsOnBench = true }], [], 80));
            if (benched.Events.OfType<AbilityActivatedEvent>().Any()) return false;
        }
        return SaleTargetsAndPersistence(new ParalyzingNeedleWaspCardDefinition(), GameElements.Lightning);
    }

    internal static bool SaleTargetsAndPersistence(CardDefinition? soldDefinition = null, StringName? elementKey = null)
    {
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
        var economy = new CardEconomyService(factory, board);
        var beetleDefinition = soldDefinition ?? new BlackRhinocerosBeetleCardDefinition();
        var targetElement = elementKey ?? GameElements.Earth;
        var earthDefinition = new ElementFixture(targetElement);
        for (var level = 1; level <= 4; level++)
        {
            var match = Match(factory); var opponent = Match(factory);
            var general = factory.CreateCard(new ElementFixture(targetElement == GameElements.Lightning ? GameElements.Earth : GameElements.Lightning));
            var left = factory.CreateCard(earthDefinition); var right = factory.CreateCard(earthDefinition);
            var benchEarth = factory.CreateCard(earthDefinition); var sold = factory.CreateCard(beetleDefinition, level);
            foreach (var card in new[] { general, left, right, benchEarth, sold }) match.Player.Inventory.Add(card);
            board.PlaceCard(match, general.Id, BoardZone.Battlefield, 0);
            board.PlaceCard(match, left.Id, BoardZone.Battlefield, 2);
            board.PlaceCard(match, right.Id, BoardZone.Battlefield, 4);
            board.PlaceCard(match, benchEarth.Id, BoardZone.Bench, 0);
            board.PlaceCard(match, sold.Id, BoardZone.Bench, 1);
            var before = MatchSnapshot.From(match).Cards.Single(card => card.Id == left.Id);
            if (economy.CheckSale(match, sold.Id).IsFailure || left.CooldownMultiplier != 1m) return false;
            sold.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, -1);
            var beforeFailedSale = match.Player.Wealth;
            if (economy.SellFromBoard(match, sold.Id).IsSuccess || left.CooldownMultiplier != 1m
                || !match.Board.Contains(sold.Id) || match.Player.Inventory.Find(sold.Id) is null
                || match.Player.Wealth != beforeFailedSale) return false;
            sold.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, 0);
            if (economy.SellFromBoard(match, sold.Id).IsFailure) return false;
            var multiplier = (100 - level) / 100m;
            if (left.CooldownMultiplier != multiplier || right.CooldownMultiplier != 1m
                || general.CooldownMultiplier != 1m || benchEarth.CooldownMultiplier != 1m
                || before.CooldownMultiplier != 1m) return false;
            var frozen = new BattleSetupFactory().Create(match, opponent, 42, new BattleTick(80));
            var after = MatchSnapshot.From(match).Cards.Single(card => card.Id == left.Id);
            if (after.CooldownMultiplier != multiplier
                || !CardDisplayAdapter.Details(after).Contains($"当前 {4 * multiplier:0.##}秒")) return false;
            var wealth = match.Player.Wealth;
            if (economy.SellFromBoard(match, sold.Id).IsSuccess || match.Player.Wealth != wealth
                || left.CooldownMultiplier != multiplier) return false;
            var another = factory.CreateCard(beetleDefinition, level);
            match.Player.Inventory.Add(another);
            if (economy.SellCard(match, another.Id).IsFailure || left.CooldownMultiplier != multiplier * multiplier
                || frozen.Player.Cards.Single(card => card.EntityId == left.Id).CooldownMultiplier != multiplier
                || after.CooldownMultiplier != multiplier) return false;
            var replay = new CombatSimulator().Simulate(frozen);
            if (replay.States[0].Cards.Single(card => card.Id == left.Id).CooldownUnits.Single() != 80 * multiplier
                || replay.Events.OfType<AbilityActivatedEvent>().First(item => item.SourceCardId == left.Id).Tick.Value
                    != (long)Math.Ceiling(40 * multiplier)) return false;
            factory.ApplyCardLevel(left, earthDefinition, 2);
            if (left.CooldownMultiplier != multiplier * multiplier) return false;
        }
        // 出售来源自身不作为目标；升级更新出售比例，不改变其他实例。
        var session = Match(factory); var target = factory.CreateCard(earthDefinition);
        var upgraded = factory.CreateCard(beetleDefinition);
        session.Player.Inventory.Add(target); session.Player.Inventory.Add(upgraded);
        board.PlaceCard(session, upgraded.Id, BoardZone.Battlefield, 0);
        board.PlaceCard(session, target.Id, BoardZone.Battlefield, 2);
        factory.ApplyCardLevel(upgraded, beetleDefinition, 4);
        if (economy.SellFromBoard(session, upgraded.Id).IsFailure || target.CooldownMultiplier != 0.96m) return false;
        board.PlaceCard(session, target.Id, BoardZone.Bench, 0);
        var nonEarth = factory.CreateCard(new ElementFixture(GameElements.General));
        session.Player.Inventory.Add(nonEarth); board.PlaceCard(session, nonEarth.Id, BoardZone.Battlefield, 0);
        var noTarget = factory.CreateCard(beetleDefinition);
        session.Player.Inventory.Add(noTarget);
        return economy.SellCard(session, noTarget.Id).IsSuccess && target.CooldownMultiplier == 0.96m
            && nonEarth.CooldownMultiplier == 1m && factory.CreateCard(earthDefinition).CooldownMultiplier == 1m;
    }

    internal static bool MultiplierBattleTiming()
    {
        var source = EntityId.New();
        var attack = Ability("timing", AbilityActivation.Active, 40, new DamageEffectDefinition(1));
        var card = new CardBattleSetup(source, 0, 0, 40, [attack], UseLegacyAttack: false)
            { CooldownMultiplier = 0.96m };
        var result = new CombatSimulator().Simulate(Battle([card], [], 118));
        if (result.States[0].Cards.Single().CooldownUnits.Single() != 76.8m
            || !result.Events.OfType<AbilityActivatedEvent>().Select(item => item.Tick.Value).SequenceEqual(new long[] { 39, 78, 117 })) return false;
        var growing = card with { Abilities = [Ability("growing", AbilityActivation.Active, 40,
            new DamageEffectDefinition(1), new IncreaseSourceCooldownEffectDefinition(20, true))] };
        var increased = new CombatSimulator().Simulate(Battle([growing], [], 98));
        if (!increased.Events.OfType<AbilityActivatedEvent>().Select(item => item.Tick.Value).SequenceEqual(new long[] { 39, 97 })
            || increased.States.Last(frame => frame.Tick.Value == 39).Cards.Single().CooldownUnits.Single() != 115.2m) return false;
        var hasted = card with { Abilities = [attack, Ability("haste", AbilityActivation.PassiveOnBattleStart, 0,
            new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 10))] };
        var fast = new CombatSimulator().Simulate(Battle([hasted], [], 30));
        if (fast.Events.OfType<AbilityActivatedEvent>().Single(item => item.Tick.Value > 0).Tick.Value != 29) return false;
        var charger = new CardBattleSetup(EntityId.New(), 1, 0, 10,
            [Ability("charge", AbilityActivation.Active, 10,
                new ChargeRandomOtherAlliedElementCardEffectDefinition(GameElements.Earth, 10), new DestroyCardEffectDefinition(false))],
            UseLegacyAttack: false);
        var charged = new CombatSimulator().Simulate(Battle([card with { ElementKeys = [GameElements.Earth] }, charger], [], 30));
        if (charged.Events.OfType<AbilityActivatedEvent>().Single(item => item.SourceCardId == source).Tick.Value != 29) return false;
        var zero = new CombatSimulator().Simulate(Battle([card with { CooldownMultiplier = 0m }], [], 30));
        if (!zero.Events.OfType<AbilityActivatedEvent>().Select(item => item.Tick.Value).SequenceEqual(new long[] { 10, 20, 30 })) return false;
        var unchanged = new CombatSimulator().Simulate(Battle([card with { CooldownMultiplier = 1m }], [], 80));
        if (!unchanged.Events.OfType<AbilityActivatedEvent>().Select(item => item.Tick.Value).SequenceEqual(new long[] { 40, 80 })) return false;
        foreach (var invalid in new[] { -0.01m, 1.01m })
        {
            try { new CombatSimulator().Simulate(Battle([card with { CooldownMultiplier = invalid }], [], 1)); return false; }
            catch (ArgumentOutOfRangeException) { }
        }
        return true;
    }

    private static MatchSession Match(EntityFactory factory) =>
        new CreateMatchService(factory).Create(42, 100, new MonaHeroDefinition());

    private static BattleSetup Battle(CardBattleSetup[] player, CardBattleSetup[] opponent, int timeout, ulong seed = 42) =>
        new(new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), player),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), opponent), seed, new BattleTick(timeout));

    private static AbilityDefinition Ability(string suffix, AbilityActivation activation, int cooldown, params EffectDefinition[] effects) =>
        new(new StringName("verification.beetle." + suffix), activation, AbilityTarget.SelfCard, 0, cooldown, effects);

    // 只供出售目标和等级保留验证的通用元素卡，不进入正式卡池。
    private sealed class ElementFixture : CardDefinition
    {
        public ElementFixture(StringName element)
            : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
                new StringName("verification.beetle." + element), "元素目标", GameFactions.Neutral, CardSize.Small, [element]),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 40 })),
                new TagSet(), [Ability("target", AbilityActivation.Active, 40, new DamageEffectDefinition(1))]) { }
    }
    internal static async Task Capture(Node owner)
    {
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new ParalyzingNeedleWaspCardDefinition()));
        var adapter = new CardDisplayAdapter();
        using var raw = adapter.Artwork(snapshot.Illustration).GetImage();
        if (!raw.HasMipmaps() || raw.GetWidth() * 2 != raw.GetHeight())
            throw new InvalidOperationException("麻痹针蜂原画比例或mipmap不合格。");
        GD.Print($"麻痹针蜂原画：{raw.GetWidth()}×{raw.GetHeight()}，mipmap层数{raw.GetMipmapCount()}");
        var viewport = new SubViewport { Size = new Vector2I(420, 460), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        var preview = new CardPreviewView { Size = owner.GetViewport().GetVisibleRect().Size, Theme = MatchTheme.Create() };
        owner.AddChild(preview);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(420, 460) });
            foreach (var (position, size) in new[] { (new Vector2(24, 24), new Vector2(200, 400)),
                (new Vector2(260, 24), new Vector2(100, 200)) })
            {
                var face = new CardItemView { Position = position, Size = size };
                viewport.AddChild(face); face.Render(snapshot, adapter);
            }
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(BlackRhinocerosBeetleChecks).Assembly)));
            preview.Open(snapshot, null, null);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            using var detail = owner.GetViewport().GetTexture().GetImage();
            if (image.SavePng("res://output/needle-wasp/card-face.png") != Error.Ok
                || detail.SavePng("res://output/needle-wasp/preview.png") != Error.Ok)
                throw new InvalidOperationException("麻痹针蜂卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); owner.RemoveChild(preview); preview.Free(); }
    }
}
