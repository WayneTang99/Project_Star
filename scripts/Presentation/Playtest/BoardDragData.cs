using System;
using Godot;
using Project_Star.Domain.Common;

namespace Project_Star.Presentation.Playtest;

// 拖拽只携带对局身份、卡牌身份与抓取格偏移，不持有可变实体。
public sealed partial class BoardDragData : RefCounted
{
    public Guid MatchId { get; }
    public EntityId CardId { get; }
    public int GrabOffset { get; }
    public int OccupiedSlots { get; }

    // 固定本次抓取信息，跨区仍按格偏移换算。
    public BoardDragData(Guid matchId, EntityId cardId, int grabOffset, int occupiedSlots)
    { MatchId = matchId; CardId = cardId; GrabOffset = grabOffset; OccupiedSlots = occupiedSlots; }
}
