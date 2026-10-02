using System.Collections.Generic;
using Godot;

namespace Project_Star.Domain.Definitions;

// 领域定义的只读内容目录；调用方不依赖扫描和校验实现。
public interface IDefinitionCatalog
{
    IReadOnlyDictionary<StringName, HeroDefinition> Heroes { get; }
    IReadOnlyDictionary<StringName, CardDefinition> Cards { get; }
    IReadOnlyDictionary<StringName, CardSetDefinition> Sets { get; }
    IReadOnlyDictionary<StringName, SkillDefinition> Skills { get; }
    IReadOnlyDictionary<StringName, EncounterDefinition> Encounters { get; }
    IReadOnlyDictionary<StringName, MonsterDefinition> Monsters { get; }
}
