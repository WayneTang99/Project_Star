using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Heroes;

// 哈尔拉女性机械师英雄内容定义（内容层）。
public sealed class HarlaHeroDefinition : HeroDefinition
{
    public HarlaHeroDefinition()
        : base(
            new EntityAttributes<HeroIdentityAttributes>(
                new HeroIdentityAttributes(
                    new StringName("hero.harla"),
                    "哈尔拉",
                    new StringName("harla"),
                    "机械师", new StringName("res://art/ui/heroes/harla-illustration.png")),
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
