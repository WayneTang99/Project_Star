using System.Threading.Tasks;
using Project_Star.Application.Content;
using Project_Star.Presentation.Game;
using System;
using System.Collections.Generic;
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

// 手弩分级攻击、受疾速倍率及通用状态入口验证（表现层）。
internal static class HandCrossbowChecks
{
    internal static bool LevelsAndHaste()
    {
        var definition = new HandCrossbowCardDefinition(); var identity = definition.Attributes.Identity;
        if (identity.DisplayName != "手弩" || identity.Size != CardSize.Small || identity.FactionKey != new StringName("paladin")
            || definition.InitialLevel != 1 || definition.SupportsLevel(0) || definition.SupportsLevel(5)
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !definition.Tags.ToArray().ToHashSet().SetEquals(new[] { GameTags.Small, GameTags.Equipment })
            || !DefinitionRegistry.Scan(typeof(HandCrossbowChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        for (var level = 1; level <= 4; level++)
        foreach (var reverse in new[] { false, true })
        {
            var source = Crossbow(level); var damage = 20 << (level - 1);
            var normal = Run([source], [], 80, reverse);
            var hits = normal.Events.OfType<DamageDealtEvent>().ToArray();
            if (!hits.Select(e => (int)e.Tick.Value).SequenceEqual(new[] { 40, 80 })
                || hits.Any(e => e.RawDamage != damage || e.TargetSide != (reverse ? SideId.Player : SideId.Opponent))) return false;
            var self = source with { Abilities = source.Abilities!.Append(Opening(AbilityTarget.SelfCard,
                new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 10))).ToArray() };
            var hastened = Run([self], [], 40, reverse);
            if (State(hastened, source, 0).Haste != 20 || State(hastened, source, 20).Haste != 0
                || hastened.Events.OfType<DamageDealtEvent>().First().Tick.Value != 20
                || hastened.Events.OfType<StatusChangedEvent>().Single().Amount != 20) return false;
            var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            if (CardDisplayAdapter.CooldownSeconds(snapshot) != 4
                || !CardDisplayAdapter.AbilityDetails(snapshot.Abilities).Contains("疾速时长 ×2")
                || !CardDisplayAdapter.FaceEffects(snapshot).Any(e => e.Kind == CardFaceEffectKind.Damage && e.Value == damage.ToString())) return false;
        }
        return true;
    }

    internal static bool RoutesAndStacking()
    {
        var source = Crossbow(1);
        var giver = new CardBattleSetup(EntityId.New(), 0, 0, 0, [], UseLegacyAttack: false);
        foreach (var reverse in new[] { false, true })
        {
            AbilityDefinition[] routes =
            [
                Opening(AbilityTarget.RightAdjacentAlliedCard, new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 10)),
                Opening(AbilityTarget.SelfCard, new ApplyStatusToAdjacentAlliedCardsEffectDefinition(BattleStatus.HasteDuration, 10, GameTags.Human, 1)),
                Opening(AbilityTarget.SelfCard, new ApplyStatusToRandomAlliedCardEffectDefinition(BattleStatus.HasteDuration, 10, GameTags.Equipment)),
            ];
            foreach (var route in routes)
            {
                var caster = giver with { Abilities = [route], CombatValues = new Dictionary<StringName, int> { [GameAttributeKeys.HasteDurationBonus] = 5 } };
                if (State(Run([caster, source], [], 1, reverse), source, 0).Haste != 30) return false;
            }
            var enemy = giver with { Abilities = [Opening(AbilityTarget.SelfCard,
                new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.HasteDuration, 10))] };
            if (State(Run([source], [enemy], 1, reverse), source, 0).Haste != 20) return false;
            var repeated = giver with { Multicast = 1, Abilities = [new AbilityDefinition(new StringName("verification.crossbow_repeat"),
                AbilityActivation.Active, AbilityTarget.RightAdjacentAlliedCard, 0, 10,
                [new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 10)])] };
            var stacked = Run([repeated, source], [], 12, reverse);
            if (State(stacked, source, 12).Haste != 38
                || !stacked.Events.OfType<StatusChangedEvent>().Select(e => e.Amount).SequenceEqual(new[] { 20, 20 })) return false;
            foreach (var status in new[] { BattleStatus.SlowDuration, BattleStatus.ImmobilizeDuration })
            {
                var affected = source with { Abilities = source.Abilities!.Append(Opening(AbilityTarget.SelfCard,
                    new ApplyStatusEffectDefinition(status, 10))).ToArray() };
                var result = Run([affected], [], 1, reverse);
                if (result.Events.OfType<StatusChangedEvent>().Single().Amount != 10) return false;
            }
            var plain = source with { Abilities = [source.Abilities![0], Opening(AbilityTarget.SelfCard,
                new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 10))] };
            if (State(Run([plain], [], 1, reverse), source, 0).Haste != 10) return false;
            var doubled = source with { Abilities = source.Abilities!.Append(source.Abilities[1]).Append(Opening(AbilityTarget.SelfCard,
                new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 10))).ToArray() };
            if (State(Run([doubled], [], 1, reverse), source, 0).Haste != 40) return false;
            foreach (var inactive in new[] { source with { IsOnBench = true }, source with { Abilities = source.Abilities!.Append(
                Opening(AbilityTarget.SelfCard, new DestroyCardEffectDefinition(false))).ToArray() } })
                if (Run([inactive, giver with { Abilities = [routes[0]] }], [], 40, reverse).Events.OfType<DamageDealtEvent>().Any()) return false;
        }
        foreach (var invalid in new (AbilityActivation Activation, AbilityTarget Target, BattleStatus Status, int Multiplier, bool Bench)[]
        {
            (AbilityActivation.Active, AbilityTarget.SelfCard, BattleStatus.HasteDuration, 2, false),
            (AbilityActivation.PassiveAura, AbilityTarget.AlliedHero, BattleStatus.HasteDuration, 2, false),
            (AbilityActivation.PassiveAura, AbilityTarget.SelfCard, BattleStatus.Burn, 2, false),
            (AbilityActivation.PassiveAura, AbilityTarget.SelfCard, BattleStatus.HasteDuration, 0, false),
            (AbilityActivation.PassiveAura, AbilityTarget.SelfCard, BattleStatus.HasteDuration, 2, true),
        })
        {
            try
            {
                _ = new AbilityDefinition(new StringName("verification.crossbow_invalid"), invalid.Activation, invalid.Target, 0, 0,
                    [new MultiplyReceivedCardStatusDurationEffectDefinition(invalid.Status, invalid.Multiplier)], allowsBench: invalid.Bench);
                return false;
            }
            catch (ArgumentException) { }
        }
        return true;
    }

    private static CardBattleSetup Crossbow(int level)
    {
        var card = new EntityFactory().CreateCard(new HandCrossbowCardDefinition(), level);
        return new CardBattleSetup(card.Id, 1, card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage), 40,
            card.Abilities, UseLegacyAttack: false, Tags: card.Tags, ElementKeys: card.Attributes.Identity.ElementKeys)
            { CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues(), Level = level };
    }

    private static AbilityDefinition Opening(AbilityTarget target, EffectDefinition effect) => new(new StringName("verification.crossbow_opening"),
        AbilityActivation.PassiveOnBattleStart, target, 0, 0, [effect]);

    private static CardBattleSnapshot State(BattleResult result, CardBattleSetup card, int tick) =>
        result.States.Last(s => s.Tick.Value == tick).Cards.Single(c => c.Id == card.EntityId);

    private static BattleResult Run(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks, bool reverse)
    {
        var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), allies);
        var other = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), enemies);
        var setup = new BattleSetup(reverse ? other : own, reverse ? own : other, 42, new BattleTick(ticks), new BattleTick(ticks + 1));
        var result = new CombatSimulator().Simulate(setup);
        if (!result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
            || result.PermanentAttributeBonuses.Any() || result.PermanentChanges.Any()) throw new InvalidOperationException("手弩确定性或战斗隔离失败。");
        return result;
    }
    internal static async Task Capture(Node owner)
    {
        if (!LevelsAndHaste() || !RoutesAndStacking()) throw new InvalidOperationException("手弩战斗验证失败。");
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new HandCrossbowCardDefinition()));
        var adapter = new CardDisplayAdapter();
        using var raw = adapter.Artwork(snapshot.Illustration).GetImage();
        GD.Print($"手弩纹理检查：{raw.GetWidth()}×{raw.GetHeight()}，mipmap层数{raw.GetMipmapCount()}");
        if (!raw.HasMipmaps() || raw.GetWidth() * 2 != raw.GetHeight())
            throw new InvalidOperationException("手弩原画比例或mipmap不合格。");
        GD.Print($"手弩原画：{raw.GetWidth()}×{raw.GetHeight()}，mipmap层数{raw.GetMipmapCount()}");
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
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(HandCrossbowChecks).Assembly)));
            preview.Open(snapshot, null, null);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            using var detail = owner.GetViewport().GetTexture().GetImage();
            if (image.SavePng("res://output/hand-crossbow/card-face.png") != Error.Ok
                || detail.SavePng("res://output/hand-crossbow/preview.png") != Error.Ok)
                throw new InvalidOperationException("手弩卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); owner.RemoveChild(preview); preview.Free(); }
    }
}
