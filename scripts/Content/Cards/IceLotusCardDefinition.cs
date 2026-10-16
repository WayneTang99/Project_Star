using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 冰莲极云植物：双方迟缓与己方受到迟缓后的随机敌方迟缓。
public sealed class IceLotusCardDefinition : CardDefinition
{
    public IceLotusCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.ice_lotus"), "冰莲", new StringName("jiyun"), CardSize.Small, [GameElements.Ice],
            illustration: new StringName("res://art/ui/card-face/artwork/ice_lotus-illustration.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却3秒，魔法消耗0，给予己方随机1张和敌方随机1张存活战场卡牌各1秒迟缓。"),
                new(CardKeywords.Echo, "当己方卡牌受到迟缓后，给予敌方随机1张存活战场卡牌1/2秒迟缓。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 30 })), new TagSet([GameTags.Plant]), initialLevel: 3,
            levels: [CreateLevel(3, 10), CreateLevel(4, 20)]) { }

    private static CardLevelDefinition CreateLevel(int level, int echoTicks) => new(level, null,
        [
            new AbilityDefinition(new StringName("ability.slow_both_sides"), AbilityActivation.Active, AbilityTarget.SelfCard, 0, 30,
                [new ApplyStatusToRandomAlliedCardEffectDefinition(BattleStatus.SlowDuration, 10),
                    new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.SlowDuration, 10)]),
            new AbilityDefinition(new StringName("ability.slow_enemy_on_allied_slow_gained"), AbilityActivation.EchoOnAlliedCardStatusGained,
                AbilityTarget.SelfCard, 0, 0, [new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.SlowDuration, echoTicks)],
                triggerStatus: BattleStatus.SlowDuration),
        ]);
}
