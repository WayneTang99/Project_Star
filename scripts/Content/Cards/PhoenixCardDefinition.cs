using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 菲尼克斯莫娜卡牌定义，组合灼伤与首次致命伤害救援（内容层）。
public sealed class PhoenixCardDefinition : CardDefinition
{
    public PhoenixCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.phoenix"), "菲尼克斯", new StringName("mona"),
                CardSize.Large, [GameElements.Fire],
                illustration: new StringName("res://art/ui/card-face/artwork/phoenix-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却4秒，魔法消耗0，对敌方英雄施加164灼伤。"),
                    new(CardKeywords.Echo, "每场战斗，己方英雄首次受到致命伤害时，阻止死亡并治疗其最大生命值的50%（向下取整），此卡牌灼伤翻倍，仅本场生效。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 40 })), new TagSet([GameTags.Beast, GameTags.Spirit]), initialLevel: 4,
            levels:
            [
                new CardLevelDefinition(4, new Dictionary<StringName, int> { [GameAttributeKeys.Burn] = 164 },
                [
                    new AbilityDefinition(new StringName("ability.apply_burn"), AbilityActivation.Active,
                        AbilityTarget.EnemyHero, 0, 40,
                        [new ApplyAttributeStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.Burn)]),
                    new AbilityDefinition(new StringName("ability.first_lethal_heal_and_multiply_burn"),
                        AbilityActivation.EchoOnSourceHeroFirstLethalDamage, AbilityTarget.AlliedHero, 0, 0,
                        [new HealMaxHealthPercentEffectDefinition(50),
                            new MultiplySourceCardAttributeEffectDefinition(GameAttributeKeys.Burn, 2)]),
                ]),
            ]) { }
}
