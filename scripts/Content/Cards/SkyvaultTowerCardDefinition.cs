using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 苍穹塔圣骑士地域卡牌定义（内容层）。
public sealed class SkyvaultTowerCardDefinition : CardDefinition
{
    public SkyvaultTowerCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.skyvault_tower"), "苍穹塔", new StringName("paladin"),
            CardSize.Large, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/skyvault_tower-illustration.png"),
            descriptionEntries:
            [
                new(CardKeywords.Activate, "冷却7秒，魔法消耗0，己方英雄获得100护甲，治疗己方英雄100生命。"),
                new(CardKeywords.Echo, "当己方人类卡牌发动后，此卡牌获得1秒充能。"),
                new(CardKeywords.Echo, "当己方光属性卡牌发动后，此卡牌护甲和治疗各增加20，仅本场战斗生效。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 70 })), new TagSet([GameTags.Building, GameTags.Location]),
            initialLevel: 4, levels:
            [new CardLevelDefinition(4, new Dictionary<StringName, int>
                { [GameAttributeKeys.Armor] = 100, [GameAttributeKeys.HealingBonus] = 100 },
                [
                    new AbilityDefinition(new StringName("ability.armor_and_healing"), AbilityActivation.Active,
                        AbilityTarget.AlliedHero, 0, 70,
                        [new GainSourceHeroArmorFromAttributeEffectDefinition(GameAttributeKeys.Armor), new HealEffectDefinition(0)]),
                    new AbilityDefinition(new StringName("ability.charge_self_on_allied_human_activation"),
                        AbilityActivation.EchoOnMatchingAlliedCardActivated, AbilityTarget.SelfCard, 0, 0,
                        [new ChargeSourceCardEffectDefinition(10)], triggerCardTag: GameTags.Human),
                    new AbilityDefinition(new StringName("ability.increase_source_armor_and_healing_on_light_activation"),
                        AbilityActivation.EchoOnMatchingAlliedCardActivated, AbilityTarget.SelfCard, 0, 0,
                        [new IncreaseSourceCardAttributeEffectDefinition(GameAttributeKeys.Armor, 20),
                            new IncreaseSourceCardAttributeEffectDefinition(GameAttributeKeys.HealingBonus, 20)],
                        triggerCardElement: GameElements.Light),
                ])]) { }
}
