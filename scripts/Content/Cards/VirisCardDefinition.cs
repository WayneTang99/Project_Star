using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 天启套装圣骑士卡牌，组合通用发动与日蚀进入能力。
public sealed class VirisCardDefinition : CardDefinition
{
    public VirisCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.viris"), "饥荒之手维瑞斯", new StringName("paladin"),
            CardSize.Medium, [GameElements.Wood], setKey: new StringName("set.apocalypse"),
            illustration: new StringName("res://art/ui/card-face/artwork/viris-illustration-angle-v2.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却5秒，对敌方英雄施加24/48点中毒。"),
                new(CardKeywords.Echo, "进入日蚀时间时，此卡牌中毒翻倍，仅本场战斗生效。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 50 })), new TagSet([GameTags.Human]), initialLevel: 3,
            levels: [CreateLevel(3, 24), CreateLevel(4, 48)]) { }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.Poison] = amount },
        [
            new AbilityDefinition(new StringName("ability.apply_poison"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 50, [new ApplyAttributeStatusEffectDefinition(BattleStatus.Poison, GameAttributeKeys.Poison)]),
            new AbilityDefinition(new StringName("ability.eclipse_double_poison"), AbilityActivation.PassiveOnEclipseStart,
                AbilityTarget.SelfCard, 0, 0, [new MultiplySourceCardAttributeEffectDefinition(GameAttributeKeys.Poison, 2)]),
        ]);
}

