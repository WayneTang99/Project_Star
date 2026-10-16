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
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 战舰舵手分级冷却、载具筛选、疾速累计与实际加速验证（表现层）。
internal static class WarshipHelmsmanChecks
{
    internal static bool LevelsAndHaste()
    {
        var definition = new WarshipHelmsmanCardDefinition(); var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.Key != new StringName("card.warship_helmsman") || identity.DisplayName != "战舰舵手"
            || identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Water }) || definition.InitialLevel != 1
            || definition.SupportsLevel(5) || !definition.Tags.ToArray().ToHashSet().SetEquals(new[] { GameTags.Small, GameTags.Human })
            || !DefinitionRegistry.Scan(typeof(WarshipHelmsmanChecks).Assembly).Cards.ContainsKey(identity.Key)
            || texture is null || texture.GetHeight() != texture.GetWidth() * 2) return false;
        for (var level = 1; level <= 4; level++)
        foreach (var reverse in new[] { false, true })
        {
            var source = Helmsman(level); var cooldown = (7 - level) * 10;
            var target = Vehicle(1) with { CooldownTicks = cooldown + 10,
                Abilities = [new AbilityDefinition(new StringName("verification.vehicle_attack"), AbilityActivation.Active,
                    AbilityTarget.EnemyHero, 0, cooldown + 10, [new DamageEffectDefinition(1)])] };
            var setup = Battle([source, target], [], cooldown + 10, reverse);
            var result = new CombatSimulator().Simulate(setup);
            var applied = result.Events.OfType<StatusChangedEvent>().Single();
            var hit = result.Events.OfType<DamageDealtEvent>().Single();
            if (source.Abilities!.Single().CooldownTicks != cooldown || source.Abilities!.Single().ManaCost != 0
                || source.CombatValues[GameAttributeKeys.CooldownTicks] != cooldown
                || applied.Status != BattleStatus.HasteDuration || applied.Amount != 10 || applied.Tick.Value != cooldown
                || hit.SourceCardId != target.EntityId || hit.Tick.Value != cooldown + 5
                || result.States[^1].Cards.Single(c => c.Id == target.EntityId).Haste != 0
                || result.States.Last(s => s.Tick.Value == cooldown).Cards.Single(c => c.Id == target.EntityId).Haste != 10
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            if (CardDisplayAdapter.CooldownSeconds(snapshot) != cooldown / 10m
                || !CardDisplayAdapter.Details(snapshot).Contains("战场载具卡牌")
                || !CardDisplayAdapter.AbilityDetails(snapshot.Abilities).Contains("疾速 1秒")) return false;
        }
        return true;
    }

    internal static bool FiltersAndStacking()
    {
        var source = Helmsman(1); var first = Vehicle(1); var second = Vehicle(2);
        var nonVehicle = Vehicle(3) with { Tags = new TagSet([GameTags.Mechanical]) };
        var bench = Vehicle(4) with { IsOnBench = true };
        var destroyed = Vehicle(5) with { Abilities = [Opening(new DestroyCardEffectDefinition(false))] };
        var enemy = Vehicle(0); var chosen = new HashSet<EntityId>();
        for (ulong seed = 1; seed <= 12; seed++)
        foreach (var reverse in new[] { false, true })
        {
            var setup = Battle([source, first, second, nonVehicle, bench, destroyed], [enemy], 60, reverse, seed);
            var result = new CombatSimulator().Simulate(setup);
            var selected = result.States[^1].Cards.Where(c => c.Haste > 0).ToArray();
            if (selected.Length != 1 || selected[0].Id != first.EntityId && selected[0].Id != second.EntityId
                || selected[0].Haste != 10 || result.Events.OfType<StatusChangedEvent>().Count() != 1
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            chosen.Add(selected[0].Id);
        }
        if (chosen.Count != 2) return false;
        var repeated = source with { Multicast = 2,
            CombatValues = new Dictionary<StringName, int> { [GameAttributeKeys.HasteDurationBonus] = 5 } };
        var multi = new CombatSimulator().Simulate(Battle([repeated, first], [], 64));
        if (!multi.Events.OfType<StatusChangedEvent>().Select(e => e.Tick.Value).SequenceEqual(new long[] { 60, 62, 64 })
            || multi.Events.OfType<StatusChangedEvent>().Any(e => e.Amount != 15)
            || multi.States[^1].Cards.Single(c => c.Id == first.EntityId).Haste != 41) return false;
        var other = Helmsman(1) with { BoardStart = 1 };
        var stacked = new CombatSimulator().Simulate(Battle([source, other, first with { BoardStart = 2 }], [], 60));
        if (stacked.States[^1].Cards.Single(c => c.Id == first.EntityId).Haste != 20) return false;
        var empty = new CombatSimulator().Simulate(Battle([source, nonVehicle, bench, destroyed], [enemy], 60));
        if (empty.Events.OfType<StatusChangedEvent>().Any()
            || empty.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == source.EntityId && !e.IsEcho) != 1) return false;
        foreach (var inactive in new[] { source with { IsOnBench = true }, source with { Abilities = [Opening(new DestroyCardEffectDefinition(false))] } })
            if (new CombatSimulator().Simulate(Battle([inactive, first], [], 60)).Events.OfType<StatusChangedEvent>().Any()) return false;
        try
        {
            _ = new AbilityDefinition(new StringName("verification.empty_tag"), AbilityActivation.Active,
                AbilityTarget.SelfCard, 0, 60,
                [new ApplyStatusToRandomAlliedCardEffectDefinition(BattleStatus.HasteDuration, 10, new StringName(""))]);
            return false;
        }
        catch (ArgumentException) { return true; }
    }

    private static CardBattleSetup Helmsman(int level)
    {
        var card = new EntityFactory().CreateCard(new WarshipHelmsmanCardDefinition(), level);
        return new CardBattleSetup(card.Id, 0, 0, (7 - level) * 10, card.Abilities,
            UseLegacyAttack: false, Tags: card.Tags, ElementKeys: card.Attributes.Identity.ElementKeys)
            { Level = level, CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues() };
    }

    private static CardBattleSetup Vehicle(int position) => new(EntityId.New(), position, 0, 1000, [],
        UseLegacyAttack: false, Tags: new TagSet([GameTags.Vehicle]));

    private static AbilityDefinition Opening(params EffectDefinition[] effects) => new(new StringName("verification.opening"),
        AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, effects);

    private static BattleSetup Battle(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks,
        bool reverse = false, ulong seed = 42)
    {
        var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), allies);
        var other = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), enemies);
        return new BattleSetup(reverse ? other : own, reverse ? own : other, seed, new BattleTick(ticks), new BattleTick(ticks + 1));
    }

    internal static async Task Capture(Node owner)
    {
        if (!LevelsAndHaste() || !FiltersAndStacking()) throw new InvalidOperationException("战舰舵手验证失败。");
        var viewport = new SubViewport { Size = new Vector2I(248, 448), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(248, 448) });
            var face = new CardItemView { Position = new Vector2(24, 24), Size = new Vector2(200, 400) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new WarshipHelmsmanCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/warship-helmsman/card-face.png") != Error.Ok)
                throw new InvalidOperationException("战舰舵手卡面截图保存失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }
}
