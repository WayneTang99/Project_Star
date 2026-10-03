using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Definitions;

// 任务进度可识别的对局事件（领域定义层）。
public abstract record QuestEvent;

// 一场战斗获胜的任务事件（领域定义层）。
public sealed record BattleWonQuestEvent : QuestEvent;

// 一次成功获得卡牌，元素取自输入定义；新建来源不计入自己的本次拾取。
public sealed record CardAcquiredQuestEvent(IReadOnlyList<StringName> ElementKeys, EntityId? ExcludedCardId = null) : QuestEvent;

// 按元素匹配成功拾取，包括合并获得（领域定义层）。
public sealed record AcquiredElementCardQuestConditionDefinition : QuestConditionDefinition
{
    public AcquiredElementCardQuestConditionDefinition(StringName elementKey)
    {
        _ = GameElements.Normalize([elementKey]);
        ElementKey = elementKey;
    }

    public StringName ElementKey { get; }
    public override bool Matches(QuestEvent questEvent) => questEvent is CardAcquiredQuestEvent acquired
        && System.Linq.Enumerable.Contains(acquired.ElementKeys, ElementKey);
}

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
        IReadOnlyList<AbilityDefinition> abilities,
        IReadOnlyList<EffectDefinition>? additionalActiveEffects = null)
    {
        if (key.IsEmpty) throw new ArgumentException("Quest key cannot be empty.", nameof(key));
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        if (requiredCount < 1) throw new ArgumentOutOfRangeException(nameof(requiredCount));
        ArgumentNullException.ThrowIfNull(abilities);
        if (abilities.Count == 0 && additionalActiveEffects is not { Count: > 0 })
            throw new ArgumentException("A quest must unlock an ability or active effects.", nameof(abilities));
        foreach (var ability in abilities)
            if (ability.Activation == AbilityActivation.PassiveWhileEnabled
                && ability.Target != AbilityTarget.SelfCard)
                throw new ArgumentException("Persistent card quest abilities must target their source card.", nameof(abilities));
        Key = key;
        RequiredCount = requiredCount;
        Abilities = new List<AbilityDefinition>(abilities).AsReadOnly();
        AdditionalActiveEffects = additionalActiveEffects is null ? Array.Empty<EffectDefinition>()
            : new List<EffectDefinition>(additionalActiveEffects).AsReadOnly();
    }

    public StringName Key { get; }
    public QuestConditionDefinition Condition { get; }
    public int RequiredCount { get; }
    public IReadOnlyList<AbilityDefinition> Abilities { get; }
    public IReadOnlyList<EffectDefinition> AdditionalActiveEffects { get; }
}
