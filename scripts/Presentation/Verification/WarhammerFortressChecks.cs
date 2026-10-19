using System.Linq;
using System;
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

// 战锤要塞分级伤害与动态装备计数冷却光环验证（表现层）。
internal static class WarhammerFortressChecks
{
    internal static bool DamageAndCooldown()
    {
        var definition = new WarhammerFortressCardDefinition();
        var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.DisplayName != "战锤要塞" || identity.FactionKey != new StringName("paladin")
            || identity.Size != CardSize.Large || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.InitialLevel != 3 || definition.SupportsLevel(2) || definition.SupportsLevel(5)
            || definition.Tags.Count != 3 || !definition.Tags.Contains(GameTags.Location)
            || texture is null || texture.GetWidth() * 2 != texture.GetHeight() * 3
            || !DefinitionRegistry.Scan(typeof(WarhammerFortressChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        foreach (var (level, damage) in new[] { (3, 300), (4, 500) })
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        {
            var card = new EntityFactory().CreateCard(definition, level);
            var values = card.Attributes.BaseCombat.SnapshotFinalValues();
            if (values[GameAttributeKeys.AttackDamage] != damage || values[GameAttributeKeys.CooldownTicks] != 120) return false;
            var source = new CardBattleSetup(card.Id, 0, damage, 120, card.Abilities,
                UseLegacyAttack: false, Tags: card.Tags, OccupiedSlots: 3) { CombatValues = values, Level = level };
            var equipment = Card(3, GameTags.Equipment);
            var second = Card(4, GameTags.Equipment);
            var bench = Card(0, GameTags.Equipment) with { IsOnBench = true };
            var enemy = Card(0, GameTags.Equipment);
            var setup = Battle([source, equipment, second, bench, Card(5, GameTags.Human)], [enemy], 200, side);
            var result = new CombatSimulator().Simulate(setup);
            var hits = result.Events.OfType<DamageDealtEvent>().Where(item => item.SourceCardId == card.Id).ToArray();
            if (hits.Length != 2 || hits.Any(item => item.RawDamage != damage)
                || !hits.Select(item => item.Tick.Value).SequenceEqual(new long[] { 100, 200 })
                || result.Events.OfType<ManaChangedEvent>().Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            var empty = new CombatSimulator().Simulate(Battle([source, bench], [enemy], 120, side));
            if (empty.Events.OfType<DamageDealtEvent>().Single(item => item.SourceCardId == card.Id).Tick.Value != 120) return false;
            var scaled = new CombatSimulator().Simulate(Battle([source with { CooldownMultiplier = .5m }, equipment], [enemy], 55, side));
            if (scaled.Events.OfType<DamageDealtEvent>().Single(item => item.SourceCardId == card.Id).Tick.Value != 55) return false;
            var destroy = new AbilityDefinition(new StringName("ability.verification_destroy_equipment"), AbilityActivation.Active,
                AbilityTarget.SelfCard, 0, 20, [new DestroyCardEffectDefinition(false)]);
            var dynamic = new CombatSimulator().Simulate(Battle([source, equipment with { Abilities = [destroy] }], [enemy], 120, side));
            var frame = dynamic.States.Last(item => item.Tick.Value == 20).Cards.Single(item => item.Id == card.Id);
            if (frame.CooldownUnits.Single() != 200 || frame.CooldownDurationUnits.Single() != 240
                || dynamic.Events.OfType<DamageDealtEvent>().Single(item => item.SourceCardId == card.Id).Tick.Value != 120) return false;
            var disabled = new CombatSimulator().Simulate(Battle([source with { IsOnBench = true }, equipment], [], 120, side));
            if (disabled.Events.OfType<DamageDealtEvent>().Any()) return false;
            if (card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != 120) return false;
        }
        return true;
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(648, 448), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(648, 448) });
            var face = new CardItemView { Position = new Vector2(24, 24), Size = new Vector2(600, 400) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new WarhammerFortressCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/warhammer-fortress/card-face.png") != Error.Ok)
                throw new InvalidOperationException("Warhammer fortress card capture failed.");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardBattleSetup Card(int position, StringName tag) => new(EntityId.New(), position, 0, 0, [],
        UseLegacyAttack: false, Tags: new TagSet([tag]));

    private static BattleSetup Battle(CardBattleSetup[] allies, CardBattleSetup[] enemies, int timeout, SideId side)
    {
        var friendly = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), allies);
        var hostile = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), enemies);
        return new BattleSetup(side == SideId.Player ? friendly : hostile, side == SideId.Player ? hostile : friendly,
            42, new BattleTick(timeout), new BattleTick(timeout + 1));
    }
}
