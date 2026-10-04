using System;
using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Verification;

// 三种旗帜的分级、实际效果、动态贡献与召唤验证（表现层验证模块）。
internal static class StandardAuraChecks
{
    internal static bool LevelsAndEffects()
    {
        CardDefinition[] definitions = [new BattleStandardCardDefinition(), new GuardianStandardCardDefinition(), new MercyStandardCardDefinition()];
        StringName[] keys = [GameAttributeKeys.AttackDamage, GameAttributeKeys.Armor, GameAttributeKeys.HealingBonus];
        int[] amounts = [20, 40, 80];
        for (var index = 0; index < definitions.Length; index++)
        {
            var definition = definitions[index];
            if (definition.InitialLevel != 2 || definition.SupportsLevel(1) || definition.SupportsLevel(5)
                || definition.Attributes.Identity.FactionKey != new StringName("paladin")
                || definition.Attributes.Identity.Size != CardSize.Small
                || !definition.Attributes.Identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
                || !definition.Tags.ToArray().SequenceEqual(new[] { GameTags.Small })) return false;
            for (var level = 2; level <= 4; level++)
            {
                var ability = definition.GetLevel(level)!.Abilities.Single();
                if (ability.Activation != AbilityActivation.PassiveAura || ability.CooldownTicks != 0 || ability.ManaCost != 0
                    || ability.Effects.Single() is not IncreaseAlliedCardAttributeAuraEffectDefinition effect
                    || effect.AttributeKey != keys[index] || effect.Amount != amounts[level - 2]) return false;
                var banner = Card(definition, level, 2);
                CardDefinition receiverDefinition = index == 0 ? new FlangedMaceCardDefinition()
                    : index == 1 ? new ShieldBearerCardDefinition() : new NunCardDefinition();
                var receiver = Card(receiverDefinition, 1, 0);
                var setup = Battle([receiver, banner], index == 2 ? [OpeningDamage()] : [], index == 2 ? 60 : 50);
                var result = new CombatSimulator().Simulate(setup);
                var frame = result.States.Last(state => state.Player.Health > 0);
                var amount = amounts[level - 2];
                if (index == 0 && result.Events.OfType<DamageDealtEvent>().Single(hit => hit.SourceCardId == receiver.EntityId).RawDamage != 10 + amount
                    || index == 1 && frame.Player.Armor != 50 + amount
                    || index == 2 && frame.Player.Health != 510 + amount) return false;
                if (!result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            }
        }
        return true;
    }

    internal static bool StackingAndRemoval()
    {
        var receiver = Card(new FlangedMaceCardDefinition(), 1, 0);
        var first = Card(new BattleStandardCardDefinition(), 2, 1);
        // 通用光环组合进有攻击能力的来源时，也加成来源自身。
        first = first with { Abilities = first.Abilities!.Concat(receiver.Abilities!).ToArray() };
        var second = DestroyAfter(Card(new BattleStandardCardDefinition(), 3, 2), 75);
        var bench = Card(new BattleStandardCardDefinition(), 4, 0) with { IsOnBench = true };
        var benchReceiver = Card(new FlangedMaceCardDefinition(), 1, 1) with { IsOnBench = true };
        var enemy = Card(new BattleStandardCardDefinition(), 4, 0);
        enemy = enemy with { Abilities = enemy.Abilities!.Concat(receiver.Abilities!).ToArray() };
        var result = new CombatSimulator().Simulate(Battle([receiver, first, second, bench, benchReceiver], [enemy], 100));
        if (!result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == receiver.EntityId)
            .Select(hit => hit.RawDamage).SequenceEqual(new[] { 70, 30 })) return false;
        var initial = result.States[0];
        if (initial.Cards.Single(card => card.Id == first.EntityId).Values[GameAttributeKeys.AttackDamage] != 60
            || initial.Cards.Single(card => card.Id == benchReceiver.EntityId).Values[GameAttributeKeys.AttackDamage] != 10
            || initial.Cards.Single(card => card.Id == enemy.EntityId).Values[GameAttributeKeys.AttackDamage] != 80
            || receiver.AttackDamage != 10 || result.PermanentChanges.Count != 0) return false;
        // 治疗读冻结的加成；一面旗帜失效后，只移除它贡献的40。
        var nun = Card(new NunCardDefinition(), 1, 0);
        first = Card(new MercyStandardCardDefinition(), 2, 1);
        second = DestroyAfter(Card(new MercyStandardCardDefinition(), 3, 2), 75);
        result = new CombatSimulator().Simulate(Battle([nun, first, second,
            Card(new MercyStandardCardDefinition(), 4, 0) with { IsOnBench = true }], [OpeningDamage()], 120));
        var before = result.States.Last(state => state.Tick.Value == 60 && state.Player.Health > 0);
        var after = result.States.Last(state => state.Tick.Value == 120 && state.Player.Health > 0);
        if (before.Player.Health != 570 || after.Player.Health != 600
            || after.Cards.Single(card => card.Id == nun.EntityId).Values[GameAttributeKeys.HealingBonus] != 20) return false;
        var display = new CardSnapshot(nun.EntityId, new StringName("card.nun"), "修女", 1, 1, CardSize.Small,
            new StringName("paladin"), [GameElements.Light], [])
        { Abilities = nun.Abilities!, CurrentValues = after.Cards.Single(card => card.Id == nun.EntityId).Values };
        return CardDisplayAdapter.FaceEffects(display).Any(effect => effect.Kind == CardFaceEffectKind.Healing && effect.Value == "30");
    }

    internal static bool SummonsReceiveAura()
    {
        var banner = Card(new BattleStandardCardDefinition(), 2, 0);
        var knight = Card(new TemplarKnightCardDefinition(), 3, 2);
        var result = new CombatSimulator().Simulate(Battle([banner, knight], [], 40));
        var summons = result.Events.OfType<CardSummonedEvent>().ToArray();
        if (summons.Length != 2) return false;
        return summons.All(summon => result.States[^1].Cards.Single(card => card.Id == summon.SummonedCardId)
            .Values[GameAttributeKeys.AttackDamage] == 40)
            && result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == knight.EntityId).Single().RawDamage == 80;
    }

    // 分级发动只强化一张具备攻击能力的人类，累加值冻结在本场战斗。
    internal static bool StandardBearerBuff()
    {
        var definition = new LegionStandardBearerCardDefinition();
        if (definition.InitialLevel != 2 || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || definition.Attributes.Identity.Size != CardSize.Small
            || definition.Attributes.Identity.FactionKey != new StringName("paladin")
            || !definition.Attributes.Identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !definition.Tags.Contains(GameTags.Human)) return false;
        for (var level = 2; level <= 4; level++)
        {
            var bearer = Card(definition, level, 0);
            var first = Attacker(2);
            var second = Attacker(5);
            var bench = Attacker(0) with { IsOnBench = true };
            var nonHuman = Card(new FlangedMaceCardDefinition(), 1, 8);
            var enemy = Attacker(0);
            var setup = Battle([bearer, first, second, bench, nonHuman], [enemy], 120);
            var result = new CombatSimulator().Simulate(setup);
            var changes = result.Events.OfType<CardAttributeChangedEvent>()
                .Where(change => change.AttributeKey == GameAttributeKeys.AttackDamage).ToArray();
            var amount = 10 << (level - 2);
            if (changes.Length != 2 || changes.Any(change => change.Amount != amount
                || change.CardId != first.EntityId && change.CardId != second.EntityId)
                || !changes.Select(change => change.Tick.Value).SequenceEqual(new long[] { 60, 120 })
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                || result.PermanentChanges.Count != 0 || first.AttackDamage != 60) return false;
            var frame = result.States.Last(state => state.Player.Health > 0 && state.Opponent.Health > 0);
            if (frame.Cards.Where(card => card.Id == first.EntityId || card.Id == second.EntityId)
                .Sum(card => card.Values[GameAttributeKeys.AttackDamage]) != 120 + 2 * amount) return false;
            if (new CombatSimulator().Simulate(Battle([bearer], [], 60)).Events.OfType<CardAttributeChangedEvent>().Any()) return false;
        }
        return true;
        CardBattleSetup Attacker(int start)
        {
            var card = Card(new TemplarKnightCardDefinition(), 3, start);
            return card with { Abilities = card.Abilities!.Where(ability => ability.Activation == AbilityActivation.Active).ToArray() };
        }
    }

    // 随机召唤覆盖模板、同级、左侧边界、占位与重复模拟确定性。
    internal static bool StandardBearerSummon()
    {
        var seen = new System.Collections.Generic.HashSet<StringName>();
        StringName[] keys = [new("card.battle_standard"), new("card.guardian_standard"), new("card.mercy_standard")];
        for (var level = 2; level <= 4; level++)
        for (ulong seed = 1; seed <= 32; seed++)
        {
            var bearer = Card(new LegionStandardBearerCardDefinition(), level, 1);
            var original = Battle([bearer], [], 1);
            var setup = new BattleSetup(original.Player, original.Opponent, seed, original.Timeout, original.EclipseTime);
            var result = new CombatSimulator().Simulate(setup);
            var summon = result.Events.OfType<CardSummonedEvent>().Single();
            if (summon.Level != level || summon.BoardStart != 0 || !keys.Contains(summon.CardKey)
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                || result.PermanentChanges.Count != 0) return false;
            seen.Add(summon.CardKey);
        }
        var boundary = Card(new LegionStandardBearerCardDefinition(), 2, 0);
        var occupied = Card(new LegionStandardBearerCardDefinition(), 2, 1);
        return seen.Count == 3
            && !new CombatSimulator().Simulate(Battle([boundary], [], 1)).Events.OfType<CardSummonedEvent>().Any()
            && !new CombatSimulator().Simulate(Battle([Card(new BattleStandardCardDefinition(), 2, 0), occupied], [], 1))
                .Events.OfType<CardSummonedEvent>().Any();
    }

    private static CardBattleSetup Card(CardDefinition definition, int level, int start)
    {
        var card = new EntityFactory().CreateCard(definition, level);
        return new CardBattleSetup(card.Id, start, card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
            card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks), definition.GetLevel(level)!.Abilities,
            UseLegacyAttack: false, Tags: card.Tags, OccupiedSlots: (int)definition.Attributes.Identity.Size,
            ElementKeys: definition.Attributes.Identity.ElementKeys,
            ArmorAmount: card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Armor)) { Level = level };
    }

    private static CardBattleSetup DestroyAfter(CardBattleSetup card, int ticks) => card with
    {
        Abilities = card.Abilities!.Append(new AbilityDefinition(new StringName("verification.standard.self_destroy"),
            AbilityActivation.Active, AbilityTarget.SelfCard, 0, ticks, [new DestroyCardEffectDefinition(false)])).ToArray()
    };

    private static CardBattleSetup OpeningDamage() => new(EntityId.New(), 0, 0, 0,
        [new AbilityDefinition(new StringName("verification.standard.opening_damage"), AbilityActivation.PassiveOnBattleStart,
            AbilityTarget.EnemyHero, 0, 0, [new DamageEffectDefinition(500)])], UseLegacyAttack: false);

    private static BattleSetup Battle(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, InitialMana: 100, ManaRegen: 0), cards),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), enemies), 42,
        new BattleTick(ticks), new BattleTick(ticks + 1));
}
