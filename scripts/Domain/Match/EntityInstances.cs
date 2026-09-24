using System;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Combat;
using System.Collections.Generic;
using Godot;

namespace Project_Star.Domain.Match;

public sealed class HeroInstance
{
    public HeroInstance(EntityId id, EntityAttributes<HeroIdentityAttributes> attributes)
    {
        Id = id;
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
    }

    public EntityId Id { get; }

    public EntityAttributes<HeroIdentityAttributes> Attributes { get; }
}

public sealed class CardInstance
{
    public CardInstance(
        EntityId id,
        EntityAttributes<CardIdentityAttributes> attributes,
        TagSet tags,
        IReadOnlyList<AbilityDefinition> abilities,
        CardOnSellRewardDefinition? onSellReward,
        IReadOnlyList<CardQuestDefinition>? quests = null)
    {
        Id = id;
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        Tags = tags ?? throw new ArgumentNullException(nameof(tags));
        Abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
        OnSellReward = onSellReward;
        Quests = quests is null ? Array.Empty<CardQuestDefinition>() : new List<CardQuestDefinition>(quests).AsReadOnly();
    }

    public EntityId Id { get; }

    public EntityAttributes<CardIdentityAttributes> Attributes { get; }

    public TagSet Tags { get; }

    public IReadOnlyList<AbilityDefinition> Abilities { get; private set; }

    public CardOnSellRewardDefinition? OnSellReward { get; }

    public IReadOnlyList<CardQuestDefinition> Quests { get; }

    private readonly Dictionary<StringName, int> _questProgress = new();

    public int GetQuestProgress(StringName questKey) =>
        _questProgress.TryGetValue(questKey, out var progress) ? progress : 0;

    public bool IsQuestUnlocked(CardQuestDefinition quest) => GetQuestProgress(quest.Key) >= quest.RequiredCount;

    // 只更新本卡实例的匹配任务，已解锁任务不重复增长。
    internal bool ApplyQuestEvent(QuestEvent questEvent)
    {
        ArgumentNullException.ThrowIfNull(questEvent);
        var unlocked = false;
        foreach (var quest in Quests)
        {
            var progress = GetQuestProgress(quest.Key);
            if (progress >= quest.RequiredCount || !quest.Condition.Matches(questEvent)) continue;
            _questProgress[quest.Key] = progress + 1;
            if (progress + 1 == quest.RequiredCount) unlocked = true;
        }
        return unlocked;
    }

    internal void ReplaceAbilities(IReadOnlyList<AbilityDefinition> abilities) =>
        Abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
}

public sealed class SkillInstance
{
    public SkillInstance(
        EntityId id,
        EntityAttributes<SkillIdentityAttributes> attributes,
        IReadOnlyList<AbilityDefinition> abilities)
    {
        Id = id;
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        Abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
    }

    public EntityId Id { get; }
    public EntityAttributes<SkillIdentityAttributes> Attributes { get; }
    public IReadOnlyList<AbilityDefinition> Abilities { get; private set; }

    internal void ReplaceAbilities(IReadOnlyList<AbilityDefinition> abilities) =>
        Abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
}
