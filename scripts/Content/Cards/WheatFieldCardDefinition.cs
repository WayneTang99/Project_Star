using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 麦田圣骑士卡牌定义（内容层）。
public sealed class WheatFieldCardDefinition : CardDefinition
{
    public WheatFieldCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.wheat_field"), "麦田", new StringName("paladin"),
                CardSize.Large, [GameElements.Wood],
                illustration: new StringName("res://art/ui/card-face/artwork/wheat_field-illustration.png"), descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却12/11/10秒，魔法消耗0，己方存活战场人类卡牌获得1秒充能。"),
                    new(CardKeywords.Echo, "己方战场人类卡牌主动发动时，此卡牌获得1秒疾速；多重发动分别触发。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 120 })), new TagSet([GameTags.Plant, GameTags.Location]), initialLevel: 2,
            levels: [CreateLevel(2, 120), CreateLevel(3, 110), CreateLevel(4, 100)]) { }

    private static CardLevelDefinition CreateLevel(int level, int cooldown) => new(level,
        new Dictionary<StringName, int> { [GameAttributeKeys.CooldownTicks] = cooldown },
        [
            new AbilityDefinition(new StringName("ability.charge_allied_tagged_cards"), AbilityActivation.Active,
                AbilityTarget.SelfCard, 0, cooldown, [new ChargeTaggedAlliedCardsEffectDefinition(GameTags.Human, 10)]),
            new AbilityDefinition(new StringName("ability.haste_on_allied_tagged_card_activation"),
                AbilityActivation.EchoOnMatchingAlliedCardActivated, AbilityTarget.SelfCard, 0, 0,
                [new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 10)], triggerCardTag: GameTags.Human),
        ]);
}
