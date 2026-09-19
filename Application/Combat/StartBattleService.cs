using System;
using Godot;
using Project_Star.Application.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Combat;

public sealed class StartBattleService
{
    public static readonly BattleTick DefaultTimeout = new(600);

    private static readonly StringName InvalidSetup = new("battle.invalid_setup");

    private readonly BattleSetupFactory _setupFactory;
    private readonly CombatSimulator _simulator;

    public StartBattleService(BattleSetupFactory setupFactory, CombatSimulator simulator)
    {
        _setupFactory = setupFactory ?? throw new ArgumentNullException(nameof(setupFactory));
        _simulator = simulator ?? throw new ArgumentNullException(nameof(simulator));
    }

    public Result<BattleResult> StartBattle(
        MatchSession player,
        MatchSession opponent,
        ulong seed)
    {
        try
        {
            var setup = _setupFactory.Create(player, opponent, seed, DefaultTimeout);
            return Result<BattleResult>.Success(_simulator.Simulate(setup));
        }
        catch (InvalidOperationException exception)
        {
            return Result<BattleResult>.Fail(new Failure(InvalidSetup, exception.Message));
        }
    }

    public Result ApplyBattleResult(MatchSession session, BattleResult result)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(result);
        return Result.Success();
    }
}
