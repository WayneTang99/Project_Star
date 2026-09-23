using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Heroes;

// 帕拉帝恩英雄内容定义（内容层）。
public sealed class PaladinHeroDefinition : HeroDefinition
{
    public PaladinHeroDefinition()
        : base(
            new EntityAttributes<HeroIdentityAttributes>(
                new HeroIdentityAttributes(
                    new StringName("hero.paladin"),
                    "帕拉帝恩",
                    new StringName("paladin")),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.MaxHealth] = 100,
                    [GameAttributeKeys.Armor] = 0,
                    [GameAttributeKeys.MaxMana] = 100,
                    [GameAttributeKeys.Mana] = 0,
                    [GameAttributeKeys.ManaRegen] = 10,
                    [GameAttributeKeys.HealthRegen] = 0,
                })))
    {
    }
}
