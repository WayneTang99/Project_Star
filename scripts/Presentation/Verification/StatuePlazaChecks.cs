using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Economy;
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

// 神像广场条件多重、治疗、动态失效与原画验证（表现层）。
internal static class StatuePlazaChecks
{
    internal static bool ConditionsAndHealing()
    {
        var definition = new StatuePlazaCardDefinition(); var identity = definition.Attributes.Identity;
        using var image = GD.Load<Texture2D>(identity.Illustration.ToString()).GetImage();
        if (identity.DisplayName != "神像广场" || identity.FactionKey != new StringName("paladin")
            || identity.Size != CardSize.Large || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Light })
            || definition.InitialLevel != 3 || definition.SupportsLevel(2) || definition.SupportsLevel(5)
            || definition.Tags.Count != 3 || !definition.Tags.Contains(GameTags.Location) || !definition.Tags.Contains(GameTags.Building)
            || image.GetWidth() * 2 != image.GetHeight() * 3 || !image.HasMipmaps()
            || !DefinitionRegistry.Scan(typeof(StatuePlazaChecks).Assembly).Cards.ContainsKey(identity.Key)) return Fail();
        foreach (var level in new[] { 3, 4 })
        foreach (var reverse in new[] { false, true })
        foreach (var light in new[] { false, true })
        foreach (var human in new[] { false, true })
        foreach (var equipment in new[] { false, true })
        {
            var card = new EntityFactory().CreateCard(definition, level);
            var values = card.Attributes.BaseCombat.SnapshotFinalValues();
            var source = new CardBattleSetup(card.Id, 0, 0, 70, card.Abilities, UseLegacyAttack: false,
                Tags: card.Tags, OccupiedSlots: 3, ElementKeys: light ? [GameElements.Light] : [GameElements.General])
                { Identity = identity, Level = level, CombatValues = values };
            if (card.Abilities.Single(a => a.Activation == AbilityActivation.Active).ManaCost != 0) return Fail();
            var tags = (human ? new[] { GameTags.Human } : Array.Empty<StringName>())
                .Concat(equipment ? new[] { GameTags.Equipment } : Array.Empty<StringName>()).ToArray();
            var peer = Peer(3, tags);
            var second = Peer(4, tags);
            var irrelevantEnemy = Peer(1, [GameTags.Human, GameTags.Equipment], [GameElements.Light]);
            var bench = Peer(0, [GameTags.Human, GameTags.Equipment], [GameElements.Light]) with { IsOnBench = true };
            var dead = Peer(5, [GameTags.Human, GameTags.Equipment], [GameElements.Light]) with { Abilities = [Destroy(0)] };
            var setup = Battle([source, peer, second, bench, dead], [Attacker(), irrelevantEnemy], reverse, 80);
            var result = new CombatSimulator().Simulate(setup);
            var count = (light ? 1 : 0) + (human ? 1 : 0) + (equipment ? 1 : 0);
            var activations = result.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId == card.Id && !e.IsEcho).ToArray();
            var state = result.States.Last(s => s.Tick.Value == 79);
            var hero = reverse ? state.Opponent : state.Player;
            var healing = level == 3 ? 200 : 400;
            if (!activations.Select(e => e.Tick.Value).SequenceEqual(Enumerable.Range(0, count + 1).Select(i => 70L + i * 2))
                || hero.Health != 5000 + healing * (count + 1)
                || state.Cards.Single(c => c.Id == card.Id).Values[GameAttributeKeys.Multicast] != count
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any())
            {
                GD.Print($"广场组合 level={level},reverse={reverse},light={light},human={human},equipment={equipment},expected={count},ticks={string.Join(',', activations.Select(e => e.Tick.Value))},health={hero.Health},multicast={state.Cards.Single(c => c.Id == card.Id).Values[GameAttributeKeys.Multicast]}");
                return Fail();
            }
            if (light && human && equipment)
            {
                var supporter = peer with { Abilities = [Destroy(71)] };
                var removal = new CombatSimulator().Simulate(Battle([source, supporter], [Attacker()], reverse, 145));
                var firing = removal.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId == card.Id && !e.IsEcho).ToArray();
                if (!firing.Select(e => e.Tick.Value).SequenceEqual(new long[] { 70, 72, 74, 76, 140, 142 })
                    || removal.States.Last(s => s.Tick.Value == 71).Cards.Single(c => c.Id == card.Id).Values[GameAttributeKeys.Multicast] != 1)
                    return Fail();
            }
            var inactive = new CombatSimulator().Simulate(Battle([source with { IsOnBench = true }, peer], [Attacker()], reverse, 80));
            if (inactive.Events.OfType<AbilityActivatedEvent>().Any(e => e.SourceCardId == card.Id)) return Fail();
        }
        return true;
    }

    private static CardBattleSetup Peer(int start, StringName[] tags, StringName[]? elements = null) =>
        new(EntityId.New(), start, 0, 0, [], UseLegacyAttack: false, Tags: new TagSet(tags), ElementKeys: elements ?? [GameElements.General]);

    private static CardBattleSetup Attacker() => new(EntityId.New(), 0, 0, 0,
        [new AbilityDefinition(new StringName("ability.verification_opening_damage"), AbilityActivation.PassiveOnBattleStart,
            AbilityTarget.EnemyHero, 0, 0, [new DamageEffectDefinition(5000)])], UseLegacyAttack: false);

    private static AbilityDefinition Destroy(int cooldown) => new(new StringName("ability.verification_destroy"),
        cooldown == 0 ? AbilityActivation.PassiveOnBattleStart : AbilityActivation.Active,
        AbilityTarget.SelfCard, 0, cooldown, [new DestroyCardEffectDefinition(false)]);

    private static BattleSetup Battle(CardBattleSetup[] ownCards, CardBattleSetup[] otherCards, bool reverse, int ticks)
    {
        var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), ownCards);
        var other = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), otherCards);
        return new BattleSetup(reverse ? other : own, reverse ? own : other, 42, new BattleTick(ticks), new BattleTick(ticks + 1));
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(1100, 440), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(1100, 440) });
            using var artwork = GD.Load<Texture2D>(new StatuePlazaCardDefinition().Attributes.Identity.Illustration.ToString()).GetImage();
            GD.Print($"神像广场原画 {artwork.GetWidth()}×{artwork.GetHeight()} mipmaps={artwork.GetMipmapCount()}");
            foreach (var (level, x, y, width) in new[] { (3, 20, 20, 600), (4, 650, 20, 300), (3, 650, 260, 180) })
            {
                var face = new CardItemView { Position = new Vector2(x, y), Size = new Vector2(width, width * 2 / 3) };
                viewport.AddChild(face);
                face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new StatuePlazaCardDefinition(), level)), new CardDisplayAdapter());
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw(false);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/statue-plaza/card-face.png") != Error.Ok) throw new InvalidOperationException("神像广场截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static bool Fail([System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    { GD.PushError("神像广场验证失败，行 " + line); return false; }
}
