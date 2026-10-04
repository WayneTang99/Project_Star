using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 秩序板甲圣骑士卡牌定义（内容层）。
public sealed class OrderPlateArmorCardDefinition : CardDefinition
{
    public OrderPlateArmorCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.order_plate_armor"), "秩序板甲", new StringName("paladin"),
                CardSize.Medium, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/order_plate_armor-illustration.png"), descriptionEntries:
                [new(CardKeywords.Activate, "冷却5秒，魔法消耗0，己方英雄获得40/60/80/100护甲（随此卡牌护甲加成提高），随机使一张敌方存活战场卡牌获得1/2/3/4秒迟缓。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            {
                [GameAttributeKeys.Armor] = 40,
                [GameAttributeKeys.CooldownTicks] = 50,
            })), new TagSet([GameTags.Equipment]), initialLevel: 1,
            levels: [CreateLevel(1, 40), CreateLevel(2, 60), CreateLevel(3, 80), CreateLevel(4, 100)]) { }

    private static CardLevelDefinition CreateLevel(int level, int armor) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.Armor] = armor },
        [new AbilityDefinition(new StringName("ability.armor_and_random_enemy_slow"), AbilityActivation.Active,
            AbilityTarget.AlliedHero, 0, 50,
            [new GainSourceHeroArmorFromAttributeEffectDefinition(GameAttributeKeys.Armor),
                new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.SlowDuration, level * 10)])]);
}
