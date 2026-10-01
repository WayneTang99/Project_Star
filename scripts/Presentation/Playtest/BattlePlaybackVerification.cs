using System;
using System.Linq;
using Godot;
using Project_Star.Application.Combat;
using Project_Star.Application.Match;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.Playtest;

// 回放状态、顺序、控制与结算隔离的行为验证（表现层验证模块）。
internal static class BattlePlaybackVerification
{
    private static BattleResult Simulate()
    {
        var attack = new AbilityDefinition(new StringName("verification.playback.attack"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 2, [new DamageEffectDefinition(5)]);
        var aid = new AbilityDefinition(new StringName("verification.playback.aid"), AbilityActivation.Active,
            AbilityTarget.AlliedHero, 3, 3, [new HealEffectDefinition(10), new ArmorEffectDefinition(7),
                new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 3),
                new ChargeRandomOtherAlliedElementCardEffectDefinition(GameElements.Light, 1)]);
        var destroy = new AbilityDefinition(new StringName("verification.playback.destroy"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, 12, [new DestroyCardEffectDefinition(true)]);
        return new CombatSimulator().Simulate(new BattleSetup(
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 100, 0, 30, 20, 6, 4, 3, 4),
                [new CardBattleSetup(EntityId.New(), 0, 5, 2, [attack], Multicast: 1, ElementKeys: [GameElements.Light]),
                 new CardBattleSetup(EntityId.New(), 1, 0, 3, [aid, destroy], ElementKeys: [GameElements.Light])]),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0),
                [new CardBattleSetup(EntityId.New(), 0, 8, 1)]), 42, new BattleTick(30), new BattleTick(20)));
    }

    // 状态快照完整覆盖初始值、治疗、护甲、魔法、衰减、充能、摧毁与终止值。
    public static bool CapturedStates()
    {
        var result = Simulate();
        var states = result.States;
        if (states[0].Tick != BattleTick.Zero || states[0].EventCount != 0
            || states[0].Player.Health != 100 || states[0].Player.Mana != 20 || states[0].Player.Poison != 4) return false;
        if (!states.Any(frame => frame.Cards.Any(card => card.Haste > 0))
            || !states.Any(frame => frame.Cards.Any(card => card.Destroyed))
            || !states.Any(frame => frame.Player.Armor > 0) || !states.Any(frame => frame.Player.Mana < 20)
            || !states.Any(frame => frame.Player.Poison < 4) || !states.Any(frame => frame.Player.Burn < 3)
            || !states.Zip(states.Skip(1)).Any(pair => pair.Second.Player.Health > pair.First.Player.Health)
            || !result.Events.OfType<CardChargedEvent>().Any()
            || result.Events.OfType<DamageDealtEvent>().Count(item => item.Tick.Value == 2 && item.TargetSide == SideId.Opponent) != 2)
            return false;
        for (var index = 1; index < states.Count; index++)
            if (states[index].Tick.Value < states[index - 1].Tick.Value || states[index].EventCount < states[index - 1].EventCount) return false;
        var eclipse = new CombatSimulator().Simulate(new BattleSetup(
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 100, 0), []),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 100, 0), []),
            42, new BattleTick(100), new BattleTick(20)));
        if (eclipse.States[0].Eclipse || !eclipse.States.Any(frame => frame.Eclipse)
            || !eclipse.Events.OfType<DamageDealtEvent>().Any(item => item.SourceKind == DamageSourceKind.Eclipse)
            || eclipse.States[^1].Player.Health != eclipse.PlayerRemainingHealth) return false;
        return states[^1].EventCount == result.Events.Count && states[^1].Tick == result.EndedAt
            && states[^1].Player.Health == result.PlayerRemainingHealth && states[^1].Opponent.Health == result.OpponentRemainingHealth;
    }

    // 暂停、1×、2×、跳过消费相同的有序记录，旧刷新数据不被后续播放修改。
    public static bool ControlsAndIsolation()
    {
        var result = Simulate();
        var session = new Project_Star.Application.Match.CreateMatchService(new Project_Star.Application.Factories.EntityFactory())
            .Create(42, 100, new PaladinHeroDefinition());
        var snapshot = MatchSnapshot.From(session);
        var source = new BattleResolution(snapshot, snapshot, result, snapshot);
        var normal = new BattlePlaybackPresenter(source);
        var fast = new BattlePlaybackPresenter(source);
        var skipped = new BattlePlaybackPresenter(source);
        var initial = normal.Capture();
        normal.TogglePause(); if (normal.Advance(1) || normal.Tick != 0) return false; normal.TogglePause();
        fast.ToggleSpeed(); normal.Advance(.2); fast.Advance(.1);
        if (normal.Tick != fast.Tick || !ReferenceEquals(normal.Capture().State, fast.Capture().State)) return false;
        while (!normal.Completed) normal.Advance(.1);
        while (!fast.Completed) fast.Advance(.1);
        skipped.Skip(); skipped.Skip();
        return initial.Tick == 0 && initial.State.Player.Health == 100 && initial.Feedback.Count == 0
            && normal.Completed && fast.Completed && skipped.Completed
            && ReferenceEquals(normal.Capture().State, fast.Capture().State)
            && ReferenceEquals(normal.Capture().State, skipped.Capture().State)
            && normal.Capture().Feedback.SequenceEqual(skipped.Capture().Feedback)
            && normal.Capture().State.Player.Health == result.PlayerRemainingHealth
            && MatchSnapshot.From(session).Wealth == snapshot.Wealth;
    }
}
