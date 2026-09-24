using Godot;

namespace Project_Star.Domain.Match;

public enum MonsterRewardKind
{
    Card = 0,
    Skill = 1,
}

// 战胜怪物后待玩家领取的单件卡牌或技能（对局领域层）。
public sealed record PendingMonsterReward(
    MonsterRewardKind Kind,
    StringName Key,
    string DisplayName,
    int Level);
