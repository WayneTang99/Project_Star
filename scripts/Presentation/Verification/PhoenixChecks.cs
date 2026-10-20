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
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 菲尼克斯致命救援、灼伤成长、伤害来源及冻结回放验证（表现层）。
internal static class PhoenixChecks
{
    internal static bool DefinitionAndBattle()
    {
        var definition = new PhoenixCardDefinition(); var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.DisplayName != "菲尼克斯" || identity.Key != new StringName("card.phoenix")
            || identity.FactionKey != new StringName("mona") || identity.Size != CardSize.Large
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Fire }) || definition.InitialLevel != 4
            || definition.SupportsLevel(3) || definition.SupportsLevel(5) || definition.Tags.Count != 3
            || !definition.Tags.Contains(GameTags.Beast) || !definition.Tags.Contains(GameTags.Spirit)
            || texture is null || texture.GetWidth() * 2 != texture.GetHeight() * 3
            || !DefinitionRegistry.Scan(typeof(PhoenixChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        var bird = Bird();
        var enemy = new CardBattleSetup(EntityId.New(), 0, 2001, 50);
        var setup = Battle([bird], [enemy], 110);
        var result = new CombatSimulator().Simulate(setup);
        var burns = result.Events.OfType<StatusChangedEvent>().Where(item => item.Status == BattleStatus.Burn).ToArray();
        var rescue = result.Events.OfType<AbilityActivatedEvent>().Where(item => item.SourceCardId == bird.EntityId && item.IsEcho).ToArray();
        var growth = result.Events.OfType<CardAttributeChangedEvent>().Single(item => item.CardId == bird.EntityId);
        if (bird.CooldownTicks != 40 || bird.Abilities!.Any(item => item.ManaCost != 0)
            || burns.Length != 2 || burns[0].Tick.Value != 40 || burns[0].Amount != 164
            || burns[1].Tick.Value != 80 || burns[1].Amount != 328
            || rescue.Length != 1 || rescue[0].Tick.Value != 50 || growth.Amount != 164 || growth.CurrentValue != 328
            || result.States.Last(item => item.Tick.Value == 50).Player.Health != 1000
            || result.EndedAt.Value != 100 || result.Outcome != BattleOutcome.OpponentVictory
            || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any()
            || bird.CombatValues[GameAttributeKeys.Burn] != 164
            || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
        // 下一场重建 Runtime，恢复基础灼伤并重新拥有一次救援。
        var next = new CombatSimulator().Simulate(setup);
        return next.Events.SequenceEqual(result.Events)
            && CardDisplayAdapter.Rules(MatchDisplayQuery.FromOffer(ShopOffer.Create(definition)))
                .Any(rule => rule.Contains("首次受到致命伤害") && rule.Contains("50%") && rule.Contains("×2"));
    }

    internal static bool DamageKindsAndBoundaries()
    {
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        foreach (var kind in new[] { DamageSourceKind.Card, DamageSourceKind.Skill, DamageSourceKind.CardSet,
            DamageSourceKind.Status, DamageSourceKind.Eclipse })
        {
            var bird = Bird(); var runtime = new BattleRuntime(Battle([bird], [], 10, side));
            var hero = runtime.GetHero(side); hero.Armor = 100;
            BattleEffectResolver.ApplyDamage(runtime, EntityId.New(), side, hero, 100, false, kind);
            if (hero.Health != 2001 || runtime.Cards.Single().GetCombatAttribute(GameAttributeKeys.Burn) != 164) return false;
            BattleEffectResolver.ApplyDamage(runtime, EntityId.New(), side, hero, 2000, true, kind);
            if (hero.Health != 1 || hero.FirstLethalDamageReceived) return false;
            BattleEffectResolver.ApplyDamage(runtime, EntityId.New(), side, hero, 10000, true, kind,
                kind == DamageSourceKind.Status ? BattleStatus.Poison : null);
            if (hero.Health != 1000 || !hero.FirstLethalDamageReceived
                || runtime.Cards.Single().GetCombatAttribute(GameAttributeKeys.Burn) != 328) return false;
            BattleEffectResolver.ApplyDamage(runtime, EntityId.New(), side, hero, 10000, true, kind);
            if (hero.Health != 0 || runtime.Events.OfType<CardAttributeChangedEvent>().Count() != 1) return false;
        }
        foreach (var bench in new[] { false, true })
        {
            var runtime = new BattleRuntime(Battle([Bird() with { IsOnBench = bench }], [], 10));
            runtime.Cards.Single().Destroyed = !bench;
            BattleEffectResolver.ApplyDamage(runtime, EntityId.New(), SideId.Player, runtime.PlayerHero, 2001, false);
            if (runtime.PlayerHero.Health != 0 || runtime.Events.OfType<CardAttributeChangedEvent>().Any()) return false;
        }
        var boosted = Bird() with { Multicast = 5, CombatValues = new System.Collections.Generic.Dictionary<StringName, int>
            { [GameAttributeKeys.Burn] = 170, [GameAttributeKeys.HealingBonus] = 10 } };
        var boostedRuntime = new BattleRuntime(Battle([boosted], [], 10));
        BattleEffectResolver.ApplyDamage(boostedRuntime, EntityId.New(), SideId.Player, boostedRuntime.PlayerHero, 2001, false);
        if (boostedRuntime.PlayerHero.Health != 1010 || boostedRuntime.Cards.Single().GetCombatAttribute(GameAttributeKeys.Burn) != 340
            || boostedRuntime.Events.OfType<CardAttributeChangedEvent>().Count() != 1) return false;
        // 寂灭不经过伤害救援入口，不能以救援延长战斗。
        var extinction = new CombatSimulator().Simulate(Battle([Bird()], [], 1));
        return extinction.EndReason == BattleEndReason.Extinction
            && !extinction.Events.OfType<CardAttributeChangedEvent>().Any();
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(504, 344), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(504, 344) });
            var face = new CardItemView { Position = new Vector2(24, 20), Size = new Vector2(456, 304) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new PhoenixCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/phoenix/card-face.png") != Error.Ok)
                throw new InvalidOperationException("Phoenix card capture failed.");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardBattleSetup Bird()
    {
        var card = new EntityFactory().CreateCard(new PhoenixCardDefinition());
        return new CardBattleSetup(card.Id, 0, 0, 40, card.Abilities, UseLegacyAttack: false, Tags: card.Tags,
            OccupiedSlots: 3, ElementKeys: card.Attributes.Identity.ElementKeys)
        { Level = 4, CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues() };
    }
    private static BattleSetup Battle(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks, SideId side = SideId.Player) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), side == SideId.Player ? 2001 : 10000, 0),
            side == SideId.Player ? allies : enemies),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), side == SideId.Opponent ? 2001 : 10000, 0),
            side == SideId.Player ? enemies : allies), 42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
