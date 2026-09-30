using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Application.Encounters;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.Playtest;

internal static class PlaytestText
{
    public static string FormatEventResult(EncounterOptionResult result)
    {
        var changes = new List<string>();
        foreach (var change in result.Changes)
            changes.Add($"{change.AttributeKey} +{change.Amount}（当前 {change.CurrentValue}）");
        foreach (var change in result.CardChanges ?? Array.Empty<EncounterCardAttributeChange>())
            changes.Add($"己方战场 {change.CardCount} 张卡牌 {change.AttributeKey} +{change.Amount}");
        if (result.WealthGained > 0) changes.Add($"金钱 +{result.WealthGained}");
        if (result.WealthSpent > 0) changes.Add($"金钱 -{result.WealthSpent}");
        if (result.PendingBattleMaxHealthBonus > 0) changes.Add($"下场战斗最大生命 +{result.PendingBattleMaxHealthBonus}");
        if (result.GrantedCard is not null) changes.Add($"获得 {result.GrantedCard.Attributes.Identity.DisplayName}");
        if (result.CardRewardSkipped) changes.Add("双棋盘已满，未生成卡牌");
        return $"事件完成：{string.Join("，", changes)}。";
    }

    public static string FormatCardFace(CardSnapshot card) =>
        FormatCardFace(card.DisplayName, card.Level, card.Size, card.FactionKey, card.ElementKeys);

    public static string FormatCardFace(
        string displayName,
        int level,
        CardSize cardSize,
        StringName factionKey,
        IReadOnlyList<StringName> elementKeys)
    {
        var size = cardSize switch
        {
            CardSize.Small => "小型",
            CardSize.Medium => "中型",
            CardSize.Large => "大型",
            _ => cardSize.ToString(),
        };
        var faction = factionKey == GameFactions.Neutral
            ? "无阵营"
            : factionKey == new StringName("paladin") ? "帕拉帝恩" : factionKey.ToString();
        var elements = string.Join("、", elementKeys.Select(element => element switch
        {
            var key when key == GameElements.General => "通用",
            var key when key == GameElements.Fire => "火",
            var key when key == GameElements.Water => "水",
            var key when key == GameElements.Wind => "风",
            var key when key == GameElements.Earth => "土",
            var key when key == GameElements.Lightning => "雷",
            var key when key == GameElements.Wood => "木",
            var key when key == GameElements.Ice => "冰",
            var key when key == GameElements.Light => "光",
            var key when key == GameElements.Dark => "暗",
            _ => element.ToString(),
        }));
        return $"{level}级 {displayName}\n{size} · {faction}\n{elements}";
    }

    public static string FormatBattleLog(BattleResult result)
    {
        var lines = new List<string>();
        foreach (var battleEvent in result.Events)
            if (battleEvent is DamageDealtEvent damage)
            {
                var source = damage.SourceKind switch
                {
                    DamageSourceKind.Eclipse => "日蚀",
                    DamageSourceKind.Status => "状态",
                    DamageSourceKind.Skill => "技能",
                    DamageSourceKind.CardSet => "套装",
                    _ => "卡牌",
                };
                lines.Add($"{damage.Tick.ToSeconds(),4:0.0}s　{source}：{(damage.TargetSide == SideId.Player ? "玩家" : "敌方")}受到 {damage.HealthDamage} 点伤害，生命 {damage.RemainingHealth}");
            }
        var outcome = result.Outcome == BattleOutcome.PlayerVictory ? "玩家胜利"
            : result.Outcome == BattleOutcome.OpponentVictory ? "战斗失败" : "平局";
        lines.Add($"\n结果：{outcome}　耗时 {result.EndedAt.ToSeconds():0.0}s");
        return string.Join("\n", lines);
    }

    public static string KindName(EncounterKind kind) => kind switch
    {
        EncounterKind.Shop => "商店",
        EncounterKind.Monster => "怪物战",
        EncounterKind.Pvp => "PvP",
        _ => "事件",
    };
}
