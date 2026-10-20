using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 滚石陷阱冒险家消耗品：摧毁双方中小型卡牌并在获得疾速时自身充能（内容层）。
public sealed class RollingBoulderTrapCardDefinition : CardDefinition
{
    public RollingBoulderTrapCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.rolling_boulder_trap"), "滚石陷阱", new StringName("robin"),
            CardSize.Large, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/rolling_boulder_trap-illustration.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却30/24秒，魔法消耗0，摧毁双方所有存活战场小型和中型卡牌，仅本场战斗生效。"),
                new(CardKeywords.Echo, "当此卡牌获得疾速后，此卡牌获得1秒充能。"),
                new(CardKeywords.Consume, "使用后摧毁此卡牌，仅本场战斗生效。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 300 })),
            new TagSet([GameTags.Trap, GameTags.Consumable]), initialLevel: 3,
            levels: [CreateLevel(3, 300), CreateLevel(4, 240)]) { }

    private static CardLevelDefinition CreateLevel(int level, int cooldownTicks) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = cooldownTicks },
        [
            new AbilityDefinition(new StringName("ability.destroy_small_medium_battlefield_and_consume"), AbilityActivation.Active,
                AbilityTarget.SelfCard, 0, cooldownTicks,
                [new DestroyBattlefieldCardsEffectDefinition(CardSize.Medium), new DestroyCardEffectDefinition(false)]),
            new AbilityDefinition(new StringName("ability.charge_on_source_haste"), AbilityActivation.EchoOnSourceCardStatusGained,
                AbilityTarget.SelfCard, 0, 0, [new ChargeSourceCardEffectDefinition(10)],
                triggerStatus: BattleStatus.HasteDuration),
        ]);
}
