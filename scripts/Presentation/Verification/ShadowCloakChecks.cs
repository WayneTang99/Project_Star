using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Combat;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 暗影斗篷半血阈值、英雄隐身伤害边界及冻结回放验证（表现层验证模块）。
internal static class ShadowCloakChecks
{
    internal static bool LevelsAndTiming()
    {
        var definition = new ShadowCloakCardDefinition();
        var identity = definition.Attributes.Identity;
        var registry = DefinitionRegistry.Scan(typeof(ShadowCloakCardDefinition).Assembly);
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (!registry.Cards.ContainsKey(identity.Key) || identity.DisplayName != "暗影斗篷"
            || identity.FactionKey != new StringName("valos") || identity.Size != CardSize.Medium
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Dark })
            || !definition.Tags.Contains(GameTags.Equipment) || definition.InitialLevel != 3
            || definition.GetLevel(2) is not null || texture is null || texture.GetWidth() != texture.GetHeight()) return false;
        if (MatchDisplayQuery.FromOffer(ShopOffer.Create(definition)).Abilities.Single().TriggerHeroHealthPercent != 50) return false;
        foreach (var level in new[] { 3, 4 })
        {
            var duration = level == 3 ? 10 : 20;
            var cloak = Cloak(level);
            var setup = Battle([cloak], [Attack()], 30 + duration + 5);
            var result = new CombatSimulator().Simulate(setup);
            var changes = result.Events.OfType<HeroStealthChangedEvent>().ToArray();
            if (changes.Length != 2 || changes[0].Tick.Value != 30 || changes[0].RemainingTicks != duration
                || changes[1].Tick.Value != 30 + duration || changes[1].RemainingTicks != 0
                || result.States.Last(state => state.Tick.Value == 25).Player.Health != 500
                || result.States.Last(state => state.Tick.Value == 30).Player.Health != 400
                || result.States.Last(state => state.Tick.Value == 30).Player.StealthDurationTicks != duration
                || !result.Events.OfType<DamageDealtEvent>().Any(hit => hit.TargetSide == SideId.Player && hit.Tick.Value == 30 + duration)
                || !result.Events.OfType<AttackBlockedEvent>().Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            var mirrored = new BattleSetup(setup.Opponent, setup.Player, 42, setup.Timeout, setup.EclipseTime);
            if (!new CombatSimulator().Simulate(mirrored).Events.OfType<HeroStealthChangedEvent>()
                .Where(change => change.Side == SideId.Opponent).Select(change => change.RemainingTicks).SequenceEqual(new[] { duration, 0 })) return false;
        }
        return true;
    }

    internal static bool StackingAndSourceFilters()
    {
        var first = Cloak(); var second = Cloak() with { BoardStart = 2 };
        var result = new CombatSimulator().Simulate(Battle([first, second], [Attack()], 55));
        if (!result.Events.OfType<HeroStealthChangedEvent>().Select(change => change.RemainingTicks).SequenceEqual(new[] { 10, 20, 0 })
            || result.Events.OfType<HeroStealthChangedEvent>().Last().Tick.Value != 50) return false;
        foreach (var cloak in new[] { first with { IsOnBench = true }, first with { Abilities = first.Abilities!.Append(
            new AbilityDefinition(new StringName("verification.cloak_destroy"), AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])).ToArray() } })
            if (new CombatSimulator().Simulate(Battle([cloak], [Attack()], 40)).Events.OfType<HeroStealthChangedEvent>().Any()) return false;
        var heal = new CardBattleSetup(EntityId.New(), 2, 0, 35,
            [new AbilityDefinition(new StringName("verification.cloak_heal"), AbilityActivation.Active, AbilityTarget.AlliedHero,
                0, 35, [new HealEffectDefinition(600)])], UseLegacyAttack: false);
        result = new CombatSimulator().Simulate(Battle([first, heal], [Attack()], 65));
        if (result.Events.OfType<HeroStealthChangedEvent>().Count(change => change.RemainingTicks > 0) != 1
            || result.States.Last(state => state.Tick.Value == 35).Player.Health != 1000
            || result.States.Last(state => state.Tick.Value == 65 && state.Player.Health > 0).Player.Health != 400) return false;
        var lethal = new CombatSimulator().Simulate(Battle([first], [Attack(1000)], 5));
        return lethal.EndReason == BattleEndReason.HeroDefeated && !lethal.Events.OfType<HeroStealthChangedEvent>().Any();
    }

    internal static bool DamageKindsAndEchoes()
    {
        var runtime = new BattleRuntime(Battle([Cloak(4)], [], 30));
        var hero = runtime.PlayerHero;
        BattleEffectResolver.ApplyDamage(runtime, runtime.OpponentHero.EntityId, SideId.Player, hero, 501, false);
        hero.Armor = 50;
        foreach (var kind in new[] { DamageSourceKind.Card, DamageSourceKind.Skill, DamageSourceKind.CardSet })
        foreach (var bypassArmor in new[] { false, true })
            if (BattleEffectResolver.ApplyDamage(runtime, runtime.OpponentHero.EntityId, SideId.Player, hero, 100, bypassArmor, kind)
                || hero.Health != 499 || hero.Armor != 50) return false;
        BattleEffectResolver.ApplyDamage(runtime, hero.EntityId, SideId.Player, hero, 10, false, DamageSourceKind.Status, BattleStatus.Burn);
        BattleEffectResolver.ApplyDamage(runtime, hero.EntityId, SideId.Player, hero, 10, true, DamageSourceKind.Status, BattleStatus.Poison);
        BattleEffectResolver.ApplyDamage(runtime, hero.EntityId, SideId.Player, hero, 10, false, DamageSourceKind.Eclipse);
        if (hero.Health != 489 || hero.Armor != 30 || hero.StealthDurationTicks != 20) return false;
        var watcher = new CardBattleSetup(EntityId.New(), 2, 0, 1,
            [new AbilityDefinition(new StringName("verification.cloak_damage_echo"), AbilityActivation.EchoOnDamageDealt,
                AbilityTarget.AlliedHero, 0, 0, [new GainSourceHeroArmorEffectDefinition(1)])], UseLegacyAttack: false);
        var attack = new CardBattleSetup(EntityId.New(), 0, 501, 1,
            [new AbilityDefinition(new StringName("verification.cloak_same_activation"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 1,
                [new DamageEffectDefinition(501), new DamageEffectDefinition(100),
                    new ApplyStatusEffectDefinition(BattleStatus.Burn, 5), new ApplyStatusEffectDefinition(BattleStatus.Poison, 3),
                    new DestroyCardEffectDefinition(false)])], UseLegacyAttack: false);
        var result = new CombatSimulator().Simulate(Battle([Cloak(4), watcher], [attack], 12, eclipse: 10));
        var alive = result.States.Last(state => state.Player.Health > 0 && state.Tick.Value == 12).Player;
        return result.Events.OfType<AttackBlockedEvent>().Count() == 1
            && result.Events.OfType<AbilityActivatedEvent>().Count(item => item.SourceCardId == watcher.EntityId) == 1
            && result.Events.OfType<DamageDealtEvent>().Any(item => item.StatusOrigin == BattleStatus.Burn)
            && result.Events.OfType<DamageDealtEvent>().Any(item => item.StatusOrigin == BattleStatus.Poison)
            && result.Events.OfType<DamageDealtEvent>().Any(item => item.SourceKind == DamageSourceKind.Eclipse)
            && alive.Health == 487 && alive.StealthDurationTicks == 9;
    }

    internal static bool PlaybackAndValidation(Control owner)
    {
        var result = new CombatSimulator().Simulate(Battle([Cloak(4)], [Attack()], 55));
        var session = new CreateMatchService(new EntityFactory()).Create(42, 0, new ValosHeroDefinition());
        var snapshot = MatchSnapshot.From(session);
        var source = new BattleResolution(snapshot, snapshot, result, snapshot);
        var playback = new BattlePlaybackPresenter(source);
        var fast = new BattlePlaybackPresenter(source); fast.ToggleSpeed();
        playback.Advance(3); fast.Advance(1.5);
        var frozen = playback.Capture();
        if (frozen.State.Player.StealthDurationTicks != 20 || !ReferenceEquals(frozen.State, fast.Capture().State)) return false;
        playback.TogglePause(); playback.Advance(1);
        if (!ReferenceEquals(frozen.State, playback.Capture().State)) return false;
        var view = new HeroBattleStatusView(); owner.AddChild(view);
        try
        {
            view.Render(frozen.State.Player, frozen);
            if (!view.GetNode<PanelContainer>("Stealth").Visible
                || view.GetNode<Label>("Stealth/Duration").Text != "隐身 2.0秒") return false;
            playback.Skip(); fast.Skip();
            view.Render(playback.Capture().State.Player);
            if (view.GetNode<PanelContainer>("Stealth").Visible || frozen.State.Player.StealthDurationTicks != 20
                || !ReferenceEquals(playback.Capture().State, fast.Capture().State)) return false;
        }
        finally { owner.RemoveChild(view); view.Free(); }
        foreach (var percent in new[] { 0, 100 })
        {
            try { _ = new AbilityDefinition(new StringName("verification.invalid_cloak"), AbilityActivation.EchoOnSourceHeroHealthBelow,
                AbilityTarget.AlliedHero, 0, 0, [new GainSourceHeroStealthEffectDefinition(10)], triggerHeroHealthPercent: percent); return false; }
            catch (ArgumentException) { }
        }
        try { _ = new AbilityDefinition(new StringName("verification.invalid_stealth"), AbilityActivation.PassiveOnBattleStart,
            AbilityTarget.AlliedHero, 0, 0, [new GainSourceHeroStealthEffectDefinition(0)]); return false; }
        catch (ArgumentException) { }
        return true;
    }

    internal static async Task Capture(Control owner)
    {
        if (!LevelsAndTiming() || !StackingAndSourceFilters() || !DamageKindsAndEchoes() || !PlaybackAndValidation(owner))
            throw new InvalidOperationException("暗影斗篷聚焦验证失败。");
        var viewport = new SubViewport { Size = new Vector2I(680, 330), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(680, 330) });
            foreach (var level in new[] { 3, 4 })
            {
                var x = level == 3 ? 40 : 370;
                var face = new CardItemView { Position = new Vector2(x, 20), Size = new Vector2(236, 256) };
                viewport.AddChild(face);
                face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new ShadowCloakCardDefinition(), level)), new CardDisplayAdapter());
                var status = new HeroBattleStatusView { Position = new Vector2(x, 285) };
                viewport.AddChild(status);
                var result = new CombatSimulator().Simulate(Battle([Cloak(level)], [Attack()], 30));
                status.Render(result.States.Last(frame => frame.Player.Health > 0 && frame.Tick.Value == 30).Player);
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/shadow-cloak/card-and-stealth.png") != Error.Ok)
                throw new InvalidOperationException("暗影斗篷卡面捕获失败。");
            GD.Print("暗影斗篷聚焦验证与分级卡面隐身捕获：通过");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardBattleSetup Cloak(int level = 3) => new(EntityId.New(), 0, 0, 1,
        new ShadowCloakCardDefinition().GetLevel(level)!.Abilities, UseLegacyAttack: false, OccupiedSlots: 2);

    private static CardBattleSetup Attack(int damage = 100) => new(EntityId.New(), 0, damage, 5,
        [new AbilityDefinition(new StringName("verification.cloak_attack"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 5, [new DamageEffectDefinition(damage)])], UseLegacyAttack: false);

    private static BattleSetup Battle(CardBattleSetup[] player, CardBattleSetup[] opponent, int timeout, int? eclipse = null) =>
        new(new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), player),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), opponent),
            42, new BattleTick(timeout), new BattleTick(eclipse ?? timeout + 1));
}
