using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Content.Cards;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;

using static Project_Star.Presentation.Verification.VerificationFixtures;

namespace Project_Star.Presentation.Verification;

// 正式卡牌的通用战斗机制组合验证（表现层验证模块）。
internal static class CardCombatChecks
{
    internal static bool CheckBoarCard()
    {
        var definition = new BoarCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.Key != new StringName("card.boar")
            || identity.FactionKey != GameFactions.Neutral
            || identity.Size != CardSize.Medium
            || identity.ElementKeys.Count != 1
            || identity.ElementKeys[0] != GameElements.General
            || !definition.Tags.Contains(GameTags.Beast)
            || TagDisplayNames.Get(GameTags.Beast) != "野兽"
            || definition.InitialLevel != 1
            || definition.SupportsLevel(5))
            return false;

        var expectedDamage = new[] { 20, 30, 50, 100 };
        for (var level = 1; level <= 4; level++)
        {
            var levelDefinition = definition.GetLevel(level);
            if (levelDefinition is null
                || levelDefinition.BaseCombatValues[GameAttributeKeys.AttackDamage] != expectedDamage[level - 1]
                || levelDefinition.Abilities.Count != 1
                || levelDefinition.Abilities[0].Activation != AbilityActivation.Active
                || levelDefinition.Abilities[0].CooldownTicks != 60
                || levelDefinition.Abilities[0].Effects[0]
                    is not SourceHeroHealthScaledAttributeDamageEffectDefinition { BypassArmor: false })
                return false;

            var boarId = EntityId.New();
            var opponentId = EntityId.New();
            var opponentAttack = new AbilityDefinition(
                new StringName("test.boar_opponent_attack"),
                AbilityActivation.Active,
                AbilityTarget.EnemyHero,
                0,
                40,
                [new DamageEffectDefinition(25)]);
            var setup = new BattleSetup(
                new BattleSideSetup(
                    new HeroBattleSetup(EntityId.New(), 100, 0),
                    [new CardBattleSetup(boarId, 0, expectedDamage[level - 1], 60,
                        levelDefinition.Abilities, UseLegacyAttack: false, Tags: definition.Tags,
                        OccupiedSlots: 2, ElementKeys: identity.ElementKeys)]),
                new BattleSideSetup(
                    new HeroBattleSetup(EntityId.New(), 1000, 3),
                    [new CardBattleSetup(opponentId, 0, 50, 40,
                        [opponentAttack], UseLegacyAttack: false)]),
                1,
                new BattleTick(121),
                new BattleTick(1000));
            var result = new CombatSimulator().Simulate(setup);
            var damage = result.Events.OfType<DamageDealtEvent>()
                .Where(value => value.SourceCardId == boarId).ToArray();
            var firstDamage = expectedDamage[level - 1] * 75 / 100;
            var secondDamage = expectedDamage[level - 1] / 2;
            if (damage.Length != 2
                || damage[0].Tick.Value != 60
                || damage[0].TargetSide != SideId.Opponent
                || damage[0].RawDamage != firstDamage
                || damage[0].ArmorAbsorbed != 3
                || damage[0].HealthDamage != firstDamage - 3
                || damage[1].Tick.Value != 120
                || damage[1].TargetSide != SideId.Opponent
                || damage[1].RawDamage != secondDamage
                || damage[1].ArmorAbsorbed != 0
                || damage[1].HealthDamage != secondDamage)
                return false;
        }

        return true;
    }

    internal static bool CheckLightCavalry()
    {
        var definition = new LightCavalryCardDefinition();
        var identity = definition.Attributes.Identity;
        var level = definition.GetLevel(1)!;
        if (identity.FactionKey != new StringName("paladin")
            || identity.Size != CardSize.Medium
            || identity.ElementKeys.Count != 1
            || identity.ElementKeys[0] != GameElements.General
            || !definition.Tags.Contains(GameTags.Human)
            || definition.InitialLevel != 1
            || !definition.SupportsLevel(4)
            || definition.SupportsLevel(5)
            || level.BaseCombatValues[GameAttributeKeys.AttackDamage] != 10)
        {
            return false;
        }

        var cavalryId = EntityId.New();
        var attackerId = EntityId.New();
        var inertHumanId = EntityId.New();
        var attackerAbility = new AbilityDefinition(
            new StringName("test.attribute_attack"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            40,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]);
        var humanTags = new TagSet([GameTags.Human]);
        var setup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 1000, 0),
                [
                    new CardBattleSetup(cavalryId, 0, 10, 30, level.Abilities, UseLegacyAttack: false, Tags: humanTags),
                    new CardBattleSetup(attackerId, 2, 5, 40, [attackerAbility], UseLegacyAttack: false, Tags: humanTags),
                    new CardBattleSetup(inertHumanId, 3, 0, 0, Array.Empty<AbilityDefinition>(), UseLegacyAttack: false, Tags: humanTags),
                ]),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0), Array.Empty<CardBattleSetup>()),
            1,
            new BattleTick(81),
            new BattleTick(1000));
        var result = new CombatSimulator().Simulate(setup);
        var cavalryActivationTicks = new List<long>();
        var cavalryDamage = new List<int>();
        var cavalryTargetsCorrect = true;
        var attackerDamage = new List<int>();
        var changedCards = new List<EntityId>();

        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is AbilityActivatedEvent activation && activation.SourceCardId == cavalryId)
                cavalryActivationTicks.Add(activation.Tick.Value);
            if (battleEvent is DamageDealtEvent damage && damage.SourceCardId == cavalryId)
            {
                cavalryDamage.Add(damage.RawDamage);
                cavalryTargetsCorrect &= damage.TargetSide == SideId.Opponent;
            }
            if (battleEvent is DamageDealtEvent damageByAttacker && damageByAttacker.SourceCardId == attackerId)
                attackerDamage.Add(damageByAttacker.RawDamage);
            if (battleEvent is CardAttributeChangedEvent changed)
                changedCards.Add(changed.CardId);
        }

        return string.Join(",", cavalryActivationTicks) == "30,80"
            && string.Join(",", cavalryDamage) == "10,20"
            && cavalryTargetsCorrect
            && string.Join(",", attackerDamage) == "15,25"
            && changedCards.Count == 4
            && !changedCards.Contains(inertHumanId);
    }

    internal static bool CheckArmguard()
    {
        var definition = new ArmguardCardDefinition();
        var level = definition.GetLevel(1)!;
        if (definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || definition.Attributes.Identity.Size != CardSize.Small
            || definition.Attributes.Identity.ElementKeys[0] != GameElements.General
            || !definition.Tags.Contains(GameTags.Equipment)
            || definition.InitialLevel != 1
            || !definition.SupportsLevel(4)
            || definition.SupportsLevel(5)
            || definition.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.Multicast) != 1)
        {
            return false;
        }

        var armguardId = EntityId.New();
        var opponentCardId = EntityId.New();
        var opponentAttack = new AbilityDefinition(
            new StringName("test.armguard_target"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            51,
            [new DamageEffectDefinition(20)]);
        var setup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0),
                [new CardBattleSetup(
                    armguardId,
                    0,
                    5,
                    50,
                    level.Abilities,
                    UseLegacyAttack: false,
                    Tags: definition.Tags,
                    Multicast: 1,
                    ArmorAmount: level.BaseCombatValues[GameAttributeKeys.Armor])]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0),
                [new CardBattleSetup(opponentCardId, 0, 20, 51, [opponentAttack], UseLegacyAttack: false)]),
            1,
            new BattleTick(52),
            new BattleTick(1000));
        var result = new CombatSimulator().Simulate(setup);
        var armguardActivations = 0;
        var armguardDamage = 0;
        var absorbed = -1;
        var healthDamage = -1;
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is AbilityActivatedEvent activation
                && activation.SourceCardId == armguardId
                && activation.Tick.Value == 50)
            {
                armguardActivations++;
            }
            if (battleEvent is DamageDealtEvent damage && damage.SourceCardId == armguardId)
                armguardDamage += damage.RawDamage;
            if (battleEvent is DamageDealtEvent attack && attack.SourceCardId == opponentCardId)
            {
                absorbed = attack.ArmorAbsorbed;
                healthDamage = attack.HealthDamage;
            }
        }

        return armguardActivations == 2
            && armguardDamage == 10
            && absorbed == 10
            && healthDamage == 10;
    }

    internal static bool CheckThornArmor()
    {
        var definition = new ThornArmorCardDefinition();
        var levelOne = definition.GetLevel(1)!;
        var levelTwo = definition.GetLevel(2)!;
        var levelThree = definition.GetLevel(3)!;
        var levelFour = definition.GetLevel(4)!;
        if (definition.InitialLevel != 1
            || !definition.SupportsLevel(1)
            || !definition.SupportsLevel(2)
            || !definition.SupportsLevel(3)
            || !definition.SupportsLevel(4)
            || definition.SupportsLevel(5)
            || definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || definition.Attributes.Identity.Size != CardSize.Medium
            || definition.Attributes.Identity.ElementKeys.Count != 1
            || definition.Attributes.Identity.ElementKeys[0] != GameElements.General
            || !definition.Tags.Contains(GameTags.Equipment)
            || levelOne.Abilities[0].CooldownTicks != 80
            || levelTwo.Abilities[0].CooldownTicks != 70
            || levelThree.Abilities[0].CooldownTicks != 60
            || levelFour.Abilities[0].CooldownTicks != 50
            || levelOne.BaseCombatValues[GameAttributeKeys.Armor] != 10
            || levelTwo.BaseCombatValues[GameAttributeKeys.Armor] != 20
            || levelThree.BaseCombatValues[GameAttributeKeys.Armor] != 40
            || levelFour.BaseCombatValues[GameAttributeKeys.Armor] != 80
            || levelOne.Abilities[0].Effects[0]
                is not GainSourceHeroArmorFromAttributeEffectDefinition { AttributeKey: var armorKey }
            || armorKey != GameAttributeKeys.Armor
            || levelOne.Abilities[0].Effects[1]
                is not SourceHeroArmorDamageEffectDefinition { BonusAttributeKey: var bonusKey }
            || bonusKey != GameAttributeKeys.AttackDamage)
        {
            return false;
        }

        var thornArmorId = EntityId.New();
        var opponentAttackerId = EntityId.New();
        var opponentAttack = new AbilityDefinition(
            new StringName("test.thorn_armor_target"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            81,
            [new DamageEffectDefinition(20)]);
        var setup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 5, ManaRegen: 0),
                [new CardBattleSetup(
                    thornArmorId,
                    0,
                    0,
                    80,
                    levelOne.Abilities,
                    UseLegacyAttack: false,
                    Tags: definition.Tags,
                    ArmorAmount: levelOne.BaseCombatValues[GameAttributeKeys.Armor])]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 20, ManaRegen: 0),
                [new CardBattleSetup(
                    opponentAttackerId,
                    0,
                    20,
                    81,
                    [opponentAttack],
                    UseLegacyAttack: false)]),
            1,
            new BattleTick(82),
            new BattleTick(1000));
        var result = new CombatSimulator().Simulate(setup);
        var thornDamageCorrect = false;
        var gainedArmorCorrect = false;
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is DamageDealtEvent thornDamage
                && thornDamage.SourceCardId == thornArmorId)
            {
                thornDamageCorrect = thornDamage.RawDamage == 15
                    && thornDamage.ArmorAbsorbed == 15
                    && thornDamage.HealthDamage == 0;
            }
            if (battleEvent is DamageDealtEvent counterattack
                && counterattack.SourceCardId == opponentAttackerId)
            {
                gainedArmorCorrect = counterattack.ArmorAbsorbed == 15
                    && counterattack.HealthDamage == 5;
            }
        }

        return thornDamageCorrect && gainedArmorCorrect;
    }

    internal static bool CheckArcaneShield()
    {
        var definition = new ArcaneShieldCardDefinition();
        var levelTwo = definition.GetLevel(2)!;
        var levelThree = definition.GetLevel(3)!;
        var levelFour = definition.GetLevel(4)!;
        if (definition.InitialLevel != 2
            || definition.SupportsLevel(1)
            || !definition.SupportsLevel(2)
            || !definition.SupportsLevel(3)
            || !definition.SupportsLevel(4)
            || definition.SupportsLevel(5)
            || definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || definition.Attributes.Identity.Size != CardSize.Medium
            || definition.Attributes.Identity.ElementKeys.Count != 1
            || definition.Attributes.Identity.ElementKeys[0] != GameElements.Light
            || !definition.Tags.Contains(GameTags.Equipment)
            || levelTwo.Abilities[0].CooldownTicks != 80
            || levelThree.Abilities[0].CooldownTicks != 70
            || levelFour.Abilities[0].CooldownTicks != 60
            || levelTwo.Abilities[0].ManaCost != 20
            || levelThree.Abilities[0].ManaCost != 40
            || levelFour.Abilities[0].ManaCost != 80)
        {
            return false;
        }

        var spenderId = EntityId.New();
        var shieldId = EntityId.New();
        var attackerId = EntityId.New();
        var spender = new AbilityDefinition(
            new StringName("test.mana_spender"),
            AbilityActivation.Active,
            AbilityTarget.SelfCard,
            10,
            1,
            [new DestroyCardEffectDefinition(false)]);
        var attack = new AbilityDefinition(
            new StringName("test.arcane_shield_target"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            81,
            [new DamageEffectDefinition(100)]);
        var setup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0, InitialMana: 100, ManaRegen: 0),
                [
                    new CardBattleSetup(spenderId, 0, 0, 1, [spender], UseLegacyAttack: false),
                    new CardBattleSetup(shieldId, 1, 0, 80, levelTwo.Abilities, UseLegacyAttack: false),
                ]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0, ManaRegen: 0),
                [new CardBattleSetup(attackerId, 0, 100, 81, [attack], UseLegacyAttack: false)]),
            1,
            new BattleTick(82),
            new BattleTick(1000));
        var result = new CombatSimulator().Simulate(setup);
        var manaSpent = 0;
        var shieldActivatedAtExpectedTick = false;
        var absorbedExpectedArmor = false;
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is ManaChangedEvent mana && mana.Side == SideId.Player)
                manaSpent -= mana.Amount;
            if (battleEvent is AbilityActivatedEvent activation
                && activation.SourceCardId == shieldId
                && activation.Tick.Value == 80)
            {
                shieldActivatedAtExpectedTick = true;
            }
            if (battleEvent is DamageDealtEvent damage && damage.SourceCardId == attackerId)
                absorbedExpectedArmor = damage.ArmorAbsorbed == 30 && damage.HealthDamage == 70;
        }

        return manaSpent == 30 && shieldActivatedAtExpectedTick && absorbedExpectedArmor;
    }

    internal static bool CheckMilitaryBoots()
    {
        var definition = new MilitaryBootsCardDefinition();
        var levelOne = definition.GetLevel(1)!;
        var levelTwo = definition.GetLevel(2)!;
        var levelThree = definition.GetLevel(3)!;
        var levelFour = definition.GetLevel(4)!;
        var levelOneEffect = levelOne.Abilities[0].Effects[0]
            as ApplyStatusToAdjacentAlliedCardsEffectDefinition;
        if (definition.InitialLevel != 1
            || !definition.SupportsLevel(1)
            || !definition.SupportsLevel(2)
            || !definition.SupportsLevel(3)
            || !definition.SupportsLevel(4)
            || definition.SupportsLevel(5)
            || definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || definition.Attributes.Identity.Size != CardSize.Small
            || definition.Attributes.Identity.ElementKeys.Count != 1
            || definition.Attributes.Identity.ElementKeys[0] != GameElements.General
            || !definition.Tags.Contains(GameTags.Equipment)
            || levelOne.Abilities[0].ManaCost != 0
            || levelOne.Abilities[0].CooldownTicks != 50
            || levelOneEffect is null
            || levelOneEffect.Amount != 10
            || levelOneEffect.BonusTag != GameTags.Human
            || levelOneEffect.BonusMultiplier != 2
            || ((ApplyStatusToAdjacentAlliedCardsEffectDefinition)levelTwo.Abilities[0].Effects[0]).Amount != 20
            || ((ApplyStatusToAdjacentAlliedCardsEffectDefinition)levelThree.Abilities[0].Effects[0]).Amount != 30
            || ((ApplyStatusToAdjacentAlliedCardsEffectDefinition)levelFour.Abilities[0].Effects[0]).Amount != 40)
        {
            return false;
        }

        var humanId = EntityId.New();
        var bootsId = EntityId.New();
        var generalId = EntityId.New();
        var distantId = EntityId.New();
        var marker = new AbilityDefinition(
            new StringName("test.haste_marker"),
            AbilityActivation.Active,
            AbilityTarget.AlliedHero,
            0,
            80,
            [new ArmorEffectDefinition(0)]);
        var setup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0, ManaRegen: 0),
                [
                    new CardBattleSetup(
                        humanId,
                        0,
                        0,
                        80,
                        [marker],
                        UseLegacyAttack: false,
                        Tags: new TagSet([GameTags.Human]),
                        OccupiedSlots: 2),
                    new CardBattleSetup(bootsId, 2, 0, 50, levelOne.Abilities, UseLegacyAttack: false),
                    new CardBattleSetup(generalId, 3, 0, 80, [marker], UseLegacyAttack: false),
                    new CardBattleSetup(distantId, 5, 0, 80, [marker], UseLegacyAttack: false),
                ]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0, ManaRegen: 0),
                Array.Empty<CardBattleSetup>()),
            1,
            new BattleTick(81),
            new BattleTick(1000));
        var result = new CombatSimulator().Simulate(setup);
        var humanTick = -1L;
        var generalTick = -1L;
        var distantTick = -1L;
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is not AbilityActivatedEvent activation) continue;
            if (activation.SourceCardId == humanId) humanTick = activation.Tick.Value;
            if (activation.SourceCardId == generalId) generalTick = activation.Tick.Value;
            if (activation.SourceCardId == distantId) distantTick = activation.Tick.Value;
        }

        return humanTick == 65 && generalTick == 70 && distantTick == 80;
    }

    internal static bool CheckHolyGriffin()
    {
        var definition = new HolyGriffinCardDefinition();
        var levelTwo = definition.GetLevel(2)!;
        var levelThree = definition.GetLevel(3)!;
        var levelFour = definition.GetLevel(4)!;
        var activeEffect = levelTwo.Abilities[0].Effects[0]
            as ApplyStatusToAdjacentAlliedCardsEffectDefinition;
        var passiveEffect = levelTwo.Abilities[1].Effects[0]
            as ModifyAdjacentTaggedCardAttributeOnStatusGainedEffectDefinition;
        if (definition.InitialLevel != 2
            || definition.SupportsLevel(1)
            || !definition.SupportsLevel(2)
            || !definition.SupportsLevel(3)
            || !definition.SupportsLevel(4)
            || definition.SupportsLevel(5)
            || definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || definition.Attributes.Identity.Size != CardSize.Large
            || definition.Attributes.Identity.ElementKeys.Count != 1
            || definition.Attributes.Identity.ElementKeys[0] != GameElements.Light
            || !definition.Tags.Contains(GameTags.Beast)
            || !definition.Tags.Contains(GameTags.Mount)
            || levelTwo.Abilities[0].Activation != AbilityActivation.Active
            || levelTwo.Abilities[0].CooldownTicks != 50
            || activeEffect is null
            || activeEffect.Status != BattleStatus.HasteDuration
            || activeEffect.Amount != 10
            || activeEffect.BonusTag != GameTags.Human
            || activeEffect.BonusMultiplier != 1
            || levelTwo.Abilities[1].Activation != AbilityActivation.PassiveAura
            || passiveEffect is null
            || passiveEffect.Status != BattleStatus.HasteDuration
            || passiveEffect.RequiredTag != GameTags.Human
            || passiveEffect.AttributeKey != GameAttributeKeys.AttackDamage
            || passiveEffect.Amount != 10
            || ((ModifyAdjacentTaggedCardAttributeOnStatusGainedEffectDefinition)
                levelThree.Abilities[1].Effects[0]).Amount != 20
            || ((ModifyAdjacentTaggedCardAttributeOnStatusGainedEffectDefinition)
                levelFour.Abilities[1].Effects[0]).Amount != 30)
        {
            return false;
        }

        var bootsDefinition = new MilitaryBootsCardDefinition();
        var bootsId = EntityId.New();
        var humanId = EntityId.New();
        var griffinId = EntityId.New();
        var humanAttack = new AbilityDefinition(
            new StringName("test.holy_griffin_human_attack"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            100,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]);
        var setup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0, ManaRegen: 0),
                [
                    new CardBattleSetup(
                        bootsId,
                        0,
                        0,
                        50,
                        bootsDefinition.GetLevel(1)!.Abilities,
                        UseLegacyAttack: false,
                        OccupiedSlots: 1),
                    new CardBattleSetup(
                        humanId,
                        1,
                        5,
                        100,
                        [humanAttack],
                        UseLegacyAttack: false,
                        Tags: new TagSet([GameTags.Human]),
                        OccupiedSlots: 1),
                    new CardBattleSetup(
                        griffinId,
                        2,
                        0,
                        50,
                        levelTwo.Abilities,
                        UseLegacyAttack: false,
                        Tags: definition.Tags,
                        OccupiedSlots: 3),
                ]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0, ManaRegen: 0),
                Array.Empty<CardBattleSetup>()),
            1,
            new BattleTick(51),
            new BattleTick(1000));
        var result = new CombatSimulator().Simulate(setup);
        var bonusEvents = 0;
        var finalAttack = 0;
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is not CardAttributeChangedEvent changed
                || changed.CardId != humanId
                || changed.AttributeKey != GameAttributeKeys.AttackDamage)
            {
                continue;
            }
            bonusEvents++;
            finalAttack = changed.CurrentValue;
        }

        return bonusEvents == 2 && finalAttack == 25;
    }

    internal static bool CheckNun()
    {
        var definition = new NunCardDefinition();
        var levelOne = definition.GetLevel(1)!;
        var levelTwo = definition.GetLevel(2)!;
        var levelThree = definition.GetLevel(3)!;
        var levelFour = definition.GetLevel(4)!;
        var levelOneAbility = levelOne.Abilities[0];
        var charge = levelOneAbility.Effects[1]
            as ChargeRandomOtherAlliedElementCardEffectDefinition;
        if (definition.InitialLevel != 1
            || !definition.SupportsLevel(1)
            || !definition.SupportsLevel(2)
            || !definition.SupportsLevel(3)
            || !definition.SupportsLevel(4)
            || definition.SupportsLevel(5)
            || definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || definition.Attributes.Identity.Size != CardSize.Small
            || definition.Attributes.Identity.ElementKeys.Count != 1
            || definition.Attributes.Identity.ElementKeys[0] != GameElements.Light
            || !definition.Tags.Contains(GameTags.Human)
            || levelOneAbility.Activation != AbilityActivation.Active
            || levelOneAbility.Target != AbilityTarget.AlliedHero
            || levelOneAbility.ManaCost != 10
            || levelOneAbility.CooldownTicks != 60
            || ((HealEffectDefinition)levelOneAbility.Effects[0]).Amount != 10
            || ((HealEffectDefinition)levelTwo.Abilities[0].Effects[0]).Amount != 20
            || ((HealEffectDefinition)levelThree.Abilities[0].Effects[0]).Amount != 40
            || ((HealEffectDefinition)levelFour.Abilities[0].Effects[0]).Amount != 80
            || charge is null
            || charge.ElementKey != GameElements.Light
            || charge.AmountTicks != 10)
        {
            return false;
        }

        var lightTargetId = EntityId.New();
        var nunId = EntityId.New();
        var darkTargetId = EntityId.New();
        var opponentAttackerId = EntityId.New();
        var finishingAttack = new AbilityDefinition(
            new StringName("test.nun_light_target"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            70,
            [new DamageEffectDefinition(100)]);
        var darkAttack = new AbilityDefinition(
            new StringName("test.nun_dark_target"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            70,
            [new DamageEffectDefinition(1)]);
        var openingAttack = new AbilityDefinition(
            new StringName("test.nun_opening_attack"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            10,
            [new DamageEffectDefinition(30), new DestroyCardEffectDefinition(false)]);
        var setup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0, InitialMana: 10, ManaRegen: 0),
                [
                    new CardBattleSetup(
                        lightTargetId,
                        0,
                        100,
                        70,
                        [finishingAttack],
                        UseLegacyAttack: false,
                        ElementKeys: [GameElements.Light]),
                    new CardBattleSetup(
                        nunId,
                        1,
                        0,
                        60,
                        levelOne.Abilities,
                        UseLegacyAttack: false,
                        Tags: definition.Tags,
                        ElementKeys: definition.Attributes.Identity.ElementKeys),
                    new CardBattleSetup(
                        darkTargetId,
                        2,
                        1,
                        70,
                        [darkAttack],
                        UseLegacyAttack: false,
                        ElementKeys: [GameElements.Dark]),
                ]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0, ManaRegen: 0),
                [new CardBattleSetup(
                    opponentAttackerId,
                    0,
                    30,
                    10,
                    [openingAttack],
                    UseLegacyAttack: false)]),
            1,
            new BattleTick(80),
            new BattleTick(1000));
        var result = new CombatSimulator().Simulate(setup);
        var chargedCorrectTarget = false;
        var lightActivatedImmediately = false;
        var manaSpent = 0;
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is CardChargedEvent charged
                && charged.SourceCardId == nunId
                && charged.TargetCardId == lightTargetId
                && charged.AmountTicks == 10)
            {
                chargedCorrectTarget = true;
            }
            if (battleEvent is AbilityActivatedEvent activation
                && activation.SourceCardId == lightTargetId
                && activation.Tick.Value == 60)
            {
                lightActivatedImmediately = true;
            }
            if (battleEvent is ManaChangedEvent mana && mana.Side == SideId.Player)
                manaSpent -= mana.Amount;
        }

        return result.PlayerRemainingHealth == 80
            && manaSpent == 10
            && chargedCorrectTarget
            && lightActivatedImmediately;
    }

    internal static bool CheckCathedral()
    {
        var definition = new CathedralCardDefinition();
        var level = definition.GetLevel(4)!;
        var aura = level.Abilities[0];
        var auraEffect = aura.Effects[0] as GrantMulticastToAlliedElementCardsEffectDefinition;
        if (definition.InitialLevel != 4
            || definition.SupportsLevel(1)
            || definition.SupportsLevel(2)
            || definition.SupportsLevel(3)
            || !definition.SupportsLevel(4)
            || definition.SupportsLevel(5)
            || definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || definition.Attributes.Identity.Size != CardSize.Large
            || definition.Attributes.Identity.ElementKeys.Count != 1
            || definition.Attributes.Identity.ElementKeys[0] != GameElements.Light
            || !definition.Tags.Contains(GameTags.Location)
            || aura.Activation != AbilityActivation.PassiveAura
            || aura.ManaCost != 0
            || aura.CooldownTicks != 0
            || auraEffect is null
            || auraEffect.ElementKey != GameElements.Light
            || auraEffect.Amount != 1)
        {
            return false;
        }

        var firstCathedralId = EntityId.New();
        var secondCathedralId = EntityId.New();
        var benchCathedralId = EntityId.New();
        var lightCardId = EntityId.New();
        var generalCardId = EntityId.New();
        var attack = new AbilityDefinition(
            new StringName("test.cathedral_attack"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            1,
            [new DamageEffectDefinition(1)]);
        var stackedSetup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0, ManaRegen: 0),
                [
                    new CardBattleSetup(firstCathedralId, 0, 0, 0, level.Abilities,
                        UseLegacyAttack: false, OccupiedSlots: 3, ElementKeys: [GameElements.Light]),
                    new CardBattleSetup(secondCathedralId, 3, 0, 0, level.Abilities,
                        UseLegacyAttack: false, OccupiedSlots: 3, ElementKeys: [GameElements.Light]),
                    new CardBattleSetup(benchCathedralId, 0, 0, 0, level.Abilities,
                        IsOnBench: true, UseLegacyAttack: false, OccupiedSlots: 3, ElementKeys: [GameElements.Light]),
                    new CardBattleSetup(lightCardId, 6, 1, 1, [attack],
                        UseLegacyAttack: false, ElementKeys: [GameElements.Light]),
                    new CardBattleSetup(generalCardId, 7, 1, 1, [attack],
                        UseLegacyAttack: false, ElementKeys: [GameElements.General]),
                ]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0),
                Array.Empty<CardBattleSetup>()),
            1,
            new BattleTick(1),
            new BattleTick(1000));
        var stackedResult = new CombatSimulator().Simulate(stackedSetup);
        var lightDamageCount = stackedResult.Events.Count(value =>
            value is DamageDealtEvent damage && damage.SourceCardId == lightCardId);
        var generalDamageCount = stackedResult.Events.Count(value =>
            value is DamageDealtEvent damage && damage.SourceCardId == generalCardId);

        var temporaryCathedralId = EntityId.New();
        var repeatedLightCardId = EntityId.New();
        var destroySelf = new AbilityDefinition(
            new StringName("test.cathedral_destroy_self"),
            AbilityActivation.EchoOnAbilityActivated,
            AbilityTarget.SelfCard,
            0,
            0,
            [new DestroyCardEffectDefinition(false)]);
        var removableSetup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0, ManaRegen: 0),
                [
                    new CardBattleSetup(repeatedLightCardId, 0, 1, 1, [attack],
                        UseLegacyAttack: false, ElementKeys: [GameElements.Light]),
                    new CardBattleSetup(temporaryCathedralId, 1, 0, 0, [aura, destroySelf],
                        UseLegacyAttack: false, OccupiedSlots: 3, ElementKeys: [GameElements.Light]),
                ]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0),
                Array.Empty<CardBattleSetup>()),
            1,
            new BattleTick(2),
            new BattleTick(1000));
        var removableResult = new CombatSimulator().Simulate(removableSetup);
        var firstTickDamage = removableResult.Events.Count(value =>
            value is DamageDealtEvent damage
            && damage.SourceCardId == repeatedLightCardId
            && damage.Tick.Value == 1);
        var secondTickDamage = removableResult.Events.Count(value =>
            value is DamageDealtEvent damage
            && damage.SourceCardId == repeatedLightCardId
            && damage.Tick.Value == 2);

        return lightDamageCount == 3
            && generalDamageCount == 1
            && firstTickDamage == 2
            && secondTickDamage == 1;
    }

    internal static bool CheckBlacksmith()
    {
        var definition = new BlacksmithCardDefinition();
        var levelTwo = definition.GetLevel(2)!;
        var levelThree = definition.GetLevel(3)!;
        var levelFour = definition.GetLevel(4)!;
        var attackModifier = levelTwo.Abilities[0].Effects[0]
            as ModifyTaggedAlliedCardsAttributeEffectDefinition;
        var armorModifier = levelTwo.Abilities[0].Effects[1]
            as ModifyTaggedAlliedCardsAttributeEffectDefinition;
        if (definition.InitialLevel != 2
            || definition.SupportsLevel(1)
            || !definition.SupportsLevel(2)
            || !definition.SupportsLevel(3)
            || !definition.SupportsLevel(4)
            || definition.SupportsLevel(5)
            || definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || definition.Attributes.Identity.Size != CardSize.Medium
            || definition.Attributes.Identity.ElementKeys.Count != 1
            || definition.Attributes.Identity.ElementKeys[0] != GameElements.General
            || !definition.Tags.Contains(GameTags.Location)
            || levelTwo.Abilities[0].ManaCost != 0
            || levelTwo.Abilities[0].CooldownTicks != 60
            || attackModifier is null
            || attackModifier.RequiredTag != GameTags.Equipment
            || attackModifier.AttributeKey != GameAttributeKeys.AttackDamage
            || attackModifier.Amount != 10
            || armorModifier is null
            || armorModifier.RequiredTag != GameTags.Equipment
            || armorModifier.AttributeKey != GameAttributeKeys.Armor
            || armorModifier.Amount != 10
            || ((ModifyTaggedAlliedCardsAttributeEffectDefinition)levelThree.Abilities[0].Effects[0]).Amount != 20
            || ((ModifyTaggedAlliedCardsAttributeEffectDefinition)levelFour.Abilities[0].Effects[0]).Amount != 40)
        {
            return false;
        }

        var blacksmithId = EntityId.New();
        var attributeEquipmentId = EntityId.New();
        var fixedEquipmentId = EntityId.New();
        var shieldId = EntityId.New();
        var thornArmorId = EntityId.New();
        var enemyAttackerId = EntityId.New();
        var attributeEquipmentAbility = new AbilityDefinition(
            new StringName("test.blacksmith_attribute_equipment"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            61,
            [
                new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                new GainSourceHeroArmorFromAttributeEffectDefinition(GameAttributeKeys.Armor),
            ]);
        var fixedEquipmentAbility = new AbilityDefinition(
            new StringName("test.blacksmith_fixed_equipment"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            61,
            [new DamageEffectDefinition(1)]);
        var enemyAttack = new AbilityDefinition(
            new StringName("test.blacksmith_enemy_attack"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            81,
            [new DamageEffectDefinition(100)]);
        var shieldDefinition = new ArcaneShieldCardDefinition();
        var shieldLevel = shieldDefinition.GetLevel(2)!;
        var thornDefinition = new ThornArmorCardDefinition();
        var thornLevel = thornDefinition.GetLevel(1)!;
        var equipmentTags = new TagSet([GameTags.Equipment]);
        var setup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 100, 0, InitialMana: 100, ManaRegen: 0),
                [
                    new CardBattleSetup(blacksmithId, 0, 0, 60, levelTwo.Abilities, UseLegacyAttack: false),
                    new CardBattleSetup(attributeEquipmentId, 2, 5, 61, [attributeEquipmentAbility],
                        UseLegacyAttack: false, Tags: equipmentTags, ArmorAmount: 5),
                    new CardBattleSetup(fixedEquipmentId, 3, 99, 61, [fixedEquipmentAbility],
                        UseLegacyAttack: false, Tags: equipmentTags, ArmorAmount: 99),
                    new CardBattleSetup(shieldId, 4, 0, 80, shieldLevel.Abilities,
                        UseLegacyAttack: false, Tags: shieldDefinition.Tags,
                        ElementKeys: [GameElements.Light], ArmorAmount: 0),
                    new CardBattleSetup(thornArmorId, 5, 0, 80, thornLevel.Abilities,
                        UseLegacyAttack: false, Tags: thornDefinition.Tags, ArmorAmount: 10),
                ]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0),
                [new CardBattleSetup(enemyAttackerId, 0, 100, 81, [enemyAttack], UseLegacyAttack: false)]),
            1,
            new BattleTick(82),
            new BattleTick(1000));
        var result = new CombatSimulator().Simulate(setup);
        var attributeDamage = -1;
        var fixedDamage = -1;
        var absorbedArmor = -1;
        var thornDamage = -1;
        foreach (var battleEvent in result.Events)
        {
            if (battleEvent is not DamageDealtEvent damage) continue;
            if (damage.SourceCardId == attributeEquipmentId) attributeDamage = damage.RawDamage;
            if (damage.SourceCardId == fixedEquipmentId) fixedDamage = damage.RawDamage;
            if (damage.SourceCardId == enemyAttackerId) absorbedArmor = damage.ArmorAbsorbed;
            if (damage.SourceCardId == thornArmorId) thornDamage = damage.RawDamage;
        }

        return attributeDamage == 15
            && fixedDamage == 1
            && thornDamage == 75
            && absorbedArmor == 65;
    }

    internal static bool CheckHolySlashingBlade()
    {
        var definition = new HolySlashingBladeCardDefinition();
        var level = definition.GetLevel(4)!;
        var active = level.Abilities[0];
        var passive = level.Abilities[1];
        if (definition.InitialLevel != 4
            || definition.SupportsLevel(3)
            || !definition.SupportsLevel(4)
            || definition.SupportsLevel(5)
            || definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || definition.Attributes.Identity.Size != CardSize.Large
            || definition.Attributes.Identity.ElementKeys.Count != 1
            || definition.Attributes.Identity.ElementKeys[0] != GameElements.Light
            || !definition.Tags.Contains(GameTags.Equipment)
            || definition.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.AttackDamage) != 200
            || active.CooldownTicks != 100
            || active.Effects[1] is not DestroyRandomEnemyCardEffectDefinition
            || passive.Activation != AbilityActivation.PassiveAura
            || passive.Effects[0] is not MultiplySourceAttributePerDestroyedTaggedCardEffectDefinition)
        {
            return false;
        }

        var bladeId = EntityId.New();
        var alliedUndeadId = EntityId.New();
        var enemySmallDemonId = EntityId.New();
        var enemyMediumUndeadId = EntityId.New();
        var enemyLargeDemonId = EntityId.New();
        var alliedSelfDestroy = new AbilityDefinition(
            new StringName("test.allied_undead_self_destroy"),
            AbilityActivation.Active,
            AbilityTarget.SelfCard,
            0,
            1,
            [new DestroyCardEffectDefinition(false)]);
        var setup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 5000, 0, ManaRegen: 0),
                [
                    new CardBattleSetup(bladeId, 0, 200, 100, level.Abilities,
                        UseLegacyAttack: false, OccupiedSlots: 3, ElementKeys: [GameElements.Light]),
                    new CardBattleSetup(alliedUndeadId, 3, 0, 1, [alliedSelfDestroy],
                        UseLegacyAttack: false, Tags: new TagSet([GameTags.Undead])),
                ]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 5000, 0, ManaRegen: 0),
                [
                    new CardBattleSetup(enemySmallDemonId, 0, 0, 0, Array.Empty<AbilityDefinition>(),
                        UseLegacyAttack: false, Tags: new TagSet([GameTags.Demon])),
                    new CardBattleSetup(enemyMediumUndeadId, 1, 0, 0, Array.Empty<AbilityDefinition>(),
                        UseLegacyAttack: false, Tags: new TagSet([GameTags.Undead]), OccupiedSlots: 2),
                    new CardBattleSetup(enemyLargeDemonId, 3, 0, 0, Array.Empty<AbilityDefinition>(),
                        UseLegacyAttack: false, Tags: new TagSet([GameTags.Demon]), OccupiedSlots: 3),
                ]),
            17,
            new BattleTick(200),
            new BattleTick(1000));
        var result = new CombatSimulator().Simulate(setup);
        var repeated = new CombatSimulator().Simulate(setup);
        var bladeDamage = result.Events
            .OfType<DamageDealtEvent>()
            .Where(value => value.SourceCardId == bladeId)
            .Select(value => value.RawDamage)
            .ToArray();
        var enemyDestroyed = result.Events
            .OfType<CardDestroyedEvent>()
            .Where(value => value.Side == SideId.Opponent)
            .Select(value => value.CardId)
            .ToArray();
        var repeatedDestroyed = repeated.Events
            .OfType<CardDestroyedEvent>()
            .Where(value => value.Side == SideId.Opponent)
            .Select(value => value.CardId)
            .ToArray();

        return bladeDamage.SequenceEqual([400, 800])
            && enemyDestroyed.Length == 1
            && enemyDestroyed[0] == enemySmallDemonId
            && !enemyDestroyed.Contains(enemyMediumUndeadId)
            && !enemyDestroyed.Contains(enemyLargeDemonId)
            && enemyDestroyed.SequenceEqual(repeatedDestroyed);
    }

    internal static bool CheckOrderCrusader()
    {
        var definition = new OrderCrusaderCardDefinition();
        var level = definition.GetLevel(2)!;
        if (definition.InitialLevel != 2
            || definition.SupportsLevel(1)
            || !definition.SupportsLevel(4)
            || definition.SupportsLevel(5)
            || definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || definition.Attributes.Identity.Size != CardSize.Large
            || definition.Attributes.Identity.ElementKeys[0] != GameElements.Light
            || !definition.Tags.Contains(GameTags.Human)
            || level.BaseCombatValues[GameAttributeKeys.AttackDamage] != 40
            || level.Abilities[0].CooldownTicks != 80
            || level.Abilities[1].Effects[0] is not GrantTagToEnemySizeCardsEffectDefinition
            || level.Abilities[1].Effects[1]
                is not IncreaseSourceAttributePerEnemyTaggedCardEffectDefinition increase
            || increase.Amount != 10)
        {
            return false;
        }

        var crusaderId = EntityId.New();
        var destroyedSmallId = EntityId.New();
        var selfDestroy = new AbilityDefinition(
            new StringName("test.order_crusader_enemy_self_destroy"),
            AbilityActivation.Active,
            AbilityTarget.SelfCard,
            0,
            1,
            [new DestroyCardEffectDefinition(false)]);
        var countSetup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0),
                [new CardBattleSetup(crusaderId, 0, 40, 80, level.Abilities,
                    UseLegacyAttack: false, Tags: definition.Tags, OccupiedSlots: 3)]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0),
                [
                    new CardBattleSetup(destroyedSmallId, 0, 0, 1, [selfDestroy], UseLegacyAttack: false),
                    new CardBattleSetup(EntityId.New(), 1, 0, 0, Array.Empty<AbilityDefinition>(), UseLegacyAttack: false),
                    new CardBattleSetup(EntityId.New(), 2, 0, 0, Array.Empty<AbilityDefinition>(),
                        UseLegacyAttack: false, Tags: new TagSet([GameTags.Demon]), OccupiedSlots: 2),
                    new CardBattleSetup(EntityId.New(), 4, 0, 0, Array.Empty<AbilityDefinition>(),
                        UseLegacyAttack: false, Tags: new TagSet([GameTags.Demon]), OccupiedSlots: 3),
                ]),
            1,
            new BattleTick(80),
            new BattleTick(1000));
        var countResult = new CombatSimulator().Simulate(countSetup);
        var damage = countResult.Events
            .OfType<DamageDealtEvent>()
            .Single(value => value.SourceCardId == crusaderId)
            .RawDamage;

        var trigger = new AbilityDefinition(
            new StringName("test.order_crusader_aura_removal_trigger"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            1,
            [new DamageEffectDefinition(1)]);
        var destroyCrusader = new AbilityDefinition(
            new StringName("test.order_crusader_destroy_aura_source"),
            AbilityActivation.EchoOnAbilityActivated,
            AbilityTarget.SelfCard,
            0,
            0,
            [new DestroyCardEffectDefinition(false)]);
        var destroyDemon = new AbilityDefinition(
            new StringName("test.order_crusader_destroy_demon"),
            AbilityActivation.Active,
            AbilityTarget.EnemyHero,
            0,
            2,
            [new DestroyRandomEnemyCardEffectDefinition([GameTags.Demon], [CardSize.Small])]);
        var expiringCrusaderAbilities = level.Abilities.Concat([destroyCrusader]).ToArray();
        var neutralSmallId = EntityId.New();
        var expirySetup = new BattleSetup(
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0),
                [
                    new CardBattleSetup(EntityId.New(), 0, 1, 1, [trigger], UseLegacyAttack: false),
                    new CardBattleSetup(EntityId.New(), 1, 0, 2, [destroyDemon], UseLegacyAttack: false),
                    new CardBattleSetup(EntityId.New(), 2, 40, 80, expiringCrusaderAbilities,
                        UseLegacyAttack: false, Tags: definition.Tags, OccupiedSlots: 3),
                ]),
            new BattleSideSetup(
                new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0),
                [new CardBattleSetup(neutralSmallId, 0, 0, 0, Array.Empty<AbilityDefinition>(), UseLegacyAttack: false)]),
            1,
            new BattleTick(2),
            new BattleTick(1000));
        var expiryResult = new CombatSimulator().Simulate(expirySetup);

        return damage == 70
            && countResult.Events.OfType<CardDestroyedEvent>()
                .Any(value => value.CardId == destroyedSmallId)
            && !expiryResult.Events.OfType<CardDestroyedEvent>()
                .Any(value => value.CardId == neutralSmallId);
    }


}
