using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Godot;

namespace Project_Star.Application.Online;

// 云端边界返回的只读展示快照，不持有可变对局模型（应用层）。
public sealed record OnlineMatch(Guid MatchId, long Version, string RulesVersion, OnlineState State);
// 当前可达操作与结果摘要；战斗回放不从这些展示字段反推（应用层）。
public sealed record OnlineState(StringName Phase, StringName Status, int Round, int Turn, int Wealth, int Experience,
    int Reputation, int PvpWins, IReadOnlyList<OnlineChoice> Choices, IReadOnlyList<OnlineCard> Cards,
    IReadOnlyList<OnlineReward> Rewards, OnlineShop? Shop, OnlineVisit? Visit, OnlineBattle? Monster, OnlineBattle? Pvp);
// 已由服务器下发的遭遇资格（应用层）。
public sealed record OnlineChoice(StringName Key, string Name, StringName Kind, bool IsCardShop, bool IsChoiceEvent);
// 服务器报价及售罄状态（应用层）。
public sealed record OnlineShop(bool CanRefresh, int RefreshCost, IReadOnlyList<OnlineOffer> Offers);
// 商品身份、价格与购买槽（应用层）。
public sealed record OnlineOffer(int Index, StringName Key, string Name, int Level, int Price, bool Sold);
// 固定访问及已展示选项（应用层）。
public sealed record OnlineVisit(Guid Id, bool Resolved, IReadOnlyList<OnlineOption> Options);
// 事件选项身份与文案（应用层）。
public sealed record OnlineOption(StringName Key, string Name);
// 云端卡牌身份、位置与出售现值，不作为本地战斗输入（应用层）。
public sealed record OnlineCard(Guid Id, StringName Key, string Name, int Level, StringName Zone, int? Start, int Value);
// 固定对手展示与战斗结果摘要（应用层）。
public sealed record OnlineBattle(Guid Id, int Round, int Turn, string OpponentName, bool IsTestOpponent,
    IReadOnlyList<OnlineCard> OpponentCards, OnlineBattleResult? Result);
// 已结算的胜负与双方剩余生命（应用层）。
public sealed record OnlineBattleResult(StringName Outcome, int PlayerHealth, int OpponentHealth);
// 待领列表中的稳定版本槽位（应用层）。
public sealed record OnlineReward(int Index, string Name, int Level);
// HTTP边界结果；网络不确定性以0表示，不输出原始响应或凭据（应用层）。
public sealed record OnlineReply(int Status, StringName? Error, OnlineMatch? Match = null, StringName? BattleStatus = null)
{
    public bool IsSuccess => Status is >= 200 and < 300;
    public bool CanRetry => Status is 0 or 408 or 429 || Status >= 500;
}
// 不包含客户端价格、种子、实例数值或胜负的操作意图（应用层）。
public sealed record OnlineCommand(Guid RequestId, StringName Type, Guid? MatchId = null, long? ExpectedVersion = null,
    StringName? HeroKey = null, StringName? EncounterKey = null, int? OfferIndex = null, Guid? BattleId = null,
    int? RewardIndex = null, Guid? VisitId = null, StringName? OptionKey = null, Guid? CardId = null,
    StringName? TargetZone = null, int? TargetStart = null);
// 网络与会话适配边界；凭证归实现私有持有，界面只能取得展示快照（应用层）。
public interface IOnlineGateway
{
    bool IsAuthenticated { get; }
    Task<OnlineReply> SignInAsync(string username, string password, string? invitation, CancellationToken ct);
    Task<OnlineReply> SignOutAsync(CancellationToken ct);
    Task<OnlineReply> CurrentAsync(CancellationToken ct);
    Task<OnlineReply> MatchAsync(Guid id, CancellationToken ct);
    Task<OnlineReply> BattleAsync(Guid id, CancellationToken ct);
    Task<OnlineReply> SendAsync(OnlineCommand command, CancellationToken ct);
}
