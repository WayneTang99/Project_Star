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
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

internal static class LionKingShieldChecks
{
    internal static bool LevelsArmorAndDamage()
    {
        var definition = new LionKingShieldCardDefinition(); var identity = definition.Attributes.Identity;
        if (identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Medium
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General }) || !definition.Tags.Contains(GameTags.Equipment)
            || definition.InitialLevel != 3 || definition.SupportsLevel(2) || definition.SupportsLevel(5)) return false;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (texture is null || texture.GetWidth() != texture.GetHeight()) return false;
        foreach (var level in new[] { 3, 4 })
        foreach (var reverse in new[] { false, true })
        foreach (var bonus in new[] { 0, 17 })
        {
            var shield = Shield(level) with { ArmorAmount = (level == 3 ? 200 : 400) + bonus };
            var percent = level == 3 ? 50 : 80; var damage = shield.ArmorAmount * percent / 100;
            var setup = Setup([shield], 100, reverse); var simulator = new CombatSimulator(); var result = simulator.Simulate(setup);
            var hits = result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == shield.EntityId).ToArray();
            if (hits.Length != 2 || !hits.Select(hit => hit.Tick.Value).SequenceEqual(new long[] { 50, 100 })
                || hits.Any(hit => hit.RawDamage != damage)
                || (reverse ? result.States.Last().Opponent.Armor : result.States.Last().Player.Armor) != shield.ArmorAmount * 2
                || !result.Events.SequenceEqual(simulator.Simulate(setup).Events)) return false;
            var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            var effects = CardDisplayAdapter.FaceEffects(snapshot);
            if (!effects.Any(effect => effect.Kind == CardFaceEffectKind.Damage && effect.Value == ((level == 3 ? 200 : 400) * percent / 100).ToString())
                || !effects.Any(effect => effect.Kind == CardFaceEffectKind.Armor && effect.Value == (level == 3 ? "200" : "400"))) return false;
        }
        return true;
    }

    internal static bool AuraAndBerserk()
    {
        foreach (var level in new[] { 3, 4 })
        {
            var shield = Shield(level); var percent = level == 3 ? 50 : 80;
            var aura = new CardBattleSetup(EntityId.New(), 2, 0, 0,
                [new AbilityDefinition("verification.shield.armor_aura", AbilityActivation.PassiveAura, AbilityTarget.SelfCard,
                    0, 0, [new IncreaseAlliedCardAttributeAuraEffectDefinition(GameAttributeKeys.Armor, 17)])], UseLegacyAttack: false);
            var result = new CombatSimulator().Simulate(Setup([shield, aura], 50));
            if (result.Events.OfType<DamageDealtEvent>().Single().RawDamage != (shield.ArmorAmount + 17) * percent / 100
                || result.States.Last().Player.Armor != shield.ArmorAmount + 17) return false;
            var berserk = shield with { Abilities = shield.Abilities!.Append(new AbilityDefinition("verification.shield.berserk",
                AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0,
                [new SetSourceCardStateEffectDefinition(GameAttributeKeys.Berserk, true)])).ToArray(), Multicast = 1 };
            var enhanced = new CombatSimulator().Simulate(Setup([berserk], 52));
            if (enhanced.Events.OfType<DamageDealtEvent>().Count() != 2
                || enhanced.Events.OfType<DamageDealtEvent>().Any(hit => hit.RawDamage != shield.ArmorAmount * percent / 100 * 120 / 100)
                || enhanced.States.Last().Player.Armor != shield.ArmorAmount * 2) return false;
            foreach (var inactive in new[] { shield with { IsOnBench = true }, shield with
            {
                Abilities = shield.Abilities!.Append(new AbilityDefinition("verification.shield.destroy",
                    AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])).ToArray(),
            } })
                if (new CombatSimulator().Simulate(Setup([inactive], 100)).Events.OfType<DamageDealtEvent>().Any()) return false;
        }
        foreach (var percent in new[] { 0, 101 })
        {
            try { _ = new AbilityDefinition("verification.shield.invalid", AbilityActivation.Active, AbilityTarget.EnemyHero,
                0, 50, [new AttributeDamageEffectDefinition(GameAttributeKeys.Armor, Percent: percent)]); return false; }
            catch (ArgumentException) { }
        }
        return true;
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(340, 340), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            var face = new CardItemView { Position = new Vector2(20, 20), Size = new Vector2(300, 300) }; viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new LionKingShieldCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/lion-king-shield/card-face.png") != Error.Ok) throw new InvalidOperationException("Lion king shield capture failed.");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }
    private static CardBattleSetup Shield(int level)
    {
        var instance = new EntityFactory().CreateCard(new LionKingShieldCardDefinition(), level);
        return new(instance.Id, 0, 0, 50, instance.Abilities, UseLegacyAttack: false, Tags: instance.Tags, OccupiedSlots: 2,
            ArmorAmount: instance.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.Armor)) { Level = level };
    }
    private static BattleSetup Setup(CardBattleSetup[] cards, int ticks, bool reverse = false) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), reverse ? [] : cards),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), reverse ? cards : []),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
