using System;
using System.Linq;
using Godot;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Verification;

// 征服者堡垒分级增量、敌方发动筛选及战斗内成长验证（表现层验证模块）。
internal static class ConquerorFortressChecks
{
    internal static bool LevelsAndGrowth()
    {
        var definition = new ConquerorFortressCardDefinition(); var identity = definition.Attributes.Identity;
        if (identity.Key != new StringName("card.conqueror_fortress") || identity.DisplayName != "征服者堡垒"
            || identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Large
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Location)
            || definition.InitialLevel != 3 || definition.SupportsLevel(2) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(ConquerorFortressChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (texture is null || texture.GetWidth() * 2 != texture.GetHeight() * 3) return false;
        foreach (var level in new[] { 3, 4 })
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        {
            var bonus = level == 3 ? 40 : 80;
            var fortress = Fortress(level);
            var human = Human(3, 10, 40);
            var zero = Human(4, 0, 0);
            var beast = Human(5, 10, 0) with { Tags = TagSet.ForCard(CardSize.Small, [GameTags.Beast]) };
            var bench = Human(0, 10, 40) with { IsOnBench = true };
            var dead = Human(6, 10, 40) with { Abilities = [Opening(new DestroyCardEffectDefinition(false))] };
            var enemy = Activator(0) with { Multicast = 1 };
            var enemyHuman = Human(1, 10, 0);
            CardBattleSetup[] allies = [fortress, human, zero, beast, bench, dead];
            CardBattleSetup[] enemies = [enemy, enemyHuman];
            var setup = side == SideId.Player ? Battle(allies, enemies, 40) : Battle(enemies, allies, 40);
            var result = new CombatSimulator().Simulate(setup); var frame = LiveFrame(result, 40);
            var changes = result.Events.OfType<CardAttributeChangedEvent>().ToArray();
            if (Echoes(result, fortress) != 4 || changes.Length != 8 || changes.Any(change => change.Amount != bonus
                    || change.CardId != human.EntityId && change.CardId != zero.EntityId)
                || Value(frame, human) != 10 + bonus * 4 || Value(frame, zero) != bonus * 4
                || Value(frame, beast) != 10 || Value(frame, bench) != 10 || Value(frame, dead) != 10 || Value(frame, enemyHuman) != 10
                || result.Events.OfType<DamageDealtEvent>().Single(hit => hit.SourceCardId == human.EntityId).RawDamage != 10 + bonus * 2
                || result.Events.OfType<DamageDealtEvent>().Any(hit => hit.SourceCardId == fortress.EntityId)
                || frame.Cards.Single(card => card.Id == fortress.EntityId).CooldownUnits.Count != 0
                || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            if (!CardDisplayAdapter.Details(snapshot).Contains("光环：") || !CardDisplayAdapter.Details(snapshot).Contains("40/80")
                || !CardDisplayAdapter.AbilityDetails(snapshot.Abilities).Contains("敌方战场卡牌发动后触发")) return false;
        }
        return true;
    }

    internal static bool StackingAndLifetime()
    {
        var first = Fortress(3); var second = Fortress(4) with { BoardStart = 3 };
        var human = Human(6, 10, 40);
        var stacked = new CombatSimulator().Simulate(Battle([first, second, human], [Activator(0)], 40));
        if (Value(LiveFrame(stacked, 40), human) != 250
            || stacked.Events.OfType<DamageDealtEvent>().Single().RawDamage != 130) return false;
        var dying = first with { Abilities = first.Abilities!.Append(new AbilityDefinition(new StringName("verification.fortress.destroy"),
            AbilityActivation.Active, AbilityTarget.SelfCard, 0, 35, [new DestroyCardEffectDefinition(false)])).ToArray() };
        var stopped = new CombatSimulator().Simulate(Battle([dying, human], [Activator(0)], 60));
        if (Echoes(stopped, first) != 1 || Value(LiveFrame(stopped, 60), human) != 50
            || stopped.Events.OfType<DamageDealtEvent>().Single().RawDamage != 50) return false;
        foreach (var source in new[] { first with { IsOnBench = true }, first with
            { Abilities = first.Abilities!.Append(Opening(new DestroyCardEffectDefinition(false))).ToArray() } })
        {
            var result = new CombatSimulator().Simulate(Battle([source, human], [Activator(0)], 40));
            if (Echoes(result, source) != 0 || Value(LiveFrame(result, 40), human) != 10) return false;
        }
        // 下一场重新使用冻结输入，不继承上一场临时增长。
        return Value(LiveFrame(new CombatSimulator().Simulate(Battle([first, human], [], 40)), 40), human) == 10
            && !new CombatSimulator().Simulate(Battle([first], [Activator(0)], 40)).Events.OfType<CardAttributeChangedEvent>().Any();
    }

    internal static bool TriggerBoundaries()
    {
        var fortress = Fortress(3); var human = Human(3, 0, 0);
        var enemy = Activator(0);
        foreach (var excluded in new[]
        {
            enemy with { IsOnBench = true },
            enemy with { Abilities = [Opening(new GainSourceHeroArmorEffectDefinition(1))] },
            enemy with { Abilities = [Opening(new DestroyCardEffectDefinition(false))] },
            enemy with { Abilities = [new AbilityDefinition(new StringName("verification.fortress.unpaid"),
                AbilityActivation.Active, AbilityTarget.SelfCard, 1, 20, [new GainSourceHeroArmorEffectDefinition(1)])] },
        })
        {
            var result = new CombatSimulator().Simulate(Battle([fortress, human], [excluded], 40));
            if (Echoes(result, fortress) != 0 || Value(LiveFrame(result, 40), human) != 0) return false;
        }
        var own = new CombatSimulator().Simulate(Battle([fortress, human, enemy with { BoardStart = 4 }], [], 40));
        if (Echoes(own, fortress) != 0) return false;
        var echoing = enemy with { Abilities = enemy.Abilities!.Append(new AbilityDefinition(new StringName("verification.fortress.enemy_echo"),
            AbilityActivation.EchoOnSourceCardActivated, AbilityTarget.SelfCard, 0, 0,
            [new GainSourceHeroArmorEffectDefinition(1)])).ToArray() };
        var echoed = new CombatSimulator().Simulate(Battle([fortress, human], [echoing], 40));
        if (Echoes(echoed, fortress) != 2 || Value(LiveFrame(echoed, 40), human) != 80) return false;
        var skill = new SkillBattleSetup(EntityId.New(), [Opening(new GainSourceHeroArmorEffectDefinition(1))],
            new System.Collections.Generic.Dictionary<StringName, int>());
        var skillSetup = new BattleSetup(new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), [fortress, human]),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), [], [skill]),
            42, new BattleTick(40), new BattleTick(41));
        var skills = new CombatSimulator().Simulate(skillSetup);
        if (Echoes(skills, fortress) != 0 || skills.Events.OfType<AbilityActivatedEvent>().Count(item => item.SourceCardId == skill.EntityId) != 1)
            return false;
        // 通用强化默认仍只影响具备对应属性能力的卡牌，堡垒显式扩展到已有攻击属性。
        var restricted = fortress with { Abilities = [new AbilityDefinition(new StringName("verification.fortress.restricted"),
            AbilityActivation.EchoOnEnemyCardActivated, AbilityTarget.SelfCard, 0, 0,
            [new ModifyTaggedAlliedCardsAttributeEffectDefinition(GameTags.Human, GameAttributeKeys.AttackDamage, 40)])] };
        if (Value(LiveFrame(new CombatSimulator().Simulate(Battle([restricted, human], [enemy], 40)), 40), human) != 0) return false;
        foreach (var (cooldown, bench) in new[] { (10, false), (0, true) })
        {
            try
            {
                _ = new AbilityDefinition(new StringName("verification.fortress.invalid"), AbilityActivation.EchoOnEnemyCardActivated,
                    AbilityTarget.SelfCard, 0, cooldown, [new ModifyTaggedAlliedCardsAttributeEffectDefinition(
                        GameTags.Human, GameAttributeKeys.AttackDamage, 40)], allowsBench: bench);
                return false;
            }
            catch (ArgumentException) { }
        }
        return true;
    }

    private static CardBattleSetup Fortress(int level)
    {
        var definition = new ConquerorFortressCardDefinition(); var card = new EntityFactory().CreateCard(definition, level);
        return new CardBattleSetup(card.Id, 0, 0, 0, definition.GetLevel(level)!.Abilities, UseLegacyAttack: false,
            Tags: card.Tags, OccupiedSlots: 3, ElementKeys: definition.Attributes.Identity.ElementKeys) { Level = level };
    }
    private static CardBattleSetup Human(int start, int damage, int ticks) => new(EntityId.New(), start, damage, ticks,
        ticks == 0 ? [] : [new AbilityDefinition(new StringName("verification.fortress.attack"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, ticks, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)])],
        UseLegacyAttack: false, Tags: TagSet.ForCard(CardSize.Small, [GameTags.Human]));
    private static CardBattleSetup Activator(int start) => new(EntityId.New(), start, 0, 20,
        [new AbilityDefinition(new StringName("verification.fortress.support"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, 20, [new GainSourceHeroArmorEffectDefinition(1)])], UseLegacyAttack: false);
    private static AbilityDefinition Opening(EffectDefinition effect) => new(new StringName("verification.fortress.opening"),
        AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [effect]);
    private static int Echoes(BattleResult result, CardBattleSetup source) => result.Events.OfType<AbilityActivatedEvent>()
        .Count(item => item.SourceCardId == source.EntityId && item.IsEcho);
    private static int Value(BattleStateSnapshot frame, CardBattleSetup card) => frame.Cards.Single(item => item.Id == card.EntityId)
        .Values[GameAttributeKeys.AttackDamage];
    private static BattleStateSnapshot LiveFrame(BattleResult result, int tick) => result.States.Last(
        frame => frame.Tick.Value == tick && frame.Player.Health > 0 && frame.Opponent.Health > 0);
    private static BattleSetup Battle(CardBattleSetup[] player, CardBattleSetup[] opponent, int ticks) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), player),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), opponent),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
