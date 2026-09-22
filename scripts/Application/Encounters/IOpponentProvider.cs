using Project_Star.Domain.Match;
using Project_Star.Domain.Definitions;

namespace Project_Star.Application.Encounters;

// 创建试玩战斗对手的应用层接口。
public interface IOpponentProvider
{
    // 创建本地 PvP 测试对手。
    MatchSession CreateOpponent(ulong seed);

    // 按正式怪物定义创建怪物对手。
    MatchSession CreateMonsterOpponent(ulong seed, MonsterDefinition definition);
}
