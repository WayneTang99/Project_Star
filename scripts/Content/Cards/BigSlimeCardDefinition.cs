using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 大史莱姆无阵营卡牌定义，低生命时在原位置随机分裂（内容层）。
public sealed class BigSlimeCardDefinition : CardDefinition
{
    public BigSlimeCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.big_slime"), "大史莱姆", GameFactions.Neutral, CardSize.Medium, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/big_slime-illustration-redraw-20261019.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却6秒，魔法消耗0，对敌方英雄造成40/80/160/320伤害。"),
                new(CardKeywords.Echo, "每场战斗，己方英雄生命首次低于最大生命的50%时，此卡牌在原位置转变为2张随机不同元素的同级小型史莱姆，仅本场战斗生效；1～2级不包含光、暗，无属性史莱姆不参与抽取。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 60 })),
            new TagSet([GameTags.Material]), initialLevel: 1,
            levels: [CreateLevel(1, 40), CreateLevel(2, 80), CreateLevel(3, 160), CreateLevel(4, 320)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage)
    {
        List<CardDefinition> replacements = [new FireSlimeCardDefinition(), new WaterSlimeCardDefinition(),
            new WindSlimeCardDefinition(), new EarthSlimeCardDefinition(), new LightningSlimeCardDefinition(),
            new WoodSlimeCardDefinition(), new IceSlimeCardDefinition()];
        if (level >= 3) { replacements.Add(new LightSlimeCardDefinition()); replacements.Add(new DarkSlimeCardDefinition()); }
        return new CardLevelDefinition(level,
            new Dictionary<StringName, int> { [GameAttributeKeys.AttackDamage] = damage },
            [
                new AbilityDefinition(new StringName("ability.attribute_attack"), AbilityActivation.Active,
                    AbilityTarget.EnemyHero, 0, 60, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
                new AbilityDefinition(new StringName("ability.transform_source_random_cards_on_low_health"),
                    AbilityActivation.EchoOnSourceHeroHealthBelow, AbilityTarget.SelfCard, 0, 0,
                    [new TransformSourceIntoRandomCardsEffectDefinition(replacements.AsReadOnly(), 2)], triggerHeroHealthPercent: 50),
            ]);
    }
}
