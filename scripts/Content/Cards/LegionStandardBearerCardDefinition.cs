using System.Collections.Generic;
using Godot;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Content.Cards;

// 军团旗帜手圣骑士卡牌定义（内容层）。
public sealed class LegionStandardBearerCardDefinition : CardDefinition
{
    public LegionStandardBearerCardDefinition()
        : base(new EntityAttributes<CardIdentityAttributes>(
            new CardIdentityAttributes(new StringName("card.legion_standard_bearer"), "军团旗帜手", new StringName("paladin"),
                CardSize.Small, [GameElements.General], descriptionEntries:
                [
                    new(CardKeywords.Activate, "冷却6秒，随机一张具备攻击能力的己方存活战场人类卡牌攻击+10/20/40，可叠加，仅本场战斗有效。"),
                    new(CardKeywords.Summon, "战斗开始时，若左侧紧邻空位可用，随机召唤一张同级战意旗帜、守护旗帜或怜悯旗帜。"),
                ]), baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                { [GameAttributeKeys.CooldownTicks] = 60 })), new TagSet([GameTags.Human]), initialLevel: 2,
            levels: [CreateLevel(2, 10), CreateLevel(3, 20), CreateLevel(4, 40)]) { }

    private static CardLevelDefinition CreateLevel(int level, int amount) => new(level, null,
        [
            new AbilityDefinition(new StringName("ability.random_allied_human_attack_increase"), AbilityActivation.Active,
                AbilityTarget.SelfCard, 0, 60,
                [new ModifyTaggedAlliedCardsAttributeEffectDefinition(GameTags.Human, GameAttributeKeys.AttackDamage, amount, RandomSingleTarget: true)]),
            new AbilityDefinition(new StringName("ability.summon_random_adjacent_card"), AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.SelfCard, 0, 0,
                [new SummonRandomAdjacentCardEffectDefinition(
                    [new BattleStandardCardDefinition(), new GuardianStandardCardDefinition(), new MercyStandardCardDefinition()], AdjacentCardSide.Left)]),
        ]);
}
