using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;

namespace Project_Star.Domain.Definitions;

// 任务进度可识别的对局事件（领域定义层）。
public abstract record QuestEvent;

// 一场战斗获胜的任务事件（领域定义层）。
public sealed record BattleWonQuestEvent : QuestEvent;

// 可复用的任务条件判断（领域定义层）。
public abstract record QuestConditionDefinition
{
    public abstract bool Matches(QuestEvent questEvent);
}

// 任意一场战斗胜利条件（领域定义层）。
public sealed record BattleVictoryQuestConditionDefinition : QuestConditionDefinition
{
    public override bool Matches(QuestEvent questEvent) => questEvent is BattleWonQuestEvent;
}

// 卡牌任务达到指定次数后解锁通用能力（领域定义层）。
public sealed class CardQuestDefinition
{
    public CardQuestDefinition(
        StringName key,
        QuestConditionDefinition condition,
        int requiredCount,
        IReadOnlyList<AbilityDefinition> abilities)
    {
        if (key.IsEmpty) throw new ArgumentException("Quest key cannot be empty.", nameof(key));
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        if (requiredCount < 1) throw new ArgumentOutOfRangeException(nameof(requiredCount));
        ArgumentNullException.ThrowIfNull(abilities);
        if (abilities.Count == 0) throw new ArgumentException("A quest must unlock an ability.", nameof(abilities));
        foreach (var ability in abilities)
            if (ability.Activation == AbilityActivation.PassiveWhileEnabled
                && ability.Target != AbilityTarget.SelfCard)
                throw new ArgumentException("Persistent card quest abilities must target their source card.", nameof(abilities));
        Key = key;
        RequiredCount = requiredCount;
        Abilities = new List<AbilityDefinition>(abilities).AsReadOnly();
    }

    public StringName Key { get; }
    public QuestConditionDefinition Condition { get; }
    public int RequiredCount { get; }
    public IReadOnlyList<AbilityDefinition> Abilities { get; }
}
