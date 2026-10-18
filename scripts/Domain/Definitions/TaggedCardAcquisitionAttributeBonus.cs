using System;
using Godot;

namespace Project_Star.Domain.Definitions;

// 成功获得指定标签卡牌后，为战场或备战来源累加永久属性（领域定义层）。
public sealed record TaggedCardAcquisitionAttributeBonus
{
    public TaggedCardAcquisitionAttributeBonus(StringName requiredTag, StringName attributeKey, int amount)
    {
        if (requiredTag.IsEmpty || attributeKey.IsEmpty || amount < 1)
            throw new ArgumentException("Acquisition bonus requires a tag, an attribute key and a positive amount.");
        RequiredTag = requiredTag;
        AttributeKey = attributeKey;
        Amount = amount;
    }

    public StringName RequiredTag { get; }
    public StringName AttributeKey { get; }
    public int Amount { get; }
}
