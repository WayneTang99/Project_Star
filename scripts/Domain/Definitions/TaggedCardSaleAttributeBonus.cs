using System;
using Godot;

namespace Project_Star.Domain.Definitions;

// 按标签与出售价值给来源卡牌累加永久属性加成（领域内容定义层）。
public sealed record TaggedCardSaleAttributeBonus
{
    public TaggedCardSaleAttributeBonus(StringName requiredTag, StringName attributeKey, int amount, int soldValuePercent = 0)
    {
        if (requiredTag.IsEmpty && soldValuePercent == 0 || attributeKey.IsEmpty || amount < 1
            || soldValuePercent is < 0 or > 100)
            throw new ArgumentException("Sale attribute bonus requires a tag or value percentage, an attribute key and a positive amount.");
        RequiredTag = requiredTag;
        AttributeKey = attributeKey;
        Amount = amount;
        SoldValuePercent = soldValuePercent;
    }

    public StringName RequiredTag { get; }
    public StringName AttributeKey { get; }
    public int Amount { get; }
    public int SoldValuePercent { get; }

    // 空标签匹配任意出售卡牌，比例读取本次出售前的最终价值。
    public bool Matches(TagSet tags) => RequiredTag.IsEmpty || tags.Contains(RequiredTag);
    public long CalculateAmount(int soldValue) => Amount + (long)soldValue * SoldValuePercent / 100;
}
