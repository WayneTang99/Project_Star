using System;
using System.Linq;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Random;

namespace Project_Star.Application.Match;

// 统一发放对局经验，并逐级结算英雄升级的卡牌能力（应用层）。
public static class HeroExperienceService
{
    // 所有经验渠道共用；冻结本次来源，不补发获得卡牌之前的升级。
    public static void Grant(MatchSession session, int amount)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Status != MatchStatus.InProgress) return;
        var previous = session.Player.Hero?.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) ?? 0;
        var sources = session.Board.Battlefield.Placements.OrderBy(p => p.Start)
            .Concat(session.Board.Bench.Placements.OrderBy(p => p.Start))
            .Select(p => session.Player.Inventory.Find(p.CardId)!)
            .Select(card => (Card: card, Effects: CardAbilityComposer.Compose(card)
                .Where(a => a.Activation == AbilityActivation.PassiveOnHeroLevelUp
                    && (a.AllowsBench || session.Board.Battlefield.Placements.Any(p => p.CardId == card.Id)))
                .SelectMany(a => a.Effects).OfType<SocketRandomSourceGemEffectDefinition>().ToArray())).ToArray();
        var skillRewards = session.Definitions is { } catalog
            ? new CardSetEvaluator().Evaluate(session, catalog.Sets).SelectMany(set => set.Threshold.Abilities)
                .Where(ability => ability.Activation == AbilityActivation.PassiveOnHeroLevelUp)
                .SelectMany(ability => ability.Effects).OfType<GrantRandomFactionSkillEffectDefinition>().ToArray()
            : Array.Empty<GrantRandomFactionSkillEffectDefinition>();
        session.Player.AddExperience(amount);
        var current = session.Player.Hero?.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) ?? 0;
        if (current <= previous) return;
        var pool = session.GemDefinitions.OrderBy(gem => gem.Attributes.Identity.Key.ToString(), StringComparer.Ordinal).ToArray();
        for (var level = previous; level < current; level++)
        foreach (var reward in skillRewards) MatchAbilityService.GrantRandomSkill(session, reward);
        if (pool.Length == 0) return;
        var sockets = new SocketGemService();
        var random = new SeededRandom(session.Random.State);
        for (var level = previous; level < current; level++)
        foreach (var source in sources)
        foreach (var effect in source.Effects)
        {
            var index = -1;
            for (var i = 0; i < source.Card.GemSockets.Count; i++)
                if (source.Card.GemSockets[i] is null) { index = i; break; }
            if (index < 0) continue;
            var candidates = pool.Where(gem => sockets.Check(session,
                new SocketGemCommand(session.Id, source.Card.Id, index, gem)).IsSuccess).ToArray();
            if (candidates.Length == 0) continue;
            var gem = candidates[random.NextInt(0, candidates.Length)];
            sockets.Execute(session, new SocketGemCommand(session.Id, source.Card.Id, index, gem));
            session.Random.State = random.State;
        }
    }
}
