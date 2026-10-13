using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 训练营圣骑士地域卡牌：周期训练人类，拾取时补充同级小型人类（内容层）。
public sealed class TrainingCampCardDefinition : CardDefinition
{
    public TrainingCampCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.training_camp"), "训练营", new StringName("paladin"), CardSize.Medium, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/training_camp-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "冷却3秒，魔法消耗0，己方所有存活战场人类卡牌攻击增加5/10/20，可累加，仅本场战斗生效。"),
             new(CardKeywords.Pickup, "获得一张随机同级小型人类卡牌，不限制归属。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 30 })),
            new TagSet([GameTags.Location]), initialLevel: 2,
            levels: [CreateLevel(2, 5), CreateLevel(3, 10), CreateLevel(4, 20)]) { }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level, null,
        [new AbilityDefinition(new StringName("ability.increase_allied_human_attack"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, 30,
            [new ModifyTaggedAlliedCardsAttributeEffectDefinition(GameTags.Human, GameAttributeKeys.AttackDamage,
                amount, RequiresAttributeSupport: false)]),
         new AbilityDefinition(new StringName("ability.pickup_random_tagged_source_level_card"), AbilityActivation.PassiveOnPickup,
            AbilityTarget.AlliedHero, 0, 0,
            [new GrantRandomTaggedSourceLevelCardEffectDefinition(CardSize.Small, GameTags.Human)])]);
}
