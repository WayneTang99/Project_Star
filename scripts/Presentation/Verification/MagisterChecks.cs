using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Content;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Content.Sets;
using Project_Star.Content.Skills;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 魔导士装备的技能计数、光环、套装奖励与原画验收（表现验证层）。
internal static class MagisterChecks
{
    internal static bool Combat()
    {
        foreach (var count in new[] { 0, 1, 3 })
        foreach (var reverse in new[] { false, true })
        foreach (var level in new[] { 2, 3, 4 })
        {
            var boots = Card(new MagisterBootsCardDefinition(), level, 0);
            var crown = Card(new MagisterCrownCardDefinition(), level, 1);
            var cards = level == 2 ? new[] { boots, crown } : new[] { boots, crown, Card(new MagisterScepterCardDefinition(), level, 2) };
            var skills = Enumerable.Range(0, count).Select(_ => new SkillBattleSetup(EntityId.New(), [], new System.Collections.Generic.Dictionary<StringName, int>())).ToArray();
            var side = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 10), cards, skills);
            var enemy = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), []);
            var setup = new BattleSetup(reverse ? enemy : side, reverse ? side : enemy, 42, new BattleTick(100), new BattleTick(101));
            var result = new CombatSimulator().Simulate(setup);
            var frame = result.States.Last(s => s.Tick.Value == 100 && s.Player.Health > 0 && s.Opponent.Health > 0);
            var hero = reverse ? frame.Opponent : frame.Player;
            var target = reverse ? frame.Player : frame.Opponent;
            Require(hero.MaxMana == 100 + count * level * 20, $"max mana stacks count={count} level={level} actual={hero.MaxMana}");
            Require(hero.ManaRegen == 10 + count * level, "regen aura");
            if (level >= 3) Require(target.Health == 10000 - 2 * count * (level == 3 ? 40 : 80), "scaled damage");
            Require(result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events), "determinism");
            Require(result.PermanentAttributeBonuses.Count == 0, "battle isolation");
            var preview = new CombatSimulator().Preview(setup);
            Require(preview.Cards.Single(c => c.Id == crown.EntityId).EffectAmounts.Single() == count * level, "aura preview");
            var disabled = crown with { IsOnBench = true };
            var noAura = new CombatSimulator().Simulate(new BattleSetup(new BattleSideSetup(side.Hero, [disabled], skills), enemy, 42, new BattleTick(1)));
            Require(noAura.States[0].Player.ManaRegen == 10, "bench excluded");
            var runtime = new BattleRuntime(setup);
            BattleStatusResolver.RefreshCooldownAuras(runtime);
            var activeHero = runtime.GetHero(reverse ? SideId.Opponent : SideId.Player);
            activeHero.ManaRegen += 7;
            runtime.Cards.Single(card => card.EntityId == crown.EntityId).Destroyed = true;
            BattleStatusResolver.RefreshCooldownAuras(runtime);
            Require(activeHero.ManaRegen == 17, "aura removal preserves other contributions");
        }
        return true;
    }

    internal static bool Rewards()
    {
        var registry = DefinitionRegistry.Scan(typeof(MagisterSetDefinition).Assembly);
        var seen = new System.Collections.Generic.HashSet<StringName>();
        for (ulong seed = 1; seed <= 32; seed++)
        {
            var factory = new EntityFactory();
            var session = new CreateMatchService(factory, definitions: registry).Create(seed, 100, new MonaHeroDefinition());
            var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
            var economy = new CardEconomyService(factory, board, registry.Cards.Values);
            foreach (var definition in new CardDefinition[] { new MagisterBootsCardDefinition(), new MagisterCrownCardDefinition(), new MagisterScepterCardDefinition() })
                Require(economy.AcquireAndPlace(session, definition, definition.InitialLevel, CardAcquisitionSource.Reward).IsSuccess, "equip");
            Require(new CardSetEvaluator().Evaluate(session, registry.Sets).Count == 2, "cumulative thresholds");
            HeroExperienceService.Grant(session, 20);
            Require(session.Player.Skills.Items.Count >= 1, "upgrade reward");
            foreach (var skill in session.Player.Skills.Items)
            {
                Require(skill.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) >= 2, "fixed reward level");
                Require(skill.Attributes.Identity.FactionKey == new StringName("mona") || skill.Attributes.Identity.FactionKey == GameFactions.Neutral, "reward pool");
                seen.Add(skill.Attributes.Identity.FactionKey);
            }
            var battle = new CombatSimulator().Simulate(new BattleSetupFactory(registry.Sets).Create(session,
                new CreateMatchService(factory).Create(1, 0, new MonaHeroDefinition()), 42, new BattleTick(1)));
            var victory = new BattleResult(BattleOutcome.PlayerVictory, BattleEndReason.HeroDefeated, new BattleTick(1), 100, 0, [])
                { MatchRewardSources = battle.MatchRewardSources };
            var before = session.Random.State;
            MatchAbilityService.ApplyVictory(session, victory, MatchBattleKind.Pvp, board, registry.Cards.Values);
            Require(session.Random.State != before, "victory reward");
            var after = session.Random.State;
            MatchAbilityService.ApplyVictory(session, victory, MatchBattleKind.Pvp, board, registry.Cards.Values);
            Require(session.Random.State == after, "idempotent victory");
            Require(session.Player.Skills.Items.All(s => s.Attributes.Identity.FactionKey == new StringName("mona") || s.Attributes.Identity.FactionKey == GameFactions.Neutral), "victory pool");
        }
        Require(seen.Contains(GameFactions.Neutral), "current low-level pool");
        var probe = new CreateMatchService(new EntityFactory(), definitions: registry).Create(42, 0, new MonaHeroDefinition());
        for (var i = 0; i < 64; i++)
            MatchAbilityService.GrantRandomSkill(probe, new GrantRandomFactionSkillEffectDefinition(new StringName("mona"), 3));
        Require(probe.Player.Skills.Items.Any(s => s.Attributes.Identity.FactionKey == new StringName("mona"))
            && probe.Player.Skills.Items.Any(s => s.Attributes.Identity.FactionKey == GameFactions.Neutral), "both factions at supported level");
        var single = DefinitionRegistry.Create([new MonaHeroDefinition(), new MagisterSetDefinition(),
            new MagisterBootsCardDefinition(), new MagisterCrownCardDefinition(), new MagisterScepterCardDefinition(), new AssaultSkillDefinition()]);
        var merged = new CreateMatchService(new EntityFactory(), definitions: single).Create(42, 0, new MonaHeroDefinition());
        var reward = new GrantRandomFactionSkillEffectDefinition(new StringName("mona"), 1);
        MatchAbilityService.GrantRandomSkill(merged, reward);
        MatchAbilityService.GrantRandomSkill(merged, reward);
        Require(merged.Player.Skills.Items.Count == 1
            && merged.Player.Skills.Items.Single().Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 2, "duplicates merge before counting");
        var empty = DefinitionRegistry.Create([new MonaHeroDefinition()]);
        var emptyMatch = new CreateMatchService(new EntityFactory(), definitions: empty).Create(42, 0, new MonaHeroDefinition());
        var state = emptyMatch.Random.State;
        MatchAbilityService.GrantRandomSkill(emptyMatch, reward);
        Require(emptyMatch.Random.State == state && emptyMatch.Player.Skills.Items.Count == 0, "empty pool stable");
        return true;
    }

    private static CardBattleSetup Card(CardDefinition definition, int level, int start)
    {
        var card = new EntityFactory().CreateCard(definition, level); var identity = card.Attributes.Identity;
        return new CardBattleSetup(card.Id, start, 0, 50, card.Abilities, UseLegacyAttack: false,
            Tags: card.Tags, OccupiedSlots: identity.OccupiedSlots, ElementKeys: identity.ElementKeys)
            { Identity = identity, Level = level, CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues() };
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("魔导士验证失败：" + message); }

    internal static async Task Capture(Control owner)
    {
        var details = new CardDetailsView { Theme = MatchTheme.Create() }; owner.AddChild(details);
        var registry = DefinitionRegistry.Scan(typeof(MagisterSetDefinition).Assembly);
        details.SetEntries(CardCatalogQuery.Capture(registry));
        var i = 0;
        foreach (var definition in new CardDefinition[] { new MagisterBootsCardDefinition(), new MagisterCrownCardDefinition(), new MagisterScepterCardDefinition() })
        {
            var snapshot = new CardPreviewQuery(registry).Describe(MatchDisplayQuery.FromOffer(ShopOffer.Create(definition)), null);
            using var art = GD.Load<Texture2D>(snapshot.Illustration.ToString()).GetImage();
            Require(art.HasMipmaps() && (i == 2 ? art.GetWidth() == art.GetHeight() : art.GetWidth() * 2 == art.GetHeight()), "art ratio/mipmaps");
            GD.Print($"{snapshot.DisplayName}: {art.GetWidth()}×{art.GetHeight()}, mipmaps={art.GetMipmapCount()}");
            var face = new CardItemView { Position = new Vector2(30, 40), Size = new Vector2(i == 2 ? 200 : 100, 200) };
            owner.AddChild(face); face.Render(snapshot, new CardDisplayAdapter());
            details.ShowCard(snapshot, new Vector2(270, 40), owner.GetViewportRect().Size);
            for (var j = 0; j < 12; j++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw(); using var image = owner.GetViewport().GetTexture().GetImage(); image.SavePng($"res://output/magister/card-{i++}.png");
            face.QueueFree();
        }
        details.QueueFree();
    }
}
