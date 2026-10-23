using System;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Infrastructure.Encounters;
using Project_Star.Infrastructure.Server;
using Project_Star.Presentation.Verification;

namespace Project_Star.Presentation.Server;

// 常驻无图形规则进程的白名单入口（表现层服务组装）；持久化由 API 在事务中提交。
public sealed partial class ServerRules : Node
{
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    // 在 Godot 主线程串行执行纯规则，不创建 UI 或加载图形资源。
    public override void _Ready()
    {
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            var registry = DefinitionRegistry.Scan(typeof(ServerRules).Assembly);
            if (OS.GetCmdlineUserArgs().Contains("--verify-save"))
            {
                MatchSaveChecks.Run(registry);
                Console.WriteLine("SAVE_VERIFICATION_PASSED");
                GetTree().Quit();
                return;
            }
            var version = OS.GetCmdlineUserArgs().Single(arg => arg.StartsWith("--rules-version=", StringComparison.Ordinal))[16..];
            if (version.Length != 64 || !version.All(char.IsAsciiHexDigit)) throw new InvalidOperationException("Invalid rules version.");
            var service = new ServerMatchService(registry, new LocalTestOpponentProvider(registry));
            var codec = new MatchSaveCodec(registry, version);
            Console.WriteLine("RULES_READY=" + version);
            while (Console.ReadLine() is { } line)
            {
                Request? request = null;
                try
                {
                    if (line.Length > 4 * 1024 * 1024) throw new InvalidOperationException("Request too large.");
                    request = JsonSerializer.Deserialize<Request>(line, Json) ?? throw new JsonException();
                    if (request.RulesVersion != version) throw new InvalidOperationException("Rule version mismatch.");
                    var response = Execute(request, service, codec, registry);
                    Console.WriteLine("RULES_RESPONSE=" + JsonSerializer.Serialize(response, Json));
                }
                catch (Exception exception)
                {
                    // 不把存档或输入正文写进日志；格式损坏不会变成部分成功。
                    Console.Error.WriteLine("RULES_REQUEST_FAILED=" + exception.GetType().Name);
                    Console.WriteLine("RULES_RESPONSE=" + JsonSerializer.Serialize(new Reply(request?.RequestId ?? Guid.Empty,
                        false, "rules.request_failed", null, null), Json));
                }
            }
            GetTree().Quit();
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine("RULES_START_FAILED=" + exception.GetType().Name);
            GetTree().Quit(1);
        }
    }

    private static Reply Execute(Request request, ServerMatchService service, MatchSaveCodec codec, IDefinitionCatalog registry)
    {
        ServerMatchSession run;
        if (request.Action == "create")
        {
            var created = service.Create(new StringName(request.HeroKey ?? ""), ulong.Parse(request.Seed!, CultureInfo.InvariantCulture));
            if (created.IsFailure) return new Reply(request.RequestId, false, created.Failure!.Code.ToString(), null, null);
            run = created.Value!;
        }
        else
        {
            run = codec.Decode(request.Save ?? throw new JsonException("Save required."));
            var result = request.Action switch
            {
                "select_shop" => service.SelectShop(run, new StringName(request.EncounterKey ?? "")),
                "buy" => service.Buy(run, request.OfferIndex ?? -1),
                "move_card" => service.MoveCard(run, new EntityId(request.CardId ?? Guid.Empty),
                    request.TargetZone switch { "Battlefield" => BoardZone.Battlefield, "Bench" => BoardZone.Bench, _ => (BoardZone)(-1) }, request.TargetStart ?? -1),
                "sell_card" => service.SellCard(run, new EntityId(request.CardId ?? Guid.Empty)),
                "refresh_shop" => service.RefreshShop(run),
                "select_event" => service.SelectEvent(run, new StringName(request.EncounterKey ?? "")),
                "resolve_event" => service.ResolveEvent(run, request.VisitId ?? Guid.Empty, new StringName(request.OptionKey ?? "")),
                "leave_event" => service.LeaveEvent(run, request.VisitId ?? Guid.Empty),
                "leave_shop" => service.LeaveShop(run),
                "select_monster" => service.SelectMonster(run, new StringName(request.EncounterKey ?? "")),
                "resolve_monster" => service.ResolveMonster(run, request.BattleId ?? Guid.Empty),
                "claim_monster_reward" => service.ClaimMonsterReward(run, request.BattleId ?? Guid.Empty, request.RewardIndex ?? -1),
                "finish_monster" => service.FinishMonster(run, request.BattleId ?? Guid.Empty),
                "prepare_pvp" => service.PreparePvp(run, request.BattleId ?? Guid.Empty, new StringName(request.EncounterKey ?? ""),
                    ulong.Parse(request.Seed!, CultureInfo.InvariantCulture), request.OpponentSave is null ? null : codec.Decode(request.OpponentSave), request.OpponentSnapshotId),
                "resolve_pvp" => service.ResolvePvp(run, request.BattleId ?? Guid.Empty),
                "finish_pvp" => service.FinishPvp(run, request.BattleId ?? Guid.Empty),
                "claim_reward" => service.ClaimReward(run, request.BattleId ?? Guid.Empty, request.RewardIndex ?? -1),
                "view" => Project_Star.Application.Common.Result.Success(),
                _ => Project_Star.Application.Common.Result.Fail(new Project_Star.Application.Common.Failure(new StringName("rules.unknown_command"), "Unknown command")),
            };
            if (result.IsFailure) return new Reply(request.RequestId, false, result.Failure!.Code.ToString(), null, null);
        }
        var match = run.Match;
        var view = new
        {
            matchId = match.Id, phase = run.Phase.ToString(), status = match.Status.ToString(),
            round = match.Progress.Round, turn = match.Progress.Turn, wealth = match.Player.Wealth,
            experience = match.Player.Experience, reputation = match.Player.Reputation,
            pvpWins = match.Progress.PvpWins,
            matchSummary = match.Summary is not { } summary ? null : new
            {
                status = summary.Status.ToString(), completedRounds = summary.CompletedRounds, pvpWins = summary.PvpWins,
                finalReputation = summary.FinalReputation, finalWealth = summary.FinalWealth,
            },
            pvp = run.Pvp is not { } pvp ? null : new
            {
                id = pvp.Id, round = pvp.Round, opponentSnapshotId = pvp.OpponentSnapshotId, isTestOpponent = pvp.IsTestOpponent,
                opponent = new
                {
                    heroKey = pvp.Opponent.Player.Hero!.Attributes.Identity.Key.ToString(), name = pvp.Opponent.Player.Hero.Attributes.Identity.DisplayName,
                    cards = pvp.Opponent.Player.Inventory.Cards.Select(card => new
                    {
                        id = card.Id.Value, key = card.Attributes.Identity.Key.ToString(), level = card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level),
                        zone = pvp.Opponent.Board.Locate(card.Id)?.Zone.ToString(), start = pvp.Opponent.Board.Locate(card.Id)?.Placement.Start,
                    }).ToArray(),
                },
                result = pvp.Result is not { } pvpResult ? null : new
                {
                    outcome = pvpResult.Outcome.ToString(), endReason = pvpResult.EndReason.ToString(), endedAt = pvpResult.EndedAt,
                    playerRemainingHealth = pvpResult.PlayerRemainingHealth, opponentRemainingHealth = pvpResult.OpponentRemainingHealth,
                },
            },
            pendingBattleMaxHealthBonus = match.PendingBattleMaxHealthBonus,
            eventVisit = run.Event is not { } visit ? null : new
            {
                id = visit.Id, key = visit.EncounterKey.ToString(), round = visit.Round, turn = visit.Turn,
                level = visit.Options.Level, resolved = visit.Options.IsResolved,
                options = visit.Options.Options.Select(option => new { key = option.Key.ToString(), name = option.DisplayName }).ToArray(),
                result = visit.Result is not { } outcome ? null : new
                {
                    optionKey = outcome.OptionKey.ToString(), wealthGained = outcome.WealthGained, wealthSpent = outcome.WealthSpent,
                    grantedCardId = outcome.GrantedCardId, grantedCardKey = outcome.GrantedCardKey?.ToString(), grantedCardLevel = outcome.GrantedCardLevel,
                    cardRewardSkipped = outcome.CardRewardSkipped, pendingBattleMaxHealthBonus = outcome.PendingBattleMaxHealthBonus,
                    monsterEncounterSkipped = outcome.MonsterEncounterSkipped,
                    changes = outcome.Changes.Select(change => new { key = change.AttributeKey.ToString(), amount = change.Amount, currentValue = change.CurrentValue }).ToArray(),
                    cardChanges = outcome.CardChanges.Select(change => new { key = change.AttributeKey.ToString(), amount = change.Amount, cardCount = change.CardCount }).ToArray(),
                },
            },
            skills = match.Player.Skills.Items.Select(skill => new
            {
                id = skill.Id.Value, key = skill.Attributes.Identity.Key.ToString(), name = skill.Attributes.Identity.DisplayName,
                level = skill.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level),
            }).ToArray(),
            pendingRewards = match.PendingMonsterRewards.Select((reward, index) => new
            {
                index, kind = reward.Kind.ToString(), key = reward.Key.ToString(), name = reward.DisplayName, level = reward.Level,
            }).ToArray(),
            battle = run.Battle is not { } battle ? null : new
            {
                id = battle.Id, monsterKey = battle.MonsterKey.ToString(), round = battle.Round, turn = battle.Turn,
                sourceEncounterKey = battle.SourceEncounterKey?.ToString(),
                opponent = new
                {
                    id = battle.Opponent.Player.Hero!.Id.Value, name = battle.Opponent.Player.Hero.Attributes.Identity.DisplayName,
                    cards = battle.Opponent.Player.Inventory.Cards.Select(card => new
                    {
                        id = card.Id.Value, key = card.Attributes.Identity.Key.ToString(), name = card.Attributes.Identity.DisplayName,
                        level = card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level),
                        start = battle.Opponent.Board.Locate(card.Id)?.Placement.Start,
                    }).ToArray(),
                },
                result = battle.Result is not { } monsterSummary ? null : new
                {
                    outcome = monsterSummary.Outcome.ToString(), endReason = monsterSummary.EndReason.ToString(), endedAt = monsterSummary.EndedAt,
                    playerRemainingHealth = monsterSummary.PlayerRemainingHealth, opponentRemainingHealth = monsterSummary.OpponentRemainingHealth,
                },
            },
            hero = new { key = match.Player.Hero!.Attributes.Identity.Key.ToString(), name = match.Player.Hero.Attributes.Identity.DisplayName },
            choices = match.EncounterSchedule.CurrentChoices.Select(choice => new
            {
                key = choice.Key.ToString(), name = choice.DisplayName, kind = choice.Kind.ToString(),
                level = choice.Level, shopLevel = choice.ShopLevel,
                isCardShop = registry.Encounters.TryGetValue(choice.Key, out var definition) && definition is CardShopEncounterDefinition,
                isChoiceEvent = registry.Encounters.TryGetValue(choice.Key, out var eventDefinition) && eventDefinition is ChoiceEncounterDefinition,
            }).ToArray(),
            cards = match.Player.Inventory.Cards.Select(card => new
            {
                id = card.Id.Value, key = card.Attributes.Identity.Key.ToString(), name = card.Attributes.Identity.DisplayName,
                level = card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level),
                value = card.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value),
                cooldownMultiplier = card.CooldownMultiplier,
                zone = match.Board.Locate(card.Id)?.Zone.ToString(), start = match.Board.Locate(card.Id)?.Placement.Start,
            }).ToArray(),
            shop = run.Shop is null ? null : new
            {
                key = run.ShopKey!.ToString(), hasRefreshed = run.Shop.HasRefreshed,
                refreshCost = run.Shop.RefreshCost, canRefresh = service.CanRefreshShop(run),
                offers = run.Shop.Offers.Select((offer, index) => new
                {
                    index, key = offer.Definition.Attributes.Identity.Key.ToString(),
                    name = offer.Definition.Attributes.Identity.DisplayName, level = offer.Level,
                    price = offer.Price, sold = offer.IsSold,
                }).ToArray(),
            },
        };
        return new Reply(request.RequestId, true, null, request.Action == "view" ? null : codec.Encode(run), view);
    }

    // 私有进程协议；Save 只由数据库适配提供，HTTP 客户端不能上传它。
    private sealed record Request(Guid RequestId, string Action, string RulesVersion, string? HeroKey,
        string? Seed, string? Save, string? EncounterKey, int? OfferIndex, Guid? BattleId, int? RewardIndex,
        Guid? CardId, string? TargetZone, int? TargetStart, Guid? VisitId, string? OptionKey, string? OpponentSave, Guid? OpponentSnapshotId);
    // 候选状态只有在数据库事务提交后才成为权威状态。
    private sealed record Reply(Guid RequestId, bool Ok, string? Error, string? Save, object? View);
}
