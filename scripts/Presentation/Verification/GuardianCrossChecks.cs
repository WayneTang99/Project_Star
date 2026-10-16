using System;
using System.Threading.Tasks;
using Project_Star.Application.Content;
using Project_Star.Presentation.Playtest;
using System.Linq;
using Godot;
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

// 守护十字分级、相邻发动护甲与回响隔离验证（表现层）。
internal static class GuardianCrossChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new GuardianCrossCardDefinition(); var identity = definition.Attributes.Identity;
        if (identity.DisplayName != "守护十字" || identity.FactionKey != new StringName("paladin")
            || identity.Size != CardSize.Small || definition.InitialLevel != 1
            || definition.SupportsLevel(0) || definition.SupportsLevel(5)
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Light })
            || !definition.Tags.ToArray().ToHashSet().SetEquals(new[] { GameTags.Small, GameTags.Equipment })
            || !DefinitionRegistry.Scan(typeof(GuardianCrossChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        for (var level = 1; level <= 4; level++)
        foreach (var reverse in new[] { false, true })
        {
            var card = new EntityFactory().CreateCard(definition, level); var amount = 10 << (level - 1);
            var source = new CardBattleSetup(card.Id, 3, 0, 0, card.Abilities, UseLegacyAttack: false,
                Tags: card.Tags, ElementKeys: identity.ElementKeys, ArmorAmount: amount)
                { CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues(), Level = level };
            var left = Neighbor(0, 3); var right = Neighbor(4, 2);
            if (card.Abilities.Any(a => a.Activation == AbilityActivation.Active || a.CooldownTicks != 0)
                || !Check(source, [], [], reverse, amount, 0)
                || !Check(source, [left], [], reverse, amount, 2)
                || !Check(source, [right], [], reverse, amount, 2)
                || !Check(source, [left with { Multicast = 1 }, right], [], reverse, amount, 6)
                || !Check(source with { Multicast = 2 }, [left], [], reverse, amount, 2)) return false;
            foreach (var excluded in new[]
            {
                left with { OccupiedSlots = 2 }, right with { BoardStart = 5 }, left with { IsOnBench = true },
                left with { Abilities = [new AbilityDefinition(new StringName("verification.cross_unpaid"),
                    AbilityActivation.Active, AbilityTarget.SelfCard, 1, 20, [new RestoreManaEffectDefinition(1)])] },
                left with { Abilities = [new AbilityDefinition(new StringName("verification.cross_opening"),
                    AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new RestoreManaEffectDefinition(1)])] },
                left with { Abilities = [new AbilityDefinition(new StringName("verification.cross_destroy"),
                    AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])] },
                left with { Abilities = [new AbilityDefinition(new StringName("verification.cross_echo"),
                    AbilityActivation.EchoOnAbilityActivated, AbilityTarget.SelfCard, 0, 0, [new RestoreManaEffectDefinition(1)])] },
            })
                if (!Check(source, [excluded], [], reverse, amount, 0)) return false;
            var dead = source with { Abilities = source.Abilities!.Append(new AbilityDefinition(new StringName("verification.cross_source_destroy"),
                AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])).ToArray() };
            if (!Check(source, [], [left], reverse, amount, 0)
                || !Check(source with { IsOnBench = true }, [left], [], reverse, amount, 0)
                || !Check(dead, [left], [], reverse, amount, 0)) return false;
            var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            if (snapshot.Abilities.Any(a => a.Activation == AbilityActivation.Active)
                || CardDisplayAdapter.CurrentDetails(snapshot).Contains("冷却")
                || !CardDisplayAdapter.FaceEffects(snapshot).Any(e => e.Kind == CardFaceEffectKind.Armor && e.Value == amount.ToString())) return false;
        }
        return true;
    }

    private static CardBattleSetup Neighbor(int start, int slots) => new(EntityId.New(), start, 0, 20,
        [new AbilityDefinition(new StringName("verification.cross_neighbor"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, 20, [new RestoreManaEffectDefinition(1)])],
        UseLegacyAttack: false, Tags: new TagSet([GameTags.Human]), OccupiedSlots: slots);

    private static bool Check(CardBattleSetup source, CardBattleSetup[] allies, CardBattleSetup[] enemies,
        bool reverse, int amount, int count)
    {
        var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), [source, .. allies]);
        var other = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), enemies);
        var setup = new BattleSetup(reverse ? other : own, reverse ? own : other, 42, new BattleTick(43), new BattleTick(44));
        var result = new CombatSimulator().Simulate(setup);
        var state = result.States.Last(s => s.Tick.Value == 42);
        var hero = reverse ? state.Opponent : state.Player;
        var activations = result.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId == source.EntityId).ToArray();
        var echoes = source.Abilities!.Count(a => a.Activation == AbilityActivation.EchoOnAdjacentAlliedCardActivated);
        var passed = hero.Armor == amount * count && activations.Count(e => e.IsEcho) == count
            && activations.Count(e => !e.IsEcho) == source.Abilities!.Count(a => a.Activation == AbilityActivation.PassiveOnBattleStart)
            && echoes == 2
            && !result.PermanentAttributeBonuses.Any() && !result.PermanentChanges.Any()
            && result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events);
        if (!passed) GD.Print($"守护十字验证失败：level={source.Level},reverse={reverse},expected={count}×{amount},armor={hero.Armor},events={activations.Length}");
        return passed;
    }
    internal static async Task Capture(Node owner)
    {
        if (!LevelsAndCombat()) throw new InvalidOperationException("守护十字战斗验证失败。");
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new GuardianCrossCardDefinition()));
        var adapter = new CardDisplayAdapter();
        using var raw = adapter.Artwork(snapshot.Illustration).GetImage();
        if (!raw.HasMipmaps() || raw.GetWidth() * 2 != raw.GetHeight())
            throw new InvalidOperationException("守护十字原画比例或mipmap不合格。");
        GD.Print($"守护十字原画：{raw.GetWidth()}×{raw.GetHeight()}，mipmap层数{raw.GetMipmapCount()}");
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
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(GuardianCrossChecks).Assembly)));
            preview.Open(snapshot, null, null);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            using var detail = owner.GetViewport().GetTexture().GetImage();
            if (image.SavePng("res://output/guardian-cross/card-face.png") != Error.Ok
                || detail.SavePng("res://output/guardian-cross/preview.png") != Error.Ok)
                throw new InvalidOperationException("守护十字卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); owner.RemoveChild(preview); preview.Free(); }
    }
}
