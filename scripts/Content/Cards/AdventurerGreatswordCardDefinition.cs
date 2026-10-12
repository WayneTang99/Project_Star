using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 冒险者大剑罗宾装备定义，复用己方英雄等级伤害（内容层）。
public sealed class AdventurerGreatswordCardDefinition : CardDefinition
{
    public AdventurerGreatswordCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.adventurer_greatsword"), "冒险者大剑", new StringName("robin"),
            CardSize.Large, [GameElements.General], setKey: new StringName("set.adventurer"),
            illustration: new StringName("res://art/ui/card-face/artwork/adventurer_greatsword-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "冷却8秒，对敌方英雄造成等同于己方英雄等级×20/40的伤害。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 80 })), new TagSet([GameTags.Equipment]), initialLevel: 3,
            levels: [CreateLevel(3, 20), CreateLevel(4, 40)]) { }

    private static CardLevelDefinition CreateLevel(int level, int multiplier) => new(level, null,
        [new AbilityDefinition(new StringName("ability.hero_level_scaled_attack"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 80, [new SourceHeroLevelScaledDamageEffectDefinition(multiplier)])]);
}
