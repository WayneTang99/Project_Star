using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Match;

// 对局事件能力的经验与卡牌奖励结算，复用获得和套装计数（应用层）。
public static class MatchAbilityService
{
    // 只读计算当前战场套装在轮开始提供的经验。
    public static int RoundExperience(MatchSession session, IReadOnlyDictionary<StringName, CardSetDefinition> sets) =>
        checked(new CardSetEvaluator().Evaluate(session, sets).SelectMany(active => active.Threshold.Abilities)
            .Where(ability => ability.Activation == AbilityActivation.PassiveOnRoundStart)
            .SelectMany(ability => ability.Effects).OfType<GrantExperienceEffectDefinition>().Sum(effect => effect.Amount));

    // 使用开战冻结来源，胜利才结算；新奖励不会参与生成它的本次战斗。
    public static void ApplyVictory(MatchSession session, BattleResult battle, MatchBattleKind kind, BoardService board)
    {
        if (battle.Outcome != BattleOutcome.PlayerVictory || !session.AppliedMatchRewards.Add(battle)) return;
        var economy = new CardEconomyService(new EntityFactory(), board);
        foreach (var source in battle.MatchRewardSources)
        {
            if (source.RequiresOwnedCard && session.Player.Inventory.Find(source.SourceId) is null) continue;
            foreach (var ability in source.Abilities)
            {
                if (ability.Activation != AbilityActivation.PassiveOnBattleVictory
                    && !(kind == MatchBattleKind.Monster && ability.Activation == AbilityActivation.PassiveOnMonsterVictory)) continue;
                foreach (var effect in ability.Effects)
                    switch (effect)
                    {
                        case GrantExperienceEffectDefinition experience:
                            session.Player.AddExperience(experience.Amount);
                            break;
                        case GrantSourceLevelCardEffectDefinition reward:
                            if (economy.AcquireAndPlace(session, reward.Card, source.Level, CardAcquisitionSource.Reward).IsFailure)
                                session.AddPendingMonsterReward(new PendingMonsterReward(MonsterRewardKind.Card,
                                    reward.Card.Attributes.Identity.Key, reward.Card.Attributes.Identity.DisplayName, source.Level));
                            break;
                    }
            }
        }
        board.RefreshBonuses(session);
    }
}
