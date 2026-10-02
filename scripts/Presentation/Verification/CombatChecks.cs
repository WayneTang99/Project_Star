using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;

using static Project_Star.Presentation.Verification.VerificationFixtures;

namespace Project_Star.Presentation.Verification;

// 战斗队列、状态与结束规则验证（表现层验证模块）。
internal static class CombatChecks
{
    internal static bool CheckBattleSnapshotIsolation()
    {
        var (player, opponent) = CreateBattlePair(true, true);
        var setup = new BattleSetupFactory().Create(player, opponent, 9, new BattleTick(300));
        player.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, 1);
        var result = new CombatSimulator().Simulate(setup);
        return setup.Player.Hero.MaxHealth == 100
            && result.PlayerRemainingHealth == 0
            && player.Player.Hero.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.MaxHealth) == 1;
    }

    internal static bool CheckDeterministicBattle()
    {
        var (player, opponent) = CreateBattlePair(true, true);
        var setup = new BattleSetupFactory().Create(player, opponent, 99, new BattleTick(300));
        var first = new CombatSimulator().Simulate(setup);
        var second = new CombatSimulator().Simulate(setup);
        return first.Outcome == second.Outcome
            && first.EndedAt == second.EndedAt
            && string.Join("|", first.Events) == string.Join("|", second.Events);
    }

    internal static bool CheckFirstActivationTiming()
    {
        var (player, opponent) = CreateBattlePair(true, false);
        var result = new StartBattleService(new BattleSetupFactory(), new CombatSimulator())
            .StartBattle(player, opponent, 1).Value!;
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is DamageDealtEvent damage)
            {
                return damage.Tick.Value == 10;
            }
        }

        return false;
    }

    internal static bool CheckStableFifoOrder()
    {
        var (player, opponent) = CreateBattlePair(true, true);
        var result = new StartBattleService(new BattleSetupFactory(), new CombatSimulator())
            .StartBattle(player, opponent, 1).Value!;
        var targets = new List<SideId>();
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is DamageDealtEvent damage && damage.Tick.Value == 10)
            {
                targets.Add(damage.TargetSide);
            }
        }

        return targets.Count == 2 && targets[0] == SideId.Opponent && targets[1] == SideId.Player;
    }

    internal static bool CheckArmorDamage()
    {
        var playerHero = new HeroBattleSetup(EntityId.New(), 100, 0);
        var opponentHero = new HeroBattleSetup(EntityId.New(), 100, 10);
        var playerCard = new CardBattleSetup(EntityId.New(), 0, 25, 1);
        var setup = new BattleSetup(
            new BattleSideSetup(playerHero, [playerCard]),
            new BattleSideSetup(opponentHero, Array.Empty<CardBattleSetup>()),
            1,
            new BattleTick(1));
        var result = new CombatSimulator().Simulate(setup);
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is DamageDealtEvent damage)
            {
                return damage.ArmorAbsorbed == 10
                    && damage.HealthDamage == 15
                    && damage.RemainingHealth == 85;
            }
        }

        return false;
    }

    internal static bool CheckMaxHealthPercentDamage()
    {
        var ability = new AbilityDefinition(
            new StringName("test.percent_damage"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            1,
            [new MaxHealthPercentDamageEffectDefinition(20)]);
        var result = SimulateAbilities([ability], timeout: 1, opponentArmor: 10, opponentMaxHealth: 103);
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is DamageDealtEvent damage
                && damage.TargetSide == SideId.Opponent
                && damage.RawDamage == 20)
            {
                return damage.ArmorAbsorbed == 10
                    && damage.HealthDamage == 10
                    && damage.RemainingHealth == 93;
            }
        }

        return false;
    }

    internal static bool CheckBattleTimeout()
    {
        var (player, opponent) = CreateBattlePair(false, false);
        var result = new StartBattleService(new BattleSetupFactory(), new CombatSimulator())
            .StartBattle(player, opponent, 1).Value!;
        return result.Outcome == BattleOutcome.PlayerVictory
            && result.EndReason == BattleEndReason.SimultaneousDefeat
            && result.EndedAt.Value == 360;
    }

    internal static bool CheckInsufficientMana()
    {
        var ability = new AbilityDefinition(new StringName("test.mana"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 20, 1, [new DamageEffectDefinition(50)]);
        var result = SimulateAbilities([ability], timeout: 5);
        foreach (var battleEvent in result.Events)
            if (battleEvent is AbilityActivatedEvent) return false;
        return result.OpponentRemainingHealth == 0 && result.EndReason == BattleEndReason.Extinction;
    }

    internal static bool CheckEchoDoesNotChain()
    {
        var active = new AbilityDefinition(new StringName("test.active"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 1, [new DamageEffectDefinition(1)]);
        var echoA = new AbilityDefinition(new StringName("test.echo_a"), AbilityActivation.EchoOnAbilityActivated,
            AbilityTarget.AlliedHero, 0, 0, [new ArmorEffectDefinition(1)]);
        var echoB = new AbilityDefinition(new StringName("test.echo_b"), AbilityActivation.EchoOnAbilityActivated,
            AbilityTarget.AlliedHero, 0, 0, [new ArmorEffectDefinition(1)]);
        var result = SimulateAbilities([active, echoA, echoB], timeout: 1);
        var activated = 0; var echoes = 0;
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is AbilityActivatedEvent abilityEvent)
            {
                activated++;
                if (abilityEvent.IsEcho) echoes++;
            }
        }
        return activated == 3 && echoes == 2;
    }

    internal static bool CheckBurnAndPoison()
    {
        var ability = new AbilityDefinition(new StringName("test.status"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 1,
            [new ApplyStatusEffectDefinition(BattleStatus.Burn, 10),
             new ApplyStatusEffectDefinition(BattleStatus.Poison, 8),
             new DestroyCardEffectDefinition(false)]);
        var result = SimulateAbilities([ability], timeout: 11, opponentArmor: 100);
        var burnCorrect = false; var poisonCorrect = false;
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is not DamageDealtEvent damage || damage.Tick.Value != 10) continue;
            burnCorrect |= damage.RawDamage == 6 && damage.ArmorAbsorbed == 6 && damage.HealthDamage == 0;
            poisonCorrect |= damage.RawDamage == 8 && damage.ArmorAbsorbed == 0 && damage.HealthDamage == 8;
        }
        return burnCorrect && poisonCorrect;
    }

    internal static bool CheckPermanentDestroy()
    {
        var ability = new AbilityDefinition(new StringName("test.destroy"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, 1, [new DestroyCardEffectDefinition(true)]);
        var result = SimulateAbilities([ability], timeout: 2);
        return result.PermanentChanges.Count == 1 && result.PermanentChanges[0].ChangeType == "Destroy";
    }


}
