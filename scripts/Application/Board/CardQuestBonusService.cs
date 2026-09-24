using System;
using System.Security.Cryptography;
using System.Text;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Board;

// 按战场卡牌已解锁任务重算持续属性能力（应用棋盘层）。
public sealed class CardQuestBonusService
{
    // 精确移除旧贡献后，仅重新施加战场卡牌自己的已解锁能力。
    public void Recalculate(MatchSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        foreach (var applied in session.AppliedCardQuestModifiers)
            session.Player.Inventory.Find(applied.CardId)?.Attributes.BaseCombat.RemoveModifier(applied.ModifierId);
        session.AppliedCardQuestModifiers.Clear();

        foreach (var placement in session.Board.Battlefield.Placements)
        {
            var card = session.Player.Inventory.Find(placement.CardId)
                ?? throw new InvalidOperationException($"Battlefield card '{placement.CardId}' is not owned.");
            foreach (var quest in card.Quests)
            {
                if (!card.IsQuestUnlocked(quest)) continue;
                for (var abilityIndex = 0; abilityIndex < quest.Abilities.Count; abilityIndex++)
                {
                    var ability = quest.Abilities[abilityIndex];
                    if (ability.Activation != AbilityActivation.PassiveWhileEnabled) continue;
                    for (var effectIndex = 0; effectIndex < ability.Effects.Count; effectIndex++)
                    {
                        var effect = (ModifyAttributeEffectDefinition)ability.Effects[effectIndex];
                        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(
                            $"quest:{card.Id}:{quest.Key}:{abilityIndex}:{effectIndex}"));
                        var modifierId = new ModifierId(new Guid(bytes.AsSpan(0, 16)));
                        card.Attributes.BaseCombat.ApplyModifier(new StatModifier(
                            modifierId, card.Id, effect.AttributeKey, effect.Amount));
                        session.AppliedCardQuestModifiers.Add(new AppliedCardQuestModifier(modifierId, card.Id));
                    }
                }
            }
        }
    }
}
