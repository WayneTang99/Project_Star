using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 鬼眼莫娜卡牌，组合随机禁锢与亡语伤害。
public sealed class GhostEyeCardDefinition : CardDefinition
{
    public GhostEyeCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.ghost_eye"), "鬼眼", new StringName("mona"), CardSize.Small, [GameElements.Dark],
            illustration: new StringName("res://art/ui/card-face/artwork/ghost_eye-illustration-20261022.png"),
            descriptionEntries: [new(CardKeywords.Activate, "给予敌方随机1张卡牌1秒禁锢。"),
                new(CardKeywords.Deathrattle, "造成120/240/480伤害。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 70 })),
            new TagSet(), initialLevel: 2,
            levels: [CreateLevel(2, 120), CreateLevel(3, 240), CreateLevel(4, 480)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
        [new AbilityDefinition(new StringName("ability.random_immobilize"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 70,
            [new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.ImmobilizeDuration, 10)]),
         new AbilityDefinition(new StringName("ability.damage_on_source_destroyed"), AbilityActivation.EchoOnSourceDestroyed,
            AbilityTarget.EnemyHero, 0, 0, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)])]);
}
