using System;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Common;
using Project_Star.Application.Economy;
using Project_Star.Application.Encounters;
using Project_Star.Application.Factories;
using Project_Star.Application.Combat;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Match;

// 第一条联网存档操作链（应用层）；复用正式创建、遭遇、商店和购买用例。
public sealed class ServerMatchService
{
    private readonly IDefinitionCatalog _definitions;
    private readonly EncounterScheduler _encounters;
    private readonly CardEconomyService _economy;
    private readonly EntityFactory _factory = new();
    private readonly ShopCardPoolService _shops = new();
    private readonly IOpponentProvider _opponents;
    private readonly StartBattleService _battles;
    private readonly MatchResultService _results;
    private readonly MonsterRewardClaimService _rewards;
    private readonly BoardService _board;
    private readonly ResolveEncounterOptionService _events;

    public ServerMatchService(IDefinitionCatalog definitions, IOpponentProvider opponents)
    {
        _definitions = definitions;
        _opponents = opponents;
        _encounters = new EncounterScheduler(definitions);
        var board = new BoardService(new BoardPlacementSolver(), definitions.Sets);
        _board = board;
        _economy = new CardEconomyService(_factory, board, definitions.Cards.Values);
        _battles = new StartBattleService(new BattleSetupFactory(definitions.Sets), new CombatSimulator());
        _results = new MatchResultService(board, definitions.Cards.Values);
        _rewards = new MonsterRewardClaimService(definitions, _economy, new SkillAcquisitionService(_factory), board);
        _events = new ResolveEncounterOptionService(_factory, board, definitions.Cards.Values, definitions.Monsters.Values);
    }

    // 服务器提供随机种子；采用零额外启动资金及现有正式收入结算，不使用试玩999金币。
    public Result<ServerMatchSession> Create(StringName heroKey, ulong seed)
    {
        if (!_definitions.Heroes.TryGetValue(heroKey, out var hero)) return Reject("match.hero_missing");
        var match = new CreateMatchService(_factory, _definitions.Gems.Values, _definitions).Create(seed, 0, hero);
        var generated = _encounters.Generate(match);
        return generated.IsFailure ? Result<ServerMatchSession>.Fail(generated.Failure!)
            : Result<ServerMatchSession>.Success(new ServerMatchSession(match));
    }

    // 当前纵向验证仅开放已经下发的卡牌商店；其他遭遇在后续操作服务化时接入。
    public Result SelectShop(ServerMatchSession session, StringName encounterKey)
    {
        if (session.Phase != ServerMatchPhase.EncounterChoice) return Fail("match.wrong_phase");
        var choice = session.Match.EncounterSchedule.CurrentChoices.FirstOrDefault(value => value.Key == encounterKey);
        if (choice is null || !_definitions.Encounters.TryGetValue(encounterKey, out var definition)
            || definition is not CardShopEncounterDefinition shop) return Fail("match.shop_unavailable");
        var selected = _encounters.Select(session.Match, encounterKey);
        if (selected.IsFailure) return Result.Fail(selected.Failure!);
        session.Shop = _shops.CreateStock(session.Match, shop, _definitions.Cards.Values, choice.ShopLevel);
        session.ShopKey = encounterKey;
        session.Phase = ServerMatchPhase.Shop;
        return Result.Success();
    }

    // 商品来自服务器访问状态，客户端不能提交价格、卡牌数值或任意卡牌定义。
    public Result Buy(ServerMatchSession session, int offerIndex)
    {
        if (session.Phase != ServerMatchPhase.Shop || session.Shop is null) return Fail("match.wrong_phase");
        if (offerIndex < 0 || offerIndex >= session.Shop.Offers.Count) return Fail("match.offer_missing");
        var result = _economy.BuyAndPlace(session.Match, session.Shop.Offers[offerIndex]);
        return result.IsFailure ? Result.Fail(result.Failure!) : Result.Success();
    }

    // 整理阶段复用完整推挤与跨区放置事务，卡牌必须属于本局。
    public Result MoveCard(ServerMatchSession session, EntityId cardId, BoardZone zone, int start)
    {
        if (!CanEditBoard(session)) return Fail("match.wrong_phase");
        if (!Enum.IsDefined(zone)) return Fail("match.invalid_zone");
        var moved = _board.PlaceCard(session.Match, cardId, zone, start);
        return moved.IsFailure ? Result.Fail(moved.Failure!) : Result.Success();
    }

    // 金额与出售能力只读取权威实例，预期版本由宿主拦截过期确认。
    public Result SellCard(ServerMatchSession session, EntityId cardId)
    {
        if (!CanEditBoard(session)) return Fail("match.wrong_phase");
        var sold = _economy.SellFromBoard(session.Match, cardId);
        return sold.IsFailure ? Result.Fail(sold.Failure!) : Result.Success();
    }

    // 当前访问只能付费刷新一次；复用正式筛选、余额与随机规则。
    public Result RefreshShop(ServerMatchSession session)
    {
        if (session.Match.Status != MatchStatus.InProgress || session.Phase != ServerMatchPhase.Shop || session.Shop is null)
            return Fail("match.wrong_phase");
        var refreshed = _shops.Refresh(session.Match, session.Shop);
        return refreshed.IsFailure ? Result.Fail(refreshed.Failure!) : Result.Success();
    }

    // 只读展示当前商店能否刷新，不扣款或推进随机。
    public bool CanRefreshShop(ServerMatchSession session) => session.Match.Status == MatchStatus.InProgress
        && session.Phase == ServerMatchPhase.Shop && session.Shop is not null && _shops.CheckRefresh(session.Match, session.Shop).IsSuccess;

    // 结束卡牌商店并生成已推进回合的候选；调用方只提交成功候选，失败时丢弃本次模型。
    public Result LeaveShop(ServerMatchSession session)
    {
        if (session.Match.Status != MatchStatus.InProgress || session.Phase != ServerMatchPhase.Shop
            || session.Shop is null) return Fail("match.wrong_phase");
        // Select已经推进Round/Turn，Generate负责幂等结算；这里不再递增回合。
        var generated = _encounters.Generate(session.Match);
        if (generated.IsFailure) return Result.Fail(generated.Failure!);
        session.Shop = null;
        session.ShopKey = null;
        session.Phase = ServerMatchPhase.EncounterChoice;
        return Result.Success();
    }

    // 仅打开已下发的选项事件；先固定访问身份及展示槽，不提前执行加权结果。
    public Result SelectEvent(ServerMatchSession session, StringName key)
    {
        if (session.Match.Status != MatchStatus.InProgress || session.Phase != ServerMatchPhase.EncounterChoice)
            return Fail("match.wrong_phase");
        var choice = session.Match.EncounterSchedule.CurrentChoices.FirstOrDefault(value => value.Key == key);
        if (choice is null || !_definitions.Encounters.TryGetValue(key, out var definition)
            || definition is not ChoiceEncounterDefinition encounter) return Fail("match.event_unavailable");
        var round = session.Match.Progress.Round;
        var turn = session.Match.Progress.Turn;
        var selected = _encounters.Select(session.Match, key);
        if (selected.IsFailure) return Result.Fail(selected.Failure!);
        session.Event = new ServerEncounterVisit(Guid.NewGuid(), key, round, turn,
            _events.CreateOptionSet(session.Match, encounter, choice.Level));
        session.Phase = ServerMatchPhase.Event;
        return Result.Success();
    }

    // 当前访问的已展示选项只能结算一次；失败候选整体丢弃，重试不另抽结果。
    public Result ResolveEvent(ServerMatchSession session, Guid visitId, StringName optionKey)
    {
        if (session.Match.Status != MatchStatus.InProgress || session.Phase != ServerMatchPhase.Event
            || session.Event is not { } visit) return Fail("match.wrong_phase");
        if (visit.Id != visitId) return Fail("match.visit_mismatch");
        var resolved = _events.Resolve(session.Match, visit.Options, optionKey);
        if (resolved.IsFailure) return Result.Fail(resolved.Failure!);
        var result = resolved.Value!;
        if (result.MonsterEncounter is { } monster)
        {
            session.Battle = new ServerMonsterBattle(Guid.NewGuid(), monster.Key, visit.Round, visit.Turn,
                session.Match.Random.State, _opponents.CreateMonsterOpponent(session.Match.Random.State, _definitions.Monsters[monster.Key]),
                SourceEncounterKey: visit.EncounterKey);
            session.Event = null;
            session.Phase = ServerMatchPhase.MonsterPreparation;
        }
        else session.Event = visit with { Result = new ServerEncounterOutcome(result.OptionKey, result.Changes.ToArray(),
            result.WealthGained, result.GrantedCard?.Id.Value, result.GrantedCard?.Attributes.Identity.Key,
            result.GrantedCard?.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level), result.CardRewardSkipped,
            result.WealthSpent, result.PendingBattleMaxHealthBonus, result.CardChanges?.ToArray() ?? [], result.MonsterEncounterSkipped) };
        return Result.Success();
    }

    // 确认已完成事件后生成当前边界候选；不重复选择遭遇或递增回合。
    public Result LeaveEvent(ServerMatchSession session, Guid visitId)
    {
        if (session.Match.Status != MatchStatus.InProgress || session.Phase != ServerMatchPhase.Event
            || session.Event is not { } visit) return Fail("match.wrong_phase");
        if (visit.Id != visitId) return Fail("match.visit_mismatch");
        if (!visit.Options.IsResolved) return Fail("match.event_pending");
        var generated = _encounters.Generate(session.Match);
        if (generated.IsFailure) return Result.Fail(generated.Failure!);
        session.Event = null;
        session.Phase = ServerMatchPhase.EncounterChoice;
        return Result.Success();
    }

    // 只接受已下发的怪物候选；选择成功后固定实例身份，恢复时不得重新创建对手。
    public Result SelectMonster(ServerMatchSession session, StringName key)
    {
        if (session.Phase != ServerMatchPhase.EncounterChoice || session.Match.Status != MatchStatus.InProgress)
            return Fail("match.wrong_phase");
        if (!session.Match.EncounterSchedule.CurrentChoices.Any(choice => choice.Key == key && choice.Kind == EncounterKind.Monster)
            || !_definitions.Monsters.TryGetValue(key, out var monster)) return Fail("match.monster_unavailable");
        var match = session.Match;
        var battle = new ServerMonsterBattle(Guid.NewGuid(), key, match.Progress.Round, match.Progress.Turn,
            match.Random.State, _opponents.CreateMonsterOpponent(match.Random.State, monster));
        var selected = _encounters.Select(match, key);
        if (selected.IsFailure) return Result.Fail(selected.Failure!);
        session.Battle = battle;
        session.Phase = ServerMatchPhase.MonsterPreparation;
        return Result.Success();
    }

    // 仅准备阶段可执行；结果、永久变化与奖励由调用方同事务提交，失败候选整体丢弃。
    public Result ResolveMonster(ServerMatchSession session, Guid battleId)
    {
        if (session.Phase != ServerMatchPhase.MonsterPreparation || session.Battle is not { Result: null } battle)
            return Fail("match.wrong_phase");
        if (battle.Id != battleId) return Fail("match.battle_mismatch");
        var simulated = _battles.StartBattle(session.Match, battle.Opponent, battle.Seed);
        if (simulated.IsFailure) return Result.Fail(simulated.Failure!);
        var result = simulated.Value!;
        var applied = _results.Apply(session.Match, result, MatchBattleKind.Monster, battle.Round, battle.Opponent);
        if (applied.IsFailure) return applied;
        session.Battle = battle with { Result = new ServerBattleSummary(result.Outcome, result.EndReason, result.EndedAt.Value,
            result.PlayerRemainingHealth, result.OpponentRemainingHealth) };
        session.Phase = ServerMatchPhase.MonsterResult;
        return Result.Success();
    }

    // 待领奖励只能在对应战斗结果阶段领取；满盘失败保留奖励与版本。
    public Result ClaimMonsterReward(ServerMatchSession session, Guid battleId, int index)
    {
        if (session.Phase != ServerMatchPhase.MonsterResult || session.Battle is null) return Fail("match.wrong_phase");
        if (session.Battle.Id != battleId) return Fail("match.battle_mismatch");
        var claimed = _rewards.Claim(session.Match, index);
        return claimed.IsFailure ? Result.Fail(claimed.Failure!) : Result.Success();
    }

    // 结果确认不再结算战斗或递增回合；必须先处理待领奖励。
    public Result FinishMonster(ServerMatchSession session, Guid battleId)
    {
        if (session.Phase != ServerMatchPhase.MonsterResult || session.Battle is null) return Fail("match.wrong_phase");
        if (session.Battle.Id != battleId) return Fail("match.battle_mismatch");
        if (session.Match.PendingMonsterRewards.Count != 0) return Fail("match.rewards_pending");
        var generated = _encounters.Generate(session.Match);
        if (generated.IsFailure) return Result.Fail(generated.Failure!);
        session.Battle = null;
        session.Phase = ServerMatchPhase.EncounterChoice;
        return Result.Success();
    }

    // 仅在第8回合确认参战；宿主选择快照，输入必须与当前轮次／版本相符。
    public Result PreparePvp(ServerMatchSession session, Guid battleId, StringName key, ulong seed,
        ServerMatchSession? opponent, Guid? snapshotId)
    {
        if (session.Match.Status != MatchStatus.InProgress || session.Phase != ServerMatchPhase.EncounterChoice
            || session.Match.Progress.Turn != 8) return Fail("match.wrong_phase");
        if (battleId == Guid.Empty || !session.Match.EncounterSchedule.CurrentChoices.Any(choice => choice.Key == key && choice.Kind == EncounterKind.Pvp))
            return Fail("match.pvp_unavailable");
        if (session.Match.PendingMonsterRewards.Count != 0) return Fail("match.rewards_pending");
        var round = session.Match.Progress.Round;
        var test = opponent is null;
        if (test)
        {
            var fixture = _opponents.CreateOpponent(seed);
            fixture.Progress.Round = round;
            fixture.Progress.Turn = 8;
            var generated = _encounters.Generate(fixture);
            if (generated.IsFailure) return Result.Fail(generated.Failure!);
            opponent = new ServerMatchSession(fixture);
        }
        if ((!test && snapshotId is null) || (test && snapshotId is not null)
            || opponent!.Match.Id == session.Match.Id || opponent.Match.Status != MatchStatus.InProgress
            || opponent.Phase != ServerMatchPhase.EncounterChoice || opponent.Match.Progress.Round != round
            || opponent.Match.Progress.Turn != 8 || !opponent.Match.EncounterSchedule.CurrentChoices.Any(choice => choice.Kind == EncounterKind.Pvp))
            return Fail("match.invalid_opponent");
        var selected = _encounters.Select(session.Match, key);
        if (selected.IsFailure) return Result.Fail(selected.Failure!);
        session.Pvp = new ServerPvpBattle(battleId, round, seed, opponent.Match, snapshotId, test);
        session.Phase = ServerMatchPhase.PvpPending;
        return Result.Success();
    }

    // 由后台执行器读取已冻结输入；候选变化仅在任务令牌及数据库事务通过后提交。
    public Result ResolvePvp(ServerMatchSession session, Guid battleId)
    {
        if (session.Phase != ServerMatchPhase.PvpPending || session.Pvp is not { Result: null } battle
            || session.Match.Status != MatchStatus.InProgress) return Fail("match.wrong_phase");
        if (battle.Id != battleId) return Fail("match.battle_mismatch");
        var simulated = _battles.StartBattle(session.Match, battle.Opponent, battle.Seed);
        if (simulated.IsFailure) return Result.Fail(simulated.Failure!);
        var result = simulated.Value!;
        var applied = _results.Apply(session.Match, result, MatchBattleKind.Pvp, battle.Round);
        if (applied.IsFailure) return applied;
        session.Pvp = battle with { Result = new ServerBattleSummary(result.Outcome, result.EndReason, result.EndedAt.Value,
            result.PlayerRemainingHealth, result.OpponentRemainingHealth) };
        session.Phase = ServerMatchPhase.PvpResult;
        return Result.Success();
    }

    // PvP战后待领复用已有奖励用例，不以再次模拟发放奖励。
    public Result ClaimReward(ServerMatchSession session, Guid battleId, int index)
    {
        if (session.Phase == ServerMatchPhase.MonsterResult) return ClaimMonsterReward(session, battleId, index);
        if (session.Match.Status != MatchStatus.InProgress || session.Phase != ServerMatchPhase.PvpResult || session.Pvp is null)
            return Fail("match.wrong_phase");
        if (session.Pvp.Id != battleId) return Fail("match.battle_mismatch");
        var claimed = _rewards.Claim(session.Match, index);
        return claimed.IsFailure ? Result.Fail(claimed.Failure!) : Result.Success();
    }

    // 结果确认后才发新轮收入／回合经验；终局保留结果，不再开启下一轮。
    public Result FinishPvp(ServerMatchSession session, Guid battleId)
    {
        if (session.Match.Status != MatchStatus.InProgress || session.Phase != ServerMatchPhase.PvpResult || session.Pvp is null)
            return Fail("match.wrong_phase");
        if (session.Pvp.Id != battleId) return Fail("match.battle_mismatch");
        if (session.Match.PendingMonsterRewards.Count != 0) return Fail("match.rewards_pending");
        var generated = _encounters.Generate(session.Match);
        if (generated.IsFailure) return Result.Fail(generated.Failure!);
        session.Pvp = null;
        session.Phase = ServerMatchPhase.EncounterChoice;
        return Result.Success();
    }

    private static Result<ServerMatchSession> Reject(string code) => Result<ServerMatchSession>.Fail(new Failure(new StringName(code), code));
    private static Result Fail(string code) => Result.Fail(new Failure(new StringName(code), code));
    private static bool CanEditBoard(ServerMatchSession session) => session.Match.Status == MatchStatus.InProgress
        && session.Phase is ServerMatchPhase.EncounterChoice or ServerMatchPhase.Shop
            or ServerMatchPhase.MonsterPreparation or ServerMatchPhase.MonsterResult or ServerMatchPhase.Event or ServerMatchPhase.PvpResult;
}
