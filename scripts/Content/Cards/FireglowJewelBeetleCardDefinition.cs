using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 萤火吉丁虫莫娜卡牌内容定义（内容层）。
public sealed class FireglowJewelBeetleCardDefinition : CardDefinition
{
    public FireglowJewelBeetleCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.fireglow_jewel_beetle"), "萤火吉丁虫", new StringName("mona"),
                CardSize.Small, [GameElements.Fire],
                illustration: new StringName("res://art/ui/card-face/artwork/fireglow_jewel_beetle-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却4秒，魔法消耗0，对敌方英雄施加3/6/10/16灼伤。"),
                    new(CardKeywords.Sell, "出售此卡牌时，己方战场最左侧灼伤卡牌施加的灼伤永久增加1/2/3/4。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 40 })), new TagSet([GameTags.Insect]), initialLevel: 1,
            levels: [CreateLevel(1, 3), CreateLevel(2, 6), CreateLevel(3, 10), CreateLevel(4, 16)]) { }

    private static CardLevelDefinition CreateLevel(int level, int burn) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.Burn] = burn },
        [new AbilityDefinition(new StringName("ability.apply_burn"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 40,
            [new ApplyAttributeStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.Burn)])],
        onSellReward: new IncreaseLeftmostBurnCardOnSellDefinition(level));
}
