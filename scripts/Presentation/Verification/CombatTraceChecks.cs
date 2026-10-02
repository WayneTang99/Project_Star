using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.Verification;

// 固定输入的完整战斗记录回归，保护结果、事件顺序与播放状态（表现层验证模块）。
internal static class CombatTraceChecks
{
    // 指纹由职责拆分前的模拟器生成；变化时应先确认具体规则差异。
    private static readonly string[] Baseline = [
        "81B4CA1E935085DF5C69C20E5527E51F07CE298806E942AC629CFDD79657E080",
        "B8359B1F3BA79831BFB2EA8D0A9E63257243C4045BEAF71FEA429B105FF49AD0",
        "268BE18E8730015CAEB11AB497B8563CC062CEE5C3110DCBF03257F177D4D612",
    ];

    // 同一输入复算一致，并与整理前记录逐字段一致。
    internal static bool PreservesTraces()
    {
        var setups = Scenarios();
        for (var index = 0; index < setups.Length; index++)
        {
            var simulator = new CombatSimulator();
            if (simulator.Simulate(setups[index]).States.Any(frame => frame.Cards.Any(card => card.IsFlying || card.IsBerserk))) return false;
            var actual = Fingerprint(simulator.Simulate(setups[index]));
            if (actual != Fingerprint(simulator.Simulate(setups[index]))) return false;
            if (actual != Baseline[index]) return false;
        }
        return true;
    }

    private static BattleSetup[] Scenarios()
    {
        var attack = Ability("attack", AbilityActivation.Active, AbilityTarget.EnemyHero, 2,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
             new ApplyStatusEffectDefinition(BattleStatus.Poison, 3)], 2);
        var aid = Ability("aid", AbilityActivation.Active, AbilityTarget.AlliedHero, 3,
            [new HealEffectDefinition(10), new ArmorEffectDefinition(7),
             new ApplyStatusToAdjacentAlliedCardsEffectDefinition(BattleStatus.HasteDuration, 3, GameTags.Human),
             new ChargeRandomOtherAlliedElementCardEffectDefinition(GameElements.Light, 1)]);
        var destroy = Ability("destroy", AbilityActivation.Active, AbilityTarget.SelfCard, 12,
            [new DestroyCardEffectDefinition(true)]);
        var aura = Ability("aura", AbilityActivation.PassiveAura, AbilityTarget.SelfCard, 0,
            [new GrantMulticastToAlliedElementCardsEffectDefinition(GameElements.Light, 1)]);
        var skill = Ability("skill", AbilityActivation.EchoOnFirstAlliedCardActivated, AbilityTarget.EnemyHero, 0,
            [new SourceHeroLevelScaledDamageEffectDefinition(10)]);
        var player = new BattleSideSetup(new HeroBattleSetup(Id(1), 100, 0, 30, 20, 6, 4, 3, 4, 2),
            [new CardBattleSetup(Id(2), 0, 5, 2, [attack], Tags: new TagSet([GameTags.Human]),
                Multicast: 1, ElementKeys: [GameElements.Light]),
             new CardBattleSetup(Id(3), 1, 0, 3, [aid, destroy, aura], ElementKeys: [GameElements.Light])],
            [new SkillBattleSetup(Id(4), [skill], new Dictionary<StringName, int>())]);
        var enemy = new BattleSideSetup(new HeroBattleSetup(Id(5), 1000, 12),
            [new CardBattleSetup(Id(6), 0, 8, 1)]);
        var emptyPlayer = new BattleSideSetup(new HeroBattleSetup(Id(1), 100, 0), []);
        var emptyEnemy = new BattleSideSetup(new HeroBattleSetup(Id(5), 100, 0), []);
        return [new BattleSetup(player, enemy, 42, new BattleTick(30), new BattleTick(20)),
            new BattleSetup(emptyPlayer, emptyEnemy, 42, new BattleTick(100), new BattleTick(20)),
            new BattleSetup(emptyPlayer, emptyEnemy, 42, new BattleTick(2))];
    }

    private static AbilityDefinition Ability(string key, AbilityActivation activation, AbilityTarget target,
        int cooldown, IReadOnlyList<EffectDefinition> effects, int mana = 0) =>
        new(new StringName("verification.trace." + key), activation, target, mana, cooldown, effects);

    private static EntityId Id(int value) => new(new Guid($"00000000-0000-0000-0000-{value:000000000000}"));

    private static string Fingerprint(BattleResult result)
    {
        var text = new StringBuilder();
        text.AppendLine($"{result.Outcome}|{result.EndReason}|{result.EndedAt.Value}|{result.PlayerRemainingHealth}|{result.OpponentRemainingHealth}");
        foreach (var item in result.Events) text.AppendLine(item.ToString());
        foreach (var item in result.PermanentChanges) text.AppendLine(item.ToString());
        foreach (var frame in result.States)
        {
            text.AppendLine($"{frame.Tick.Value}|{frame.EventCount}|{frame.Eclipse}|{frame.Player}|{frame.Opponent}");
            foreach (var card in frame.Cards)
            {
                text.AppendLine($"{card.Id}|{card.Side}|{card.Destroyed}|{card.Haste}|{card.Slow}|{card.Immobilize}");
                text.AppendLine(string.Join(",", card.CooldownUnits));
                foreach (var value in card.Values.OrderBy(pair => pair.Key.ToString(), StringComparer.Ordinal))
                    text.AppendLine($"{value.Key}={value.Value}");
            }
        }
        var canonical = text.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }
}
