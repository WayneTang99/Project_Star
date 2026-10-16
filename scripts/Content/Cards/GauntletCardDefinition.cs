using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 护手圣骑士装备卡牌定义（内容层）。
public sealed class GauntletCardDefinition : CardDefinition
{
    public GauntletCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.gauntlet"), "护手", new StringName("paladin"), CardSize.Small, [GameElements.General],
            illustration: new StringName("res://art/ui/card-face/artwork/gauntlet-illustration.png"),
            descriptionEntries:
            [
                new(CardKeywords.Aura, "此卡牌具有1层多重（每层多重额外发动一次）。"),
                new(CardKeywords.Activate, "冷却5秒，魔法消耗0，己方英雄获得10/20/40/80护甲。"),
                new(CardKeywords.Echo, "当直接相邻的己方战场装备卡牌发动后，对敌方英雄造成10/20/40/80伤害；多重发动分别触发。"),
            ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 50, [GameAttributeKeys.Multicast] = 1 })),
            new TagSet([GameTags.Equipment]), initialLevel: 1,
            levels: [CreateLevel(1, 10), CreateLevel(2, 20), CreateLevel(3, 40), CreateLevel(4, 80)]) { }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.Armor] = amount, [GameAttributeKeys.AttackDamage] = amount },
        [
            new AbilityDefinition(new StringName("ability.gain_attribute_armor"), AbilityActivation.Active,
                AbilityTarget.AlliedHero, 0, 50, [new GainSourceHeroArmorFromAttributeEffectDefinition(GameAttributeKeys.Armor)]),
            Echo(AdjacentCardSide.Left, new StringName("ability.damage_on_left_equipment_activation")),
            Echo(AdjacentCardSide.Right, new StringName("ability.damage_on_right_equipment_activation")),
        ]);

    private static AbilityDefinition Echo(AdjacentCardSide side, StringName key) => new(key,
        AbilityActivation.EchoOnAdjacentAlliedCardActivated, AbilityTarget.EnemyHero, 0, 0,
        [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)],
        triggerCardTag: GameTags.Equipment, triggerCardSide: side);
}
