using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Application.Match;
using Project_Star.Domain.Match;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 破晓长弓累计冷却、最终下限与多重分时发动验证（表现层）。
internal static class DawnLongbowChecks
{
    internal static bool LevelsAndCooldown()
    {
        var definition = new DawnLongbowCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.Key != new StringName("card.dawn_longbow") || identity.DisplayName != "破晓长弓"
            || identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Medium
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !definition.Tags.Contains(GameTags.Equipment) || !definition.Tags.Contains(GameTags.Medium)
            || definition.InitialLevel != 3 || definition.SupportsLevel(2) || definition.SupportsLevel(5)) return false;
        foreach (var level in new[] { 3, 4 })
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        {
            var instance = new EntityFactory().CreateCard(definition, level);
            var damage = level == 3 ? 200 : 400;
            var source = new CardBattleSetup(instance.Id, 0, damage, 80, instance.Abilities, UseLegacyAttack: false);
            var bench = source with { EntityId = EntityId.New(), IsOnBench = true };
            var setup = Battle(side == SideId.Player ? [source, bench] : [], side == SideId.Opponent ? [source, bench] : [], 400);
            var result = new CombatSimulator().Simulate(setup);
            var ticks = result.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId == source.EntityId && !e.IsEcho)
                .Select(e => (int)e.Tick.Value).ToArray();
            if (!ticks.SequenceEqual(new[] { 80, 150, 210, 260, 300, 330, 350, 360, 370, 380, 390, 400 })
                || result.Events.OfType<DamageDealtEvent>().Count(e => e.SourceCardId == source.EntityId && e.RawDamage == damage) != ticks.Length
                || result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == source.EntityId && e.IsEcho) != ticks.Length
                || result.Events.OfType<AbilityActivatedEvent>().Any(e => e.SourceCardId == bench.EntityId)
                || result.States[^1].Cards.Single(c => c.Id == source.EntityId).CooldownDurationUnits.Single() != 20m
                || result.PermanentChanges.Count != 0 || instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != 80
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                || new CombatSimulator().Simulate(Battle([source], [], 1)).States[0].Cards.Single().CooldownDurationUnits.Single() != 160m)
                return false;
        }
        // 下限在固定量和倍率之后应用；百分百缩减仍需一秒，非主动来源保持无冷却。
        foreach (var multiplier in new[] { 0m, .01m, .5m, 1m })
        {
            var source = new CardBattleSetup(EntityId.New(), 0, 1, 5) { CooldownMultiplier = multiplier };
            var result = new CombatSimulator().Simulate(Battle([source], [], 20));
            if (!result.Events.OfType<AbilityActivatedEvent>().Select(e => (int)e.Tick.Value).SequenceEqual(new[] { 10, 20 })
                || result.States.Any(s => s.Cards.Single().CooldownDurationUnits.Single() != 20m)) return false;
        }
        return BattleCooldownRules.DurationUnits(80, -100, .5m) == 20m
            && BattleCooldownRules.DurationUnits(80, -10, .5m) == 70m;
    }

    internal static bool DelayedMulticast()
    {
        var attack = new AbilityDefinition(new StringName("verification.delayed.attack"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 3, 10, [new DamageEffectDefinition(10)]);
        var echo = new AbilityDefinition(new StringName("verification.delayed.echo"), AbilityActivation.EchoOnAbilityActivated,
            AbilityTarget.EnemyHero, 0, 0, [new DamageEffectDefinition(1)]);
        var source = new CardBattleSetup(EntityId.New(), 0, 10, 10, [attack], Multicast: 2, UseLegacyAttack: false);
        var listener = new CardBattleSetup(EntityId.New(), 1, 0, 0, [echo], UseLegacyAttack: false);
        var setup = Battle([source, listener], [], 14);
        var result = new CombatSimulator().Simulate(setup);
        var events = result.Events.OfType<AbilityActivatedEvent>().ToArray();
        if (!events.Select(e => (int)e.Tick.Value).SequenceEqual(new[] { 10, 10, 12, 12, 14, 14 })
            || !events.Select(e => e.SourceCardId).SequenceEqual(new[] { source.EntityId, listener.EntityId,
                source.EntityId, listener.EntityId, source.EntityId, listener.EntityId })
            || result.Events.OfType<ManaChangedEvent>().Count(e => e.Amount == -3) != 1
            || result.States[^1].Player.Mana != 97
            || result.States[^1].Cards.Single(c => c.Id == source.EntityId).CooldownUnits.Single() != 12m
            || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
        // 禁锢停止冷却，但已排程的多重仍使用固定0.2秒间隔。
        var bind = new AbilityDefinition("verification.delayed.bind", AbilityActivation.EchoOnSourceCardActivated,
            AbilityTarget.SelfCard, 0, 0, [new ApplyStatusEffectDefinition(BattleStatus.ImmobilizeDuration, 20)]);
        var bound = new CombatSimulator().Simulate(Battle([source with { Abilities = [attack, bind] }], [], 14));
        if (!bound.Events.OfType<AbilityActivatedEvent>().Where(e => !e.IsEcho).Select(e => (int)e.Tick.Value)
            .SequenceEqual(new[] { 10, 12, 14 })) return false;
        // 多重在首次发动时冻结：长弓逐次回响减CD，第二次延迟0.2秒释放。
        var bow = new EntityFactory().CreateCard(new DawnLongbowCardDefinition());
        var bowSetup = new CardBattleSetup(bow.Id, 0, 200, 80, bow.Abilities, Multicast: 1, UseLegacyAttack: false);
        var bowResult = new CombatSimulator().Simulate(Battle([bowSetup], [], 82));
        return bowResult.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId == bow.Id && !e.IsEcho)
            .Select(e => (int)e.Tick.Value).SequenceEqual(new[] { 80, 82 })
            && bowResult.States[^1].Cards.Single().CooldownDurationUnits.Single() == 120m
            && bowResult.States[^1].Cards.Single().CooldownUnits.Single() == 116m;
    }

    internal static bool CancellationAndFrozenCount()
    {
        var active = new AbilityDefinition("verification.delayed.cancel.attack", AbilityActivation.Active,
            AbilityTarget.EnemyHero, 3, 10, [new DamageEffectDefinition(10)]);
        var source = new CardBattleSetup(EntityId.New(), 0, 10, 10, [active], UseLegacyAttack: false,
            Tags: TagSet.ForCard(CardSize.Small, [GameTags.Equipment]), Multicast: 2);
        var destroy = new AbilityDefinition("verification.delayed.destroy", AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 11, [new DestroyRandomEnemyCardEffectDefinition([GameTags.Equipment], [CardSize.Small])]);
        var transform = new AbilityDefinition("verification.delayed.transform", AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 11, [new TransformRandomEnemyCardEffectDefinition(new TrainingDummyCardDefinition())]);
        foreach (var effect in new[] { destroy, transform })
        {
            var enemy = new CardBattleSetup(EntityId.New(), 0, 0, 11, [effect], UseLegacyAttack: false);
            var result = new CombatSimulator().Simulate(Battle([source], [enemy], 15));
            if (result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == source.EntityId && !e.IsEcho) != 1)
                return false;
        }
        // 摧毁后马上修复也不能复活旧排程；修复后的新冷却需完整一秒。
        var repair = new CardBattleSetup(EntityId.New(), 1, 0, 12,
            [new AbilityDefinition("verification.delayed.repair", AbilityActivation.Active, AbilityTarget.AlliedHero, 0, 12,
                [new RepairRandomDestroyedAlliedCardEffectDefinition(GameTags.Equipment)])], UseLegacyAttack: false);
        var repaired = new CombatSimulator().Simulate(Battle([source, repair],
            [new CardBattleSetup(EntityId.New(), 0, 0, 11, [destroy], UseLegacyAttack: false)], 20));
        if (repaired.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == source.EntityId && !e.IsEcho) != 1
            || repaired.Events.OfType<CardRepairedEvent>().Single().Tick.Value != 12
            || repaired.States[^1].Cards.Single(c => c.Id == source.EntityId).CooldownUnits.Single() != 4m)
            return false;
        // 多重成长只影响后续新组，首次确定的两次不被本组回响扩展。
        var growth = new AbilityDefinition("verification.delayed.growth", AbilityActivation.EchoOnSourceCardActivated,
            AbilityTarget.SelfCard, 0, 0, [new IncreaseSourceCardAttributeEffectDefinition(GameAttributeKeys.Multicast, 1)]);
        var setup = Battle([source with { Abilities = [active, growth] }], [], 14);
        var frozen = new CombatSimulator().Simulate(setup);
        return frozen.Events.OfType<AbilityActivatedEvent>().Count(e => !e.IsEcho) == 3
            && frozen.States[^1].Cards.Single().Values[GameAttributeKeys.Multicast] == 5;
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(448, 448), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(448, 448) });
            var face = new CardItemView { Position = new Vector2(24, 24), Size = new Vector2(400, 400) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new DawnLongbowCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/dawn-longbow/card-face.png") != Error.Ok)
                throw new InvalidOperationException("破晓长弓卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static BattleSetup Battle(CardBattleSetup[] player, CardBattleSetup[] enemy, int timeout) =>
        new(new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 100000, 0, 100, 100), player),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 100000, 0, 100, 100), enemy), 42, new BattleTick(timeout));
}
