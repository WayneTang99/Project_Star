using System.Linq;
using Project_Star.Application.Factories;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Economy;

// 永久卡牌升级与升级奖励统一结算，不修改任何战斗Runtime。
public static class CardLevelUpgradeService
{
    public static void Upgrade(MatchSession session, CardInstance card, CardDefinition definition, int level, EntityFactory factory)
    {
        var previous = card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level);
        if (level <= previous || Enumerable.Range(previous + 1, level - previous).Any(next => !definition.SupportsLevel(next)))
            throw new System.ArgumentOutOfRangeException(nameof(level));
        for (var next = previous + 1; next <= level; next++)
        {
            factory.ApplyCardLevel(card, definition, next);
            var rewards = session.Board.Battlefield.Placements.Select(placement => session.Player.Inventory.Find(placement.CardId))
                .Where(source => source is not null).SelectMany(source => CardAbilityComposer.Compose(source!))
                .Where(ability => ability.Activation == AbilityActivation.EchoOnAlliedCardUpgraded)
                .SelectMany(ability => ability.Effects).OfType<ReduceEventCardCooldownPercentEffectDefinition>().ToArray();
            foreach (var reward in rewards) card.ReduceCooldown(reward.Percent);
        }
    }
}
