using System.Threading.Tasks;
using Project_Star.Application.Content;
using Project_Star.Presentation.Playtest;
using System;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Content.Skills;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Verification;

// 学者之书无主动身份、轮初同级技能、备战与合并验证（表现层）。
internal static class ScholarsBookChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new ScholarsBookCardDefinition(); var identity = definition.Attributes.Identity;
        if (identity.DisplayName != "学者之书" || identity.FactionKey != new StringName("mona")
            || identity.Size != CardSize.Medium || definition.InitialLevel != 2
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !definition.Tags.ToArray().ToHashSet().SetEquals(new[] { GameTags.Medium })
            || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(ScholarsBookChecks).Assembly).Cards.ContainsKey(identity.Key)) return Fail();
        foreach (var level in new[] { 2, 3, 4 })
        {
            var card = new EntityFactory().CreateCard(definition, level);
            var ability = card.Abilities.Single();
            if (ability.Activation != AbilityActivation.PassiveOnRoundStart || !ability.AllowsBench
                || ability.ManaCost != 0 || ability.CooldownTicks != 0
                || ability.Effects.Single() is not GrantRandomHeroSourceLevelSkillEffectDefinition) return Fail();
            var offer = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            if (new CardDisplayAdapter().Build(offer).Effects.Count != 0 || CardDisplayAdapter.CurrentDetails(offer).Contains("冷却")
                || !CardDisplayAdapter.Rules(offer).Single().Contains("同级技能")) return Fail();
            var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0),
                [new CardBattleSetup(card.Id, 0, 0, 0, card.Abilities, UseLegacyAttack: false)]);
            var enemy = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), []);
            var result = new CombatSimulator().Simulate(new BattleSetup(own, enemy, 42, new BattleTick(10), new BattleTick(11)));
            if (result.Events.OfType<AbilityActivatedEvent>().Any()) return Fail();
        }
        return true;
    }
    internal static bool RoundSkills()
    {
        var training = new TrainingSkillDefinition(); var ritual = new RebirthRitualSkillDefinition();
        var selectedOwn = false; var selectedOther = false;
        foreach (var level in new[] { 2, 3, 4 })
        foreach (var zone in new[] { BoardZone.Battlefield, BoardZone.Bench })
        {
            var registry = Catalog(training); var match = Fixture(42, level, zone, registry);
            var rounds = new RoundIncomeService(registry.Sets, registry);
            match.Progress.Round = 2; rounds.SettleCurrentRound(match);
            var skill = match.Player.Skills.Items.Single(); var id = skill.Id; var state = match.Random.State;
            if (skill.Attributes.Identity.Key != training.Attributes.Identity.Key || Level(skill) != level) return Fail();
            rounds.SettleCurrentRound(match);
            if (match.Random.State != state || match.Player.Skills.Items.Count != 1) return Fail();
            match.Progress.Round = 3; rounds.SettleCurrentRound(match);
            if (level < 4 ? match.Player.Skills.Items.Count != 1 || Level(skill) != level + 1 || skill.Id != id
                : match.Player.Skills.Items.Count != 2) return Fail();
            var replay = Fixture(42, level, zone, registry); replay.Progress.Round = 2; rounds.SettleCurrentRound(replay);
            replay.Progress.Round = 3; rounds.SettleCurrentRound(replay);
            if (replay.Random.State != match.Random.State
                || !replay.Player.Skills.Items.Select(Level).SequenceEqual(match.Player.Skills.Items.Select(Level))) return Fail();
        }
        foreach (var skills in new SkillDefinition[][] { [], [ritual] })
        {
            var registry = Catalog(skills); var match = Fixture(42, 2, BoardZone.Bench, registry); var state = match.Random.State;
            match.Progress.Round = 2; new RoundIncomeService(registry.Sets, registry).SettleCurrentRound(match);
            if (match.Random.State != state || match.Player.Skills.Items.Count != 0) return Fail();
        }
        var mixed = Catalog(training, ritual);
        for (ulong seed = 1; seed <= 24; seed++)
        {
            var match = Fixture(seed, 4, BoardZone.Bench, mixed); match.Progress.Round = 2;
            new RoundIncomeService(mixed.Sets, mixed).SettleCurrentRound(match);
            var key = match.Player.Skills.Items.Single().Attributes.Identity.Key;
            selectedOwn |= key == ritual.Attributes.Identity.Key; selectedOther |= key == training.Attributes.Identity.Key;
        }
        var twoCatalog = Catalog(training); var two = Fixture(42, 2, BoardZone.Battlefield, twoCatalog);
        Add(two, 2, BoardZone.Bench, twoCatalog); two.Progress.Round = 2;
        new RoundIncomeService(twoCatalog.Sets, twoCatalog).SettleCurrentRound(two);
        if (two.Player.Skills.Items.Count != 1 || Level(two.Player.Skills.Items.Single()) != 3) return Fail();
        var legacy = DefinitionRegistry.Create([new MonaHeroDefinition(), new ValosHeroDefinition(),
            new NightThiefGarbCardDefinition(), new AssassinDaggerCardDefinition()]);
        var legacyMatch = new CreateMatchService(new EntityFactory()).Create(42, 0, new MonaHeroDefinition());
        var legacyCard = new EntityFactory().CreateCard(new NightThiefGarbCardDefinition(), 2);
        legacyMatch.Player.Inventory.Add(legacyCard);
        var legacyBoard = new BoardService(new BoardPlacementSolver(), legacy.Sets);
        legacyBoard.PlaceCard(legacyMatch, legacyCard.Id, BoardZone.Bench, 0);
        var before = legacyMatch.Random.State; legacyMatch.Progress.Round = 2;
        var legacyRounds = new RoundIncomeService(legacy.Sets, legacy); legacyRounds.SettleCurrentRound(legacyMatch);
        var oldRounds = before == legacyMatch.Random.State && legacyMatch.Player.Inventory.Cards.Count == 1;
        legacyBoard.PlaceCard(legacyMatch, legacyCard.Id, BoardZone.Battlefield, 0);
        legacyMatch.Progress.Round = 3; legacyRounds.SettleCurrentRound(legacyMatch);
        oldRounds &= legacyMatch.Player.Inventory.Cards.Count == 2 && legacyMatch.Random.State != before;
        var merge = SkillChecks.CheckSkillMerge();
        GD.Print($"技能候选 当前={selectedOwn} 其他={selectedOther} 既有轮初={oldRounds} 合并={merge}");
        return selectedOwn && selectedOther && oldRounds && merge;
    }

    private static int Level(SkillInstance skill) => skill.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level);
    private static DefinitionRegistry Catalog(params SkillDefinition[] skills) => DefinitionRegistry.Create(
        new object[] { new MonaHeroDefinition(), new PaladinHeroDefinition(), new ScholarsBookCardDefinition(), new Project_Star.Content.Sets.ScholarSetDefinition(), new SkeletonCardDefinition() }.Concat(skills));
    private static MatchSession Fixture(ulong seed, int level, BoardZone zone, IDefinitionCatalog catalog)
    {
        var match = new CreateMatchService(new EntityFactory()).Create(seed, 0, new MonaHeroDefinition());
        Add(match, level, zone, catalog); return match;
    }
    private static void Add(MatchSession match, int level, BoardZone zone, IDefinitionCatalog catalog)
    {
        var card = new EntityFactory().CreateCard(new ScholarsBookCardDefinition(), level); match.Player.Inventory.Add(card);
        if (new BoardService(new BoardPlacementSolver(), catalog.Sets).PlaceCard(match, card.Id, zone, 0).IsFailure)
            throw new InvalidOperationException("学者之书摆放失败。");
    }
    private static bool Fail([System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    { GD.Print($"学者之书验证失败，行{line}"); return false; }
    internal static async Task Capture(Node owner)
    {
        if (!LevelsAndCombat()) throw new InvalidOperationException("学者之书战斗验证失败。");
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new ScholarsBookCardDefinition()));
        var adapter = new CardDisplayAdapter();
        using var raw = adapter.Artwork(snapshot.Illustration).GetImage();
        if (!raw.HasMipmaps() || raw.GetWidth() != raw.GetHeight())
            throw new InvalidOperationException("学者之书原画比例或mipmap不合格。");
        GD.Print($"学者之书原画：{raw.GetWidth()}×{raw.GetHeight()}，mipmap层数{raw.GetMipmapCount()}");
        var viewport = new SubViewport { Size = new Vector2I(420, 460), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        var preview = new CardPreviewView { Size = owner.GetViewport().GetVisibleRect().Size, Theme = MatchTheme.Create() };
        owner.AddChild(preview);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(420, 460) });
            foreach (var (position, size) in new[] { (new Vector2(24, 24), new Vector2(200, 200)),
                (new Vector2(260, 24), new Vector2(100, 100)) })
            {
                var face = new CardItemView { Position = position, Size = size };
                viewport.AddChild(face); face.Render(snapshot, adapter);
            }
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(ScholarsBookChecks).Assembly)));
            preview.Open(snapshot, null, null);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            using var detail = owner.GetViewport().GetTexture().GetImage();
            if (image.SavePng("res://output/scholars-book/card-face.png") != Error.Ok
                || detail.SavePng("res://output/scholars-book/preview.png") != Error.Ok)
                throw new InvalidOperationException("学者之书卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); owner.RemoveChild(preview); preview.Free(); }
    }
}
