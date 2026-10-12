using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 宝石手套罗宾装备定义，组合攻击与英雄升级随机镶嵌（内容层）。
public sealed class GemGloveCardDefinition : CardDefinition
{
    public GemGloveCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.gem_glove"), "宝石手套", new StringName("robin"),
            CardSize.Small, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/gem_glove-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "冷却6秒，造成20/40/80伤害；拥有5个宝石孔。"),
             new(CardKeywords.Upgrade, "己方英雄每升一级，为此卡牌随机镶嵌一颗宝石；战场区与备战区均生效，孔满时跳过。")],
            gemSocketCount: 5)), new TagSet([GameTags.Equipment]), initialLevel: 2,
            levels: [CreateLevel(2, 20), CreateLevel(3, 40), CreateLevel(4, 80)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage) => new(level,
        new System.Collections.Generic.Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage, [GameAttributeKeys.CooldownTicks] = 60 },
        [new AbilityDefinition(new StringName("ability.gem_glove_attack"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 60, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
         new AbilityDefinition(new StringName("ability.upgrade_socket_random_gem"), AbilityActivation.PassiveOnHeroLevelUp,
            AbilityTarget.SelfCard, 0, 0, [new SocketRandomSourceGemEffectDefinition()], allowsBench: true)]);
}
