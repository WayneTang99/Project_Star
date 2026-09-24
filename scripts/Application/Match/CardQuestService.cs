using System;
using Project_Star.Application.Board;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Match;

// 将对局事件交给每张现存卡牌的任务条件，保留实例独立进度（应用对局层）。
public sealed class CardQuestService
{
    private readonly BoardService _boardService;

    public CardQuestService(BoardService boardService) =>
        _boardService = boardService ?? throw new ArgumentNullException(nameof(boardService));

    // 战场和备战区的卡牌均可累计进度，只有新解锁时刷新战场数值。
    public void ProcessEvent(MatchSession session, QuestEvent questEvent)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(questEvent);
        var unlocked = false;
        foreach (var card in session.Player.Inventory.Cards)
            unlocked |= card.ApplyQuestEvent(questEvent);
        if (unlocked) _boardService.RefreshQuestAbilities(session);
    }
}
