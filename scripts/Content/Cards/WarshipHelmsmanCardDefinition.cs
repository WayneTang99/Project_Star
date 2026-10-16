using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 战舰舵手圣骑士人类卡牌定义（内容层）。
public sealed class WarshipHelmsmanCardDefinition : CardDefinition
{
    public WarshipHelmsmanCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("card.warship_helmsman"), "战舰舵手", new StringName("paladin"),
            CardSize.Small, [GameElements.Water],
            illustration: new StringName("res://art/ui/card-face/artwork/warship_helmsman-illustration.png"),
            descriptionEntries:
            [new(CardKeywords.Activate, "冷却6/5/4/3秒，魔法消耗0，己方随机1张战场载具卡牌获得1秒疾速。")]),
            baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
            { [GameAttributeKeys.CooldownTicks] = 60 })), new TagSet([GameTags.Human]), initialLevel: 1,
            levels: [CreateLevel(1, 60), CreateLevel(2, 50), CreateLevel(3, 40), CreateLevel(4, 30)]) { }

    private static CardLevelDefinition CreateLevel(int level, int cooldown) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = cooldown },
        [new AbilityDefinition(new StringName("ability.random_allied_tagged_card_haste"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, cooldown,
            [new ApplyStatusToRandomAlliedCardEffectDefinition(BattleStatus.HasteDuration, 10, GameTags.Vehicle)])]);
}
