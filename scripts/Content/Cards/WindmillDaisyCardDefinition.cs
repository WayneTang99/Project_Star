using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 风车菊圣骑士双属性植物，治疗与出售永久生命均复用通用效果。
public sealed class WindmillDaisyCardDefinition : CardDefinition
{
    public WindmillDaisyCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.windmill_daisy"), "风车菊", new StringName("paladin"),
            CardSize.Small, [GameElements.Wind, GameElements.Wood],
            illustration: new StringName("res://art/ui/card-face/artwork/windmill_daisy-illustration.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却4秒，魔法消耗0，治疗己方英雄10/20/40/80生命。"),
                new(CardKeywords.Sell, "出售此卡牌后，己方英雄永久获得10/20/30/40最大生命值。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 40 })),
            new TagSet([GameTags.Plant]), initialLevel: 1,
            levels: [CreateLevel(1, 10, 10), CreateLevel(2, 20, 20), CreateLevel(3, 40, 30), CreateLevel(4, 80, 40)]) { }

    private static CardLevelDefinition CreateLevel(int level, int healing, int maxHealth) => new(level, null,
        [new AbilityDefinition(new StringName("ability.heal"), AbilityActivation.Active, AbilityTarget.AlliedHero,
            0, 40, [new HealEffectDefinition(healing)])],
        onSellReward: new IncreaseHeroCombatAttributeOnSellDefinition(GameAttributeKeys.MaxHealth, maxHealth));
}
