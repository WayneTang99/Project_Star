using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Economy;
using Project_Star.Application.Match;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 月影喵分级、随机禁锢及相邻光环贡献验证（表现层）。
internal static class MoonshadowCatChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new MoonshadowCatCardDefinition();
        var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.Key != new StringName("card.moonshadow_cat") || identity.DisplayName != "月影喵"
            || identity.FactionKey != new StringName("mona") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Dark })
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Beast)
            || definition.InitialLevel != 1 || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(MoonshadowCatChecks).Assembly).Cards.ContainsKey(identity.Key)
            || texture is null || texture.GetWidth() * 2 != texture.GetHeight()) return false;
        for (var level = 1; level <= 4; level++)
        {
            var cat = Cat(level);
            var cooldown = (10 - level) * 10;
            for (var neighbors = 0; neighbors <= 2; neighbors++)
            foreach (var mirror in new[] { false, true })
            {
                var allies = new[] { cat }.Concat(Enumerable.Range(0, neighbors)
                    .Select(i => Neighbor(i == 0 ? 0 : 4) with { OccupiedSlots = i == 0 ? 3 : 1 })).ToArray();
                var targets = new[] { Neighbor(0), Neighbor(2), Neighbor(4) with { IsOnBench = true } };
                var setup = Setup(allies, targets, cooldown + neighbors * 10, mirror);
                var result = new CombatSimulator().Simulate(setup);
                var applied = result.Events.OfType<StatusChangedEvent>().Where(e => e.Status == BattleStatus.ImmobilizeDuration).ToArray();
                var activations = result.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId == cat.EntityId).ToArray();
                if (applied.Length != 1 || applied[0].Amount != 10 || applied[0].Tick.Value != cooldown + neighbors * 10
                    || activations.Length != 1 || activations[0].Tick.Value != cooldown + neighbors * 10
                    || result.States.Last().Cards.Single(c => c.Id == targets[2].EntityId).Immobilize != 0
                    || result.Events.OfType<ManaChangedEvent>().Any() || result.PermanentChanges.Any()
                    || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            }
            if (cat.Abilities![0].ManaCost != 0 || cat.CooldownTicks != cooldown) return false;
        }
        return true;
    }

    internal static bool FiltersAndRemoval()
    {
        var cat = Cat(4);
        foreach (var excluded in new[] { Neighbor(0) with { OccupiedSlots = 2 }, Neighbor(5),
            Neighbor(2) with { IsOnBench = true }, Neighbor(2) with { Tags = new TagSet([GameTags.Beast]) } })
        {
            var result = new CombatSimulator().Simulate(Setup([cat, excluded], [Neighbor(2)], 60));
            if (result.Events.OfType<AbilityActivatedEvent>().Single(e => e.SourceCardId == cat.EntityId).Tick.Value != 60) return false;
        }
        var destroy = new AbilityDefinition("verification.moonshadow.destroy", AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, 20, [new DestroyCardEffectDefinition(false)]);
        var neighbor = Neighbor(2) with { Abilities = [destroy] };
        // 邻居在2秒摧毁，撤销1秒光环后仍于原6秒发动；永久倍率同时作用于光环。
        foreach (var multiplier in new[] { 1m, 0.5m })
        {
            var ticks = (int)(60 * multiplier);
            var result = new CombatSimulator().Simulate(Setup([cat with { CooldownMultiplier = multiplier }, neighbor],
                [Neighbor(0)], ticks));
            if (result.Events.OfType<AbilityActivatedEvent>().Single(e => e.SourceCardId == cat.EntityId).Tick.Value != ticks) return false;
        }
        var benchResult = new CombatSimulator().Simulate(Setup([cat with { IsOnBench = true }, Neighbor(2)], [], 100));
        if (benchResult.Events.OfType<AbilityActivatedEvent>().Any(e => e.SourceCardId == cat.EntityId)) return false;
        // 多重对唯一有效敌方目标累加，禁锢自然衰减；空目标仍完成发动。
        var target = Neighbor(0);
        var stacked = new CombatSimulator().Simulate(Setup([cat with { Multicast = 1 }], [target], 82));
        if (stacked.Events.OfType<StatusChangedEvent>().Count(e => e.Status == BattleStatus.ImmobilizeDuration && e.Amount == 10) != 2
            || stacked.States.Last(f => f.Tick.Value == 60).Cards.Single(c => c.Id == target.EntityId).Immobilize != 10
            || stacked.States.Last().Cards.Single(c => c.Id == target.EntityId).Immobilize != 0) return false;
        var empty = new CombatSimulator().Simulate(Setup([cat], [], 60));
        if (empty.Events.OfType<StatusChangedEvent>().Any() || empty.Events.OfType<AbilityActivatedEvent>().Count() != 1) return false;
        foreach (var invalid in new Action[] {
            () => _ = new AbilityDefinition("verification.invalid", AbilityActivation.Active, AbilityTarget.SelfCard, 0, 1,
                [new IncreaseSourceCooldownPerAdjacentAlliedTaggedCardEffectDefinition(GameTags.Human, 10)]),
            () => _ = new AbilityDefinition("verification.invalid", AbilityActivation.PassiveAura, AbilityTarget.SelfCard, 0, 0,
                [new IncreaseSourceCooldownPerAdjacentAlliedTaggedCardEffectDefinition(GameTags.Human, 0)]) })
        {
            try { invalid(); return false; } catch (ArgumentException) { }
        }
        return true;
    }

    private static CardBattleSetup Cat(int level)
    {
        var instance = new EntityFactory().CreateCard(new MoonshadowCatCardDefinition(), level);
        return new(instance.Id, 3, 0, instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks),
            instance.Abilities, UseLegacyAttack: false, Tags: instance.Tags, ElementKeys: instance.Attributes.Identity.ElementKeys);
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(320, 608), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(320, 608) });
            var face = new CardItemView { Position = new Vector2(24, 24), Size = new Vector2(272, 560) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new MoonshadowCatCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/moonshadow-cat/card-face.png") != Error.Ok)
                throw new InvalidOperationException("月影喵卡面截图保存失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }
    private static CardBattleSetup Neighbor(int start) => new(EntityId.New(), start, 0, 0, [],
        UseLegacyAttack: false, Tags: new TagSet([GameTags.Human]));
    private static BattleSetup Setup(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks, bool mirror = false)
    {
        var player = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, MaxMana: 0, ManaRegen: 0), cards);
        var opponent = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, MaxMana: 0, ManaRegen: 0), enemies);
        return new(mirror ? opponent : player, mirror ? player : opponent, 42, new BattleTick(ticks), new BattleTick(ticks + 1));
    }
}
