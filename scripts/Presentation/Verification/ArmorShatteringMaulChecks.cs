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

// 破碎之星护甲消耗、伤害倍率与冻结回放验证（表现层）。
internal static class ArmorShatteringMaulChecks
{
    internal static bool ConsumptionAndDamage()
    {
        var definition = new ArmorShatteringMaulCardDefinition(); var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (definition.InitialLevel != 4 || definition.SupportsLevel(3) || definition.SupportsLevel(5)
            || identity.DisplayName != "破碎之星" || identity.FactionKey != new StringName("paladin")
            || identity.Size != CardSize.Large || !definition.Tags.Contains(GameTags.Equipment)
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Light })
            || texture is null || texture.GetWidth() * 2 != texture.GetHeight() * 3
            || !DefinitionRegistry.Scan(typeof(ArmorShatteringMaulChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        foreach (var reverse in new[] { false, true })
        foreach (var armor in new[] { 0, 17, 200 })
        {
            var maul = Maul(); var setup = Battle([maul], [], armor, 13, 120, reverse);
            var result = new CombatSimulator().Simulate(setup);
            var hits = result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == maul.EntityId).ToArray();
            if (hits.Length != 2 || !hits.Select(hit => hit.Tick.Value).SequenceEqual(new long[] { 60, 120 })
                || hits[0].RawDamage != armor * 5 || hits[1].RawDamage != 0
                || hits[0].ArmorAbsorbed != Math.Min(13, armor * 5)
                || hits[0].HealthDamage != Math.Max(0, armor * 5 - 13)
                || OwnArmor(result, reverse) != 0 || result.States[0].Player.Armor != (reverse ? 13 : armor)
                || result.PermanentAttributeBonuses.Count != 0 || result.PermanentChanges.Count != 0
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            var damageState = result.States.First(state => state.EventCount > Array.IndexOf(result.Events.ToArray(), hits[0]));
            if ((reverse ? damageState.Opponent : damageState.Player).Armor != 0) return false;
        }
        var display = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition));
        return CardDisplayAdapter.Details(display).Contains("失去") && CardDisplayAdapter.Details(display).Contains("×5")
            && CardDisplayAdapter.FaceEffects(display).Any(effect => effect.Kind == CardFaceEffectKind.Damage && effect.Value == "0");
    }

    internal static bool OrderingAndBoundaries()
    {
        var maul = Maul();
        var gem = new ApplyPrimaryEffectPercentStatusEffectDefinition(BattleStatus.Poison, 20);
        maul = maul with { Abilities = [new AbilityDefinition(new StringName("verification.maul.gem"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 60, maul.Abilities!.Single().Effects.Append(gem).ToArray())], Multicast = 1 };
        var result = new CombatSimulator().Simulate(Battle([maul], [], 17, 0, 62));
        if (!result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == maul.EntityId)
                .Select(hit => hit.RawDamage).SequenceEqual(new[] { 85, 0 })
            || !result.Events.OfType<StatusChangedEvent>().Where(item => item.Status == BattleStatus.Poison)
                .Select(item => item.Amount).SequenceEqual(new[] { 17, 0 })
            || OwnArmor(result) != 0) return false;
        var berserk = Maul(); berserk = berserk with { Abilities = berserk.Abilities!.Append(new AbilityDefinition(
            new StringName("verification.maul.berserk"), AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0,
            [new SetSourceCardStateEffectDefinition(GameAttributeKeys.Berserk, true)])).ToArray() };
        result = new CombatSimulator().Simulate(Battle([berserk], [], 17, 0, 60));
        if (result.Events.OfType<DamageDealtEvent>().Single().RawDamage != 102 || OwnArmor(result) != 0) return false;
        var plain = Maul();
        foreach (var inactive in new[] { plain with { IsOnBench = true }, plain with { Abilities = plain.Abilities!.Append(
            new AbilityDefinition(new StringName("verification.maul.destroy"), AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])).ToArray() } })
        {
            result = new CombatSimulator().Simulate(Battle([inactive], [], 17, 0, 60));
            if (OwnArmor(result) != 17 || result.Events.OfType<DamageDealtEvent>().Any()) return false;
        }
        var costly = plain with { Abilities = [new AbilityDefinition(new StringName("verification.maul.mana"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 1, 60, plain.Abilities!.Single().Effects)] };
        if (OwnArmor(new CombatSimulator().Simulate(Battle([costly], [], 17, 0, 60))) != 17) return false;
        var armorSource = new CardBattleSetup(EntityId.New(), 0, 0, 60,
            [new AbilityDefinition(new StringName("verification.maul.gain_armor"), AbilityActivation.Active,
                AbilityTarget.AlliedHero, 0, 60, [new GainSourceHeroArmorEffectDefinition(7)])], UseLegacyAttack: false);
        var lateMaul = Maul() with { BoardStart = 1 };
        result = new CombatSimulator().Simulate(Battle([armorSource, lateMaul], [], 17, 0, 120));
        if (!result.Events.OfType<DamageDealtEvent>().Select(hit => hit.RawDamage).SequenceEqual(new[] { 120, 35 })
            || OwnArmor(result) != 0) return false;
        var twin = Maul() with { BoardStart = 3 };
        result = new CombatSimulator().Simulate(Battle([plain, twin], [], 17, 0, 60));
        if (!result.Events.OfType<DamageDealtEvent>().Select(hit => hit.RawDamage).SequenceEqual(new[] { 85, 0 })) return false;
        var stealth = new CardBattleSetup(EntityId.New(), 0, 0, 0,
            [new AbilityDefinition(new StringName("verification.maul.stealth"), AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.AlliedHero, 0, 0, [new GainSourceHeroStealthEffectDefinition(100)])], UseLegacyAttack: false);
        result = new CombatSimulator().Simulate(Battle([plain], [stealth], 17, 0, 60));
        if (OwnArmor(result) != 0 || !result.Events.OfType<AttackBlockedEvent>().Any()
            || result.Events.OfType<DamageDealtEvent>().Any()) return false;
        foreach (var multiplier in new[] { 0, -1 })
        {
            try { _ = new AbilityDefinition(new StringName("verification.maul.invalid"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 60, [new SourceHeroArmorDamageEffectDefinition(Multiplier: multiplier)]); return false; }
            catch (ArgumentException) { }
        }
        return true;
    }

    internal static async Task Capture(Control owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(648, 448), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(648, 448) });
            var face = new CardItemView { Position = new Vector2(24, 24), Size = new Vector2(600, 400) }; viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new ArmorShatteringMaulCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/armor-shattering-maul/card-face.png") != Error.Ok)
                throw new InvalidOperationException("破碎之星卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }
    private static CardBattleSetup Maul()
    {
        var card = new EntityFactory().CreateCard(new ArmorShatteringMaulCardDefinition());
        return new(card.Id, 0, 0, 60, card.Abilities, UseLegacyAttack: false, Tags: card.Tags, OccupiedSlots: 3,
            ElementKeys: card.Attributes.Identity.ElementKeys) { Level = 4 };
    }
    private static int OwnArmor(BattleResult result, bool reverse = false) => (reverse ? result.States.Last().Opponent : result.States.Last().Player).Armor;
    private static BattleSetup Battle(CardBattleSetup[] own, CardBattleSetup[] other, int armor, int enemyArmor, int ticks, bool reverse = false) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, reverse ? enemyArmor : armor, MaxMana: 0, ManaRegen: 0), reverse ? other : own),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, reverse ? armor : enemyArmor, MaxMana: 0, ManaRegen: 0), reverse ? own : other),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
