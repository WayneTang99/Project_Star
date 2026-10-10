using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Combat;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

public enum MatchPage { HeroSelection, EncounterChoice, Shop, Event, Preparation, BattleResult, MatchEnded, BattlePlayback }

public sealed record UiAction(string Text, bool Visible = true, bool Enabled = true, string Reason = "");
public sealed record KeyedAction(StringName Key, UiAction Action)
{
    public StringName Illustration { get; init; } = new("");
    public int ShopLevel { get; init; }
    public int Level { get; init; }
    public string Subtitle { get; init; } = "";
    public HeroSelectionDetails? Hero { get; init; }
}

// 选角页读取的英雄身份与初始数值副本（表现层）。
public sealed record HeroSelectionDetails(string DisplayName, string Title, int Level, int Income,
    int MaxHealth, int MaxMana, int Mana, int ManaRegen, int Armor, int HealthRegen)
{
    internal static HeroSelectionDetails From(HeroDefinition definition)
    {
        var attributes = definition.Attributes;
        return new(attributes.Identity.DisplayName, attributes.Identity.Title,
            Math.Max(1, attributes.Persistent.GetFinalValue(GameAttributeKeys.Level)),
            attributes.Persistent.GetFinalValue(GameAttributeKeys.Income),
            attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth),
            attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxMana),
            attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Mana),
            attributes.BaseCombat.GetFinalValue(GameAttributeKeys.ManaRegen),
            attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Armor),
            attributes.BaseCombat.GetFinalValue(GameAttributeKeys.HealthRegen));
    }
}
public sealed record ShopItemViewModel(int Index, long Revision, UiAction Action, CardSnapshot Card)
{
    public int Price { get; init; }
    public int MergeLevel { get; init; }
}
public sealed record RewardItemViewModel(int Index, long Revision, string Text, CardSnapshot? Card, string Details)
{
    public int Level { get; init; }
}

// 任务定义与冻结进度按key关联的只读投影；回放不读取战后的实例。
public sealed record CardQuestViewModel(StringName Key, string Condition, string Reward, int Progress, int RequiredCount, bool Unlocked)
{
    internal static IReadOnlyList<CardQuestViewModel> From(CardSnapshot card, CardBattleSnapshot? battle) =>
        Array.AsReadOnly(card.QuestDefinitions.Select(quest =>
        {
            var replay = battle?.Quests.FirstOrDefault(value => value.Key == quest.Key);
            var current = card.Quests.FirstOrDefault(value => value.Key == quest.Key);
            return new CardQuestViewModel(quest.Key, CardDisplayAdapter.QuestCondition(quest), CardDisplayAdapter.QuestReward(card, quest),
                replay?.Progress ?? current?.Progress ?? 0, quest.RequiredCount, replay?.Unlocked ?? current?.Unlocked ?? false);
        }).ToArray());
}

/// <summary>One captured refresh; views never receive mutable sessions or visit contexts.</summary>
public sealed record MatchPageViewModel(
    MatchPage Page, string Title, string Message, MatchSnapshot? Player, MatchSnapshot? Enemy,
    EntityId? SelectedCardId, bool BoardEnabled, bool EnemyVisible,
    IReadOnlyList<KeyedAction> Heroes, IReadOnlyList<KeyedAction> Choices,
    IReadOnlyList<ShopItemViewModel> Offers, IReadOnlyList<KeyedAction> EventOptions,
    long EventRevision, UiAction Refresh, UiAction Battle, UiAction Continue, UiAction Reward)
{
    public UiAction Sell { get; init; } = new("出售", false);
    public int ShopLevel { get; init; }
    public string BattleLog { get; init; } = "";
    public StringName ContextIllustration { get; init; } = new("");
    public int EncounterLevel { get; init; }
    // 访问中的遭遇保持进入前的轮回合，领域进度可以已推进到下一回合。
    public int DisplayRound { get; init; }
    public int DisplayTurn { get; init; }
    public BattlePlaybackViewModel? Playback { get; init; }
    public IReadOnlyList<RewardItemViewModel> Rewards { get; init; } = Array.Empty<RewardItemViewModel>();
}
