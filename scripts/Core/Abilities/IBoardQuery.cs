using System.Collections.Generic;
using Project_Star.Core.States;

namespace Project_Star.Core.Abilities;

// 棋盘查询接口（核心层）：能力/效果按需查询棋盘信息，由战斗层实现。
// 能力不直接操作棋盘，通过此接口间接查询，遵循迪米特法则。
public interface IBoardQuery
{
    // 获取指定英雄所有战场区卡牌的战斗状态
    IReadOnlyList<CardBattleState> GetBattlefieldCards(HeroBattleState hero);
    // 获取指定英雄所有备战区卡牌的战斗状态
    IReadOnlyList<CardBattleState> GetBenchCards(HeroBattleState hero);
    // 获取指定英雄的所有卡牌（战场 + 备战）
    IReadOnlyList<CardBattleState> GetAllCards(HeroBattleState hero);
}
