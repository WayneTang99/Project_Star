using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 登神者帕拉帝恩卡牌内容定义（内容层）。
public sealed class AscendantCardDefinition : CardDefinition
{
    public AscendantCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.ascendant"), "登神者", new StringName("paladin"), CardSize.Medium, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/ascendant-illustration.png"), descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却3秒，魔法消耗0，造成此卡牌攻击力的伤害（初始攻击力1）。"),
                new(CardKeywords.Echo, "当此卡牌发动后，攻击力永久增加1/2/3/4；包含多重的额外发动。"),
                new(CardKeywords.Quest, "累计发动20次，攻击力+80。"),
                new(CardKeywords.Quest, "累计发动60次，获得多重1，此卡牌改为光属性。"),
                new(CardKeywords.Quest, "累计发动120次，发动时额外施加此卡牌当前攻击力的10%灼伤（向下取整），并获得神明标签。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.AttackDamage] = 1, [GameAttributeKeys.CooldownTicks] = 30 })),
            new TagSet([GameTags.Human]), initialLevel: 1,
            levels: [CreateLevel(1), CreateLevel(2), CreateLevel(3), CreateLevel(4)], quests:
            [
                new CardQuestDefinition(new StringName("quest.ascendant.attack"),
                    new SourceCardActivationQuestConditionDefinition(), 20,
                    [AttributeReward(new StringName("ability.quest_attack_bonus"), GameAttributeKeys.AttackDamage, 80)]),
                new CardQuestDefinition(new StringName("quest.ascendant.multicast"),
                    new SourceCardActivationQuestConditionDefinition(), 60,
                    [AttributeReward(new StringName("ability.quest_multicast_bonus"), GameAttributeKeys.Multicast, 1)],
                    unlockedElementKeys: [GameElements.Light]),
                new CardQuestDefinition(new StringName("quest.ascendant.divinity"),
                    new SourceCardActivationQuestConditionDefinition(), 120, [],
                    [new ApplySourceAttributePercentStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.AttackDamage, 10)],
                    unlockedTags: [GameTags.Deity]),
            ]) { }

    private static CardLevelDefinition CreateLevel(int level) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = 1, [GameAttributeKeys.CooldownTicks] = 30 },
        [
            new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, 30, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            new AbilityDefinition(new StringName("ability.permanent_source_attack_growth"), AbilityActivation.EchoOnSourceCardActivated,
                AbilityTarget.SelfCard, 0, 0, [new IncreaseSourceCardAttributeEffectDefinition(GameAttributeKeys.AttackDamage, level, Permanent: true)]),
        ]);

    private static AbilityDefinition AttributeReward(StringName key, StringName attribute, int amount) =>
        new(key, AbilityActivation.PassiveWhileEnabled, AbilityTarget.SelfCard, 0, 0,
            [new ModifyAttributeEffectDefinition(attribute, amount)]);
}
