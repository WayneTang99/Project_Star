using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Domain.Common;

namespace Project_Star.Presentation.Playtest;

public enum MatchPage { HeroSelection, EncounterChoice, Shop, Event, Preparation, BattleResult, MatchEnded }

public sealed record UiAction(string Text, bool Visible = true, bool Enabled = true, string Reason = "");
public sealed record KeyedAction(StringName Key, UiAction Action);
public sealed record ShopItemViewModel(int Index, long Revision, UiAction Action, CardSnapshot Card);

/// <summary>One captured refresh; views never receive mutable sessions or visit contexts.</summary>
public sealed record MatchPageViewModel(
    MatchPage Page, string Title, string Message, MatchSnapshot? Player, MatchSnapshot? Enemy,
    EntityId? SelectedCardId, bool BoardEnabled, bool EnemyVisible,
    IReadOnlyList<KeyedAction> Heroes, IReadOnlyList<KeyedAction> Choices,
    IReadOnlyList<ShopItemViewModel> Offers, IReadOnlyList<KeyedAction> EventOptions,
    long EventRevision, UiAction Refresh, UiAction Battle, UiAction Continue, UiAction Reward);
