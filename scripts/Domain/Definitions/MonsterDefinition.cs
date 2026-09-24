using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Definitions;

// 怪物固定卡组中的单张卡牌配置（领域定义层）。
public sealed record MonsterCardEntry(StringName CardKey, int Level, int BoardStart);

// 怪物固定技能配置（领域定义层）。
public sealed record MonsterSkillEntry(StringName SkillKey, int Level);

// 怪物的固定战斗属性与卡组定义（领域定义层）。
public abstract class MonsterDefinition
{
    protected MonsterDefinition(
        StringName key,
        string displayName,
        IReadOnlyList<MonsterCardEntry> cards,
        int maxHealth = 100,
        int armor = 0,
        int maxMana = 100,
        int mana = 0,
        int manaRegen = 10,
        int healthRegen = 0,
        int level = 1,
        IReadOnlyList<MonsterSkillEntry>? skills = null)
    {
        ArgumentNullException.ThrowIfNull(cards);
        if (level < 1) throw new ArgumentOutOfRangeException(nameof(level));
        Attributes = new EntityAttributes<MonsterIdentityAttributes>(
            new MonsterIdentityAttributes(key, displayName),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            {
                [GameAttributeKeys.MaxHealth] = maxHealth,
                [GameAttributeKeys.Armor] = armor,
                [GameAttributeKeys.MaxMana] = maxMana,
                [GameAttributeKeys.Mana] = mana,
                [GameAttributeKeys.ManaRegen] = manaRegen,
                [GameAttributeKeys.HealthRegen] = healthRegen,
            }));
        Cards = new List<MonsterCardEntry>(cards).AsReadOnly();
        Skills = skills is null ? Array.Empty<MonsterSkillEntry>() : new List<MonsterSkillEntry>(skills).AsReadOnly();
        Level = level;
        DefinitionFreezer.Freeze(Attributes);
    }

    public EntityAttributes<MonsterIdentityAttributes> Attributes { get; }

    public IReadOnlyList<MonsterCardEntry> Cards { get; }
    public IReadOnlyList<MonsterSkillEntry> Skills { get; }
    public int Level { get; }
}
