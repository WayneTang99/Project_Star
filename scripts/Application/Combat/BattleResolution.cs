using Project_Star.Application.Match;
using Project_Star.Domain.Combat;

namespace Project_Star.Application.Combat;

// 一次战斗用例的冻结输出；播放与查看无需重新访问或结算对局。
public sealed record BattleResolution(MatchSnapshot PlayerBefore, MatchSnapshot OpponentBefore,
    BattleResult Result, MatchSnapshot PlayerAfter);
