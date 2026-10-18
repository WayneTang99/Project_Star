using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 光辉腰带帕拉帝恩装备卡牌定义（内容层）。
public sealed class RadiantBeltCardDefinition : CardDefinition
{
    public RadiantBeltCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.radiant_belt"), "光辉腰带", new StringName("paladin"), CardSize.Medium, [GameElements.Light],
            setKey: new StringName("set.radiant"), illustration: new StringName("res://art/ui/card-face/artwork/radiant_belt-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Aura, "己方英雄最大生命值增加己方存活战场装备卡牌数量×30/60/100，包含此卡牌；当前生命同步增减，撤销加成时最低保留1生命，仅本场战斗生效。") ])),
            new TagSet([GameTags.Equipment]), initialLevel: 2,
            levels: [CreateLevel(2, 30), CreateLevel(3, 60), CreateLevel(4, 100)]) { }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level, null,
        [new AbilityDefinition(new StringName("ability.max_health_per_allied_equipment"), AbilityActivation.PassiveAura,
            AbilityTarget.AlliedHero, 0, 0,
            [new IncreaseHeroMaxHealthPerAlliedTaggedCardEffectDefinition(GameTags.Equipment, amount)])]);
}
