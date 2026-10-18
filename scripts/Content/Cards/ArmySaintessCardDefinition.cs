using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 随军圣女帕拉帝恩人类卡牌定义（内容层）。
public sealed class ArmySaintessCardDefinition : CardDefinition
{
    public ArmySaintessCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.army_saintess"), "随军圣女", new StringName("paladin"),
                CardSize.Small, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/army_saintess-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却6秒，消耗10魔法，己方所有存活战场人类卡牌攻击增加5/10/20/40，包含此卡牌与零攻击卡牌，可累计，仅本场战斗生效。"),
                ]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            {
                [GameAttributeKeys.CooldownTicks] = 60,
            })), new TagSet([GameTags.Human]), initialLevel: 1,
            levels: [CreateLevel(1, 5), CreateLevel(2, 10), CreateLevel(3, 20), CreateLevel(4, 40)]) { }

    private static CardLevelDefinition CreateLevel(int level, int bonus) => new(level, null,
        [new AbilityDefinition(new StringName("ability.increase_allied_human_attack"),
            AbilityActivation.Active, AbilityTarget.SelfCard, 10, 60,
            [new ModifyTaggedAlliedCardsAttributeEffectDefinition(GameTags.Human, GameAttributeKeys.AttackDamage,
                bonus, RequiresAttributeSupport: false)])]);
}
