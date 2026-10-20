using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 火焰护腕莫娜卡牌内容定义（内容层）。
public sealed class FlameGauntletCardDefinition : CardDefinition
{
    public FlameGauntletCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.flame_gauntlet"), "火焰护腕", new StringName("mona"),
                CardSize.Small, [GameElements.Fire],
                illustration: new StringName("res://art/ui/card-face/artwork/flame_bracer-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却7秒，魔法消耗0，对敌方英雄造成20/40/80/160伤害，并施加6/12/24/48灼伤；多重1，额外发动一次。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 70, [GameAttributeKeys.Multicast] = 1 })),
            new TagSet([GameTags.Equipment]), initialLevel: 1,
            levels: [CreateLevel(1, 20, 6), CreateLevel(2, 40, 12), CreateLevel(3, 80, 24), CreateLevel(4, 160, 48)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage, int burn) => new(level,
        new Dictionary<StringName, int>
        { [GameAttributeKeys.AttackDamage] = damage, [GameAttributeKeys.Burn] = burn },
        [new AbilityDefinition(new StringName("ability.attribute_attack_and_burn"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 70,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                new ApplyAttributeStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.Burn)])]);
}
