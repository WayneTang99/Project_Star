using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Definitions;

// 宝石的只读身份、追加发动效果与展示配色（领域定义层）。
public abstract class GemDefinition
{
    protected GemDefinition(EntityAttributes<GemIdentityAttributes> attributes,
        IReadOnlyList<EffectDefinition>? activeEffects = null, Color? color = null)
    {
        Attributes = attributes ?? throw new ArgumentNullException(nameof(attributes));
        DefinitionFreezer.Freeze(attributes);
        ActiveEffects = Array.AsReadOnly((activeEffects ?? []).ToArray());
        Color = color ?? new Color("d8b86d");
    }
    public EntityAttributes<GemIdentityAttributes> Attributes { get; }
    public IReadOnlyList<EffectDefinition> ActiveEffects { get; }
    public Color Color { get; }
}
