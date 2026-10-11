using System;
using System.Collections.Generic;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Match;

// 一次宝石商店访问，状态只由应用渠道提交。
public sealed class GemShopVisit
{
    internal GemShopVisit(GemShopDefinition definition, IReadOnlyList<GemDefinition> offers, long revision)
    { Definition = definition; Offers = offers; Revision = revision; }
    public GemShopDefinition Definition { get; }
    public IReadOnlyList<GemDefinition> Offers { get; private set; }
    public long Revision { get; private set; }
    public bool HasRefreshed { get; private set; }
    public bool IsResolved { get; private set; }
    internal void Refresh(IReadOnlyList<GemDefinition> offers) { Offers = offers; HasRefreshed = true; Revision++; }
    internal void Resolve() { IsResolved = true; Revision++; }
}
