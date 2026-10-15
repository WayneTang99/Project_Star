using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 小天使圣骑士卡牌定义，组合治疗与敌方指定标签发动成长（内容层）。
public sealed class LittleAngelCardDefinition : CardDefinition
{
    public LittleAngelCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.little_angel"), "小天使", new StringName("paladin"),
                CardSize.Small, [GameElements.Light],
                illustration: new StringName("res://art/ui/card-face/artwork/little_angel-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却5秒，魔法消耗0，治疗己方英雄10/20/40/80生命。"),
                    new(CardKeywords.Echo, "敌方存活战场恶魔或亡灵卡牌每次发动后，此卡牌治疗增加5/10/15/20，可累加，仅本场生效；多重分别触发，同时具有两种标签也只触发一次。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 50 })),
            new TagSet(), initialLevel: 1,
            levels: [CreateLevel(1, 10, 5), CreateLevel(2, 20, 10), CreateLevel(3, 40, 15), CreateLevel(4, 80, 20)]) { }

    private static CardLevelDefinition CreateLevel(int level, int healing, int growth) => new(level, null,
        [
            new AbilityDefinition("ability.heal", AbilityActivation.Active, AbilityTarget.AlliedHero,
                0, 50, [new HealEffectDefinition(healing)]),
            new AbilityDefinition("ability.enemy_tag_healing_growth", AbilityActivation.EchoOnEnemyCardActivated,
                AbilityTarget.SelfCard, 0, 0,
                [new IncreaseSourceCardAttributeEffectDefinition(GameAttributeKeys.HealingBonus, growth)],
                triggerCardAnyTags: [GameTags.Demon, GameTags.Undead]),
        ]);
}
