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

// 卡牌自身的一次成功主动发动，包含多重额外发动（领域定义层）。
public sealed record SourceCardActivatedQuestEvent(EntityId CardId) : QuestEvent;

// 累计自身发动的可复用任务条件（领域定义层）。
public sealed record SourceCardActivationQuestConditionDefinition : QuestConditionDefinition
{
    public override bool Matches(QuestEvent questEvent) => questEvent is SourceCardActivatedQuestEvent;
}

// 一次成功获得卡牌，元素取自输入定义；新建来源不计入自己的本次拾取。
public sealed record CardAcquiredQuestEvent(IReadOnlyList<StringName> ElementKeys, EntityId? ExcludedCardId = null) : QuestEvent;

// 一次成功出售卡牌，标签取自被出售的真实实例（领域定义层）。
public sealed record CardSoldQuestEvent(IReadOnlyList<StringName> Tags) : QuestEvent;

// 按标签累计出售卡牌的可复用任务条件（领域定义层）。
public sealed record SoldTaggedCardQuestConditionDefinition : QuestConditionDefinition
{
    public SoldTaggedCardQuestConditionDefinition(StringName requiredTag)
    {
        if (requiredTag.IsEmpty) throw new ArgumentException("Sale quest tag cannot be empty.", nameof(requiredTag));
        RequiredTag = requiredTag;
    }

    public StringName RequiredTag { get; }
    public override bool Matches(QuestEvent questEvent) => questEvent is CardSoldQuestEvent sold
        && System.Linq.Enumerable.Contains(sold.Tags, RequiredTag);
}

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
        IReadOnlyList<EffectDefinition>? additionalActiveEffects = null,
        IReadOnlyList<StringName>? unlockedElementKeys = null,
        IReadOnlyList<StringName>? unlockedTags = null)
    {
        if (key.IsEmpty) throw new ArgumentException("Quest key cannot be empty.", nameof(key));
        Condition = condition ?? throw new ArgumentNullException(nameof(condition));
        if (requiredCount < 1) throw new ArgumentOutOfRangeException(nameof(requiredCount));
        ArgumentNullException.ThrowIfNull(abilities);
        if (abilities.Count == 0 && additionalActiveEffects is not { Count: > 0 }
            && unlockedElementKeys is not { Count: > 0 } && unlockedTags is not { Count: > 0 })
            throw new ArgumentException("A quest must unlock an ability or active effects.", nameof(abilities));
        if (unlockedTags is not null && System.Linq.Enumerable.Any(unlockedTags, tag => tag.IsEmpty))
            throw new ArgumentException("Unlocked tags cannot contain empty keys.", nameof(unlockedTags));
        foreach (var ability in abilities)
            if (ability.Activation == AbilityActivation.PassiveWhileEnabled
                && ability.Target != AbilityTarget.SelfCard)
                throw new ArgumentException("Persistent card quest abilities must target their source card.", nameof(abilities));
        Key = key;
        RequiredCount = requiredCount;
        Abilities = new List<AbilityDefinition>(abilities).AsReadOnly();
        AdditionalActiveEffects = additionalActiveEffects is null ? Array.Empty<EffectDefinition>()
            : new List<EffectDefinition>(additionalActiveEffects).AsReadOnly();
        UnlockedElementKeys = unlockedElementKeys is null ? Array.Empty<StringName>() : GameElements.Normalize(unlockedElementKeys);
        UnlockedTags = unlockedTags is null ? Array.Empty<StringName>() : new List<StringName>(unlockedTags).AsReadOnly();
    }

    public StringName Key { get; }
    public QuestConditionDefinition Condition { get; }
    public int RequiredCount { get; }
    public IReadOnlyList<AbilityDefinition> Abilities { get; }
    public IReadOnlyList<EffectDefinition> AdditionalActiveEffects { get; }
    public IReadOnlyList<StringName> UnlockedElementKeys { get; }
    public IReadOnlyList<StringName> UnlockedTags { get; }
}
