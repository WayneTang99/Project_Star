using System;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Definitions;

// 宝石定义的只读基础，具体种类和效果由后续正式内容提供（领域定义层）。
public abstract class GemDefinition
{
    protected GemDefinition(EntityAttributes<GemIdentityAttributes> attributes)
    {
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        DefinitionFreezer.Freeze(attributes);
    }
    public EntityAttributes<GemIdentityAttributes> Attributes { get; }
}
