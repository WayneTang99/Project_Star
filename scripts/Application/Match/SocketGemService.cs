using System;
using System.Linq;
using Godot;
using Project_Star.Application.Common;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Match;

// 已获授权的渠道提交镶嵌目标；宝石获取与费用由该渠道用例负责。
public sealed record SocketGemCommand(Guid MatchId, EntityId CardId, int SocketIndex, GemDefinition Gem);

// 镶嵌用例校验对局、归属和空孔，不提供取下或替换操作（应用层）。
public sealed class SocketGemService
{
    // 将渠道提供的宝石写入己方卡牌实例；失败不改变任何宝石孔。
    public Result Execute(MatchSession session, SocketGemCommand command)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(command);
        if (command.MatchId != session.Id)
            return Fail("gem.stale_match", "镶嵌请求不属于当前对局。");
        if (session.Status != MatchStatus.InProgress)
            return Fail("gem.match_ended", "对局已结束，无法镶嵌。");
        var card = session.Player.Inventory.Cards.FirstOrDefault(item => item.Id == command.CardId);
        if (card is null) return Fail("gem.card_missing", "镶嵌目标卡牌不存在。");
        if (command.Gem is null) return Fail("gem.missing", "未提供宝石。");
        if (command.SocketIndex < 0 || command.SocketIndex >= card.GemSockets.Count)
            return Fail("gem.invalid_socket", "宝石孔不存在。");
        if (card.GemSockets[command.SocketIndex] is not null)
            return Fail("gem.socket_occupied", "该宝石孔已镶嵌，不能替换。");
        card.SocketGem(command.SocketIndex, command.Gem.Attributes.Identity);
        return Result.Success();
    }

    private static Result Fail(string key, string message) => Result.Fail(new Failure(new StringName(key), message));
}
