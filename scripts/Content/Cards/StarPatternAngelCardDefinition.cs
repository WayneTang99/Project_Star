using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 星纹天使帕拉帝恩神明卡牌，组合恶魔随机禁锢与无恶魔条件治疗（内容层）。
public sealed class StarPatternAngelCardDefinition : CardDefinition
{
    public StarPatternAngelCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.star_pattern_angel"), "星纹天使", new StringName("paladin"),
            CardSize.Small, [GameElements.Light],
            illustration: new StringName("res://art/ui/card-face/artwork/star_pattern_angel-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Opening, "此卡牌进入飞行。"),
             new(CardKeywords.Activate, "冷却4秒，消耗20魔法，随机给予敌方1/2张存活战场恶魔卡牌1秒禁锢，不重复选取，数量不足时全部选取；敌方无存活战场恶魔卡牌时，改为治疗己方英雄80/160生命。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = 40 })),
            new TagSet([GameTags.Deity]), initialLevel: 3, levels: [CreateLevel(3, 1, 80), CreateLevel(4, 2, 160)]) { }

    private static CardLevelDefinition CreateLevel(int level, int count, int healing) => new(level, null,
        [new AbilityDefinition(new StringName("ability.enter_flying_on_battle_start"), AbilityActivation.PassiveOnBattleStart,
            AbilityTarget.SelfCard, 0, 0, [new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, true)]),
         new AbilityDefinition(new StringName("ability.enemy_tag_control_or_heal"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 20, 40,
            [new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.ImmobilizeDuration, 10, count, GameTags.Demon),
             new HealEffectDefinition(healing, TargetsSourceHero: true, UnlessEnemyTag: GameTags.Demon)])]);
}
