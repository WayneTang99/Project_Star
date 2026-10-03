using System;
using Godot;

namespace Project_Star.Domain.Definitions;

// 卡牌位于战场区参战并获胜后，累加自身永久属性（领域内容定义层）。
public sealed record BattleVictoryAttributeBonus
{
    public BattleVictoryAttributeBonus(StringName attributeKey, int amount)
    {
        if (attributeKey.IsEmpty || amount < 1)
            throw new ArgumentException("Battle victory bonus requires a nonempty attribute key and a positive amount.");
        AttributeKey = attributeKey;
        Amount = amount;
    }

    public StringName AttributeKey { get; }
    public int Amount { get; }
}
