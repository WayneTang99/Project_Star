using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 万象舆图罗宾卡牌定义，组合地域拾取成长、出售任务与价值生命光环（内容层）。
public sealed class MyriadAtlasCardDefinition : CardDefinition
{
    public MyriadAtlasCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.myriad_atlas"), "万象舆图", new StringName("robin"),
            CardSize.Medium, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/myriad_atlas-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Trade, "在战场区或备战区时，每成功获得1张地域卡牌后，此卡牌价值永久增加1/2/3/4；合并获得也触发。"),
             new(CardKeywords.Quest, "累计出售5张地域卡牌，解锁光环，己方英雄获得等同于此卡牌当前价值×20的最大生命值；当前生命同步增减，来源失效撤销该贡献，仅本场战斗生效。") ])),
            new TagSet(), initialLevel: 1,
            levels: [CreateLevel(1), CreateLevel(2), CreateLevel(3), CreateLevel(4)],
            quests:
            [new CardQuestDefinition(new StringName("quest.myriad_atlas.sell_locations"),
                new SoldTaggedCardQuestConditionDefinition(GameTags.Location), 5,
                [new AbilityDefinition(new StringName("ability.source_value_max_health_aura"),
                    AbilityActivation.PassiveAura, AbilityTarget.AlliedHero, 0, 0,
                    [new IncreaseHeroMaxHealthBySourceValueEffectDefinition(20)])])]) { }

    private static CardLevelDefinition CreateLevel(int level) => new(level, null, [],
        acquisitionAttributeBonuses: [new TaggedCardAcquisitionAttributeBonus(GameTags.Location, GameAttributeKeys.Value, level)]);
}
