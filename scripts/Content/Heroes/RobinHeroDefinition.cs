using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Heroes;

// 罗宾冒险家英雄内容定义（内容层）。
public sealed class RobinHeroDefinition : HeroDefinition
{
    public RobinHeroDefinition()
        : base(
            new EntityAttributes<HeroIdentityAttributes>(
                new HeroIdentityAttributes(
                    new StringName("hero.robin"),
                    "罗宾",
                    new StringName("robin"),
                    "冒险家", new StringName("res://art/ui/heroes/robin-illustration-v2.png")),
                baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                {
                    [GameAttributeKeys.MaxHealth] = 200,
                    [GameAttributeKeys.Armor] = 0,
                    [GameAttributeKeys.MaxMana] = 100,
                    [GameAttributeKeys.Mana] = 0,
                    [GameAttributeKeys.ManaRegen] = 10,
                    [GameAttributeKeys.HealthRegen] = 0,
                })))
    {
    }
}
