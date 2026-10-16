using System;
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

// 弓骑兵身份、分级伤害与相邻载具疾速验证（表现层）。
internal static class MountedArcherChecks
{
    internal static bool LevelsAndTargets()
    {
        var definition = new MountedArcherCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.Key != new StringName("card.mounted_archer") || identity.DisplayName != "弓骑兵"
            || identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Medium
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Human)
            || definition.InitialLevel != 2 || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(MountedArcherCardDefinition).Assembly).Cards.ContainsKey(identity.Key)) return false;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (texture is null || texture.GetWidth() != texture.GetHeight()) return false;
        foreach (var level in new[] { 2, 3, 4 })
        foreach (var opposite in new[] { false, true })
        {
            var archer = Archer(level);
            var left = Fixture(0, GameTags.Vehicle);
            var right = Fixture(3, GameTags.Vehicle);
            var distant = Fixture(5, GameTags.Vehicle);
            var bench = Fixture(3, GameTags.Vehicle) with { IsOnBench = true };
            var enemy = Fixture(3, GameTags.Vehicle);
            var result = Run([left, archer, right, distant, bench], [enemy], 50, opposite);
            var hit = result.Events.OfType<DamageDealtEvent>().Single();
            if (hit.SourceCardId != archer.EntityId || hit.Tick.Value != 50 || hit.RawDamage != (80 << (level - 2))
                || hit.TargetSide != (opposite ? SideId.Player : SideId.Opponent)
                || Haste(result, left) != 10 || Haste(result, right) != 10 || Haste(result, archer) != 0
                || Haste(result, distant) != 0 || Haste(result, bench) != 0 || Haste(result, enemy) != 0
                || result.Events.OfType<ManaChangedEvent>().Any() || result.PermanentChanges.Any()) return false;
            var human = right with { Tags = new TagSet([GameTags.Human]) };
            if (Haste(Run([archer, human], [], 50, opposite), human) != 0) return false;
            if (Run([archer with { IsOnBench = true }, right], [], 50, opposite).Events
                .Any(e => e is DamageDealtEvent or StatusChangedEvent)) return false;
            var expiry = Run([archer, right], [], 60, opposite);
            if (Haste(expiry, right) != 0) return false;
        }
        return true;
    }

    internal static bool StackingAndCooldown()
    {
        var archer = Archer(2) with { Multicast = 1 };
        var mount = Fixture(3, GameTags.Vehicle);
        var result = Run([archer, mount], [], 52);
        if (Haste(result, mount) != 18 || result.Events.OfType<DamageDealtEvent>().Count() != 2) return false;
        var result2 = Run([archer, mount], [], 52);
        if (!result.Events.SequenceEqual(result2.Events)) return false;
        var activeMount = mount with { CooldownTicks = 60, Abilities = [new AbilityDefinition(
            "verification.mount.active", AbilityActivation.Active, AbilityTarget.EnemyHero, 0, 60,
            [new DamageEffectDefinition(1)])] };
        var accelerated = Run([Archer(2), activeMount], [], 65);
        return accelerated.Events.OfType<DamageDealtEvent>().Single(e => e.SourceCardId == activeMount.EntityId).Tick.Value == 55
            && Haste(accelerated, activeMount) == 0;
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(360, 330), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            var face = new CardItemView { Position = new Vector2(40, 25), Size = new Vector2(280, 280) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new MountedArcherCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/mounted-archer/card-face.png") != Error.Ok)
                throw new InvalidOperationException("Mounted archer capture failed");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardBattleSetup Archer(int level)
    {
        var card = new EntityFactory().CreateCard(new MountedArcherCardDefinition(), level);
        return new(card.Id, 1, card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
            50, card.Abilities, UseLegacyAttack: false, Tags: card.Tags, OccupiedSlots: 2,
            ElementKeys: card.Attributes.Identity.ElementKeys) { Level = level };
    }
    private static CardBattleSetup Fixture(int start, StringName tag) =>
        new(EntityId.New(), start, 0, 0, [], UseLegacyAttack: false, Tags: new TagSet([tag]));
    private static int Haste(BattleResult result, CardBattleSetup card) =>
        result.States.Last().Cards.Single(c => c.Id == card.EntityId).Haste;
    private static BattleResult Run(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks, bool opposite = false) =>
        new CombatSimulator().Simulate(new BattleSetup(
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), opposite ? enemies : cards),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), opposite ? cards : enemies),
            42, new BattleTick(ticks), new BattleTick(ticks + 1)));
}
