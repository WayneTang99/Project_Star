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
        IReadOnlyList<CardQuestDefinition>? quests = null,
        IReadOnlyList<TaggedCardSaleAttributeBonus>? saleAttributeBonuses = null,
        IReadOnlyList<BattleVictoryAttributeBonus>? battleVictoryBonuses = null)
    {
        Id = id;
        _attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        _tags = tags ?? throw new ArgumentNullException(nameof(tags));
        Abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
        OnSellReward = onSellReward;
        SaleAttributeBonuses = saleAttributeBonuses ?? Array.Empty<TaggedCardSaleAttributeBonus>();
        BattleVictoryBonuses = battleVictoryBonuses ?? Array.Empty<BattleVictoryAttributeBonus>();
        Quests = quests is null ? Array.Empty<CardQuestDefinition>() : new List<CardQuestDefinition>(quests).AsReadOnly();
        _gemSockets = new GemIdentityAttributes?[attributes.Identity.GemSocketCount];
        GemSockets = Array.AsReadOnly(_gemSockets);
    }

    public EntityId Id { get; }

    private EntityAttributes<CardIdentityAttributes> _attributes;
    public EntityAttributes<CardIdentityAttributes> Attributes => _attributes;

    private TagSet _tags;
    public TagSet Tags => _tags;
    private readonly GemIdentityAttributes?[] _gemSockets;
    public IReadOnlyList<GemIdentityAttributes?> GemSockets { get; }

    // 镶嵌只允许写入空孔，升级不重建宝石数组。
    internal void SocketGem(int index, GemIdentityAttributes gem)
    {
        ArgumentNullException.ThrowIfNull(gem);
        if (index < 0 || index >= _gemSockets.Length || _gemSockets[index] is not null)
            throw new InvalidOperationException("Gem socket is unavailable.");
        _gemSockets[index] = gem;
    }

    public IReadOnlyList<AbilityDefinition> Abilities { get; private set; }

    public CardOnSellRewardDefinition? OnSellReward { get; private set; }
    public decimal CooldownMultiplier { get; private set; } = 1m;
    public IReadOnlyList<TaggedCardSaleAttributeBonus> SaleAttributeBonuses { get; private set; }
    public IReadOnlyList<BattleVictoryAttributeBonus> BattleVictoryBonuses { get; private set; }

    public IReadOnlyList<CardQuestDefinition> Quests { get; }

    private readonly Dictionary<StringName, int> _questProgress = new();

    public int GetQuestProgress(StringName questKey) =>
        _questProgress.TryGetValue(questKey, out var progress) ? progress : 0;

    public bool IsQuestUnlocked(CardQuestDefinition quest) => GetQuestProgress(quest.Key) >= quest.RequiredCount;

    // 只更新本卡实例的匹配任务，已解锁任务不重复增长。
    internal bool ApplyQuestEvent(QuestEvent questEvent)
    {
        ArgumentNullException.ThrowIfNull(questEvent);
        if (questEvent is SourceCardActivatedQuestEvent activated && activated.CardId != Id) return false;
        var unlocked = false;
        foreach (var quest in Quests)
        {
            var progress = GetQuestProgress(quest.Key);
            if (progress >= quest.RequiredCount || !quest.Condition.Matches(questEvent)) continue;
            unlocked |= SetQuestProgress(quest.Key, progress + 1);
        }
        return unlocked;
    }

    // 从冻结战斗结果更新任务，进度只增不减，身份奖励只在首次解锁时应用。
    internal bool SetQuestProgress(StringName questKey, int progress)
    {
        var quest = System.Linq.Enumerable.FirstOrDefault(Quests, item => item.Key == questKey);
        if (quest is null || progress <= GetQuestProgress(questKey)) return false;
        var previouslyUnlocked = IsQuestUnlocked(quest);
        _questProgress[questKey] = Math.Min(progress, quest.RequiredCount);
        if (previouslyUnlocked || !IsQuestUnlocked(quest)) return false;
        if (quest.UnlockedElementKeys.Count > 0)
        {
            var identity = Attributes.Identity;
            _attributes = new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
                identity.Key, identity.DisplayName, identity.FactionKey, identity.Size, quest.UnlockedElementKeys,
                identity.SetKey, identity.Illustration, identity.DescriptionEntries, identity.GemSocketCount),
                Attributes.Persistent, Attributes.BaseCombat);
        }
        if (quest.UnlockedTags.Count > 0)
            _tags = new TagSet(System.Linq.Enumerable.Concat(Tags, quest.UnlockedTags));
        return true;
    }

    internal void ReplaceAbilities(IReadOnlyList<AbilityDefinition> abilities) =>
        Abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));

    internal void ReplaceSaleAttributeBonuses(IReadOnlyList<TaggedCardSaleAttributeBonus>? bonuses) =>
        SaleAttributeBonuses = bonuses ?? Array.Empty<TaggedCardSaleAttributeBonus>();

    internal void ReplaceBattleVictoryBonuses(IReadOnlyList<BattleVictoryAttributeBonus>? bonuses) =>
        BattleVictoryBonuses = bonuses ?? Array.Empty<BattleVictoryAttributeBonus>();

    // 累乘对局内永久冷却倍率，升级不重置已获得的缩减。
    internal void ReduceCooldown(int percent)
    {
        if (percent is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(percent));
        CooldownMultiplier *= (100 - percent) / 100m;
    }

    internal void ReplaceOnSellReward(CardOnSellRewardDefinition? reward) => OnSellReward = reward;
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
