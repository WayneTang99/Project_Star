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

// 农夫分级、随机人类充能与目标边界验证（表现层）。
internal static class FarmerChecks
{
    internal static bool LevelsAndCharge()
    {
        var definition = new FarmerCardDefinition();
        var identity = definition.Attributes.Identity;
        var registry = DefinitionRegistry.Scan(typeof(FarmerCardDefinition).Assembly);
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (!registry.Cards.ContainsKey(identity.Key) || identity.Key != new StringName("card.farmer")
            || identity.DisplayName != "农夫" || identity.FactionKey != new StringName("paladin")
            || identity.Size != CardSize.Small || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !definition.Tags.Contains(GameTags.Human) || definition.InitialLevel != 1 || definition.SupportsLevel(5)
            || texture is null || texture.GetHeight() != texture.GetWidth() * 2) return false;
        for (var level = 1; level <= 4; level++)
        foreach (var reverse in new[] { false, true })
        {
            var instance = new EntityFactory().CreateCard(definition, level);
            var cooldown = (10 - level) * 10;
            var farmer = new CardBattleSetup(instance.Id, 0, 0, cooldown, instance.Abilities,
                UseLegacyAttack: false, Tags: instance.Tags);
            if (instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != cooldown
                || instance.Abilities.Single().CooldownTicks != cooldown || instance.Abilities.Single().ManaCost != 0) return false;
            var human = Target(1, cooldown + 10);
            var setup = Battle([farmer, human], [], cooldown * 2, reverse);
            var result = new CombatSimulator().Simulate(setup);
            var charges = result.Events.OfType<CardChargedEvent>().ToArray();
            if (charges.Length != 2 || charges.Any(e => e.SourceCardId != farmer.EntityId || e.TargetCardId != human.EntityId || e.AmountTicks != 10)
                || !charges.Select(e => e.Tick.Value).SequenceEqual(new long[] { cooldown, cooldown * 2 })
                || !result.Events.OfType<DamageDealtEvent>().Where(e => e.SourceCardId == human.EntityId)
                    .Select(e => e.Tick.Value).SequenceEqual(new long[] { cooldown, cooldown * 2 })
                || result.Events.OfType<ManaChangedEvent>().Any() || result.PermanentChanges.Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            if (CardDisplayAdapter.CooldownSeconds(snapshot) != cooldown / 10m
                || !CardDisplayAdapter.AbilityDetails(snapshot.Abilities).Contains("人类")
                || CardDisplayAdapter.FaceEffects(snapshot).Any(e => e.Kind == CardFaceEffectKind.Damage)) return false;
        }
        return true;
    }

    internal static bool FiltersAndMulticast()
    {
        var instance = new EntityFactory().CreateCard(new FarmerCardDefinition());
        var farmer = new CardBattleSetup(instance.Id, 0, 0, 90, instance.Abilities,
            UseLegacyAttack: false, Tags: instance.Tags);
        var first = Target(1); var second = Target(2);
        var beast = Target(3) with { Tags = new TagSet([GameTags.Beast]) };
        var bench = Target(4) with { IsOnBench = true };
        var dead = Target(5) with { Abilities = [Opening(new DestroyCardEffectDefinition(false))] };
        var ready = Target(6, 90);
        var passive = Target(7) with { Abilities = [Opening(new ArmorEffectDefinition(0))] };
        var enemy = Target(0);
        var selected = new HashSet<EntityId>();
        for (ulong seed = 1; seed <= 12; seed++)
        {
            var setup = Battle([farmer, first, second, beast, bench, dead, ready, passive], [enemy], 90, seed: seed);
            var result = new CombatSimulator().Simulate(setup);
            var charge = result.Events.OfType<CardChargedEvent>().Single();
            if (charge.SourceCardId != farmer.EntityId || charge.TargetCardId != first.EntityId && charge.TargetCardId != second.EntityId
                || charge.AmountTicks != 10 || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            selected.Add(charge.TargetCardId);
        }
        if (selected.Count != 2) return false;
        var repeated = new CombatSimulator().Simulate(Battle([farmer with { Multicast = 1 }, first], [], 92));
        if (!repeated.Events.OfType<CardChargedEvent>().Select(e => e.Tick.Value).SequenceEqual(new long[] { 90, 92 })
            || repeated.Events.OfType<CardChargedEvent>().Any(e => e.TargetCardId != first.EntityId)
            || repeated.States[^1].Cards.Single(c => c.Id == farmer.EntityId).CooldownUnits.Single() != 176m
            || repeated.States[^1].Cards.Single(c => c.Id == first.EntityId).CooldownUnits.Single() != 1776m) return false;
        foreach (var inactive in new[] { farmer with { IsOnBench = true }, farmer with { Abilities = [Opening(new DestroyCardEffectDefinition(false))] } })
            if (new CombatSimulator().Simulate(Battle([inactive, first], [], 180)).Events.OfType<CardChargedEvent>().Any()) return false;
        if (new CombatSimulator().Simulate(Battle([farmer, beast, bench, dead, passive], [enemy], 90)).Events.OfType<CardChargedEvent>().Any()) return false;
        // 标签可复用且不改变未配置标签时的全卡筛选。
        foreach (var tag in new StringName?[] { GameTags.Beast, null })
        {
            var generic = farmer with { Abilities = [new AbilityDefinition("verification.farmer.generic", AbilityActivation.Active,
                AbilityTarget.SelfCard, 0, 90, [new ChargeRandomOtherAlliedCardsEffectDefinition(10, 1, tag)])] };
            if (new CombatSimulator().Simulate(Battle([generic, beast], [], 90)).Events.OfType<CardChargedEvent>().Single().TargetCardId != beast.EntityId)
                return false;
        }
        try
        {
            _ = new AbilityDefinition("verification.farmer.invalid_tag", AbilityActivation.Active, AbilityTarget.SelfCard,
                0, 90, [new ChargeRandomOtherAlliedCardsEffectDefinition(10, 1, new StringName(""))]);
            return false;
        }
        catch (ArgumentException) { return true; }
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(248, 448), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(248, 448) });
            var face = new CardItemView { Position = new Vector2(24, 24), Size = new Vector2(200, 400) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new FarmerCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/farmer/card-face.png") != Error.Ok) throw new InvalidOperationException("农夫卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardBattleSetup Target(int start, int cooldown = 1000) => new(EntityId.New(), start, 1, cooldown,
        Tags: new TagSet([GameTags.Human]));
    private static AbilityDefinition Opening(EffectDefinition effect) => new("verification.farmer.opening",
        AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [effect]);
    private static BattleSetup Battle(CardBattleSetup[] allies, CardBattleSetup[] enemies, int timeout, bool reverse = false, ulong seed = 42) =>
        new(new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), reverse ? enemies : allies),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), reverse ? allies : enemies), seed,
            new BattleTick(timeout), new BattleTick(timeout + 1));
}
