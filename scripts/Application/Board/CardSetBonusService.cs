using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Board;

// 棋盘变化后精确重算套装的非战斗数值贡献（应用棋盘层）。
public sealed class CardSetBonusService
{
    private readonly IReadOnlyDictionary<StringName, CardSetDefinition> _definitions;
    private readonly CardSetEvaluator _evaluator = new();

    public CardSetBonusService(IReadOnlyDictionary<StringName, CardSetDefinition> definitions) =>
        _definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));

    // 移除旧阈值来源，再按当前战场快照施加各档贡献。
    public void Recalculate(MatchSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        foreach (var applied in session.AppliedCardSetModifiers)
        {
            var attributes = applied.IsHero
                ? session.Player.Hero?.Attributes.BaseCombat
                : session.Player.Inventory.Find(applied.TargetId)?.Attributes.BaseCombat;
            attributes?.RemoveModifier(applied.Id);
        }
        session.AppliedCardSetModifiers.Clear();

        foreach (var active in _evaluator.Evaluate(session, _definitions))
        {
            var sourceId = CardSetEvaluator.SourceId(active.SetKey, active.Threshold.RequiredDistinctCards);
            for (var abilityIndex = 0; abilityIndex < active.Threshold.Abilities.Count; abilityIndex++)
            {
                var ability = active.Threshold.Abilities[abilityIndex];
                if (ability.Activation != AbilityActivation.PassiveWhileEnabled) continue;
                for (var effectIndex = 0; effectIndex < ability.Effects.Count; effectIndex++)
                {
                    var effect = (ModifyAttributeEffectDefinition)ability.Effects[effectIndex];
                    if (ability.Target == AbilityTarget.AlliedHero)
                    {
                        var hero = session.Player.Hero
                            ?? throw new InvalidOperationException("A hero is required for a set ability targeting the hero.");
                        Apply(hero.Attributes.BaseCombat, hero.Id, true);
                        continue;
                    }
                    foreach (var placement in session.Board.Battlefield.Placements)
                    {
                        var card = session.Player.Inventory.Find(placement.CardId)!;
                        var isMember = card.Attributes.Identity.SetKey == active.SetKey;
                        if (ability.Target == AbilityTarget.SourceGroupCards && !isMember
                            || ability.Target == AbilityTarget.OtherBattlefieldCards && isMember)
                            continue;
                        Apply(card.Attributes.BaseCombat, card.Id, false);
                    }

                    void Apply(ModifiableAttributeSet attributes, EntityId targetId, bool isHero)
                    {
                        var id = new ModifierId(StableGuid($"{sourceId}:{abilityIndex}:{effectIndex}:{targetId}"));
                        attributes.ApplyModifier(new StatModifier(id, sourceId, effect.AttributeKey, effect.Amount));
                        session.AppliedCardSetModifiers.Add(new AppliedCardSetModifier(id, targetId, isHero));
                    }
                }
            }
        }
    }

    private static Guid StableGuid(string value)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(value));
        return new Guid(hash.AsSpan(0, 16));
    }
}
