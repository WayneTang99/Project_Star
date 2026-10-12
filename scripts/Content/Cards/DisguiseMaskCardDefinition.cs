using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 伪装面具瓦洛斯卡牌内容定义（内容层）。
public sealed class DisguiseMaskCardDefinition : CardDefinition
{
    public DisguiseMaskCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.disguise_mask"), "伪装面具", new StringName("valos"),
                CardSize.Small, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/disguise_mask-illustration.png"),
                descriptionEntries:
                [new(CardKeywords.Aura, "己方当前英雄以外的卡牌（含无阵营）冷却缩减4%/8%/16%。"),
                 new(CardKeywords.Pickup, "随机获得一张当前英雄以外的同级小型或中型卡牌（含无阵营）；合并时仍触发，按合并后的等级获得。")]),
            baseCombat: new ModifiableAttributeSet()), new TagSet([GameTags.Equipment]), initialLevel: 2,
            levels: [CreateLevel(2, 4), CreateLevel(3, 8), CreateLevel(4, 16)]) { }

    private static CardLevelDefinition CreateLevel(int level, int percent) => new(level, null,
        [new AbilityDefinition(new StringName("ability.other_hero_cooldown_aura"), AbilityActivation.PassiveAura,
            AbilityTarget.SelfCard, 0, 0, [new ReduceOtherHeroCardCooldownAuraEffectDefinition(percent)]),
         new AbilityDefinition(new StringName("ability.pickup_random_other_hero_card"), AbilityActivation.PassiveOnPickup,
            AbilityTarget.AlliedHero, 0, 0,
            [new GrantRandomOtherHeroSourceLevelCardEffectDefinition(CardSize.Small, CardSize.Medium)])]);
}
