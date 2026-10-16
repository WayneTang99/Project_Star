using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 月影喵莫娜卡牌定义，组合随机禁锢与相邻标签冷却光环（内容层）。
public sealed class MoonshadowCatCardDefinition : CardDefinition
{
    public MoonshadowCatCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.moonshadow_cat"), "月影喵", new StringName("mona"),
                CardSize.Small, [GameElements.Dark],
                illustration: new StringName("res://art/ui/card-face/artwork/moonshadow_cat-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却9/8/7/6秒，魔法消耗0，给予敌方随机1张未被摧毁的战场卡牌1秒禁锢。"),
                    new(CardKeywords.Aura, "此卡牌每与己方1张存活战场人类卡牌直接相邻，冷却增加1秒。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 90 })),
            new TagSet([GameTags.Beast]), initialLevel: 1,
            levels: [CreateLevel(1), CreateLevel(2), CreateLevel(3), CreateLevel(4)]) { }

    private static CardLevelDefinition CreateLevel(int level) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = (10 - level) * 10 },
        [
            new AbilityDefinition(new StringName("ability.random_enemy_immobilize"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, 0, (10 - level) * 10,
                [new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.ImmobilizeDuration, 10)]),
            new AbilityDefinition(new StringName("ability.cooldown_per_adjacent_tagged_card"), AbilityActivation.PassiveAura,
                AbilityTarget.SelfCard, 0, 0,
                [new IncreaseSourceCooldownPerAdjacentAlliedTaggedCardEffectDefinition(GameTags.Human, 10)]),
        ]);
}
