using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Heroes;

// 极云英雄内容定义（内容层）。
public sealed class JiyunHeroDefinition : HeroDefinition
{
    public JiyunHeroDefinition()
        : base(
            new EntityAttributes<HeroIdentityAttributes>(
                new HeroIdentityAttributes(
                    new StringName("hero.jiyun"),
                    "极云",
                    new StringName("jiyun"),
                    "熊猫人", new StringName("res://art/ui/heroes/jiyun-illustration.png")),
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
