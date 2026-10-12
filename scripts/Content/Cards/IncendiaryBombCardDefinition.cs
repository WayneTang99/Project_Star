using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 燃爆弹哈尔拉卡牌内容定义（内容层）。
public sealed class IncendiaryBombCardDefinition : CardDefinition
{
    public IncendiaryBombCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.incendiary_bomb"), "燃爆弹", new StringName("harla"),
                CardSize.Small, [GameElements.Fire],
                illustration: new StringName("res://art/ui/card-face/artwork/incendiary_bomb-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却7秒，魔法消耗0，对敌方英雄造成40/80/160伤害，并施加4/8/16灼伤。"),
                    new(CardKeywords.Consume, "使用后摧毁此卡牌，仅本场战斗生效。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 70 })),
            new TagSet([GameTags.Consumable]), initialLevel: 2,
            levels: [CreateLevel(2, 40, 4), CreateLevel(3, 80, 8), CreateLevel(4, 160, 16)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage, int burn) => new(level,
        new Dictionary<StringName, int>
        { [GameAttributeKeys.AttackDamage] = damage, [GameAttributeKeys.Burn] = burn },
        [new AbilityDefinition(new StringName("ability.attribute_attack_burn_and_consume"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 70,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                new ApplyAttributeStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.Burn),
                new DestroyCardEffectDefinition(false)])]);
}
