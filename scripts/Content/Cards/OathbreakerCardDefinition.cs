using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 破誓者帕拉帝恩卡牌内容定义（内容层）。
public sealed class OathbreakerCardDefinition : CardDefinition
{
    public OathbreakerCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.oathbreaker"), "破誓者", new StringName("paladin"),
                CardSize.Medium, [GameElements.Dark], descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却3秒，魔法消耗0，随机使敌方1/2/3/4张不同的未被摧毁的战场卡牌获得1秒迟缓；不足时影响全部可选卡牌。"),
                    new(CardKeywords.Aura, "双方战场区每有一张未被摧毁的光属性卡牌，此卡牌冷却延长1秒；数量变化时立即调整，保留已走过的冷却进度。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 30 })), new TagSet([GameTags.Human]), initialLevel: 1,
            levels: [CreateLevel(1), CreateLevel(2), CreateLevel(3), CreateLevel(4)]) { }

    private static CardLevelDefinition CreateLevel(int level) => new(level, null,
        [
            new AbilityDefinition(new StringName("ability.slow_random_enemy_cards"), AbilityActivation.Active,
                AbilityTarget.OtherBattlefieldCards, 0, 30,
                [new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.SlowDuration, 10, level)]),
            new AbilityDefinition(new StringName("ability.battlefield_light_cooldown_aura"), AbilityActivation.PassiveAura,
                AbilityTarget.SelfCard, 0, 0,
                [new IncreaseSourceCooldownPerBattlefieldElementCardEffectDefinition(GameElements.Light, 10)]),
        ]);
}
