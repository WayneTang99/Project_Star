using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 虫蛊袋莫娜卡牌，组合虫族发动与摧毁回响（内容层）。
public sealed class GuInsectPouchCardDefinition : CardDefinition
{
    public GuInsectPouchCardDefinition() : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
        new StringName("card.gu_insect_pouch"), "虫蛊袋", new StringName("mona"), CardSize.Medium, [GameElements.General],
        illustration: new StringName("res://art/ui/card-face/artwork/gu_insect_pouch-illustration.png"),
        descriptionEntries:
        [new(CardKeywords.Echo, "己方虫族卡牌发动后，给予敌方随机1张卡牌1秒迟缓。"),
         new(CardKeywords.Echo, "己方虫族卡牌被摧毁后，造成40/80/160伤害。")])),
        new TagSet(), initialLevel: 2, levels: [CreateLevel(2, 40), CreateLevel(3, 80), CreateLevel(4, 160)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [new AbilityDefinition(new StringName("ability.slow_on_allied_insect_activation"), AbilityActivation.EchoOnMatchingAlliedCardActivated,
            AbilityTarget.OtherBattlefieldCards, 0, 0,
            [new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.SlowDuration, 10)], triggerCardTag: GameTags.Insect),
         new AbilityDefinition(new StringName("ability.damage_on_allied_tagged_card_destroyed"), AbilityActivation.EchoOnAlliedCardDestroyed,
            AbilityTarget.EnemyHero, 0, 0, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)], triggerCardTag: GameTags.Insect)]);
}
