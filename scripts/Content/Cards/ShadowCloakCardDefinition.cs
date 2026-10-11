using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 暗影斗篷瓦洛斯卡牌内容定义（内容层）。
public sealed class ShadowCloakCardDefinition : CardDefinition
{
    public ShadowCloakCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.shadow_cloak"), "暗影斗篷", new StringName("valos"),
                CardSize.Medium, [GameElements.Dark],
                illustration: new StringName("res://art/ui/card-face/artwork/shadow_cloak-illustration.png"), descriptionEntries:
                [new(CardKeywords.Echo, "每场战斗，当己方英雄生命值首次少于最大生命的50%时，己方英雄获得1/2秒隐身；隐身期间无法受到攻击造成的伤害。")]),
            baseCombat: new ModifiableAttributeSet()), new TagSet([GameTags.Equipment]), initialLevel: 3,
            levels: [CreateLevel(3, 10), CreateLevel(4, 20)]) { }

    private static CardLevelDefinition CreateLevel(int level, int durationTicks) => new(level, null,
        [new AbilityDefinition(new StringName("ability.low_health_stealth"), AbilityActivation.EchoOnSourceHeroHealthBelow,
            AbilityTarget.AlliedHero, 0, 0, [new GainSourceHeroStealthEffectDefinition(durationTicks)],
            triggerHeroHealthPercent: 50)]);
}
