using System;
using System.Linq;
using System.Collections.Generic;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Content;
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

// 元素使分级、事件元素冻结、首次元素回响与动态光环验证（表现层）。
internal static class ElementalistChecks
{
    internal static bool Combat()
    {
        var registry = DefinitionRegistry.Scan(typeof(ElementalistChecks).Assembly);
        Require(registry.Cards.Values.Count(card => card.Attributes.Identity.SetKey == new StringName("set.elementalist")) == 4, "四个成员");
        foreach (var reverse in new[] { false, true })
        {
            Require(!new ElementalNecklaceCardDefinition().SupportsLevel(1), "项链2级起");
            for (var level = 2; level <= 4; level++)
            {
                var necklace = Card(new ElementalNecklaceCardDefinition(), level, 0);
                var trigger = Peer(1, [GameElements.Fire, GameElements.Water], 10);
                var peers = Enumerable.Range(2, 4).Select(i => Peer(i, [GameElements.Wind])).ToArray();
                var bench = Peer(6, [GameElements.Light]) with { IsOnBench = true };
                var matching = Peer(7, [GameElements.Fire]);
                var setup = Battle([necklace, trigger, .. peers, bench, matching], reverse, 10);
                var result = new CombatSimulator().Simulate(setup);
                var states = result.States.Last().Cards;
                Require(states.Count(card => card.Haste == 10) == level - 1, "随机目标数与无重复");
                Require(states.Single(card => card.Id == trigger.EntityId).Haste == 0
                    && states.Single(card => card.Id == matching.EntityId).Haste == 0
                    && states.Single(card => card.Id == bench.EntityId).Haste == 0, "双元素及备战排除");
                Require(result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events), "随机确定性");
                var runtime = new BattleRuntime(setup);
                var source = runtime.Cards.Single(card => card.EntityId == necklace.EntityId);
                var eventCard = runtime.Cards.Single(card => card.EntityId == trigger.EntityId);
                var pending = new PendingAbility(source, source.Abilities.Single(), true, false, BattleTick.Zero, eventCard)
                    { EventElementKeys = [GameElements.Fire, GameElements.Water] };
                eventCard.ElementKeys = new System.Collections.Generic.HashSet<StringName> { GameElements.Wind };
                BattleStatusResolver.ApplyStatusToRandomAlliedCard(runtime, pending,
                    new ApplyStatusToRandomAlliedCardEffectDefinition(BattleStatus.HasteDuration, 10, TargetCount: 20, ExcludeEventCardElements: true));
                Require(runtime.Cards.Single(card => card.EntityId == matching.EntityId).HasteDuration == 0
                    && runtime.Cards.Single(card => card.EntityId == peers[0].EntityId).HasteDuration == 10, "事件元素冻结");
                var noTargets = new BattleRuntime(Battle([necklace], reverse, 1));
                var only = noTargets.Cards.Single(); var control = new BattleRuntime(Battle([necklace], reverse, 1));
                BattleStatusResolver.ApplyStatusToRandomAlliedCard(noTargets,
                    new PendingAbility(only, only.Abilities.Single(), true, false, BattleTick.Zero) { EventElementKeys = [GameElements.General] },
                    new ApplyStatusToRandomAlliedCardEffectDefinition(BattleStatus.HasteDuration, 10, ExcludeEventCardElements: true));
                Require(noTargets.NextRandomIndex(100000) == control.NextRandomIndex(100000), "空候选不推进随机");
            }
            foreach (var level in new[] { 3, 4 })
            {
                var belt = Card(new ElementalBeltCardDefinition(), level, 0);
                var result = new CombatSimulator().Simulate(Battle([belt], reverse, 50));
                var hero = reverse ? result.States.Last().Opponent : result.States.Last().Player;
                Require(hero.Mana == (level == 3 ? 10 : 20), "恢复魔法");
                var elements = result.States.Last().Cards.Single().QuestElementKeys;
                Require(elements.Count == 1 && elements[0] != GameElements.General && GameElements.IsValid(elements[0]), "自身发动随机换元素");
                Require(belt.ElementKeys!.Single() == GameElements.General, "战斗输入隔离");
                var trigger = Peer(3, [GameElements.Fire], 10);
                var setup = Battle([belt, trigger], reverse, 10);
                Require(new CombatSimulator().Simulate(setup).States.Last().Cards.Single(card => card.Id == belt.EntityId)
                    .QuestElementKeys.Single() != GameElements.General, "其他己方发动换元素");
            }
            var amalgam = Card(new ElementalAmalgamCardDefinition(), 4, 0);
            var dual = Peer(3, [GameElements.Fire, GameElements.Water], 10) with { Multicast = 2 };
            var repeated = Peer(4, [GameElements.Fire], 20);
            var ordinary = Peer(5, [GameElements.General], 10);
            var fusionSetup = Battle([amalgam, dual, repeated, ordinary], reverse, 90);
            var fusion = new CombatSimulator().Simulate(fusionSetup);
            Require(fusion.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == amalgam.EntityId)
                .Single().RawDamage == 160, "双元素分别翻倍且重复与多重不重复触发");
            Require(fusion.Events.OfType<CardAttributeChangedEvent>().Count(change => change.CardId == amalgam.EntityId) == 2, "每种元素一次");
            Require(new CombatSimulator().Simulate(Battle([amalgam], reverse, 90)).Events.OfType<DamageDealtEvent>()
                .Single().RawDamage == 40, "新战斗重置历史");
            Require(fusion.Events.SequenceEqual(new CombatSimulator().Simulate(fusionSetup).Events), "融合怪确定性");
        }
        return true;
    }

    internal static bool Sets()
    {
        var registry = DefinitionRegistry.Scan(typeof(ElementalistChecks).Assembly);
        var factory = new EntityFactory();
        var session = new CreateMatchService(factory, definitions: registry).Create(42, 100, new MonaHeroDefinition());
        var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
        var economy = new CardEconomyService(factory, board, registry.Cards.Values);
        foreach (var definition in new CardDefinition[] { new ElementalCodexCardDefinition(), new ElementalBeltCardDefinition(), new ElementalNecklaceCardDefinition() })
            Require(economy.AcquireAndPlace(session, definition, definition.InitialLevel, CardAcquisitionSource.Reward).IsSuccess, "装备三件");
        Require(new CardSetEvaluator().Evaluate(session, registry.Sets).Count == 1, "三件阈值");
        var opponent = new CreateMatchService(factory, definitions: registry).Create(43, 100, new MonaHeroDefinition());
        var before = MatchSnapshot.From(session); var enemyBefore = MatchSnapshot.From(opponent);
        var previewSetup = new BattleSetupFactory(registry.Sets).CreatePreview(session, opponent);
        var setup = new BattleSetup(previewSetup.Player, previewSetup.Opponent, 42, new BattleTick(60), new BattleTick(61));
        var result = new CombatSimulator().Simulate(setup);
        var playback = new BattlePlaybackPresenter(new BattleResolution(before, enemyBefore, result, before));
        playback.Skip();
        var belt = before.Cards.Single(card => card.Key == new StringName("card.elemental_belt"));
        var frozenElements = result.States.Last().Cards.Single(card => card.Id == belt.Id).QuestElementKeys;
        Require(frozenElements.Single() != GameElements.General
            && playback.Project(SideId.Player).Cards.Single(card => card.Id == belt.Id).ElementKeys.SequenceEqual(frozenElements)
            && belt.ElementKeys.Single() == GameElements.General, "无任务腰带回放元素及战前快照隔离");
        foreach (var reverse in new[] { false, true })
        {
            var target = Peer(0, [GameElements.General], 100);
            var dual = Peer(1, [GameElements.Fire, GameElements.Water]);
            var duplicate = Peer(2, [GameElements.Fire]);
            var bench = Peer(3, [GameElements.Light]) with { IsOnBench = true };
            var runtime = new BattleRuntime(Battle([target, dual, duplicate, bench], reverse, 1, true));
            BattleStatusResolver.RefreshCooldownAuras(runtime);
            var state = runtime.Cards.Single(card => card.EntityId == target.EntityId);
            Require(state.CooldownAuraReductionPercent == 6 && state.Abilities.Single().RemainingCooldownUnits == 188m, "包含普通属性、去重及双元素百分比");
            runtime.Cards.Single(card => card.EntityId == dual.EntityId).Destroyed = true;
            BattleStatusResolver.RefreshCooldownAuras(runtime);
            Require(state.CooldownAuraReductionPercent == 4, "摧毁后动态减少种类");
            runtime.Sets.Clear();
            BattleStatusResolver.RefreshCooldownAuras(runtime);
            Require(state.CooldownAuraReductionPercent == 0 && state.Abilities.Single().RemainingCooldownUnits == 200m, "贡献撤销");
        }
        return true;
    }

    private static CardBattleSetup Card(CardDefinition definition, int level, int start)
    {
        var card = new EntityFactory().CreateCard(definition, level); var identity = card.Attributes.Identity;
        var values = card.Attributes.BaseCombat.SnapshotFinalValues();
        return new CardBattleSetup(card.Id, start, values.GetValueOrDefault(GameAttributeKeys.AttackDamage),
            values.GetValueOrDefault(GameAttributeKeys.CooldownTicks), card.Abilities, UseLegacyAttack: false,
            Tags: card.Tags, OccupiedSlots: identity.OccupiedSlots, ElementKeys: identity.ElementKeys)
            { Identity = identity, Level = level, CombatValues = values };
    }

    private static CardBattleSetup Peer(int start, StringName[] elements, int cooldown = 0) => new(EntityId.New(), start, 0, cooldown,
        cooldown == 0 ? [] : [new AbilityDefinition("verification.elementalist.activate", AbilityActivation.Active,
            AbilityTarget.AlliedHero, 0, cooldown, [new RestoreManaEffectDefinition(0)])], UseLegacyAttack: false, ElementKeys: elements);

    private static BattleSetup Battle(CardBattleSetup[] own, bool reverse, int ticks, bool set = false)
    {
        var ally = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 100000, 0, ManaRegen: 0), own,
            sets: set ? [new CardSetBattleSetup(EntityId.New(), new ElementalistSetDefinition().Thresholds.Single().Abilities)] : []);
        var enemy = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 100000, 0, ManaRegen: 0), []);
        return new BattleSetup(reverse ? enemy : ally, reverse ? ally : enemy, 42, new BattleTick(ticks), new BattleTick(ticks + 1));
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("元素使验证失败：" + message); }

    internal static async Task Capture(Control owner)
    {
        var registry = DefinitionRegistry.Scan(typeof(ElementalistChecks).Assembly);
        var preview = new CardPreviewView { Size = owner.GetViewportRect().Size, Theme = MatchTheme.Create() };
        owner.AddChild(preview); preview.SetQuery(new CardPreviewQuery(registry));
        preview.SetEntries(CardCatalogQuery.Capture(registry));
        var compact = new SubViewport { Size = new Vector2I(520, 280), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(compact);
        foreach (var definition in new CardDefinition[] { new ElementalNecklaceCardDefinition(), new ElementalBeltCardDefinition(), new ElementalAmalgamCardDefinition(), new ElementalCodexCardDefinition() })
        {
            var snapshot = new CardPreviewQuery(registry).Describe(MatchDisplayQuery.FromOffer(ShopOffer.Create(definition)), null);
            using var art = GD.Load<Texture2D>(snapshot.Illustration.ToString()).GetImage();
            Require(art.HasMipmaps() && art.GetWidth() * 2 == art.GetHeight() * (int)definition.Attributes.Identity.Size, "原画比例与mipmap");
            GD.Print($"{snapshot.DisplayName}: {art.GetWidth()}×{art.GetHeight()}, mipmaps={art.GetMipmapCount()}");
            var face = new CardItemView { Position = new Vector2(20, 20),
                Size = new Vector2(100 * (int)definition.Attributes.Identity.Size, 200) };
            var small = new CardItemView { Position = new Vector2(340, 20),
                Size = new Vector2(50 * (int)definition.Attributes.Identity.Size, 100) };
            compact.AddChild(face); face.Render(snapshot, new CardDisplayAdapter());
            compact.AddChild(small); small.Render(snapshot, new CardDisplayAdapter());
            preview.Open(snapshot, null, null);
            for (var i = 0; i < 8; i++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = owner.GetViewport().GetTexture().GetImage();
            image.SavePng($"res://output/elementalist/{definition.Attributes.Identity.Key.ToString().Replace("card.", "")}.png");
            using var compactImage = compact.GetTexture().GetImage();
            compactImage.SavePng($"res://output/elementalist/{definition.Attributes.Identity.Key.ToString().Replace("card.", "")}-compact.png");
            face.QueueFree(); small.QueueFree();
            preview.Close();
        }
        preview.QueueFree();
        compact.QueueFree();
    }
}
