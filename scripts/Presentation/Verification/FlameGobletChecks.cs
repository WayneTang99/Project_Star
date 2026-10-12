using System.Linq;
using System;
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
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 火焰杯分级灼伤、元素发动筛选与战斗隔离验证（表现层）。
internal static class FlameGobletChecks
{
    internal static bool LevelsAndBurn()
    {
        var definition = new FlameGobletCardDefinition(); var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.Key != new StringName("card.flame_goblet") || identity.DisplayName != "火焰杯"
            || identity.FactionKey != new StringName("mona") || identity.Size != CardSize.Medium
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Fire }) || definition.InitialLevel != 2
            || definition.SupportsLevel(1) || definition.SupportsLevel(5) || definition.Tags.Count != 1
            || !definition.Tags.Contains(GameTags.Medium) || texture is null || texture.GetWidth() != texture.GetHeight()
            || !DefinitionRegistry.Scan(typeof(FlameGobletChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        foreach (var (level, burn, bonus) in new[] { (2, 6, 2), (3, 12, 4), (4, 24, 8) })
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        {
            var cup = Cup(level); var setup = Battle([cup], [], 120, side);
            var result = new CombatSimulator().Simulate(setup);
            var burns = result.Events.OfType<StatusChangedEvent>().Where(item => item.Status == BattleStatus.Burn).ToArray();
            var growth = result.Events.OfType<CardAttributeChangedEvent>().ToArray();
            if (cup.CooldownTicks != 60 || cup.Abilities!.Any(item => item.ManaCost != 0)
                || burns.Length != 2 || burns[0].Tick.Value != 60 || burns[0].Amount != burn
                || burns[1].Tick.Value != 120 || burns[1].Amount != burn + bonus
                || growth.Length != 2 || growth.Any(item => item.CardId != cup.EntityId
                    || item.AttributeKey != GameAttributeKeys.Burn || item.Amount != bonus)
                || Value(result, cup) != burn + 2 * bonus || cup.CombatValues[GameAttributeKeys.Burn] != burn
                || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
        }
        var repeatedCup = Cup(2) with { Multicast = 1 };
        var repeated = new CombatSimulator().Simulate(Battle([repeatedCup], [], 60));
        return Echoes(repeated, repeatedCup) == 2 && Value(repeated, repeatedCup) == 10;
    }

    internal static bool TriggersAndReset()
    {
        var cup = Cup(2); var fire = Support(GameElements.Fire);
        var grown = new CombatSimulator().Simulate(Battle([cup, fire], [], 60));
        if (Echoes(grown, cup) != 4 || Value(grown, cup) != 14
            || grown.Events.OfType<StatusChangedEvent>().Single(item => item.Status == BattleStatus.Burn).Amount != 10)
            return false;
        // 40 Tick短于自身冷却，保留灼伤能力以验证真实属性支持条件。
        var echoOnly = cup;
        foreach (var excluded in new[]
        {
            fire with { ElementKeys = [GameElements.Water] }, fire with { IsOnBench = true },
            fire with { Abilities = [Opening(new GainSourceHeroArmorEffectDefinition(1))] },
            fire with { Abilities = [Opening(new DestroyCardEffectDefinition(false))] },
            fire with { Abilities = [new AbilityDefinition("verification.goblet.unpaid", AbilityActivation.Active,
                AbilityTarget.SelfCard, 1, 20, [new GainSourceHeroArmorEffectDefinition(1)])] },
        })
            if (Echoes(new CombatSimulator().Simulate(Battle([echoOnly, excluded], [], 40)), cup) != 0) return false;
        if (Echoes(new CombatSimulator().Simulate(Battle([echoOnly], [fire], 40)), cup) != 0) return false;
        foreach (var inactive in new[] { echoOnly with { IsOnBench = true }, echoOnly with
            { Abilities = echoOnly.Abilities!.Append(Opening(new DestroyCardEffectDefinition(false))).ToArray() } })
            if (Echoes(new CombatSimulator().Simulate(Battle([inactive, fire], [], 40)), cup) != 0) return false;
        var echoing = fire with { Multicast = 1, Abilities = fire.Abilities!.Append(new AbilityDefinition(
            "verification.goblet.echo", AbilityActivation.EchoOnSourceCardActivated, AbilityTarget.SelfCard, 0, 0,
            [new GainSourceHeroArmorEffectDefinition(1)])).ToArray() };
        var result = new CombatSimulator().Simulate(Battle([echoOnly, echoing], [], 40));
        var reset = new CombatSimulator().Simulate(Battle([cup], [], 60));
        return Echoes(result, cup) == 4 && Value(result, cup) == 14
            && reset.Events.OfType<StatusChangedEvent>().Single(item => item.Status == BattleStatus.Burn).Amount == 6
            && cup.CombatValues[GameAttributeKeys.Burn] == 6 && !result.PermanentAttributeBonuses.Any();
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(360, 360), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(360, 360) });
            var face = new CardItemView { Position = new Vector2(24, 24), Size = new Vector2(312, 312) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new FlameGobletCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/flame-goblet/card-face.png") != Error.Ok)
                throw new InvalidOperationException("Flame goblet card capture failed.");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardBattleSetup Cup(int level)
    {
        var card = new EntityFactory().CreateCard(new FlameGobletCardDefinition(), level);
        return new CardBattleSetup(card.Id, 0, 0, 60, card.Abilities, UseLegacyAttack: false,
            Tags: card.Tags, OccupiedSlots: 2, ElementKeys: card.Attributes.Identity.ElementKeys)
        { Level = level, CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues() };
    }
    private static CardBattleSetup Support(StringName element) => new(EntityId.New(), 2, 0, 20,
        [new AbilityDefinition("verification.goblet.support", AbilityActivation.Active, AbilityTarget.SelfCard,
            0, 20, [new GainSourceHeroArmorEffectDefinition(1)])], UseLegacyAttack: false, ElementKeys: [element]);
    private static AbilityDefinition Opening(EffectDefinition effect) => new("verification.goblet.opening",
        AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [effect]);
    private static int Echoes(BattleResult result, CardBattleSetup card) => result.Events.OfType<AbilityActivatedEvent>()
        .Count(item => item.SourceCardId == card.EntityId && item.IsEcho);
    private static int Value(BattleResult result, CardBattleSetup card) => result.States.Last(frame => frame.Player.Health > 0
        && frame.Opponent.Health > 0).Cards.Single(item => item.Id == card.EntityId).Values[GameAttributeKeys.Burn];
    private static BattleSetup Battle(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks, SideId side = SideId.Player) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), side == SideId.Player ? allies : enemies),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), side == SideId.Player ? enemies : allies),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
