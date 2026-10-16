using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Content;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 护手分级护甲、多重、相邻装备回响和原画验证（表现层）。
internal static class GauntletChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new GauntletCardDefinition(); var identity = definition.Attributes.Identity;
        if (identity.DisplayName != "护手" || identity.FactionKey != new StringName("paladin")
            || identity.Size != CardSize.Small || definition.InitialLevel != 1
            || definition.SupportsLevel(0) || definition.SupportsLevel(5)
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !definition.Tags.ToArray().ToHashSet().SetEquals(new[] { GameTags.Small, GameTags.Equipment })
            || !DefinitionRegistry.Scan(typeof(GauntletChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        for (var level = 1; level <= 4; level++)
        foreach (var reverse in new[] { false, true })
        {
            var card = new EntityFactory().CreateCard(definition, level); var amount = 10 << (level - 1);
            var source = new CardBattleSetup(card.Id, 3, amount, 50, card.Abilities, UseLegacyAttack: false,
                Tags: card.Tags, Multicast: card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Multicast),
                ElementKeys: identity.ElementKeys, ArmorAmount: amount)
                { CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues(), Level = level };
            if (source.Multicast != 1 || !Check(source, [], [], reverse, amount, [])) return false;
            var left = Neighbor(0, 3); var right = Neighbor(4, 2);
            if (!Check(source, [left], [], reverse, amount, [20, 40])
                || !Check(source, [right], [], reverse, amount, [20, 40])
                || !Check(source, [left with { Multicast = 1 }, right], [], reverse, amount, [20, 20, 22, 40, 40, 42])) return false;
            foreach (var excluded in new[]
            {
                left with { BoardStart = 0, OccupiedSlots = 2 }, right with { BoardStart = 5 },
                left with { Tags = new TagSet([GameTags.Human]) }, left with { IsOnBench = true },
                left with { Abilities = [new AbilityDefinition(new StringName("verification.gauntlet_unpaid"),
                    AbilityActivation.Active, AbilityTarget.SelfCard, 1, 20, [new GainSourceHeroArmorEffectDefinition(1)])] },
                left with { Abilities = [new AbilityDefinition(new StringName("verification.gauntlet_opening"),
                    AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new GainSourceHeroArmorEffectDefinition(1)])] },
                left with { Abilities = [new AbilityDefinition(new StringName("verification.gauntlet_destroy"),
                    AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])] },
            })
                if (!Check(source, [excluded], [], reverse, amount, [])) return false;
            if (!Check(source, [], [left], reverse, amount, [])
                || !Check(source with { IsOnBench = true }, [left], [], reverse, amount, [])) return false;
            var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            var effects = CardDisplayAdapter.FaceEffects(snapshot);
            if (CardDisplayAdapter.CooldownSeconds(snapshot) != 5
                || !effects.Any(e => e.Kind == CardFaceEffectKind.Damage && e.Value == amount.ToString())
                || !effects.Any(e => e.Kind == CardFaceEffectKind.Armor && e.Value == amount.ToString())) return false;
        }
        return true;
    }

    private static CardBattleSetup Neighbor(int start, int slots) => new(EntityId.New(), start, 0, 20,
        [new AbilityDefinition(new StringName("verification.gauntlet_neighbor"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, 20, [new GainSourceHeroArmorEffectDefinition(1)])],
        UseLegacyAttack: false, Tags: new TagSet([GameTags.Equipment]), OccupiedSlots: slots);

    private static bool Check(CardBattleSetup source, CardBattleSetup[] allies, CardBattleSetup[] enemies,
        bool reverse, int amount, int[] ticks)
    {
        var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), [source, .. allies]);
        var other = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), enemies);
        var setup = new BattleSetup(reverse ? other : own, reverse ? own : other, 42, new BattleTick(53), new BattleTick(54));
        var result = new CombatSimulator().Simulate(setup);
        var hits = result.Events.OfType<DamageDealtEvent>().Where(e => e.SourceCardId == source.EntityId).ToArray();
        var activations = result.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId == source.EntityId && !e.IsEcho).ToArray();
        var before = result.States.Last(s => s.Tick.Value == 49);
        var after = result.States.Last(s => s.Tick.Value == 52);
        var armorDelta = reverse ? after.Opponent.Armor - before.Opponent.Armor : after.Player.Armor - before.Player.Armor;
        var passed = hits.Select(e => (int)e.Tick.Value).SequenceEqual(ticks)
            && hits.All(e => e.RawDamage == amount && e.TargetSide == (reverse ? SideId.Player : SideId.Opponent))
            && activations.Select(e => (int)e.Tick.Value).SequenceEqual(source.IsOnBench ? Array.Empty<int>() : new[] { 50, 52 })
            && armorDelta == (source.IsOnBench ? 0 : amount * 2)
            && !result.PermanentAttributeBonuses.Any() && !result.PermanentChanges.Any()
            && result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events);
        if (!passed) GD.Print($"护手验证失败：level={source.Level},reverse={reverse},expected={string.Join(',', ticks)},hits={string.Join(',', hits.Select(e => e.Tick.Value))},armorDelta={armorDelta}");
        return passed;
    }

    internal static async Task Capture(Node owner)
    {
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new GauntletCardDefinition()));
        var adapter = new CardDisplayAdapter();
        using var raw = adapter.Artwork(snapshot.Illustration).GetImage();
        if (!raw.HasMipmaps() || raw.GetWidth() * 2 != raw.GetHeight())
            throw new InvalidOperationException("护手原画比例或mipmap不合格。");
        GD.Print($"护手原画：{raw.GetWidth()}×{raw.GetHeight()}，mipmap层数{raw.GetMipmapCount()}");
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
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(GauntletChecks).Assembly)));
            preview.Open(snapshot, null, null);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            using var detail = owner.GetViewport().GetTexture().GetImage();
            if (image.SavePng("res://output/gauntlet/card-face.png") != Error.Ok
                || detail.SavePng("res://output/gauntlet/preview.png") != Error.Ok)
                throw new InvalidOperationException("护手卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); owner.RemoveChild(preview); preview.Free(); }
    }
}
