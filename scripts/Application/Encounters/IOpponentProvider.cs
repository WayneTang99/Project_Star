using Project_Star.Domain.Match;
using Project_Star.Domain.Definitions;

namespace Project_Star.Application.Encounters;

// 应用层对手来源接口，具体创建方式由基础设施适配。
public interface IOpponentProvider
{
    // 按显式随机种子创建 PvP 对手。
    MatchSession CreateOpponent(ulong seed);

    // 按正式怪物定义创建怪物对手。
    MatchSession CreateMonsterOpponent(ulong seed, MonsterDefinition definition);
}
