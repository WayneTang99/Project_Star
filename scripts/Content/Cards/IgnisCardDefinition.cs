using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 天启套装无阵营卡牌，组合通用发动与日蚀进入能力。
public sealed class IgnisCardDefinition : CardDefinition
{
    public IgnisCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.ignis"), "战争之手伊格尼斯", new StringName("neutral"),
            CardSize.Medium, [GameElements.Fire], setKey: new StringName("set.apocalypse"),
            illustration: new StringName("res://art/ui/card-face/artwork/ignis-illustration-neutral-v2.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却5秒，对敌方英雄施加12/24/48点灼伤。"),
                new(CardKeywords.Echo, "进入日蚀时间时，此卡牌灼伤翻倍，仅本场战斗生效。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 50 })), new TagSet([GameTags.Human]), initialLevel: 2,
            levels: [CreateLevel(2, 12), CreateLevel(3, 24), CreateLevel(4, 48)]) { }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.Burn] = amount },
        [
            new AbilityDefinition(new StringName("ability.apply_burn"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 50, [new ApplyAttributeStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.Burn)]),
            new AbilityDefinition(new StringName("ability.eclipse_double_burn"), AbilityActivation.PassiveOnEclipseStart,
                AbilityTarget.SelfCard, 0, 0, [new MultiplySourceCardAttributeEffectDefinition(GameAttributeKeys.Burn, 2)]),
        ]);
}

