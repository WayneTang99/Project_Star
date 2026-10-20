using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Random;

namespace Project_Star.Application.Match;

// 对局事件的经验、卡牌与技能奖励结算，复用获得和套装计数（应用层）。
public static class MatchAbilityService
{
    // 冻结轮开始来源与等级，备战仅执行明确允许的能力；新奖励不参与本次触发。
    public static void ApplyRoundCards(MatchSession session, IDefinitionCatalog catalog, BoardService board)
    {
        var faction = session.Player.Hero!.Attributes.Identity.FactionKey;
        var heroFactions = catalog.Heroes.Values.Select(hero => hero.Attributes.Identity.FactionKey).ToHashSet();
        var sources = session.Board.Battlefield.Placements.OrderBy(placement => placement.Start)
            .Select(placement => (Card: session.Player.Inventory.Find(placement.CardId)!, OnBench: false))
            .Concat(session.Board.Bench.Placements.OrderBy(placement => placement.Start)
                .Select(placement => (Card: session.Player.Inventory.Find(placement.CardId)!, OnBench: true)))
            .Select(source => (Level: source.Card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level),
                Effects: source.Card.Abilities.Where(ability => ability.Activation == AbilityActivation.PassiveOnRoundStart
                    && (!source.OnBench || ability.AllowsBench))
                    .SelectMany(ability => ability.Effects).ToArray())).ToArray();
        var economy = new CardEconomyService(new EntityFactory(), board, catalog.Cards.Values);
        foreach (var source in sources)
        foreach (var effect in source.Effects)
        {
            if (effect is GrantRandomHeroSourceLevelSkillEffectDefinition)
            {
                var skills = catalog.Skills.Values.Where(skill => heroFactions.Contains(skill.Attributes.Identity.FactionKey)
                    && skill.SupportsLevel(source.Level))
                    .OrderBy(skill => skill.Attributes.Identity.Key.ToString(), StringComparer.Ordinal).ToArray();
                if (skills.Length == 0) continue;
                var random = new SeededRandom(session.Random.State);
                var skill = skills[random.NextInt(0, skills.Length)];
                session.Random.State = random.State;
                new SkillAcquisitionService(new EntityFactory()).AcquireSkill(session, skill, source.Level);
                continue;
            }
            CardDefinition? reward = null;
            if (effect is GrantRandomOtherHeroSourceLevelCardEffectDefinition randomReward)
            {
                var candidates = catalog.Cards.Values.Where(card => (card.Attributes.Identity.Size == randomReward.Size
                    || card.Attributes.Identity.Size == randomReward.AdditionalSize)
                    && card.Attributes.Identity.FactionKey != faction
                    && (heroFactions.Contains(card.Attributes.Identity.FactionKey)
                        || card.Attributes.Identity.FactionKey == GameFactions.Neutral)
                    && card.SupportsLevel(source.Level))
                    .OrderBy(card => card.Attributes.Identity.Key.ToString(), StringComparer.Ordinal).ToArray();
                if (candidates.Length == 0) continue;
                var random = new SeededRandom(session.Random.State);
                reward = candidates[random.NextInt(0, candidates.Length)];
                session.Random.State = random.State;
            }
            else if (effect is GrantSourceLevelCardEffectDefinition fixedReward) reward = fixedReward.Card;
            else if (effect is GrantExperienceEffectDefinition experience) HeroExperienceService.Grant(session, experience.Amount);
            if (reward is not null
                && economy.AcquireAndPlace(session, reward, source.Level, CardAcquisitionSource.Reward).IsFailure)
                session.AddPendingMonsterReward(new PendingMonsterReward(MonsterRewardKind.Card,
                    reward.Attributes.Identity.Key, reward.Attributes.Identity.DisplayName, source.Level));
        }
        board.RefreshBonuses(session);
    }

    // 只读计算当前战场套装在轮开始提供的经验。
    public static int RoundExperience(MatchSession session, IReadOnlyDictionary<StringName, CardSetDefinition> sets) =>
        checked(new CardSetEvaluator().Evaluate(session, sets).SelectMany(active => active.Threshold.Abilities)
            .Where(ability => ability.Activation == AbilityActivation.PassiveOnRoundStart)
            .SelectMany(ability => ability.Effects).OfType<GrantExperienceEffectDefinition>().Sum(effect => effect.Amount));

    // 轮边界先冻结已激活阈值，只缩减当前战场内对应套装成员。
    public static void ApplyRoundSetCooldowns(MatchSession session, IReadOnlyDictionary<StringName, CardSetDefinition> sets)
    {
        var active = new CardSetEvaluator().Evaluate(session, sets);
        foreach (var threshold in active)
        foreach (var effect in threshold.Threshold.Abilities
            .Where(ability => ability.Activation == AbilityActivation.PassiveOnRoundStart)
            .SelectMany(ability => ability.Effects).OfType<ReduceSetCardCooldownEffectDefinition>())
        foreach (var placement in session.Board.Battlefield.Placements)
        {
            var card = session.Player.Inventory.Find(placement.CardId)!;
            if (card.Attributes.Identity.SetKey == threshold.SetKey) card.ReduceCooldown(effect.Percent);
        }
    }

    // 使用开战冻结来源，胜利才结算；新奖励不会参与生成它的本次战斗。
    public static void ApplyVictory(MatchSession session, BattleResult battle, MatchBattleKind kind, BoardService board,
        IEnumerable<CardDefinition>? cards = null)
    {
        if (battle.Outcome != BattleOutcome.PlayerVictory || !session.AppliedMatchRewards.Add(battle)) return;
        var economy = new CardEconomyService(new EntityFactory(), board, cards);
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
                            HeroExperienceService.Grant(session, experience.Amount);
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
