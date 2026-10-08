using System;
using System.Linq;
using Project_Star.Application.Board;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Match;

// 将对局事件交给每张现存卡牌的任务条件，保留实例独立进度（应用对局层）。
public sealed class CardQuestService
{
    private readonly BoardService? _boardService;

    public CardQuestService(BoardService? boardService = null) => _boardService = boardService;

    // 战场和备战区的卡牌均可累计进度，只有新解锁时刷新战场数值。
    public void ProcessEvent(MatchSession session, QuestEvent questEvent)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(questEvent);
        var unlocked = false;
        foreach (var card in session.Player.Inventory.Cards)
        {
            if (questEvent is CardAcquiredQuestEvent acquired && acquired.ExcludedCardId == card.Id) continue;
            unlocked |= card.ApplyQuestEvent(questEvent);
        }
        if (unlocked)
        {
            if (_boardService is not null) _boardService.RefreshQuestAbilities(session);
            else new CardQuestBonusService().Recalculate(session);
        }
    }

    // 只接收己方真实实例在冻结战斗结果中的任务进度，不重新模拟发动。
    public void ApplyBattleProgress(MatchSession session, BattleResult battle)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(battle);
        var unlocked = false;
        foreach (var change in battle.Events.OfType<CardQuestProgressChangedEvent>())
        {
            if (change.Side != SideId.Player || !change.Persists) continue;
            var card = session.Player.Inventory.Find(change.CardId);
            if (card is not null) unlocked |= card.SetQuestProgress(change.QuestKey, change.Progress);
        }
        if (!unlocked) return;
        if (_boardService is not null) _boardService.RefreshQuestAbilities(session);
        else new CardQuestBonusService().Recalculate(session);
    }
}
