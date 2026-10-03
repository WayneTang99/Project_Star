using Godot;
using System.Collections.Generic;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 原木法杖莫娜卡牌内容定义（内容层）。
public sealed class LogStaffCardDefinition : CardDefinition
{
    public LogStaffCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.log_staff"), "原木法杖", new StringName("mona"),
                CardSize.Medium, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/placeholder.svg"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却8秒，消耗10魔法，对敌方英雄造成10/20/40/80伤害。"),
                    new(CardKeywords.Quest, "拾取5张火属性卡牌，解锁：发动时额外施加8/12/20/32灼伤。"),
                    new(CardKeywords.Quest, "拾取5张木属性卡牌，解锁：发动时额外施加8/12/20/32中毒。"),
                    new(CardKeywords.Quest, "拾取10张无属性卡牌，解锁：此卡牌冷却减少3秒。"),
                ]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            {
                [GameAttributeKeys.CooldownTicks] = 80,
            })), new TagSet([GameTags.Equipment]), initialLevel: 1,
            levels: [CreateLevel(1, 10, 8), CreateLevel(2, 20, 12), CreateLevel(3, 40, 20), CreateLevel(4, 80, 32)],
            quests:
            [
                new CardQuestDefinition(new StringName("quest.log_staff.fire"),
                    new AcquiredElementCardQuestConditionDefinition(GameElements.Fire), 5, [],
                    [new ApplyAttributeStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.Burn)]),
                new CardQuestDefinition(new StringName("quest.log_staff.wood"),
                    new AcquiredElementCardQuestConditionDefinition(GameElements.Wood), 5, [],
                    [new ApplyAttributeStatusEffectDefinition(BattleStatus.Poison, GameAttributeKeys.Poison)]),
                new CardQuestDefinition(new StringName("quest.log_staff.general"),
                    new AcquiredElementCardQuestConditionDefinition(GameElements.General), 10,
                    [new AbilityDefinition(new StringName("ability.quest_reduce_cooldown"),
                        AbilityActivation.PassiveWhileEnabled, AbilityTarget.SelfCard, 0, 0,
                        [new ModifyAttributeEffectDefinition(GameAttributeKeys.CooldownTicks, -30)])]),
            ])
    {
    }

    private static CardLevelDefinition CreateLevel(int level, int damage, int status) => new(level,
        new Dictionary<StringName, int>
        {
            [GameAttributeKeys.AttackDamage] = damage,
            [GameAttributeKeys.Burn] = status,
            [GameAttributeKeys.Poison] = status,
        },
        [new AbilityDefinition(new StringName("ability.log_staff_attack"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 10, 80, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)])]);
}
