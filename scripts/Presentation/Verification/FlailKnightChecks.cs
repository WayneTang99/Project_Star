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

// 连枷骑士分级伤害、多重共用冷却与卡面验证（表现层）。
internal static class FlailKnightChecks
{
    internal static bool LevelsAndMulticast()
    {
        var definition = new FlailKnightCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.Key != new StringName("card.flail_knight") || identity.DisplayName != "连枷骑士"
            || identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Medium
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Human)
            || definition.InitialLevel != 2 || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(FlailKnightCardDefinition).Assembly).Cards.ContainsKey(identity.Key)) return false;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (texture is null || texture.GetWidth() != texture.GetHeight() || texture.GetWidth() < 512) return false;
        foreach (var level in new[] { 2, 3, 4 })
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        foreach (var bonus in new[] { 0, 17 })
        {
            var card = Knight(level);
            var damage = 40 << (level - 2);
            var boosted = card with { AttackDamage = damage + bonus };
            var setup = Battle(boosted, side);
            var result = new CombatSimulator().Simulate(setup);
            var hits = Hits(result, card);
            if (card.AttackDamage != damage || card.CooldownTicks != 70 || card.Multicast != 1
                || card.Abilities!.Single().ManaCost != 0
                || !hits.Select(hit => hit.Tick.Value).SequenceEqual(new long[] { 70, 72, 140, 142 })
                || hits.Any(hit => hit.RawDamage != damage + bonus || hit.TargetSide == side)
                || hits[0].ArmorAbsorbed != 1 || hits[0].HealthDamage != damage + bonus - 1
                || result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == card.EntityId && !e.IsEcho) != 4
                || result.Events.OfType<ManaChangedEvent>().Any() || result.PermanentChanges.Any()
                || State(result, card, 70).CooldownUnits.Single() != 140
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            var face = new CardDisplayAdapter().Build(snapshot);
            if (face.Multicast != 1 || !CardDisplayAdapter.FaceEffects(snapshot).Any(effect =>
                    effect.Kind == CardFaceEffectKind.Damage && effect.Value == damage.ToString())) return false;
        }
        return true;
    }

    internal static bool RuntimeIsolation()
    {
        var card = Knight(2);
        if (Hits(new CombatSimulator().Simulate(Battle(card with { IsOnBench = true })), card).Any()) return false;
        var destroyed = card with { Abilities = card.Abilities!.Append(new AbilityDefinition("verification.flail.destroy",
            AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])).ToArray() };
        if (Hits(new CombatSimulator().Simulate(Battle(destroyed)), card).Any()) return false;
        var berserk = card with { Abilities = card.Abilities!.Append(new AbilityDefinition("verification.flail.berserk",
            AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0,
            [new SetSourceCardStateEffectDefinition(GameAttributeKeys.Berserk, true)])).ToArray() };
        if (Hits(new CombatSimulator().Simulate(Battle(berserk)), card).Any(hit => hit.RawDamage != 48)) return false;
        var result = new CombatSimulator().Simulate(Battle(card with { Multicast = 3 }));
        return Hits(result, card).Length == 8 && Hits(new CombatSimulator().Simulate(Battle(card)), card).Length == 4
            && State(result, card, 70).CooldownUnits.Single() == 140;
    }

    internal static async Task Capture(Node owner)
    {
        if (!LevelsAndMulticast() || !RuntimeIsolation()) throw new InvalidOperationException("Flail knight checks failed");
        var viewport = new SubViewport { Size = new Vector2I(360, 300), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(360, 300) });
            var face = new CardItemView { Position = new Vector2(62, 22), Size = new Vector2(236, 256) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new FlailKnightCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/flail-knight/card-face.png") != Error.Ok)
                throw new InvalidOperationException("Flail knight capture failed");
            GD.Print("连枷骑士聚焦验证与卡面捕获：通过");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardBattleSetup Knight(int level)
    {
        var instance = new EntityFactory().CreateCard(new FlailKnightCardDefinition(), level);
        return new CardBattleSetup(instance.Id, 0, instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
            instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks), instance.Abilities,
            UseLegacyAttack: false, Tags: instance.Tags, Multicast: instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Multicast),
            OccupiedSlots: 2, ElementKeys: instance.Attributes.Identity.ElementKeys) { Level = level };
    }
    private static BattleSetup Battle(CardBattleSetup card, SideId side = SideId.Player) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 1, ManaRegen: 0), side == SideId.Player ? [card] : []),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 1, ManaRegen: 0), side == SideId.Opponent ? [card] : []),
        42, new BattleTick(140 + card.Multicast * 2), new BattleTick(141 + card.Multicast * 2));
    private static DamageDealtEvent[] Hits(BattleResult result, CardBattleSetup card) =>
        result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == card.EntityId).ToArray();
    private static CardBattleSnapshot State(BattleResult result, CardBattleSetup card, long tick) =>
        result.States.Last(frame => frame.Tick.Value == tick).Cards.Single(snapshot => snapshot.Id == card.EntityId);
}
