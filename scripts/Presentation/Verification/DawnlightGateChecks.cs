using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
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
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

// 辰光之门分级、发动者排除、随机充能与回响截断验证（表现层）。
internal static class DawnlightGateChecks
{
    internal static bool LevelsAndCharge()
    {
        var definition = new DawnlightGateCardDefinition();
        var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (!DefinitionRegistry.Scan(typeof(DawnlightGateCardDefinition).Assembly).Cards.ContainsKey(identity.Key)
            || identity.DisplayName != "辰光之门" || identity.FactionKey != new StringName("paladin")
            || identity.Size != CardSize.Medium || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Light })
            || !definition.Tags.Contains(GameTags.Location) || definition.InitialLevel != 3
            || definition.SupportsLevel(2) || definition.SupportsLevel(5)
            || identity.DescriptionEntries.Single().KeywordKey != CardKeywords.Echo
            || texture is null || texture.GetWidth() != texture.GetHeight()) return false;
        foreach (var level in new[] { 3, 4 })
        foreach (var reverse in new[] { false, true })
        {
            var gate = Gate(level); var source = Active(2, 10); var target = Active(3, 100);
            var setup = Battle([gate, source, target], [], 10, reverse);
            var result = new CombatSimulator().Simulate(setup);
            var charge = result.Events.OfType<CardChargedEvent>().Single();
            if (charge.SourceCardId != gate.EntityId || charge.TargetCardId != target.EntityId
                || charge.AmountTicks != (level == 3 ? 5 : 10) || charge.Tick.Value != 10
                || result.States[^1].Cards.Single(c => c.Id == target.EntityId).CooldownUnits.Single() != (90 - charge.AmountTicks) * 2
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                || gate.Abilities!.Any(a => a.Activation == AbilityActivation.Active || a.CooldownTicks != 0 || a.ManaCost != 0)) return false;
            var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            if (snapshot.Abilities.Any(a => a.Activation == AbilityActivation.Active)) return false;
        }
        return true;
    }

    internal static bool FiltersAndRandom()
    {
        var gate = Gate(3); var source = Active(2, 10); var first = Active(3, 100); var second = Active(4, 100);
        var nonLight = Active(5, 100) with { ElementKeys = [GameElements.Fire] };
        var bench = Active(6, 100) with { IsOnBench = true };
        var dead = Active(7, 100) with { Abilities = [Opening(new DestroyCardEffectDefinition(false))] };
        var passive = Active(8, 100) with { Abilities = [Opening(new ArmorEffectDefinition(0))] };
        var enemy = Active(0, 100);
        var chosen = new HashSet<EntityId>();
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var setup = Battle([gate, source, first, second, nonLight, bench, dead, passive], [enemy], 10, seed: seed);
            var result = new CombatSimulator().Simulate(setup);
            var charge = result.Events.OfType<CardChargedEvent>().Single();
            if (charge.TargetCardId != first.EntityId && charge.TargetCardId != second.EntityId
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            chosen.Add(charge.TargetCardId);
        }
        if (chosen.Count != 2) return false;
        foreach (var inactive in new[] { gate with { IsOnBench = true }, gate with { Abilities = [Opening(new DestroyCardEffectDefinition(false))] } })
            if (new CombatSimulator().Simulate(Battle([inactive, source, first], [], 10)).Events.OfType<CardChargedEvent>().Any()) return false;
        foreach (var invalidSource in new[] { source with { ElementKeys = [GameElements.Fire] }, source with { IsOnBench = true },
            source with { Abilities = [Opening(new ArmorEffectDefinition(0))] },
            source with { Abilities = [new AbilityDefinition("verification.gate.mana", AbilityActivation.Active, AbilityTarget.EnemyHero, 999, 10, [new DamageEffectDefinition(0)])] } })
            if (new CombatSimulator().Simulate(Battle([gate, invalidSource, first], [], 10)).Events.OfType<CardChargedEvent>().Any()) return false;
        if (new CombatSimulator().Simulate(Battle([gate, first], [source], 10)).Events.OfType<CardChargedEvent>().Any()
            || new CombatSimulator().Simulate(Battle([gate, source, nonLight, bench, dead, passive], [], 10)).Events.OfType<CardChargedEvent>().Any()) return false;
        var repeated = new CombatSimulator().Simulate(Battle([gate, source with { Multicast = 1 }, first], [], 12));
        return repeated.Events.OfType<CardChargedEvent>().Select(e => e.Tick.Value).SequenceEqual(new long[] { 10, 12 });
    }

    internal static bool ImmediateChargeAndEchoCutoff()
    {
        var gate = Gate(4); var source = Active(2, 10); var target = Active(3, 20) with { Multicast = 1 };
        var setup = Battle([gate, source, target], [], 12);
        var result = new CombatSimulator().Simulate(setup);
        if (result.Events.OfType<CardChargedEvent>().Count() != 1
            || !result.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId == target.EntityId)
                .Select(e => e.Tick.Value).SequenceEqual(new long[] { 10, 12 })
            || result.States[^1].Cards.Single(c => c.Id == target.EntityId).CooldownUnits.Single() != 36
            || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
        // 独立正常发动仍触发回响；充能立即发动及其多重不再连锁。
        var later = new CombatSimulator().Simulate(Battle([gate, source, target], [], 22));
        if (!later.Events.OfType<CardChargedEvent>().Select(e => e.Tick.Value).SequenceEqual(new long[] { 10, 20 })) return false;
        var echo = gate.Abilities!.Single();
        try
        {
            _ = new AbilityDefinition("verification.gate.invalid", AbilityActivation.Active, AbilityTarget.SelfCard, 0, 10, echo.Effects);
            return false;
        }
        catch (ArgumentException) { return true; }
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(448, 448), Disable3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(448, 448) });
            var face = new CardItemView { Position = new Vector2(24, 24), Size = new Vector2(400, 400) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new DawnlightGateCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/dawnlight-gate/card-face.png") != Error.Ok) throw new InvalidOperationException("辰光之门卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardBattleSetup Gate(int level)
    {
        var card = new EntityFactory().CreateCard(new DawnlightGateCardDefinition(), level);
        return new(card.Id, 0, 0, 0, card.Abilities, UseLegacyAttack: false, Tags: card.Tags, OccupiedSlots: 2, ElementKeys: [GameElements.Light]);
    }
    private static CardBattleSetup Active(int start, int cooldown) => new(EntityId.New(), start, 0, cooldown, ElementKeys: [GameElements.Light]);
    private static AbilityDefinition Opening(EffectDefinition effect) => new("verification.gate.opening", AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [effect]);
    private static BattleSetup Battle(CardBattleSetup[] allies, CardBattleSetup[] enemies, int timeout, bool reverse = false, ulong seed = 42) =>
        new(new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), reverse ? enemies : allies),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), reverse ? allies : enemies), seed,
            new BattleTick(timeout), new BattleTick(timeout + 1));
}
