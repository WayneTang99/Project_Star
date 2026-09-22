using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Common;

namespace Project_Star.Domain.Definitions;

// 怪物固定卡组中的单张卡牌配置（领域定义层）。
public sealed record MonsterCardEntry(StringName CardKey, int Level, int BoardStart);

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
        int healthRegen = 0)
    {
        ArgumentNullException.ThrowIfNull(cards);
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
        DefinitionFreezer.Freeze(Attributes);
    }

    public EntityAttributes<MonsterIdentityAttributes> Attributes { get; }

    public IReadOnlyList<MonsterCardEntry> Cards { get; }
}
