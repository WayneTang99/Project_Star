using System;
using Godot;

namespace Project_Star.Domain.Definitions;

// 出售指定标签卡牌后，来源卡牌获得永久属性加成（领域内容定义层）。
public sealed record TaggedCardSaleAttributeBonus
{
    public TaggedCardSaleAttributeBonus(StringName requiredTag, StringName attributeKey, int amount)
    {
        if (requiredTag.IsEmpty || attributeKey.IsEmpty || amount < 1)
            throw new ArgumentException("Sale attribute bonus requires nonempty keys and a positive amount.");
        RequiredTag = requiredTag;
        AttributeKey = attributeKey;
        Amount = amount;
    }

    public StringName RequiredTag { get; }
    public StringName AttributeKey { get; }
    public int Amount { get; }
}
