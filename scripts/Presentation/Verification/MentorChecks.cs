using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Encounters;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Application.Mentors;
using Project_Star.Content.Encounters;
using Project_Star.Content.Heroes;
using Project_Star.Content.Mentors;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Infrastructure.Encounters;
using Project_Star.Presentation.Playtest;

using static Project_Star.Presentation.Verification.VerificationFixtures;

namespace Project_Star.Presentation.Verification;

// 导师解耦、筛选、领取与遭遇入口验证（表现层验证模块）。
internal static class MentorChecks
{
    internal static bool Archbishop()
    {
        var registry = DefinitionRegistry.Scan(typeof(MentorChecks).Assembly);
        if (!registry.Mentors.TryGetValue("mentor.archbishop", out var mentor)
            || mentor is not ArchbishopMentorDefinition || mentor.Level != 1
            || mentor.Attributes.Identity.DisplayName != "大主教") return false;
        var player = Session(42);
        var state = player.Random.State;
        var neutralSkill = new MentorVerificationSkillDefinition("skill.verification.neutral", GameFactions.Neutral);
        var emptyCatalog = DefinitionRegistry.Create([mentor, new VerificationHeroDefinition(), neutralSkill]);
        var empty = Service(emptyCatalog).Open(player, mentor.Attributes.Identity.Key).Value!;
        if (empty.Offers.Count != 0 || !empty.IsResolved || player.Random.State != state) return false;

        var paladinSkill = new MentorVerificationSkillDefinition("skill.verification.paladin", "paladin");
        var otherSkill = new MentorVerificationSkillDefinition("skill.verification.other", "mona");
        var hero = new VerificationHeroDefinition().Attributes.Identity;
        if (!mentor.CanOfferSkill(paladinSkill, hero) || mentor.CanOfferSkill(neutralSkill, hero)
            || mentor.CanOfferSkill(otherSkill, hero)) return false;
        var configured = DefinitionRegistry.Create([
            mentor, new PaladinHeroDefinition(), new VerificationHeroDefinition(), paladinSkill, neutralSkill,
        ]);
        var service = Service(configured);
        var visit = service.Open(player, mentor.Attributes.Identity.Key).Value!;
        if (visit.Level != 1 || visit.Offers.Count != 1 || visit.Offers[0].SkillKey != paladinSkill.Attributes.Identity.Key)
            return false;
        var acquired = service.ChooseSkill(player, visit, visit.Offers[0].SkillKey);
        if (acquired.IsFailure || acquired.Value!.CurrentLevel != 1) return false;
        var higherVisit = service.Open(player, mentor.Attributes.Identity.Key, 3).Value!;
        return higherVisit.Level == 3 && higherVisit.Offers.Single().Level == 3
            && service.ChooseSkill(player, higherVisit, higherVisit.Offers[0].SkillKey).Value?.CurrentLevel == 3;
    }

    internal static bool Definitions()
    {
        var mentor = new VerificationMentorDefinition();
        var registry = DefinitionRegistry.Create([mentor]);
        if (registry.Mentors.Count != 1 || registry.Encounters.Count != 0) return false;
        try { mentor.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Level, 2); return false; }
        catch (InvalidOperationException) { }
        try { DefinitionRegistry.Create([mentor, new VerificationMentorDefinition()]); return false; }
        catch (DefinitionValidationException) { }
        try { DefinitionRegistry.Create([new VerificationMentorEncounterDefinition()]); return false; }
        catch (DefinitionValidationException) { }
        try { _ = new VerificationMentorDefinition(level: 6); return false; }
        catch (ArgumentOutOfRangeException) { }
        return DefinitionRegistry.Create([mentor, new VerificationMentorEncounterDefinition()]).Encounters.Count == 1;
    }

    internal static bool FilteringAndLevels()
    {
        var ownFaction = new StringName("verification.faction");
        var factionMentor = new VerificationMentorDefinition("mentor.verification.faction", 3,
            (skill, hero) => skill.Attributes.Identity.FactionKey == hero.FactionKey);
        var fixedMentor = new VerificationMentorDefinition("mentor.verification.fixed", 2,
            (skill, _) => skill.Attributes.Identity.Key == new StringName("skill.verification.neutral"));
        var registry = DefinitionRegistry.Create([
            new VerificationHeroDefinition(), factionMentor, fixedMentor,
            new MentorVerificationSkillDefinition("skill.verification.own", ownFaction),
            new MentorVerificationSkillDefinition("skill.verification.unsupported", ownFaction, [1, 2]),
            new MentorVerificationSkillDefinition("skill.verification.neutral", GameFactions.Neutral),
        ]);
        var service = Service(registry);
        var player = Session(42);
        var visit = service.Open(player, factionMentor.Attributes.Identity.Key).Value!;
        if (visit.Level != 3 || visit.Offers.Count != 1
            || visit.Offers[0].SkillKey != new StringName("skill.verification.own")) return false;
        if (service.ChooseSkill(player, visit, visit.Offers[0].SkillKey).Value?.CurrentLevel != 3) return false;
        var fixedVisit = service.Open(player, fixedMentor.Attributes.Identity.Key).Value!;
        return fixedVisit.Level == 2 && fixedVisit.Offers.Count == 1
            && fixedVisit.Offers[0].SkillKey == new StringName("skill.verification.neutral");
    }

    internal static bool DeterministicChoices()
    {
        var mentor = new VerificationMentorDefinition();
        var skills = Enumerable.Range(0, 5).Select(index => new MentorVerificationSkillDefinition(
            new StringName($"skill.verification.{index}"), GameFactions.Neutral)).ToArray();
        var registry = DefinitionRegistry.Create(new object[] { mentor }.Concat(skills));
        var reversed = DefinitionRegistry.Create(new object[] { mentor }.Concat(skills.Reverse()));
        var player = Session(42);
        var control = Session(42);
        var service = Service(registry);
        var visit = service.Open(player, mentor.Attributes.Identity.Key).Value!;
        var same = Service(reversed).Open(control, mentor.Attributes.Identity.Key).Value!;
        var state = player.Random.State;
        var snapshot = MatchSnapshot.From(player);
        if (visit.Offers.Count != 3 || visit.Offers.Select(value => value.SkillKey).Distinct().Count() != 3
            || !visit.Offers.SequenceEqual(same.Offers) || player.Random.State != control.Random.State
            || service.Open(player, mentor.Attributes.Identity.Key).IsSuccess || state != player.Random.State)
            return false;
        if (service.ChooseSkill(player, visit, visit.Offers[0].SkillKey).IsFailure
            || snapshot.MentorVisit!.IsResolved || !MatchSnapshot.From(player).MentorVisit!.IsResolved) return false;
        var small = Service(DefinitionRegistry.Create([mentor, skills[0], skills[1]]))
            .Open(Session(42), mentor.Attributes.Identity.Key).Value!;
        var emptyPlayer = Session(42);
        var emptyState = emptyPlayer.Random.State;
        var empty = service.Open(emptyPlayer, mentor.Attributes.Identity.Key, 5).Value!;
        return small.Offers.Count == 2 && small.Offers.Select(value => value.SkillKey).Distinct().Count() == 2
            && empty.Offers.Count == 0 && empty.IsResolved && emptyPlayer.Random.State == emptyState;
    }

    internal static bool ClaimAndIsolation()
    {
        var mentor = new VerificationMentorDefinition();
        var skill = new MentorVerificationSkillDefinition("skill.verification.reward", GameFactions.Neutral);
        var registry = DefinitionRegistry.Create([mentor, skill]);
        var service = Service(registry);
        var player = Session(42);
        var other = Session(43);
        var before = player.Random.State;
        if (service.Open(player, "mentor.missing").IsSuccess || player.Random.State != before
            || service.Open(player, mentor.Attributes.Identity.Key, 0).IsSuccess
            || service.Open(new MatchSession(42), mentor.Attributes.Identity.Key).IsSuccess) return false;
        var visit = service.Open(player, mentor.Attributes.Identity.Key).Value!;
        if (service.ChooseSkill(other, visit, skill.Attributes.Identity.Key).IsSuccess
            || other.Player.Skills.Items.Count != 0
            || service.ChooseSkill(player, visit, "skill.missing").IsSuccess || visit.IsResolved) return false;
        var acquired = service.ChooseSkill(player, visit, skill.Attributes.Identity.Key).Value!;
        if (!acquired.WasCreated || !visit.IsResolved
            || service.ChooseSkill(player, visit, skill.Attributes.Identity.Key).IsSuccess) return false;
        var next = service.Open(player, mentor.Attributes.Identity.Key).Value!;
        if (service.ChooseSkill(player, visit, skill.Attributes.Identity.Key).IsSuccess) return false;
        var merged = service.ChooseSkill(player, next, skill.Attributes.Identity.Key).Value!;
        if (merged.WasCreated || merged.CurrentLevel != 2 || merged.Skill.Id != acquired.Skill.Id
            || player.Player.Skills.Items.Count != 1) return false;
        var finalVisit = service.Open(player, mentor.Attributes.Identity.Key, 4).Value!;
        player.Status = MatchStatus.Won;
        return service.ChooseSkill(player, finalVisit, skill.Attributes.Identity.Key).IsFailure
            && service.Open(player, mentor.Attributes.Identity.Key).IsFailure
            && !finalVisit.IsResolved && player.Player.Skills.Items.Count == 1
            && Session(42).ActiveMentorVisit is null;
    }

    internal static bool EncounterFlow()
    {
        var presenter = CreatePresenter();
        presenter.ChooseEncounter("encounter.verification.mentor");
        var before = presenter.View;
        if (before.Page != MatchPage.Event || before.EncounterLevel != 3
            || before.Player!.MentorVisit?.Level != 3 || before.EventOptions.Count != 1
            || !before.EventOptions[0].Action.Text.Contains("3级")
            || !before.EventOptions[0].Subtitle.Contains("15") || before.Continue.Visible) return false;
        for (var count = 0; count < 3; count++) presenter.RefreshView();
        if (!before.EventOptions.SequenceEqual(presenter.View.EventOptions)) return false;
        var option = before.EventOptions[0];
        presenter.ResolveEventOption(option.Key, before.EventRevision - 1);
        if (presenter.View.Player!.Skills.Count != 0) return false;
        presenter.ResolveEventOption(option.Key, before.EventRevision);
        presenter.ResolveEventOption(option.Key, before.EventRevision);
        if (presenter.View.Player!.Skills.Single().Level != 3 || presenter.View.EventOptions.Count != 0
            || !presenter.View.Continue.Visible || before.Player.MentorVisit!.IsResolved) return false;
        presenter.ContinueMatch();
        presenter.ChooseEncounter("encounter.verification.mentor");
        presenter.ResolveEventOption(option.Key, before.EventRevision);
        if (presenter.View.Player!.Skills.Single().Level != 3) return false;
        presenter.Reset();
        if (presenter.View.Player is not null || presenter.View.EventOptions.Count != 0) return false;
        var empty = CreatePresenter(5);
        empty.ChooseEncounter("encounter.verification.mentor");
        return empty.View.EncounterLevel == 5 && empty.View.Player!.MentorVisit!.Level == 5
            && empty.View.EventOptions.Count == 0 && empty.View.Continue.Visible
            && empty.View.Player.Skills.Count == 0;
    }

    internal static MatchPresenter CreatePresenter(int? levelOverride = null, int skillCount = 1)
    {
        var registry = DefinitionRegistry.Create(new object[] {
            new VerificationHeroDefinition(), new VerificationMentorDefinition(), new VerificationMentorEncounterDefinition(),
            new SmallShopEncounterDefinition(),
        }.Concat(Enumerable.Range(0, skillCount).Select(index => new MentorVerificationSkillDefinition(
            index == 0 ? new StringName("skill.verification.reward") : new StringName($"skill.verification.{index}"),
            GameFactions.Neutral, displayName: $"训练技能{index + 1}"))));
        var factory = new EntityFactory();
        var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
        var economy = new CardEconomyService(factory, board, registry.Cards.Values);
        var game = new GameCoordinator(new CreateMatchService(factory),
            new EncounterScheduler(registry, encounterLevelOverride: levelOverride),
            new StartBattleService(new BattleSetupFactory(), new CombatSimulator()), new MatchResultService(board));
        var presenter = new MatchPresenter(registry, board, economy, new ShopCardPoolService(),
            new ResolveEncounterOptionService(factory, board, registry.Cards.Values), Service(registry),
            new MonsterRewardClaimService(registry, economy, new SkillAcquisitionService(factory), board), game,
            new LocalTestOpponentProvider(registry));
        presenter.Reset();
        presenter.SelectHero("verification.hero");
        return presenter;
    }

    internal static bool RenderedChoices(Control owner)
    {
        var shell = CreateShell(owner);
        try
        {
            var presenter = CreatePresenter(skillCount: 3);
            presenter.ChooseEncounter("encounter.verification.mentor");
            var capture = presenter.View;
            shell.ChoiceSelected += (_, key, revision) => presenter.ResolveEventOption(key, revision);
            for (var count = 0; count < 3; count++) shell.Render(capture);
            var actions = shell.GetNode<VBoxContainer>("ContextRow/ContextHost/EventView/ActionScroll/Actions");
            if (actions.GetChildCount() != 3 || !shell.GetNode<Control>("ContextRow/ContextHost/EventView").Visible)
                return false;
            var button = actions.GetChild<Button>(0);
            if (!button.Text.Contains("3级") || !button.TooltipText.Contains(capture.EventOptions[0].Subtitle)) return false;
            shell.Render(capture);
            button.EmitSignal(Button.SignalName.Pressed);
            if (presenter.View.Player!.Skills.Count != 0) return false;
            actions.GetChild<Button>(0).EmitSignal(Button.SignalName.Pressed);
            shell.Render(presenter.View);
            return presenter.View.Player!.Skills.Single().Level == 3 && actions.GetChildCount() == 0
                && presenter.View.Continue.Visible;
        }
        finally { owner.RemoveChild(shell); shell.Free(); }
    }

    // 截图使用内部导师与技能夹具，检查两种窗口下的真实事件页面。
    internal static async Task Capture(Control owner)
    {
        var shell = CreateShell(owner);
        try
        {
            var presenter = CreatePresenter(skillCount: 3);
            presenter.ChooseEncounter("encounter.verification.mentor");
            shell.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1920, 1080) })
            {
                owner.GetWindow().Size = size;
                owner.GetTree().Root.ContentScaleSize = new Vector2I(1600, 900);
                shell.Render(presenter.View);
                for (var frame = 0; frame < 8; frame++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                if (shell.Size != new Vector2(1600, 900) || shell.GetNode<Control>("BenchRow/Content/Board").GetGlobalRect().End.Y > 900)
                    throw new InvalidOperationException("导师捕获画布或完整备战区越界。");
                await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var image = owner.GetViewport().GetTexture().GetImage();
                if (owner.GetViewport().UseHdr2D)
                    for (var y = 0; y < image.GetHeight(); y++)
                        for (var x = 0; x < image.GetWidth(); x++)
                            image.SetPixel(x, y, image.GetPixel(x, y).LinearToSrgb());
                image.Convert(Image.Format.Rgba8);
                if (image.SavePng($"res://output/mentor-{size.X}x{size.Y}.png") != Error.Ok)
                    throw new InvalidOperationException("导师页面截图保存失败。");
            }
        }
        finally { owner.RemoveChild(shell); shell.Free(); }
    }

    private static MatchShell CreateShell(Control owner)
    {
        var scene = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<MinimalPlaytest>();
        var shell = scene.GetNode<MatchShell>("MatchShell");
        scene.RemoveChild(shell); scene.Free();
        shell.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.TopLeft);
        shell.Size = new Vector2(1280, 720); owner.AddChild(shell);
        return shell;
    }

    private static MentorService Service(IDefinitionCatalog registry) => new(registry, new SkillAcquisitionService(new EntityFactory()));
    private static MatchSession Session(ulong seed) => new CreateMatchService(new EntityFactory()).Create(seed, 0, new VerificationHeroDefinition());

    // 内部导师夹具用不同纯筛选函数模拟不同导师，不进入正式内容池。
    private sealed class VerificationMentorDefinition : MentorDefinition
    {
        private readonly Func<SkillDefinition, HeroIdentityAttributes, bool> _filter;
        public VerificationMentorDefinition(StringName? key = null, int level = 1,
            Func<SkillDefinition, HeroIdentityAttributes, bool>? filter = null)
            : base(new EntityAttributes<MentorIdentityAttributes>(new MentorIdentityAttributes(
                key ?? new StringName("mentor.verification"), "验证导师", summary: "选择一个技能")), level)
            => _filter = filter ?? ((_, _) => true);
        public override bool CanOfferSkill(SkillDefinition skill, HeroIdentityAttributes hero) => _filter(skill, hero);
    }

    // 内部导师遭遇夹具只配置引用和3级包装，不进入正式内容池。
    private sealed class VerificationMentorEncounterDefinition : MentorEncounterDefinition
    {
        public VerificationMentorEncounterDefinition()
            : base(new EntityAttributes<EncounterIdentityAttributes>(new EncounterIdentityAttributes(
                "encounter.verification.mentor", "验证导师遭遇", summary: "选择一个技能")), "mentor.verification", level: 3) { }
    }

    // 内部技能夹具提供可筛选的身份和明确的支持等级，不进入正式内容池。
    private sealed class MentorVerificationSkillDefinition : SkillDefinition
    {
        public MentorVerificationSkillDefinition(StringName key, StringName faction, int[]? levels = null, string displayName = "验证技能")
            : base(new EntityAttributes<SkillIdentityAttributes>(new SkillIdentityAttributes(key, displayName, faction)),
                levels: (levels ?? [1, 2, 3, 4]).Select(value => VerificationSkillDefinition.CreateLevel(value, value * 5)).ToArray()) { }
    }
}
