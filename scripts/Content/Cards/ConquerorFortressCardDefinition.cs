using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 征服者堡垒帕拉帝恩地域卡牌定义（内容层）。
public sealed class ConquerorFortressCardDefinition : CardDefinition
{
    public ConquerorFortressCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.conqueror_fortress"), "征服者堡垒", new StringName("paladin"),
                CardSize.Large, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/conqueror_fortress-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Echo, "当敌方战场卡牌发动后，己方所有存活战场人类卡牌攻击增加40/80，可累计，包含零攻击卡牌；多重发动分别触发。此卡牌被摧毁后停止触发，已获得的攻击加成本场保留。"),
                ])), new TagSet([GameTags.Location]), initialLevel: 3,
            levels: [CreateLevel(3, 40), CreateLevel(4, 80)]) { }

    private static CardLevelDefinition CreateLevel(int level, int bonus) => new(level, null,
        [new AbilityDefinition(new StringName("ability.increase_tagged_allied_attribute_on_enemy_activation"),
            AbilityActivation.EchoOnEnemyCardActivated, AbilityTarget.SelfCard, 0, 0,
            [new ModifyTaggedAlliedCardsAttributeEffectDefinition(GameTags.Human, GameAttributeKeys.AttackDamage,
                bonus, RequiresAttributeSupport: false)])]);
}
