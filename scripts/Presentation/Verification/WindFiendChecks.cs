using System.Threading.Tasks;
using Project_Star.Application.Content;
using Project_Star.Presentation.Playtest;
using System;
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

// 风魔怪四级多重伤害、自身疾速成长与触发隔离验证（表现层）。
internal static class WindFiendChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new WindFiendCardDefinition(); var identity = definition.Attributes.Identity;
        if (identity.DisplayName != "风魔怪" || identity.FactionKey != new StringName("mona")
            || identity.Size != CardSize.Medium || definition.InitialLevel != 1
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Wind })
            || definition.Tags.Count != 1 || definition.SupportsLevel(0) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(WindFiendChecks).Assembly).Cards.ContainsKey(identity.Key)) return Fail();
        foreach (var level in new[] { 1, 2, 3, 4 })
        foreach (var reverse in new[] { false, true })
        {
            var instance = new EntityFactory().CreateCard(definition, level); var damage = 10 << (level - 1); var growth = 5 << (level - 1);
            var source = new CardBattleSetup(instance.Id, 1, damage, 60, instance.Abilities, UseLegacyAttack: false,
                Tags: instance.Tags, Multicast: instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Multicast),
                ElementKeys: identity.ElementKeys, OccupiedSlots: 2) { Level = level };
            var normal = Run([source], [], 125, reverse);
            if (normal.States.Last(s => s.Tick.Value == 59).Cards.Single(c => c.Id == source.EntityId).IsFlying
                || !normal.States.Last(s => s.Tick.Value == 60).Cards.Single(c => c.Id == source.EntityId).IsFlying
                || !normal.States.Last().Cards.Single(c => c.Id == source.EntityId).IsFlying
                || normal.Events.OfType<CardStateChangedEvent>().Count(e => e.CardId == source.EntityId
                    && e.StateKey == GameAttributeKeys.Flying && e.Enabled && e.Tick.Value == 60) != 1) return Fail();
            if (!Hits(normal, source).Select(e => e.Tick.Value).SequenceEqual(new long[] { 60, 62, 120, 122 })
                || Hits(normal, source).Any(e => e.RawDamage != damage) || Echoes(normal, source) != 0) return Fail();
            var broom = new EntityFactory().CreateCard(new FlyingBroomCardDefinition(), 1);
            var paired = Run([source, new CardBattleSetup(broom.Id, 3, 0, 50, broom.Abilities,
                UseLegacyAttack: false, ElementKeys: broom.Attributes.Identity.ElementKeys, OccupiedSlots: 2)], [], 70, reverse);
            // 扫帚回响给予飞行者疾速，但回响产生的状态沿用连锁截断，不再触发攻击成长。
            if (paired.States.Last(s => s.Tick.Value == 60).Cards.Single(c => c.Id == source.EntityId).Haste != 10
                || Attack(paired, source) != damage || Echoes(paired, source) != 0) return Fail();
            var giver = Giver(AbilityActivation.PassiveOnBattleStart, BattleStatus.HasteDuration, 10, 20);
            var boosted = Run([giver, source], [], 125, reverse);
            if (Echoes(boosted, source) != 2 || Attack(boosted, source) != damage + growth * 2
                || Hits(boosted, source).Any(e => e.RawDamage != damage + growth * 2)
                || Hits(boosted, source).First().Tick.Value >= 60
                || boosted.States.Last().Cards.Single(c => c.Id == source.EntityId).Haste != 0
                || boosted.PermanentAttributeBonuses.Any() || boosted.PermanentChanges.Any()
                || instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != damage) return Fail();
            var multiGiver = Giver(AbilityActivation.Active, BattleStatus.HasteDuration, 10) with { Multicast = 1 };
            var repeated = Run([multiGiver, source], [], 12, reverse);
            if (Echoes(repeated, source) != 2 || Attack(repeated, source) != damage + growth * 2) return Fail();
            foreach (var status in new[] { BattleStatus.SlowDuration, BattleStatus.ImmobilizeDuration, BattleStatus.HasteDuration })
            {
                var filtered = Giver(AbilityActivation.PassiveOnBattleStart, status, status == BattleStatus.HasteDuration ? 0 : 10);
                var result = Run([filtered, source], [], 20, reverse);
                if (Attack(result, source) != damage || Echoes(result, source) != 0) return Fail();
            }
            var echoGiver = Giver(AbilityActivation.EchoOnSourceCardActivated, BattleStatus.HasteDuration, 10);
            echoGiver = echoGiver with { Abilities = echoGiver.Abilities!.Prepend(new AbilityDefinition(
                new StringName("verification.wind_activate"), AbilityActivation.Active, AbilityTarget.AlliedHero, 0, 10,
                [new RestoreManaEffectDefinition(1)])).ToArray() };
            if (Echoes(Run([echoGiver, source], [], 12, reverse), source) != 0) return Fail();
            var distant = source with { BoardStart = 3 };
            var other = new CardBattleSetup(EntityId.New(), 1, 0, 0, [], UseLegacyAttack: false);
            if (Echoes(Run([giver, other, distant], [], 20, reverse), source) != 0
                || Hits(Run([source with { IsOnBench = true }], [], 125, reverse), source).Any()
                || !normal.Events.SequenceEqual(Run([source], [], 125, reverse).Events)) return Fail();
            var offer = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            var face = new CardDisplayAdapter().Build(offer).Effects;
            if (!face.Contains(new CardFaceEffect(CardFaceEffectKind.Damage, damage.ToString()))
                || !face.Contains(new CardFaceEffect(CardFaceEffectKind.Multicast, "1"))) return Fail();
        }
        return true;
    }

    private static CardBattleSetup Giver(AbilityActivation activation, BattleStatus status, params int[] amounts) =>
        new(EntityId.New(), 0, 0, activation == AbilityActivation.Active ? 10 : 0,
            [new AbilityDefinition(new StringName("verification.wind_haste"), activation, AbilityTarget.RightAdjacentAlliedCard,
                0, activation == AbilityActivation.Active ? 10 : 0,
                amounts.Select(amount => (EffectDefinition)new ApplyStatusEffectDefinition(status, amount)).ToArray())], UseLegacyAttack: false);
    private static BattleResult Run(CardBattleSetup[] own, CardBattleSetup[] enemy, int ticks, bool reverse) =>
        new CombatSimulator().Simulate(new BattleSetup(
            new BattleSideSetup(new HeroBattleSetup(new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000001")), 10000, 0, ManaRegen: 0), reverse ? enemy : own),
            new BattleSideSetup(new HeroBattleSetup(new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000002")), 10000, 0, ManaRegen: 0), reverse ? own : enemy),
            42, new BattleTick(ticks), new BattleTick(ticks + 1)));
    private static DamageDealtEvent[] Hits(BattleResult result, CardBattleSetup source) => result.Events.OfType<DamageDealtEvent>()
        .Where(e => e.SourceCardId == source.EntityId).ToArray();
    private static int Echoes(BattleResult result, CardBattleSetup source) => result.Events.OfType<AbilityActivatedEvent>()
        .Count(e => e.SourceCardId == source.EntityId && e.IsEcho);
    private static int Attack(BattleResult result, CardBattleSetup source) => result.States.Last().Cards.Single(c => c.Id == source.EntityId)
        .Values[GameAttributeKeys.AttackDamage];
    private static bool Fail([System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    { GD.Print($"风魔怪验证失败，行{line}"); return false; }
    internal static async Task Capture(Node owner)
    {
        if (!LevelsAndCombat()) throw new InvalidOperationException("风魔怪战斗验证失败。");
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new WindFiendCardDefinition()));
        var adapter = new CardDisplayAdapter();
        using var raw = adapter.Artwork(snapshot.Illustration).GetImage();
        if (!raw.HasMipmaps() || raw.GetWidth() != raw.GetHeight())
            throw new InvalidOperationException("风魔怪原画比例或mipmap不合格。");
        GD.Print($"风魔怪原画：{raw.GetWidth()}×{raw.GetHeight()}，mipmap层数{raw.GetMipmapCount()}");
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
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(WindFiendChecks).Assembly)));
            preview.Open(snapshot, null, null);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            using var detail = owner.GetViewport().GetTexture().GetImage();
            if (image.SavePng("res://output/wind-fiend/card-face.png") != Error.Ok
                || detail.SavePng("res://output/wind-fiend/preview.png") != Error.Ok)
                throw new InvalidOperationException("风魔怪卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); owner.RemoveChild(preview); preview.Free(); }
    }
}
