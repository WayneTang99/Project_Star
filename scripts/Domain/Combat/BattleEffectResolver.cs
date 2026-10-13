using System;
using System.Linq;
using Godot;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Combat;

// 通用效果、伤害、光环属性、充能与摧毁结算（领域战斗层内部）。
internal static class BattleEffectResolver
{
    // 累加当前有效光环提供的多重，不修改来源或目标属性。
    internal static int GetEffectiveMulticast(BattleRuntime runtime, CardBattleState card)
    {
        var multicast = card.GetCombatAttribute(GameAttributeKeys.Multicast);
        foreach (var source in runtime.AbilitySources)
        {
            if (source.Side != card.Side || source.Destroyed || source.IsOnBench) continue;
            foreach (var ability in source.Abilities)
            {
                if (ability.Definition.Activation != AbilityActivation.PassiveAura) continue;
                foreach (var effect in ability.Definition.Effects)
                {
                    if (effect is GrantMulticastToAlliedElementCardsEffectDefinition aura
                        && card.ElementKeys.Contains(aura.ElementKey))
                    {
                        multicast = checked(multicast + aura.Amount);
                    }
                }
            }
        }
        return multicast;
    }

    // 按通用效果定义提交当前 Runtime 变化，保持原事件发布顺序。
    internal static void ApplyEffect(BattleRuntime runtime, PendingAbility pending, EffectDefinition effect)
    {
        var definition = pending.Ability.Definition;
        var targetSide = definition.Target == AbilityTarget.EnemyHero
            ? Opposite(pending.Source.Side) : pending.Source.Side;
        var hero = runtime.GetHero(targetSide);
        switch (effect)
        {
            case EnterEclipseEffectDefinition:
                CombatSimulator.EnterEclipse(runtime);
                break;
            case IncreaseHeroMaxHealthPercentEffectDefinition healthGrowth:
                var healthIncrease = checked((int)((long)hero.MaxHealth * healthGrowth.Percent / 100));
                hero.MaxHealth = checked(hero.MaxHealth + healthIncrease);
                hero.Health = checked(hero.Health + healthIncrease);
                break;
            case ApplyPrimaryEffectPercentStatusEffectDefinition primaryStatus:
                var extraAmount = checked((int)((long)pending.PrimaryValue * primaryStatus.Percent / 100));
                if (primaryStatus.Status is BattleStatus.Burn or BattleStatus.Poison)
                    extraAmount = checked(extraAmount + GetEffectiveCombatAttribute(runtime, pending.Source,
                        primaryStatus.Status == BattleStatus.Burn ? GameAttributeKeys.BurnBonus : GameAttributeKeys.PoisonBonus));
                var enemyHero = runtime.GetHero(Opposite(pending.Source.Side));
                var extraApplied = BattleStatusResolver.ApplyStatus(pending.Source, enemyHero,
                    new ApplyStatusEffectDefinition(primaryStatus.Status, BerserkAmount(pending, extraAmount)));
                runtime.Events.Add(new StatusChangedEvent(runtime.Tick, primaryStatus.Status, extraApplied));
                break;
            case SummonAdjacentCardEffectDefinition summon:
                if (pending.Source is not CardBattleState summoner)
                    throw new InvalidOperationException("Adjacent summon requires a card source.");
                SummonAdjacentCard(runtime, summoner, summon);
                break;
            case SummonRandomAdjacentCardEffectDefinition randomSummon:
                if (pending.Source is not CardBattleState randomSummoner)
                    throw new InvalidOperationException("Adjacent summon requires a card source.");
                var templates = randomSummon.Cards.Where(card => CanSummonAdjacentCard(runtime,
                    randomSummoner, card, randomSummon.Side)).ToArray();
                if (templates.Length > 0)
                    SummonAdjacentCard(runtime, randomSummoner,
                        new SummonAdjacentCardEffectDefinition(templates[runtime.NextRandomIndex(templates.Length)], randomSummon.Side));
                break;
            case IncreaseSourceCardAttributeEffectDefinition increase:
                if (pending.Source is not CardBattleState attributeSource)
                    throw new InvalidOperationException("Source attribute increase requires a card source.");
                if (!attributeSource.SupportsCombatAttribute(increase.AttributeKey)) break;
                var increasedValue = attributeSource.AddCombatAttribute(increase.AttributeKey, increase.Amount);
                runtime.Events.Add(new CardAttributeChangedEvent(runtime.Tick, attributeSource.EntityId,
                    increase.AttributeKey, increase.Amount, increasedValue));
                if (increase.Permanent && attributeSource.SummonedCard is null && attributeSource.TransformedIdentity is null)
                    runtime.PermanentAttributeBonuses.Add(new PermanentCardAttributeBonus(
                        attributeSource.EntityId, attributeSource.Side, increase.AttributeKey, increase.Amount));
                break;
            case MultiplySourceCardAttributeEffectDefinition multiplier:
                if (pending.Source is not CardBattleState multipliedSource)
                    throw new InvalidOperationException("Source attribute multiplier requires a card source.");
                if (!multipliedSource.SupportsCombatAttribute(multiplier.AttributeKey)) break;
                var contribution = checked(multipliedSource.GetCombatAttribute(multiplier.AttributeKey) * (multiplier.Multiplier - 1));
                var multipliedValue = multipliedSource.AddCombatAttribute(multiplier.AttributeKey, contribution);
                runtime.Events.Add(new CardAttributeChangedEvent(runtime.Tick, multipliedSource.EntityId,
                    multiplier.AttributeKey, contribution, multipliedValue));
                break;
            case TransformRandomEnemyCardEffectDefinition transform:
                TransformRandomEnemyCard(runtime, pending.Source, transform);
                break;
            case DamageEffectDefinition damage:
                if (ApplyDamage(runtime, pending.Source.EntityId, targetSide, hero, BerserkAmount(pending, damage.Amount), damage.BypassArmor,
                    GetDamageSourceKind(pending.Source)) && !pending.IsEcho) CombatSimulator.EnqueueDamageEchoes(runtime);
                break;
            case SourceHeroLevelScaledDamageEffectDefinition levelDamage:
                var sourceLevel = runtime.GetHero(pending.Source.Side).Level;
                if (ApplyDamage(runtime, pending.Source.EntityId, targetSide, hero,
                    BerserkAmount(pending, checked(sourceLevel * levelDamage.Multiplier)), levelDamage.BypassArmor,
                    GetDamageSourceKind(pending.Source)) && !pending.IsEcho) CombatSimulator.EnqueueDamageEchoes(runtime);
                break;
            case MaxHealthPercentDamageEffectDefinition percentDamage:
                var amount = checked((int)((long)hero.MaxHealth * percentDamage.Percent / 100));
                if (ApplyDamage(runtime, pending.Source.EntityId, targetSide, hero, BerserkAmount(pending, amount), percentDamage.BypassArmor,
                    GetDamageSourceKind(pending.Source)) && !pending.IsEcho) CombatSimulator.EnqueueDamageEchoes(runtime);
                break;
            case AttributeDamageEffectDefinition attributeDamage:
                if (ApplyDamage(
                    runtime,
                    pending.Source.EntityId,
                    targetSide,
                    hero,
                    BerserkAmount(pending, AttributeDamageAmount(runtime, pending, attributeDamage)),
                    attributeDamage.BypassArmor,
                    GetDamageSourceKind(pending.Source)) && !pending.IsEcho) CombatSimulator.EnqueueDamageEchoes(runtime);
                break;
            case SourceHeroHealthScaledAttributeDamageEffectDefinition scaledDamage:
                var healthSourceHero = runtime.GetHero(pending.Source.Side);
                var scaledAmount = checked((int)((long)GetEffectiveCombatAttribute(
                    runtime, pending.Source, scaledDamage.AttributeKey) * healthSourceHero.Health / healthSourceHero.MaxHealth));
                if (ApplyDamage(runtime, pending.Source.EntityId, targetSide, hero, BerserkAmount(pending, scaledAmount),
                    scaledDamage.BypassArmor,
                    GetDamageSourceKind(pending.Source)) && !pending.IsEcho) CombatSimulator.EnqueueDamageEchoes(runtime);
                break;
            case SourceHeroArmorDamageEffectDefinition heroArmorDamage:
                var armorSourceHero = runtime.GetHero(pending.Source.Side);
                var bonusDamage = heroArmorDamage.BonusAttributeKey is not { IsEmpty: false } bonusAttributeKey
                    ? 0
                    : GetEffectiveCombatAttribute(runtime, pending.Source, bonusAttributeKey);
                if (ApplyDamage(
                    runtime,
                    pending.Source.EntityId,
                    targetSide,
                    hero,
                    BerserkAmount(pending, checked(armorSourceHero.Armor + bonusDamage)),
                    heroArmorDamage.BypassArmor,
                    GetDamageSourceKind(pending.Source)) && !pending.IsEcho) CombatSimulator.EnqueueDamageEchoes(runtime);
                break;
            case HealEffectDefinition heal:
                var healing = checked(heal.Amount + GetEffectiveCombatAttribute(runtime, pending.Source, GameAttributeKeys.HealingBonus));
                var healingHero = heal.TargetsSourceHero ? runtime.GetHero(pending.Source.Side) : hero;
                healingHero.Health = (int)Math.Min(healingHero.MaxHealth, (long)healingHero.Health + healing);
                break;
            case HealMaxHealthPercentEffectDefinition percentHeal:
                var percentHealing = checked((int)((long)hero.MaxHealth * percentHeal.Percent / 100)
                    + GetEffectiveCombatAttribute(runtime, pending.Source, GameAttributeKeys.HealingBonus));
                hero.Health = (int)Math.Min(hero.MaxHealth, (long)hero.Health + percentHealing);
                break;
            case GainSourceHeroStealthEffectDefinition stealth:
                var stealthHero = runtime.GetHero(pending.Source.Side);
                stealthHero.StealthDurationTicks = checked(stealthHero.StealthDurationTicks + stealth.DurationTicks);
                runtime.Events.Add(new HeroStealthChangedEvent(runtime.Tick, pending.Source.Side, stealthHero.StealthDurationTicks));
                break;
            case RestoreManaEffectDefinition restoreMana:
                var restored = (int)System.Math.Min(restoreMana.Amount, (long)hero.MaxMana - hero.Mana);
                hero.Mana += restored;
                runtime.Events.Add(new ManaChangedEvent(runtime.Tick, targetSide, restored, hero.Mana));
                break;
            case ArmorEffectDefinition armor:
                hero.Armor = checked(hero.Armor + armor.Amount);
                break;
            case GainSourceHeroArmorEffectDefinition sourceArmor:
                var alliedHero = runtime.GetHero(pending.Source.Side);
                alliedHero.Armor = checked(alliedHero.Armor + sourceArmor.Amount);
                break;
            case GainSourceHeroLevelScaledArmorEffectDefinition levelArmor:
                var levelArmorHero = runtime.GetHero(pending.Source.Side);
                levelArmorHero.Armor = checked(levelArmorHero.Armor + checked(levelArmorHero.Level * levelArmor.Multiplier));
                break;
            case GainSourceHeroArmorFromAttributeEffectDefinition attributeArmor:
                var armorTarget = runtime.GetHero(pending.Source.Side);
                armorTarget.Armor = checked(
                    armorTarget.Armor + GetEffectiveCombatAttribute(runtime, pending.Source, attributeArmor.AttributeKey));
                break;
            case GainArmorEqualToManaSpentEffectDefinition:
                var sourceHero = runtime.GetHero(pending.Source.Side);
                sourceHero.Armor = checked(sourceHero.Armor + sourceHero.ManaSpent);
                break;
            case ApplyAttributeStatusEffectDefinition attributeStatus:
                ApplyEffect(runtime, pending, new ApplyStatusEffectDefinition(attributeStatus.Status,
                    pending.Source.GetCombatAttribute(attributeStatus.AttributeKey)));
                break;
            case ApplySourceAttributePercentStatusEffectDefinition percentStatus:
                var statusAmount = checked((int)((long)GetEffectiveCombatAttribute(runtime, pending.Source,
                    percentStatus.AttributeKey) * percentStatus.Percent / 100));
                ApplyEffect(runtime, pending, new ApplyStatusEffectDefinition(percentStatus.Status, statusAmount));
                break;
            case ApplyStatusEffectDefinition status:
                if (status.Status is BattleStatus.Burn or BattleStatus.Poison)
                    status = status with { Amount = checked(status.Amount
                        + GetEffectiveCombatAttribute(runtime, pending.Source,
                            status.Status == BattleStatus.Burn ? GameAttributeKeys.BurnBonus : GameAttributeKeys.PoisonBonus)) };
                var appliedStatus = status.Status is BattleStatus.Burn or BattleStatus.Poison
                    ? status with { Amount = BerserkAmount(pending, status.Amount) }
                    : status with { Amount = BattleStatusResolver.DurationWithSourceBonus(runtime, pending, status.Status, status.Amount) };
                var statusTarget = definition.Target is AbilityTarget.LeftAdjacentAlliedCard or AbilityTarget.RightAdjacentAlliedCard or AbilityTarget.RightmostAlliedCard
                    ? GetTargetCard(runtime, pending)
                    : definition.Target == AbilityTarget.EventCard ? pending.EventCard : pending.Source;
                if (statusTarget is null) break;
                var appliedAmount = BattleStatusResolver.ApplyStatus(statusTarget, hero, appliedStatus);
                runtime.Events.Add(new StatusChangedEvent(runtime.Tick, status.Status, appliedAmount));
                if (appliedAmount > 0 && statusTarget is CardBattleState statusCard)
                {
                    BattleStatusResolver.ApplyAdjacentStatusReactions(runtime, statusCard, status.Status);
                    if (!pending.IsEcho) CombatSimulator.EnqueueSourceStatusGainedEchoes(runtime, statusCard, status.Status);
                }
                if (appliedAmount > 0 && status.Status == BattleStatus.SlowDuration && !pending.IsEcho)
                    CombatSimulator.EnqueueSlowAppliedEchoes(runtime, pending.Source.Side);
                break;
            case SetSourceCardStateEffectDefinition state:
                if (pending.Source is not CardBattleState stateSource)
                    throw new InvalidOperationException("Card state requires a card source.");
                SetCardState(runtime, pending, stateSource, state.StateKey, state.Enabled);
                break;
            case SetRandomAdjacentAlliedTaggedCardStateEffectDefinition adjacentState:
                if (pending.Source is not CardBattleState adjacentStateSource)
                    throw new InvalidOperationException("Adjacent card state requires a card source.");
                var candidates = runtime.Cards.Where(card => card.Side == adjacentStateSource.Side
                    && card.EntityId != adjacentStateSource.EntityId && !card.Destroyed && !card.IsOnBench
                    && HasEffectiveTag(runtime, card, adjacentState.RequiredTag)
                    && (card.BoardStart + card.OccupiedSlots == adjacentStateSource.BoardStart
                        || card.BoardStart == adjacentStateSource.BoardStart + adjacentStateSource.OccupiedSlots)).ToArray();
                if (candidates.Length > 0)
                    SetCardState(runtime, pending, candidates[runtime.NextRandomIndex(candidates.Length)],
                        adjacentState.StateKey, adjacentState.Enabled);
                break;
            case SetRandomOtherAlliedCardStateEffectDefinition otherState:
                var otherCandidates = runtime.Cards.Where(card => card.Side == pending.Source.Side
                    && card.EntityId != pending.Source.EntityId && !card.Destroyed && !card.IsOnBench).ToArray();
                if (otherCandidates.Length > 0)
                    SetCardState(runtime, pending, otherCandidates[runtime.NextRandomIndex(otherCandidates.Length)],
                        otherState.StateKey, otherState.Enabled);
                break;
            case ApplyStatusToAdjacentAlliedCardsEffectDefinition adjacent:
                if (pending.Source is not CardBattleState adjacentSource)
                    throw new InvalidOperationException("Adjacent card effects require a card source.");
                BattleStatusResolver.ApplyStatusToAdjacentAlliedCards(runtime, pending, adjacentSource, adjacent);
                break;
            case ApplyStatusToRandomEnemyCardEffectDefinition randomStatus:
                BattleStatusResolver.ApplyStatusToRandomEnemyCard(runtime, pending, randomStatus);
                break;
            case ApplyStatusToRandomAlliedCardEffectDefinition alliedStatus:
                BattleStatusResolver.ApplyStatusToRandomAlliedCard(runtime, pending, alliedStatus);
                break;
            case ChargeRandomOtherAlliedElementCardEffectDefinition charge:
                if (pending.Source is not CardBattleState chargeSource)
                    throw new InvalidOperationException("Random card charge requires a card source.");
                ChargeRandomOtherAlliedElementCard(runtime, chargeSource, charge);
                break;
            case ChargeSourceCardEffectDefinition sourceCharge:
                if (pending.Source is not CardBattleState selfChargeSource)
                    throw new InvalidOperationException("Source card charge requires a card source.");
                ChargeCard(runtime, selfChargeSource, selfChargeSource, sourceCharge.AmountTicks);
                break;
            case ChargeRandomOtherAlliedCardsEffectDefinition randomCharge:
                if (pending.Source is not CardBattleState randomChargeSource)
                    throw new InvalidOperationException("Random card charge requires a card source.");
                var chargeCandidates = runtime.Cards.Where(card => card.Side == randomChargeSource.Side
                    && card.EntityId != randomChargeSource.EntityId && !card.Destroyed && !card.IsOnBench
                    && card.Abilities.Any(ability => ability.Definition.Activation == AbilityActivation.Active
                        && ability.RemainingCooldownUnits > 0)).ToList();
                for (var i = 0; i < randomCharge.TargetCount && chargeCandidates.Count > 0; i++)
                {
                    var index = runtime.NextRandomIndex(chargeCandidates.Count);
                    ChargeCard(runtime, randomChargeSource, chargeCandidates[index], randomCharge.AmountTicks);
                    chargeCandidates.RemoveAt(index);
                }
                break;
            case ChargeCardEffectDefinition cardCharge:
                if (pending.Source is not CardBattleState cardChargeSource)
                    throw new InvalidOperationException("Card charge requires a card source.");
                var chargeTarget = GetTargetCard(runtime, pending);
                if (chargeTarget is not null)
                    ChargeCard(runtime, cardChargeSource, chargeTarget, cardCharge.AmountTicks);
                break;
            case ChargeTaggedAlliedCardsEffectDefinition taggedCharge:
                foreach (var target in runtime.Cards.Where(card => card.Side == pending.Source.Side && !card.Destroyed
                    && !card.IsOnBench && HasEffectiveTag(runtime, card, taggedCharge.RequiredTag)
                    && (!taggedCharge.ExcludeEventCard || card.EntityId != pending.EventCard?.EntityId)).ToArray())
                    ChargeCard(runtime, pending.Source, target, taggedCharge.AmountTicks);
                break;
            case RepairRandomDestroyedAlliedCardEffectDefinition repair:
                RepairRandomDestroyedAlliedCard(runtime, pending, repair);
                break;
            case DestroyCardEffectDefinition destroy:
                if (pending.Source is not CardBattleState destroySource)
                    throw new InvalidOperationException("Self-destruction requires a card source.");
                DestroyCard(runtime, destroySource, pending.Source.EntityId, destroy.Permanent);
                break;
            case DestroyRandomEnemyCardEffectDefinition randomDestroy:
                DestroyRandomEnemyCard(runtime, pending.Source, randomDestroy);
                break;
            case DestroyAlliedCardsAndDamageEffectDefinition sacrifice:
                var sacrifices = runtime.Cards.Where(card => card.Side == pending.Source.Side && !card.Destroyed && !card.IsOnBench
                    && card.OccupiedSlots == (int)sacrifice.Size && card.ElementKeys.Contains(sacrifice.ElementKey)).ToArray();
                var sacrificeDamage = BerserkAmount(pending, GetEffectiveCombatAttribute(runtime, pending.Source, sacrifice.DamageAttributeKey));
                foreach (var sacrificed in sacrifices)
                    DestroyCard(runtime, sacrificed, pending.Source.EntityId, false);
                foreach (var sacrificed in sacrifices)
                {
                    if (ApplyDamage(runtime, pending.Source.EntityId, targetSide, hero, sacrificeDamage, false, GetDamageSourceKind(pending.Source))
                        && !pending.IsEcho) CombatSimulator.EnqueueDamageEchoes(runtime);
                }
                break;
            case IncreaseSourceCooldownEffectDefinition cooldown:
                if (!cooldown.FirstActivationOnly || pending.Source.ActivationCount == 0)
                {
                    pending.Source.CooldownBonusTicks = checked(
                        pending.Source.CooldownBonusTicks + cooldown.AmountTicks);
                    foreach (var ability in pending.Source.Abilities)
                    {
                        if (ability.Definition.Activation == AbilityActivation.Active)
                            ability.RemainingCooldownUnits = checked(
                                ability.RemainingCooldownUnits + cooldown.AmountTicks * 2
                                    * (pending.Source is CardBattleState cooldownCard ? cooldownCard.EffectiveCooldownMultiplier : 1m));
                    }
                }
                break;
            case ModifyTaggedAlliedCardsAttributeEffectDefinition modifier:
                var targets = runtime.Cards.Where(card => card.Side == pending.Source.Side && !card.Destroyed && !card.IsOnBench
                    && card.Tags.Contains(modifier.RequiredTag) && (modifier.RequiresAttributeSupport
                        ? card.SupportsCombatAttribute(modifier.AttributeKey)
                        : card.CombatAttributes.ContainsKey(modifier.AttributeKey))).ToArray();
                if (modifier.RandomSingleTarget && targets.Length > 0)
                    targets = [targets[runtime.NextRandomIndex(targets.Length)]];
                foreach (var card in targets)
                {
                    var currentValue = card.AddCombatAttribute(modifier.AttributeKey, modifier.Amount);
                    runtime.Events.Add(new CardAttributeChangedEvent(
                        runtime.Tick,
                        card.EntityId,
                        modifier.AttributeKey,
                        modifier.Amount,
                        currentValue));
                }
                break;
            case ModifyAlliedAttackCardsAttributeEffectDefinition allAttackModifier:
                foreach (var card in runtime.Cards)
                {
                    if (card.Side != pending.Source.Side || card.Destroyed || card.IsOnBench || !card.IsAttackCard) continue;
                    var currentValue = card.AddCombatAttribute(allAttackModifier.AttributeKey, allAttackModifier.Amount);
                    runtime.Events.Add(new CardAttributeChangedEvent(runtime.Tick, card.EntityId,
                        allAttackModifier.AttributeKey, allAttackModifier.Amount, currentValue));
                }
                break;
            case ModifyRandomAlliedAttackCardAttributeEffectDefinition randomModifier:
                var attackers = runtime.Cards.Where(card => card.Side == pending.Source.Side
                    && !card.Destroyed && !card.IsOnBench && card.IsAttackCard).ToArray();
                if (attackers.Length > 0)
                {
                    var card = attackers[runtime.NextRandomIndex(attackers.Length)];
                    var currentValue = card.AddCombatAttribute(randomModifier.AttributeKey, randomModifier.Amount);
                    runtime.Events.Add(new CardAttributeChangedEvent(runtime.Tick, card.EntityId,
                        randomModifier.AttributeKey, randomModifier.Amount, currentValue));
                }
                break;
        }
    }

    // 只放大狂暴来源的发动数值，整数结果向下取整，不改变来源属性。
    // 读取当前来源数值一次，后续护甲、治疗或摧毁不会改变本次主值。
    internal static int PrimaryValue(BattleRuntime runtime, PendingAbility pending)
        => Math.Max(0, BaseEffectAmount(runtime, pending, pending.Ability.Definition.Effects.FirstOrDefault(), true) ?? 0);

    // 卡面读取当下的施加量；伤害在敌方护甲扣减前展示，治疗不按缺失生命截断。
    internal static int? PreviewEffectAmount(BattleRuntime runtime, CardBattleState card,
        BattleAbilityState ability, EffectDefinition effect, bool hasOpponent)
    {
        if (!hasOpponent && (effect is MaxHealthPercentDamageEffectDefinition
            || effect is ApplyPrimaryEffectPercentStatusEffectDefinition
                && ability.Definition.Effects.FirstOrDefault() is MaxHealthPercentDamageEffectDefinition)) return null;
        var pending = new PendingAbility(card, ability, false, false, runtime.Tick);
        pending = pending with { PrimaryValue = PrimaryValue(runtime, pending) };
        var amount = BaseEffectAmount(runtime, pending, effect);
        var usesBerserk = effect is DamageEffectDefinition or AttributeDamageEffectDefinition
            or DestroyAlliedCardsAndDamageEffectDefinition or SourceHeroHealthScaledAttributeDamageEffectDefinition
            or SourceHeroArmorDamageEffectDefinition or SourceHeroLevelScaledDamageEffectDefinition or MaxHealthPercentDamageEffectDefinition
            or ApplyStatusEffectDefinition { Status: BattleStatus.Burn or BattleStatus.Poison }
            or ApplyAttributeStatusEffectDefinition { Status: BattleStatus.Burn or BattleStatus.Poison }
            or ApplySourceAttributePercentStatusEffectDefinition { Status: BattleStatus.Burn or BattleStatus.Poison }
            or ApplyPrimaryEffectPercentStatusEffectDefinition;
        return amount is { } value && usesBerserk ? BerserkAmount(pending, value) : amount;
    }

    private static int? BaseEffectAmount(BattleRuntime runtime, PendingAbility pending,
        EffectDefinition? effect, bool primaryValue = false)
    {
        int Value(StringName key) => GetEffectiveCombatAttribute(runtime, pending.Source, key);
        var own = runtime.GetHero(pending.Source.Side);
        var target = runtime.GetHero(pending.Ability.Definition.Target == AbilityTarget.EnemyHero ? Opposite(pending.Source.Side) : pending.Source.Side);
        return effect switch
        {
            AttributeDamageEffectDefinition value => AttributeDamageAmount(runtime, pending, value),
            SourceHeroHealthScaledAttributeDamageEffectDefinition value => primaryValue ? Value(value.AttributeKey)
                : checked((int)((long)Value(value.AttributeKey) * own.Health / own.MaxHealth)),
            DestroyAlliedCardsAndDamageEffectDefinition value => Value(value.DamageAttributeKey),
            GainSourceHeroArmorFromAttributeEffectDefinition value => Value(value.AttributeKey),
            ApplyAttributeStatusEffectDefinition value => Value(value.AttributeKey)
                + (value.Status == BattleStatus.Burn ? Value(GameAttributeKeys.BurnBonus)
                    : value.Status == BattleStatus.Poison ? Value(GameAttributeKeys.PoisonBonus) : 0),
            ApplySourceAttributePercentStatusEffectDefinition value => checked((int)((long)Value(value.AttributeKey) * value.Percent / 100))
                + (value.Status == BattleStatus.Burn ? Value(GameAttributeKeys.BurnBonus)
                    : value.Status == BattleStatus.Poison ? Value(GameAttributeKeys.PoisonBonus) : 0),
            ApplyStatusEffectDefinition value => value.Amount + (value.Status == BattleStatus.Burn ? Value(GameAttributeKeys.BurnBonus)
                    : value.Status == BattleStatus.Poison ? Value(GameAttributeKeys.PoisonBonus) : 0),
            DamageEffectDefinition value => value.Amount,
            HealEffectDefinition value => checked(value.Amount + Value(GameAttributeKeys.HealingBonus)),
            HealMaxHealthPercentEffectDefinition value => checked((int)((long)target.MaxHealth * value.Percent / 100)
                + Value(GameAttributeKeys.HealingBonus)),
            ArmorEffectDefinition value => value.Amount,
            GainSourceHeroArmorEffectDefinition value => value.Amount,
            GainSourceHeroLevelScaledArmorEffectDefinition value => checked(own.Level * value.Multiplier),
            SourceHeroLevelScaledDamageEffectDefinition value => checked(own.Level * value.Multiplier),
            GainArmorEqualToManaSpentEffectDefinition => own.ManaSpent,
            SourceHeroArmorDamageEffectDefinition value => checked(own.Armor + (value.BonusAttributeKey is { IsEmpty: false } key ? Value(key) : 0)),
            MaxHealthPercentDamageEffectDefinition value => checked((int)((long)target.MaxHealth * value.Percent / 100)),
            RestoreManaEffectDefinition value => value.Amount,
            ChargeSourceCardEffectDefinition value => value.AmountTicks,
            ChargeCardEffectDefinition value => value.AmountTicks,
            ApplyPrimaryEffectPercentStatusEffectDefinition value when !primaryValue =>
                checked((int)((long)pending.PrimaryValue * value.Percent / 100)
                    + (value.Status == BattleStatus.Burn ? Value(GameAttributeKeys.BurnBonus)
                    : value.Status == BattleStatus.Poison ? Value(GameAttributeKeys.PoisonBonus) : 0)),
            IncreaseAlliedCardAttributeAuraEffectDefinition value => checked(value.Amount + own.Level * value.HeroLevelMultiplier),
            _ => null,
        };
    }

    private static int AttributeDamageAmount(BattleRuntime runtime, PendingAbility pending, AttributeDamageEffectDefinition effect)
    {
        var multiplier = pending.Source is CardBattleState { ActiveActivationCount: 0 }
            ? effect.FirstActivationMultiplier : 1;
        var amount = checked((int)((long)GetEffectiveCombatAttribute(runtime, pending.Source, effect.AttributeKey) * effect.Percent / 100));
        return checked(amount * multiplier);
    }

    private static int BerserkAmount(PendingAbility pending, int amount) =>
        pending.Source is CardBattleState { IsBerserk: true }
            && pending.Ability.Definition.Activation == AbilityActivation.Active
                ? checked((int)((long)amount * 120 / 100)) : amount;

    // 查询来源的基础属性与当前有效光环贡献，不提交属性写入。
    internal static int GetEffectiveCombatAttribute(BattleRuntime runtime, IBattleAbilitySource source, StringName key)
    {
        var value = source.GetCombatAttribute(key);
        if (source is CardBattleState target)
            foreach (var aura in AlliedAttributeAuras(runtime, target))
                if (aura.AttributeKey == key) value = checked(value + aura.Amount);
        foreach (var ability in source.Abilities)
        foreach (var effect in ability.Definition.Effects)
        {
            if (ability.Definition.Activation != AbilityActivation.PassiveAura
                || effect is not MultiplySourceAttributePerDestroyedTaggedCardEffectDefinition multiplier
                || multiplier.AttributeKey != key)
            {
                continue;
            }
            foreach (var card in runtime.Cards)
            {
                if (card.Destroyed && ContainsAnyTag(card, multiplier.RequiredAnyTags))
                    value = checked(value * multiplier.Multiplier);
            }
        }
        foreach (var ability in source.Abilities)
        foreach (var effect in ability.Definition.Effects)
        {
            if (ability.Definition.Activation != AbilityActivation.PassiveAura
                || effect is not IncreaseSourceAttributePerEnemyTaggedCardEffectDefinition increase
                || increase.AttributeKey != key)
            {
                continue;
            }
            foreach (var card in runtime.Cards)
            {
                if (card.Side != source.Side && !card.Destroyed && !card.IsOnBench
                    && HasEffectiveTag(runtime, card, increase.RequiredTag))
                {
                    value = checked(value + increase.Amount);
                }
            }
        }
        foreach (var ability in source.Abilities)
        foreach (var effect in ability.Definition.Effects)
        {
            if (ability.Definition.Activation != AbilityActivation.PassiveAura
                || effect is not IncreaseSourceAttributePerAlliedTaggedCardEffectDefinition increase
                || increase.AttributeKey != key)
                continue;
            foreach (var card in runtime.Cards)
                if (card.Side == source.Side && !card.Destroyed && !card.IsOnBench
                    && HasEffectiveTag(runtime, card, increase.RequiredTag)
                    && (!increase.AdjacentOnly || source is CardBattleState { Destroyed: false, IsOnBench: false } origin
                        && card.EntityId != origin.EntityId
                        && (card.BoardStart + card.OccupiedSlots == origin.BoardStart
                            || origin.BoardStart + origin.OccupiedSlots == card.BoardStart)))
                    value = checked(value + increase.Amount);
        }
        foreach (var ability in source.Abilities)
        foreach (var effect in ability.Definition.Effects)
            if (ability.Definition.Activation == AbilityActivation.PassiveAura
                && effect is MultiplySourceAttributeWhileEnemyHasArmorEffectDefinition multiplier
                && multiplier.AttributeKey == key && runtime.GetHero(Opposite(source.Side)).Armor > 0)
                value = checked(value * multiplier.Multiplier);
        if (source is CardBattleState { Destroyed: false, IsOnBench: false } receiver
            && receiver.SupportsCombatAttribute(key))
        {
            foreach (var auraSource in runtime.AbilitySources)
            {
                if (auraSource.Destroyed || auraSource.IsOnBench || auraSource.Side != receiver.Side) continue;
                foreach (var ability in auraSource.Abilities)
                foreach (var effect in ability.Definition.Effects)
                    if (ability.Definition.Activation == AbilityActivation.PassiveAura
                        && effect is MultiplyAlliedElementCardAttributeAuraEffectDefinition aura
                        && aura.AttributeKey == key && receiver.ElementKeys.Contains(aura.ElementKey)
                        && (aura.RequiredEnemyAnyTags is null || runtime.Cards.Any(card =>
                            card.Side != receiver.Side && !card.Destroyed && !card.IsOnBench
                            && ContainsAnyEffectiveTag(runtime, card, aura.RequiredEnemyAnyTags))))
                        value = checked(value * aura.Multiplier);
            }
        }
        return value;
    }

    // 贡献只读取光环定义，不依赖其他卡牌最终值；来源失效后自然停止计入。
    internal static System.Collections.Generic.IEnumerable<IncreaseAlliedCardAttributeAuraEffectDefinition> AlliedAttributeAuras(
        BattleRuntime runtime, CardBattleState target)
    {
        if (target.Destroyed || target.IsOnBench) yield break;
        foreach (var source in runtime.AbilitySources)
        {
            if (source.Destroyed || source.IsOnBench || source.Side != target.Side) continue;
            foreach (var ability in source.Abilities)
            {
                if (ability.Definition.Activation != AbilityActivation.PassiveAura) continue;
                foreach (var effect in ability.Definition.Effects)
                    if (effect is IncreaseAlliedCardAttributeAuraEffectDefinition aura
                        && target.SupportsCombatAttribute(aura.AttributeKey)
                        && (aura.ExtremeSide is not { } side || !runtime.Cards.Any(candidate =>
                            candidate.Side == target.Side && !candidate.Destroyed && !candidate.IsOnBench
                            && candidate.SupportsCombatAttribute(aura.AttributeKey)
                            && (side == AdjacentCardSide.Left ? candidate.BoardStart < target.BoardStart
                                : candidate.BoardStart > target.BoardStart))))
                        yield return aura with
                        {
                            Amount = checked(aura.Amount + checked(runtime.GetHero(source.Side).Level * aura.HeroLevelMultiplier)),
                            HeroLevelMultiplier = 0,
                        };
            }
        }
    }

    // 为事件与回放提供强类型能力来源标识。
    internal static AbilitySourceKind GetAbilitySourceKind(IBattleAbilitySource source) => source switch
    {
        SkillBattleState => AbilitySourceKind.Skill,
        CardSetBattleState => AbilitySourceKind.CardSet,
        _ => AbilitySourceKind.Card,
    };

    private static DamageSourceKind GetDamageSourceKind(IBattleAbilitySource source) => source switch
    {
        SkillBattleState => DamageSourceKind.Skill,
        CardSetBattleState => DamageSourceKind.CardSet,
        _ => DamageSourceKind.Card,
    };

    private static void DestroyRandomEnemyCard(
        BattleRuntime runtime,
        IBattleAbilitySource source,
        DestroyRandomEnemyCardEffectDefinition effect)
    {
        var candidates = new System.Collections.Generic.List<CardBattleState>();
        foreach (var card in runtime.Cards)
        {
            if (card.Side == source.Side || card.Destroyed || card.IsOnBench
                || !effect.AllowedSizes.Contains((CardSize)card.OccupiedSlots)
                || !ContainsAnyEffectiveTag(runtime, card, effect.RequiredAnyTags))
            {
                continue;
            }
            candidates.Add(card);
        }
        if (candidates.Count == 0) return;
        DestroyCard(runtime, candidates[runtime.NextRandomIndex(candidates.Count)], source.EntityId, effect.Permanent);
    }

    // 以新的 Runtime 状态替换目标；旧队列来源失效，对局实例不受影响。
    private static void TransformRandomEnemyCard(BattleRuntime runtime, IBattleAbilitySource source,
        TransformRandomEnemyCardEffectDefinition effect)
    {
        var definition = effect.Replacement;
        var identity = definition.Attributes.Identity;
        var candidates = runtime.Cards.Where(card => card.Side != source.Side && !card.Destroyed && !card.IsOnBench
            && card.OccupiedSlots == identity.OccupiedSlots && definition.SupportsLevel(card.Level)).ToArray();
        if (candidates.Length == 0) return;
        var target = candidates[runtime.NextRandomIndex(candidates.Length)];
        var level = definition.GetLevel(target.Level);
        var values = definition.Attributes.BaseCombat.CreateMutableCopy();
        if (level is not null)
            foreach (var entry in level.BaseCombatValues) values.SetBaseValue(entry.Key, entry.Value);
        var replacement = new CardBattleSetup(target.EntityId, target.BoardStart,
            values.GetFinalValue(GameAttributeKeys.AttackDamage), values.GetFinalValue(GameAttributeKeys.CooldownTicks),
            level?.Abilities ?? definition.Abilities, UseLegacyAttack: false, Tags: definition.Tags,
            Multicast: values.GetFinalValue(GameAttributeKeys.Multicast), OccupiedSlots: identity.OccupiedSlots,
            ElementKeys: identity.ElementKeys, ArmorAmount: values.GetFinalValue(GameAttributeKeys.Armor))
            { Identity = identity, Level = target.Level, CombatValues = values.SnapshotFinalValues(), Quests = definition.Quests };
        var transformed = new CardBattleState(replacement, target.Side)
            { TransformedIdentity = identity, SummonedCard = target.SummonedCard };
        foreach (var entry in replacement.CombatValues) transformed.CombatAttributes[entry.Key] = entry.Value;
        runtime.Cards[runtime.Cards.IndexOf(target)] = transformed;
        runtime.Events.Add(new CardTransformedEvent(runtime.Tick, source.EntityId, target.EntityId, identity.Key));
    }

    // 先验证等级、边界和占位，失败时不消耗随机数。
    private static bool CanSummonAdjacentCard(BattleRuntime runtime, CardBattleState source,
        CardDefinition definition, AdjacentCardSide side)
    {
        var size = definition.Attributes.Identity.OccupiedSlots;
        var start = side == AdjacentCardSide.Left ? source.BoardStart - size : source.BoardStart + source.OccupiedSlots;
        return definition.SupportsLevel(source.Level) && start >= 0 && start + size <= runtime.GetBattlefieldCapacity(source.Side)
            && !runtime.Cards.Any(card => card.Side == source.Side && !card.IsOnBench && !card.Destroyed
                && card.BoardStart < start + size && start < card.BoardStart + card.OccupiedSlots);
    }

    private static void SummonAdjacentCard(BattleRuntime runtime, CardBattleState source,
        SummonAdjacentCardEffectDefinition effect)
    {
        var definition = effect.Card;
        var identity = definition.Attributes.Identity;
        var size = identity.OccupiedSlots;
        var start = effect.Side == AdjacentCardSide.Left ? source.BoardStart - size : source.BoardStart + source.OccupiedSlots;
        if (!CanSummonAdjacentCard(runtime, source, definition, effect.Side)) return;
        var level = definition.GetLevel(source.Level);
        var values = definition.Attributes.BaseCombat.CreateMutableCopy();
        if (level is not null)
            foreach (var entry in level.BaseCombatValues) values.SetBaseValue(entry.Key, entry.Value);
        var id = runtime.NewSummonedCardId(source.EntityId);
        var setup = new CardBattleSetup(id, start, values.GetFinalValue(GameAttributeKeys.AttackDamage),
            values.GetFinalValue(GameAttributeKeys.CooldownTicks), level?.Abilities ?? definition.Abilities,
            UseLegacyAttack: false, Tags: definition.Tags, Multicast: values.GetFinalValue(GameAttributeKeys.Multicast),
            OccupiedSlots: size, ElementKeys: identity.ElementKeys, ArmorAmount: values.GetFinalValue(GameAttributeKeys.Armor))
            { Identity = identity, Level = source.Level, CombatValues = values.SnapshotFinalValues(), Quests = definition.Quests };
        var summoned = new CardBattleState(setup, source.Side)
        {
            SummonedCard = new SummonedCardSnapshot(identity, start, Array.AsReadOnly(definition.Tags.ToArray()),
                Array.AsReadOnly(setup.Abilities!.ToArray()), setup.CombatValues),
        };
        foreach (var entry in setup.CombatValues) summoned.CombatAttributes[entry.Key] = entry.Value;
        runtime.AddSummonedCard(summoned);
        runtime.Events.Add(new CardSummonedEvent(runtime.Tick, source.EntityId, id, source.Side, identity.Key, source.Level, start));
    }

    private static void ChargeRandomOtherAlliedElementCard(
        BattleRuntime runtime,
        CardBattleState source,
        ChargeRandomOtherAlliedElementCardEffectDefinition effect)
    {
        var candidates = new System.Collections.Generic.List<CardBattleState>();
        foreach (var card in runtime.Cards)
        {
            if (card.Side != source.Side || card.EntityId == source.EntityId || card.Destroyed || card.IsOnBench
                || !card.ElementKeys.Contains(effect.ElementKey)
                || !card.Abilities.Any(ability =>
                    ability.Definition.Activation == AbilityActivation.Active
                    && ability.RemainingCooldownUnits > 0))
            {
                continue;
            }
            candidates.Add(card);
        }
        if (candidates.Count == 0) return;
        var target = candidates[runtime.NextRandomIndex(candidates.Count)];
        ChargeCard(runtime, source, target, effect.AmountTicks);
    }

    private static CardBattleState? GetTargetCard(BattleRuntime runtime, PendingAbility pending)
    {
        if (pending.Source is not CardBattleState source)
            throw new InvalidOperationException("Card target requires a card source.");
        if (pending.Ability.Definition.Target == AbilityTarget.SelfCard) return source;
        if (pending.Ability.Definition.Target == AbilityTarget.RightmostAlliedCard)
            return runtime.Cards.Where(card => card.Side == source.Side && !card.Destroyed && !card.IsOnBench)
                .OrderByDescending(card => card.BoardStart).FirstOrDefault();
        return runtime.Cards.FirstOrDefault(card => card.Side == source.Side && !card.Destroyed && !card.IsOnBench
            && (pending.Ability.Definition.Target == AbilityTarget.LeftAdjacentAlliedCard
                ? card.BoardStart + card.OccupiedSlots == source.BoardStart
                : card.BoardStart == source.BoardStart + source.OccupiedSlots));
    }

    // 共用充能结算，降至零时只对原本尚在冷却的主动能力排队。
    private static void ChargeCard(BattleRuntime runtime, IBattleAbilitySource source, CardBattleState target, int amountTicks)
    {
        foreach (var ability in target.Abilities)
        {
            if (ability.Definition.Activation != AbilityActivation.Active
                || ability.RemainingCooldownUnits == 0)
            {
                continue;
            }
            ability.RemainingCooldownUnits = Math.Max(
                0,
                ability.RemainingCooldownUnits - amountTicks * 2);
            if (ability.RemainingCooldownUnits == 0)
                CombatSimulator.Enqueue(runtime, target, ability, false);
        }
        runtime.Events.Add(new CardChargedEvent(
            runtime.Tick,
            source.EntityId,
            target.EntityId,
            amountTicks));
    }

    private static void SetCardState(BattleRuntime runtime, PendingAbility pending,
        CardBattleState target, StringName stateKey, bool enabled)
    {
        var previous = stateKey == GameAttributeKeys.Flying ? target.IsFlying : target.IsBerserk;
        if (stateKey == GameAttributeKeys.Flying) target.IsFlying = enabled;
        else target.IsBerserk = enabled;
        if (previous == enabled) return;
        runtime.Events.Add(new CardStateChangedEvent(runtime.Tick, target.EntityId, stateKey, enabled));
        if (enabled && !pending.IsEcho)
            CombatSimulator.EnqueueStateEntryEchoes(runtime, target, stateKey);
    }

    private static bool ContainsAnyTag(CardBattleState card, System.Collections.Generic.IReadOnlyList<StringName> tags)
    {
        foreach (var tag in tags)
            if (card.Destroyed ? card.DestroyedTags.Contains(tag) : card.Tags.Contains(tag)) return true;
        return false;
    }

    private static bool ContainsAnyEffectiveTag(
        BattleRuntime runtime,
        CardBattleState card,
        System.Collections.Generic.IReadOnlyList<StringName> tags)
    {
        foreach (var tag in tags)
            if (HasEffectiveTag(runtime, card, tag)) return true;
        return false;
    }

    internal static bool HasEffectiveTag(BattleRuntime runtime, CardBattleState card, StringName tag)
    {
        if (card.Destroyed) return card.DestroyedTags.Contains(tag);
        if (card.Tags.Contains(tag)) return true;
        foreach (var source in runtime.AbilitySources)
        {
            if (source.Side == card.Side || source.Destroyed || source.IsOnBench) continue;
            foreach (var ability in source.Abilities)
            foreach (var effect in ability.Definition.Effects)
            {
                if (ability.Definition.Activation == AbilityActivation.PassiveAura
                    && effect is GrantTagToEnemySizeCardsEffectDefinition aura
                    && aura.Tag == tag
                    && (int)aura.Size == card.OccupiedSlots)
                {
                    return true;
                }
            }
        }
        return false;
    }

    // 先筛选空闲原位置，修复只操作已有Runtime，不重建身份或重放开战能力。
    private static void RepairRandomDestroyedAlliedCard(BattleRuntime runtime, PendingAbility pending,
        RepairRandomDestroyedAlliedCardEffectDefinition effect)
    {
        var candidates = runtime.Cards.Where(card => card.Side == pending.Source.Side && card.Destroyed && !card.IsOnBench
            && card.DestroyedTags.Contains(effect.RequiredTag)
            && !runtime.PermanentChanges.Any(change => change.CardId == card.EntityId && change.ChangeType == "Destroy")
            && !runtime.Cards.Any(other => other.Side == card.Side && !other.Destroyed && !other.IsOnBench
                && other.BoardStart < card.BoardStart + card.OccupiedSlots
                && card.BoardStart < other.BoardStart + other.OccupiedSlots)).ToArray();
        if (candidates.Length == 0) return;
        var target = candidates[runtime.NextRandomIndex(candidates.Length)];
        target.Destroyed = false;
        target.DestroyedTags.Clear();
        BattleStatusResolver.RefreshCooldownAuras(runtime);
        foreach (var ability in target.Abilities)
            if (ability.Definition.Activation == AbilityActivation.Active)
                ability.RemainingCooldownUnits = Math.Max(0, ability.Definition.CooldownTicks + target.CooldownBonusTicks)
                    * 2 * target.EffectiveCooldownMultiplier;
        runtime.Events.Add(new CardRepairedEvent(runtime.Tick, pending.Source.EntityId, target.EntityId,
            target.Side, target.BoardStart, target.OccupiedSlots));
    }

    private static void DestroyCard(BattleRuntime runtime, CardBattleState target, EntityId sourceId, bool permanent)
    {
        if (target.Destroyed) return;
        foreach (var tag in target.Tags) target.DestroyedTags.Add(tag);
        foreach (var source in runtime.AbilitySources)
        {
            if (source.Side == target.Side || source.Destroyed || source.IsOnBench) continue;
            foreach (var ability in source.Abilities)
            foreach (var effect in ability.Definition.Effects)
            {
                if (ability.Definition.Activation == AbilityActivation.PassiveAura
                    && effect is GrantTagToEnemySizeCardsEffectDefinition aura
                    && (int)aura.Size == target.OccupiedSlots)
                {
                    target.DestroyedTags.Add(aura.Tag);
                }
            }
        }
        target.Destroyed = true;
        runtime.Queue.CancelSource(target);
        runtime.Events.Add(new CardDestroyedEvent(runtime.Tick, target.EntityId, target.Side, sourceId));
        if (permanent && target.SummonedCard is null) runtime.PermanentChanges.Add(new PermanentChange(target.EntityId, "Destroy"));
    }

    // 统一扣减护甲与生命，并记录伤害事件及对应冻结状态。
    internal static bool ApplyDamage(
        BattleRuntime runtime,
        EntityId source,
        SideId targetSide,
        HeroBattleState target,
        int amount,
        bool bypassArmor,
        DamageSourceKind sourceKind = DamageSourceKind.Card,
        BattleStatus? statusOrigin = null)
    {
        if (target.StealthDurationTicks > 0 && sourceKind is DamageSourceKind.Card or DamageSourceKind.Skill or DamageSourceKind.CardSet)
        {
            runtime.Events.Add(new AttackBlockedEvent(runtime.Tick, source, targetSide));
            BattleStateRecorder.CaptureState(runtime);
            return false;
        }
        var previousHealth = target.Health;
        var absorbed = bypassArmor ? 0 : Math.Min(target.Armor, amount);
        target.Armor -= absorbed;
        var healthDamage = amount - absorbed;
        target.Health = Math.Max(0, target.Health - healthDamage);
        runtime.Events.Add(new DamageDealtEvent(
            runtime.Tick,
            source,
            targetSide,
            amount,
            absorbed,
            healthDamage,
            target.Health,
            sourceKind) { StatusOrigin = statusOrigin });
        BattleStateRecorder.CaptureState(runtime);
        CombatSimulator.ResolveHeroHealthTriggers(runtime, targetSide, previousHealth);
        return true;
    }

    private static SideId Opposite(SideId side) => side == SideId.Player ? SideId.Opponent : SideId.Player;
}
