using System;
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
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 登神者分级、即时任务、跨战斗成长及身份回放验证（表现层）。
internal static class AscendantChecks
{
    internal static bool LevelsAndThresholds()
    {
        var definition = new AscendantCardDefinition();
        var identity = definition.Attributes.Identity;
        var texture = ResourceLoader.Load<Texture2D>(identity.Illustration.ToString());
        if (!DefinitionRegistry.Scan(typeof(AscendantChecks).Assembly).Cards.ContainsKey(identity.Key)
            || identity.Key != new StringName("card.ascendant") || identity.DisplayName != "登神者"
            || identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Medium
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General }) || definition.InitialLevel != 1
            || definition.SupportsLevel(5) || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Human)
            || definition.Tags.Contains(GameTags.Deity) || TagDisplayNames.Get(GameTags.Deity) != "神明"
            || identity.DescriptionEntries.Count != 5 || texture is null || texture.GetWidth() != texture.GetHeight()) return false;
        for (var level = 1; level <= 4; level++)
        {
            var context = new Context(level);
            var initial = MatchSnapshot.From(context.Match);
            var setup = context.Setup(2730);
            var simulator = new CombatSimulator();
            var result = simulator.Simulate(setup);
            var hits = result.Events.OfType<DamageDealtEvent>().Where(e => e.SourceCardId == context.Card.Id).ToArray();
            var statuses = result.Events.OfType<StatusChangedEvent>().Where(e => e.Status == BattleStatus.Burn).ToArray();
            var last = State(result, context.Card.Id);
            if (hits.Length != 122 || hits[0].Tick.Value != 30 || hits[0].RawDamage != 1
                || hits.Single(e => e.Tick.Value == 600).RawDamage != 1 + 19 * level
                || hits.Single(e => e.Tick.Value == 630).RawDamage != 81 + 20 * level
                || hits.Count(e => e.Tick.Value == 1800) != 1 || hits.Count(e => e.Tick.Value == 1830) != 2
                || statuses.Length != 2 || statuses.Any(e => e.Tick.Value != 2730 || e.Amount != (81 + 120 * level) / 10)
                || last.Values[GameAttributeKeys.AttackDamage] != 81 + 122 * level
                || last.Values[GameAttributeKeys.Multicast] != 1 || !last.QuestTags.Contains(GameTags.Human)
                || !last.QuestTags.Contains(GameTags.Deity) || !last.QuestElementKeys.SequenceEqual(new[] { GameElements.Light })
                || !last.Quests.Select(quest => quest.Progress).SequenceEqual(new[] { 20, 60, 120 })
                || result.PermanentAttributeBonuses.Count != 122
                || result.PermanentAttributeBonuses.Any(bonus => bonus.Amount != level || bonus.CardId != context.Card.Id)
                || !result.Events.SequenceEqual(simulator.Simulate(setup).Events)
                || !result.PermanentAttributeBonuses.SequenceEqual(simulator.Simulate(setup).PermanentAttributeBonuses)
                || context.Attack != 1 || context.Card.Quests.Any(quest => context.Card.GetQuestProgress(quest.Key) != 0)) return false;
            if (StateAt(result, context.Card.Id, 600).Values[GameAttributeKeys.AttackDamage] != 81 + 20 * level
                || StateAt(result, context.Card.Id, 1799).QuestElementKeys.Contains(GameElements.Light)
                || !StateAt(result, context.Card.Id, 1800).QuestElementKeys.Contains(GameElements.Light)
                || StateAt(result, context.Card.Id, 2699).QuestTags.Contains(GameTags.Deity)
                || !StateAt(result, context.Card.Id, 2700).QuestTags.Contains(GameTags.Deity)) return false;
            if (!context.Apply(result) || context.Attack != 81 + 122 * level
                || context.Card.Quests.Any(quest => !context.Card.IsQuestUnlocked(quest))
                || !context.Card.Tags.Contains(GameTags.Deity) || context.Card.Attributes.Identity.ElementKeys.Single() != GameElements.Light
                || initial.Cards.Single().ElementKeys.Single() != GameElements.General || initial.Cards.Single().Tags.Contains(GameTags.Deity)
                || definition.Attributes.Identity.ElementKeys.Single() != GameElements.General || definition.Tags.Contains(GameTags.Deity)) return false;
        }
        return true;
    }

    internal static bool CrossBattleAndUpgrade()
    {
        var context = new Context(1);
        if (!context.Apply(context.Run(570)) || context.Attack != 20
            || context.Card.Quests.Any(quest => context.Card.GetQuestProgress(quest.Key) != 19)) return false;
        var crossing = context.Run(30);
        if (crossing.Events.OfType<DamageDealtEvent>().Single().RawDamage != 20
            || !context.Apply(crossing) || context.Attack != 101) return false;
        context.Board.PlaceCard(context.Match, context.Card.Id, BoardZone.Bench, 0);
        if (context.Attack != 21) return false;
        context.Factory.ApplyCardLevel(context.Card, context.Definition, 4);
        context.Board.PlaceCard(context.Match, context.Card.Id, BoardZone.Battlefield, 0);
        if (context.Attack != 101 || !context.Apply(context.Run(1200)) || context.Attack != 261
            || context.Card.GetQuestProgress(context.Card.Quests[1].Key) != 60
            || context.Card.Attributes.Identity.ElementKeys.Single() != GameElements.Light) return false;
        var lightBefore = MatchSnapshot.From(context.Match);
        context.Board.PlaceCard(context.Match, context.Card.Id, BoardZone.Bench, 0);
        if (context.Attack != 181 || context.Card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Multicast) != 0
            || context.Card.Attributes.Identity.ElementKeys.Single() != GameElements.Light) return false;
        context.Board.PlaceCard(context.Match, context.Card.Id, BoardZone.Battlefield, 0);
        if (!context.Apply(context.Run(900)) || context.Attack != 501 || !context.Card.Tags.Contains(GameTags.Deity)) return false;
        context.Board.RefreshQuestAbilities(context.Match);
        context.Board.RefreshQuestAbilities(context.Match);
        if (context.Attack != 501 || context.Card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Multicast) != 1) return false;
        context.Board.PlaceCard(context.Match, context.Card.Id, BoardZone.Bench, 0);
        var fresh = context.Factory.CreateCard(context.Definition);
        return context.Attack == 421 && context.Card.Tags.Contains(GameTags.Deity)
            && context.Card.Attributes.Identity.ElementKeys.Single() == GameElements.Light
            && context.Card.Attributes.Identity.GemSocketCount == 1 && context.Card.GemSockets.Count == 1
            && !lightBefore.Cards.Single().Tags.Contains(GameTags.Deity)
            && fresh.Attributes.Identity.ElementKeys.Single() == GameElements.General && !fresh.Tags.Contains(GameTags.Deity)
            && fresh.Quests.All(quest => fresh.GetQuestProgress(quest.Key) == 0);
    }

    internal static bool MergePreservesProgress()
    {
        foreach (var (level, ticks, bench) in new[] { (1, 570, false), (2, 570, false), (2, 1800, true), (3, 2730, true) })
        {
            var context = new Context(level);
            if (!context.Apply(context.Run(ticks))) return false;
            if (bench && context.Board.PlaceCard(context.Match, context.Card.Id, BoardZone.Bench, 0).IsFailure) return false;
            var before = MatchSnapshot.From(context.Match).Cards.Single();
            // 通过正式获得入口构成连续合并链，低等级材料保留独立的任务进度。
            for (var materialLevel = 1; materialLevel < level; materialLevel++)
                if (context.Economy.AcquireAndPlace(context.Match, context.Definition, materialLevel,
                    CardAcquisitionSource.Reward).IsFailure) return false;
            var originalLocation = context.Match.Board.Locate(context.Card.Id)!;
            var offer = ShopOffer.Create(context.Definition, 1);
            var wealth = context.Match.Player.Wealth;
            if (context.Economy.CheckPurchase(context.Match, offer).IsFailure) return false;
            var acquired = context.Economy.BuyAndPlace(context.Match, offer);
            if (acquired.IsFailure || !ReferenceEquals(acquired.Value!.Card, context.Card)
                || acquired.Value.PreviousLevel != level || acquired.Value.CurrentLevel != level + 1
                || context.Match.Player.Inventory.Cards.Count != 1 || !offer.IsSold
                || context.Match.Player.Wealth != wealth - offer.Price
                || context.Match.Board.Locate(context.Card.Id) != originalLocation) return false;
            var after = MatchSnapshot.From(context.Match).Cards.Single();
            if (!after.Quests.SequenceEqual(before.Quests) || after.Value != before.Value
                || !after.ElementKeys.SequenceEqual(before.ElementKeys) || !after.Tags.SequenceEqual(before.Tags)
                || after.CurrentValues[GameAttributeKeys.AttackDamage] != before.CurrentValues[GameAttributeKeys.AttackDamage]
                || after.CurrentValues.GetValueOrDefault(GameAttributeKeys.Multicast) != before.CurrentValues.GetValueOrDefault(GameAttributeKeys.Multicast)
                || context.Economy.BuyAndPlace(context.Match, offer).IsSuccess) return false;
            if (context.Board.PlaceCard(context.Match, context.Card.Id, BoardZone.Battlefield, 0).IsFailure) return false;
            var attack = context.Attack;
            var result = context.Run(30);
            var activations = result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == context.Card.Id && !e.IsEcho);
            if (!context.Apply(result) || context.Attack != attack + activations * (level + 1)
                + (before.Quests[0].Progress == 19 ? 80 : 0)) return false;
            if (before.Quests[2].Progress < 120 && context.Card.GetQuestProgress(context.Card.Quests[2].Key)
                != before.Quests[2].Progress + activations) return false;
            if (before.Quests[2].Unlocked
                && !result.Events.OfType<StatusChangedEvent>().Any(e => e.Status == BattleStatus.Burn)) return false;
        }
        return true;
    }

    internal static bool FiltersAndLoss()
    {
        var context = new Context(1);
        var setup = context.Setup(30);
        var source = setup.Player.Cards.Single();
        var destroy = new AbilityDefinition(new StringName("verification.ascendant.destroy"),
            AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)]);
        var failed = new AbilityDefinition(new StringName("verification.ascendant.fail"),
            AbilityActivation.Active, AbilityTarget.EnemyHero, 1, 30, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]);
        var other = new CardBattleSetup(EntityId.New(), 2, 1, 30);
        foreach (var excluded in new[] { source with { IsOnBench = true },
            source with { Abilities = source.Abilities!.Append(destroy).ToArray() }, source with { Abilities = [failed] },
            source with { Abilities = source.Abilities!.Where(ability => ability.Activation != AbilityActivation.Active).ToArray() } })
        {
            var result = new CombatSimulator().Simulate(new BattleSetup(
                new BattleSideSetup(setup.Player.Hero, [excluded, other]), setup.Opponent, 42, new BattleTick(30), new BattleTick(31)));
            if (result.PermanentAttributeBonuses.Any() || result.Events.OfType<CardQuestProgressChangedEvent>().Any()) return false;
        }
        var second = context.Economy.AcquireAndPlace(context.Match, context.Definition, 2, CardAcquisitionSource.Reward).Value!.Card;
        var enemy = context.Economy.AcquireAndPlace(context.Enemy, context.Definition, 4, CardAcquisitionSource.Reward).Value!.Card;
        setup = context.Setup(30);
        var playerCards = setup.Player.Cards.Select(card => card.EntityId == second.Id ? card with { CooldownMultiplier = 0.5m } : card).ToArray();
        var separated = new CombatSimulator().Simulate(new BattleSetup(new BattleSideSetup(setup.Player.Hero, playerCards),
            setup.Opponent, 42, new BattleTick(30), new BattleTick(31)));
        if (!context.Apply(separated) || context.Attack != 2 || second.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != 5
            || context.Card.GetQuestProgress(context.Card.Quests[2].Key) != 1 || second.GetQuestProgress(second.Quests[2].Key) != 2
            || enemy.GetQuestProgress(enemy.Quests[2].Key) != 0 || enemy.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != 1) return false;
        var lossContext = new Context(1);
        setup = lossContext.Setup(600);
        var lethal = new CardBattleSetup(EntityId.New(), 0, 1000000, 600);
        var loss = new CombatSimulator().Simulate(new BattleSetup(setup.Player,
            new BattleSideSetup(setup.Opponent.Hero, [lethal]), 42, new BattleTick(600), new BattleTick(601)));
        return loss.Outcome == BattleOutcome.OpponentVictory && lossContext.Apply(loss)
            && lossContext.Attack == 101 && lossContext.Card.IsQuestUnlocked(lossContext.Card.Quests[0]);
    }

    internal static bool ReplayAndPercentStatus()
    {
        var context = new Context(1);
        var quests = new CardQuestService(context.Board);
        for (var count = 0; count < 119; count++) quests.ProcessEvent(context.Match, new SourceCardActivatedQuestEvent(context.Card.Id));
        var before = MatchSnapshot.From(context.Match);
        var enemyBefore = MatchSnapshot.From(context.Enemy);
        var setup = context.Setup(30);
        var result = new CombatSimulator().Simulate(setup);
        if (result.Events.OfType<StatusChangedEvent>().Single(e => e.Status == BattleStatus.Burn).Amount != 8
            || result.Events.OfType<DamageDealtEvent>().Count() != 2
            || setup.Player.Cards.Single().Abilities!.Single(ability => ability.Activation == AbilityActivation.Active).Effects.Count != 1
            || !context.Apply(result)) return false;
        var playback = new BattlePlaybackPresenter(new BattleResolution(before, enemyBefore, result, MatchSnapshot.From(context.Match)));
        var old = playback.Project(SideId.Player).Cards.Single();
        if (old.Quests.Last().Progress != 119 || old.Tags.Contains(GameTags.Deity)) return false;
        playback.Skip(); playback.Skip();
        var projected = playback.Project(SideId.Player).Cards.Single();
        if (projected.Quests.Last().Progress != 120 || !projected.Tags.Contains(GameTags.Deity)
            || projected.ElementKeys.Single() != GameElements.Light || context.Attack != 83
            || old.Quests.Last().Progress != 119 || old.Tags.Contains(GameTags.Deity)
            || !CardDisplayAdapter.AbilityDetails(projected.Abilities).Contains("10%灼伤")) return false;
        // 百分比读取有效攻击，先取整，再遵循狂暴的状态施加倍率。
        quests.ProcessEvent(context.Match, new SourceCardActivatedQuestEvent(context.Card.Id));
        setup = context.Setup(30);
        var source = setup.Player.Cards.Single();
        var berserk = new AbilityDefinition(new StringName("verification.ascendant.berserk"), AbilityActivation.PassiveOnBattleStart,
            AbilityTarget.SelfCard, 0, 0, [new SetSourceCardStateEffectDefinition(GameAttributeKeys.Berserk, true)]);
        var aura = new CardBattleSetup(EntityId.New(), 2, 0, 0,
            [new AbilityDefinition(new StringName("verification.ascendant.aura"), AbilityActivation.PassiveAura,
                AbilityTarget.SelfCard, 0, 0, [new IncreaseAlliedCardAttributeAuraEffectDefinition(GameAttributeKeys.AttackDamage, 31)])], UseLegacyAttack: false);
        var enhanced = new CombatSimulator().Simulate(new BattleSetup(new BattleSideSetup(setup.Player.Hero,
            [source with { Abilities = source.Abilities!.Append(berserk).ToArray() }, aura]), setup.Opponent,
            42, new BattleTick(30), new BattleTick(31)));
        if (enhanced.Events.OfType<StatusChangedEvent>().Any(e => e.Status == BattleStatus.Burn && e.Amount != 13)) return false;
        var low = new CardBattleSetup(EntityId.New(), 0, 9, 30,
            [new AbilityDefinition(new StringName("verification.ascendant.percent"), AbilityActivation.Active, AbilityTarget.EnemyHero,
                0, 30, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                    new ApplySourceAttributePercentStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.AttackDamage, 10)])]);
        var rounded = new CombatSimulator().Simulate(new BattleSetup(new BattleSideSetup(setup.Player.Hero, [low]),
            setup.Opponent, 42, new BattleTick(30), new BattleTick(31)));
        return rounded.Events.OfType<StatusChangedEvent>().Single().Amount == 0;
    }

    internal static bool TemporarySourcesAndValidation()
    {
        var context = new Context(1);
        var setup = context.Setup(1800);
        var summon = new CardBattleSetup(EntityId.New(), 0, 0, 0,
            [new AbilityDefinition(new StringName("verification.ascendant.summon"), AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.SelfCard, 0, 0, [new SummonAdjacentCardEffectDefinition(context.Definition, AdjacentCardSide.Right)])], UseLegacyAttack: false);
        var summoned = new CombatSimulator().Simulate(new BattleSetup(new BattleSideSetup(setup.Player.Hero, [summon]),
            setup.Opponent, 42, new BattleTick(600), new BattleTick(601)));
        if (summoned.PermanentAttributeBonuses.Count != 0
            || summoned.Events.OfType<CardQuestProgressChangedEvent>().Any(e => e.Persists)
            || !context.Apply(summoned) || context.Attack != 1
            || !summoned.States[^1].Cards.Single(card => card.SummonedCard is not null).Quests.First().Unlocked) return false;
        var transform = summon with { Abilities = [new AbilityDefinition(new StringName("verification.ascendant.transform"),
            AbilityActivation.PassiveOnBattleStart, AbilityTarget.EnemyHero, 0, 0, [new TransformRandomEnemyCardEffectDefinition(context.Definition)])] };
        var transformed = new CombatSimulator().Simulate(new BattleSetup(setup.Player,
            new BattleSideSetup(setup.Opponent.Hero, [transform]), 42, new BattleTick(1800), new BattleTick(1801)));
        if (transformed.PermanentAttributeBonuses.Count != 0 || transformed.Events.OfType<CardQuestProgressChangedEvent>().Any(e => e.Persists)
            || !context.Apply(transformed) || context.Attack != 1 || context.Card.Quests.Any(quest => context.Card.GetQuestProgress(quest.Key) != 0)) return false;
        var before = MatchSnapshot.From(context.Match);
        var playback = new BattlePlaybackPresenter(new BattleResolution(before, MatchSnapshot.From(context.Enemy), transformed, before));
        playback.Skip();
        var temporary = playback.Project(SideId.Player).Cards.Single();
        if (temporary.ElementKeys.Single() != GameElements.Light || temporary.Quests[1].Progress != 60
            || context.Card.Attributes.Identity.ElementKeys.Single() != GameElements.General
            || before.Cards.Single().ElementKeys.Single() != GameElements.General) return false;
        foreach (var percent in new[] { 0, 101 })
        {
            try
            {
                _ = new AbilityDefinition("verification.ascendant.invalid", AbilityActivation.Active, AbilityTarget.EnemyHero,
                    0, 30, [new ApplySourceAttributePercentStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.AttackDamage, percent)]);
                return false;
            }
            catch (ArgumentException) { }
        }
        return true;
    }

    private static CardBattleSnapshot State(BattleResult result, EntityId id) => result.States[^1].Cards.Single(card => card.Id == id);
    private static CardBattleSnapshot StateAt(BattleResult result, EntityId id, int tick) =>
        result.States.Last(frame => frame.Tick.Value == tick).Cards.Single(card => card.Id == id);

    // 使用正式获得、棋盘、战斗输入和结算入口的验证上下文（表现层）。
    private sealed class Context
    {
        public Context(int level)
        {
            Economy = new CardEconomyService(Factory, Board);
            Match = new CreateMatchService(Factory).Create(42, 999, new PaladinHeroDefinition());
            Enemy = new CreateMatchService(Factory).Create(43, 999, new PaladinHeroDefinition());
            foreach (var session in new[] { Match, Enemy })
            {
                session.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, 1000000);
                session.Player.Hero.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxMana, 0);
                session.Player.Hero.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.ManaRegen, 0);
            }
            Card = Economy.AcquireAndPlace(Match, Definition, level, CardAcquisitionSource.Reward).Value!.Card;
        }
        public EntityFactory Factory { get; } = new();
        public BoardService Board { get; } = new(new BoardPlacementSolver());
        public CardEconomyService Economy { get; }
        public AscendantCardDefinition Definition { get; } = new();
        public MatchSession Match { get; }
        public MatchSession Enemy { get; }
        public CardInstance Card { get; }
        public int Attack => Card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage);
        public BattleSetup Setup(int ticks) => new BattleSetupFactory().Create(Match, Enemy, 42, new BattleTick(ticks), new BattleTick(ticks + 1));
        public BattleResult Run(int ticks) => new CombatSimulator().Simulate(Setup(ticks));
        public bool Apply(BattleResult result) => new MatchResultService(Board).Apply(Match, result, MatchBattleKind.Pvp).IsSuccess;
    }
}
