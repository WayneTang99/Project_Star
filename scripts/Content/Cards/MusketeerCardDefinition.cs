using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 火枪手帕拉帝恩卡牌内容定义（内容层）。
public sealed class MusketeerCardDefinition : CardDefinition
{
    public MusketeerCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.musketeer"), "火枪手", new StringName("paladin"),
                CardSize.Small, [GameElements.General],
                illustration: new StringName("res://art/ui/card-face/artwork/musketeer-illustration.png"),
                descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却7秒，魔法消耗0，对敌方英雄造成10/20/40/80伤害，并施加2/4/8/16灼伤。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 70 })),
            new TagSet([GameTags.Human]), initialLevel: 1,
            levels: [CreateLevel(1, 10, 2), CreateLevel(2, 20, 4), CreateLevel(3, 40, 8), CreateLevel(4, 80, 16)]) { }

    private static CardLevelDefinition CreateLevel(int level, int damage, int burn) => new(level,
        new Dictionary<StringName, int>
        { [GameAttributeKeys.AttackDamage] = damage, [GameAttributeKeys.Burn] = burn },
        [new AbilityDefinition(new StringName("ability.attribute_attack_and_burn"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 70,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                new ApplyAttributeStatusEffectDefinition(BattleStatus.Burn, GameAttributeKeys.Burn)])]);
}
