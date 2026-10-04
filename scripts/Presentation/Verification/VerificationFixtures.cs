using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Economy;
using Project_Star.Application.Encounters;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Monsters;
using Project_Star.Content.Skills;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Infrastructure.Encounters;

namespace Project_Star.Presentation.Verification;

// 各规则检查共用的内部夹具与构造辅助（表现层验证模块）。
internal static class VerificationFixtures
{
    internal static EncounterScheduler CreateEncounterScheduler() => new(CreateVerificationRegistry());

    internal static MatchSession CreateBoarOpponent()
    {
        var registry = DefinitionRegistry.Create([
            new BoarMonsterDefinition(), new BeastHideCardDefinition(), new BoarCardDefinition(),
            new ChargeSkillDefinition()]);
        return new LocalTestOpponentProvider(registry).CreateMonsterOpponent(
            2, registry.Monsters[new StringName("monster.boar")]);
    }

    // 验证怪物定义可直接携带技能（表现层验证模块）。
    // 内部验证定义 SkillOnlyVerificationMonsterDefinition，不进入正式内容池。
    internal sealed class SkillOnlyVerificationMonsterDefinition : MonsterDefinition
    {
        public SkillOnlyVerificationMonsterDefinition()
            : base(new StringName("verification.monster.skill"), "验证技能怪物",
                Array.Empty<MonsterCardEntry>(),
                skills: [new MonsterSkillEntry(new StringName("skill.assault"), 1)])
        {
        }
    }

    internal static MatchResultService CreateMatchResultService() => new(CreateBoardService());

    internal static BattleResult CreateBattleResult(BattleOutcome outcome) =>
        new(outcome, BattleEndReason.HeroDefeated, new BattleTick(1), 1, 0, Array.Empty<BattleEvent>());

    internal static BattleResult SimulateAbilities(
        IReadOnlyList<AbilityDefinition> abilities,
        int timeout,
        int opponentArmor = 0,
        int opponentMaxHealth = 100)
    {
        var playerHero = new HeroBattleSetup(EntityId.New(), 100, 0, ManaRegen: 0);
        var opponentHero = new HeroBattleSetup(EntityId.New(), opponentMaxHealth, opponentArmor, ManaRegen: 0);
        var card = new CardBattleSetup(EntityId.New(), 0, 0, 1, abilities);
        return new CombatSimulator().Simulate(new BattleSetup(
            new BattleSideSetup(playerHero, [card]),
            new BattleSideSetup(opponentHero, Array.Empty<CardBattleSetup>()),
            1,
            new BattleTick(timeout),
            new BattleTick(timeout)));
    }

    internal static (MatchSession Player, MatchSession Opponent) CreateBattlePair(
        bool playerHasCard,
        bool opponentHasCard)
    {
        var factory = new EntityFactory();
        var player = new MatchSession(1);
        var opponent = new MatchSession(2);
        player.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        opponent.Player.SelectHero(factory.CreateHero(new VerificationHeroDefinition()));
        AddBattleCardIfNeeded(player, playerHasCard);
        AddBattleCardIfNeeded(opponent, opponentHasCard);
        return (player, opponent);
    }

    internal static void AddBattleCardIfNeeded(MatchSession session, bool shouldAdd)
    {
        if (!shouldAdd)
        {
            return;
        }

        var economy = new CardEconomyService(new EntityFactory());
        var card = economy.AcquireCard(
            session,
            new VerificationCardDefinition(),
            1,
            CardAcquisitionSource.Reward).Value!;
        _ = CreateBoardService().PlaceCard(session, card.Id, BoardZone.Battlefield, 0);
    }

    internal static BoardService CreateBoardService() => new(new BoardPlacementSolver());

    internal static CardInstance AcquireBoardCard(MatchSession session, CardSize size)
    {
        var card = new EntityFactory().CreateCard(new EconomyCardDefinition(size), 1);
        session.Player.Inventory.Add(card);
        return card;
    }

    internal static CardIdentityAttributes CreateCardIdentity(
        IEnumerable<StringName> elements,
        CardSize size = CardSize.Small) =>
        new(new StringName("verification.card"), "验证卡牌", new StringName("verification.faction"), size, elements);

    internal static bool RejectsElements(IEnumerable<StringName> elements)
    {
        try
        {
            _ = CreateCardIdentity(elements);
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    internal static DefinitionRegistry CreateVerificationRegistry() => DefinitionRegistry.Create(
    [
        new VerificationHeroDefinition(),
        new VerificationCardDefinition(),
        new VerificationSkillDefinition(),
        new VerificationEncounterDefinition("verification.event", EncounterKind.Other),
        new VerificationEncounterDefinition("verification.shop.primary", EncounterKind.Shop, 2),
        new VerificationEncounterDefinition("verification.shop.secondary", EncounterKind.Shop),
        new VerificationEncounterDefinition("verification.monster.one", EncounterKind.Monster),
        new VerificationEncounterDefinition("verification.monster.two", EncounterKind.Monster),
        new VerificationEncounterDefinition("verification.monster.three", EncounterKind.Monster),
        new VerificationEncounterDefinition("verification.pvp", EncounterKind.Pvp),
    ]);

    // 验证专用英雄定义（表现层验证模块）。
    // 内部验证定义 VerificationHeroDefinition，不进入正式内容池。
    internal sealed class VerificationHeroDefinition : HeroDefinition
    {
        public VerificationHeroDefinition()
            : base(
                new EntityAttributes<HeroIdentityAttributes>(
                    new HeroIdentityAttributes(
                        new StringName("verification.hero"),
                        "验证英雄",
                        new StringName("verification.faction")),
                    baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                    {
                        [GameAttributeKeys.MaxHealth] = 100,
                        [GameAttributeKeys.Armor] = 0,
                        [GameAttributeKeys.MaxMana] = 100,
                        [GameAttributeKeys.Mana] = 0,
                        [GameAttributeKeys.ManaRegen] = 10,
                    })))
        {
        }
    }

    // 内部验证定义 VerificationSkillDefinition，不进入正式内容池。
    internal sealed class VerificationSkillDefinition : SkillDefinition
    {
        public VerificationSkillDefinition()
            : base(
                new EntityAttributes<SkillIdentityAttributes>(
                    new SkillIdentityAttributes(
                        new StringName("verification.skill"),
                        "验证回击",
                        GameFactions.Neutral)),
                initialLevel: 1,
                levels:
                [
                    CreateLevel(1, 5),
                    CreateLevel(2, 10),
                    CreateLevel(3, 15),
                    CreateLevel(4, 20),
                    CreateLevel(5, 25),
                ])
        {
        }

        internal static SkillLevelDefinition CreateLevel(int level, int damage) => new(
            level,
            null,
            [new AbilityDefinition(
                new StringName("verification.skill.damage"),
                AbilityActivation.EchoOnDamageDealt,
                AbilityTarget.EnemyHero,
                0,
                0,
                [new DamageEffectDefinition(damage)])]);
    }

    // 验证用战士套装，不进入正式内容注册扫描（表现层验证模块）。
    // 内部验证定义 VerificationCardSetDefinition，不进入正式内容池。
    internal sealed class VerificationCardSetDefinition : CardSetDefinition
    {
        public VerificationCardSetDefinition(bool explicitTargets = false)
            : base(
                new EntityAttributes<CardSetIdentityAttributes>(
                    new CardSetIdentityAttributes(new StringName("verification.warrior_set"), "验证战士套装")),
                explicitTargets
                    ? [new CardSetThresholdDefinition(1, [
                        SetModifier("verification.set.all_attack", GameAttributeKeys.AttackDamage, 10, AbilityTarget.AllBattlefieldCards),
                        SetModifier("verification.set.hero_armor", GameAttributeKeys.Armor, 3, AbilityTarget.AlliedHero),
                    ])]
                    : [
                    new CardSetThresholdDefinition(2, [SetModifier("verification.set.two_attack", GameAttributeKeys.AttackDamage, 10)]),
                    new CardSetThresholdDefinition(3, [SetModifier("verification.set.three_attack", GameAttributeKeys.AttackDamage, 50)]),
                    ])
        {
        }

        internal static AbilityDefinition SetModifier(
            string key, StringName attributeKey, int amount,
            AbilityTarget target = AbilityTarget.SourceGroupCards) => new(
                new StringName(key), AbilityActivation.PassiveWhileEnabled, target, 0, 0,
                [new ModifyAttributeEffectDefinition(attributeKey, amount)]);
    }

    // 验证套装的战斗回响来源（表现层验证模块）。
    // 内部验证定义 VerificationBattleSetDefinition，不进入正式内容池。
    internal sealed class VerificationBattleSetDefinition : CardSetDefinition
    {
        public VerificationBattleSetDefinition()
            : base(
                new EntityAttributes<CardSetIdentityAttributes>(
                    new CardSetIdentityAttributes(new StringName("verification.battle_set"), "验证战斗套装")),
                [new CardSetThresholdDefinition(1, [new AbilityDefinition(
                    new StringName("verification.battle_set_echo"), AbilityActivation.EchoOnDamageDealt,
                    AbilityTarget.EnemyHero, 0, 0, [new DamageEffectDefinition(7)])])])
        {
        }
    }

    // 验证卡牌在造成伤害前摧毁自身（表现层验证模块）。
    // 内部验证定义 VerificationSelfDestroyingSetCardDefinition，不进入正式内容池。
    internal sealed class VerificationSelfDestroyingSetCardDefinition : CardDefinition
    {
        public VerificationSelfDestroyingSetCardDefinition()
            : base(
                new EntityAttributes<CardIdentityAttributes>(
                    new CardIdentityAttributes(
                        new StringName("verification.self_destroying_set_card"), "验证自毁套装卡",
                        GameFactions.Neutral, CardSize.Small, [GameElements.General],
                        new StringName("verification.battle_set")),
                    baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                    {
                        [GameAttributeKeys.CooldownTicks] = 1,
                    })),
                new TagSet(),
                [new AbilityDefinition(
                    new StringName("verification.self_destroying_attack"), AbilityActivation.Active,
                    AbilityTarget.EnemyHero, 0, 1,
                    [new DestroyCardEffectDefinition(false), new DamageEffectDefinition(1)])])
        {
        }
    }

    // 验证任务数值解锁与实例独立进度的卡牌（表现层验证模块）。
    // 内部验证定义 VerificationQuestCardDefinition，不进入正式内容池。
    internal sealed class VerificationQuestCardDefinition : CardDefinition
    {
        public VerificationQuestCardDefinition()
            : base(
                new EntityAttributes<CardIdentityAttributes>(
                    new CardIdentityAttributes(
                        new StringName("verification.quest_card"), "验证任务卡",
                        GameFactions.Neutral, CardSize.Small, [GameElements.General]),
                    baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                    {
                        [GameAttributeKeys.AttackDamage] = 5,
                        [GameAttributeKeys.CooldownTicks] = 1,
                    })),
                new TagSet(),
                [new AbilityDefinition(new StringName("verification.quest_attack"),
                    AbilityActivation.Active, AbilityTarget.EnemyHero, 0, 1,
                    [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)])],
                quests: [new CardQuestDefinition(
                    new StringName("verification.win_once"), new BattleVictoryQuestConditionDefinition(), 1,
                    [new AbilityDefinition(new StringName("verification.quest_attack_bonus"),
                        AbilityActivation.PassiveWhileEnabled, AbilityTarget.SelfCard, 0, 0,
                        [new ModifyAttributeEffectDefinition(GameAttributeKeys.AttackDamage, 100)])])])
        {
        }
    }

    // 验证任务战斗能力随卡牌摧毁失效（表现层验证模块）。
    // 内部验证定义 VerificationQuestSelfDestroyCardDefinition，不进入正式内容池。
    internal sealed class VerificationQuestSelfDestroyCardDefinition : CardDefinition
    {
        public VerificationQuestSelfDestroyCardDefinition()
            : base(
                new EntityAttributes<CardIdentityAttributes>(
                    new CardIdentityAttributes(
                        new StringName("verification.quest_destroy_card"), "验证自毁任务卡",
                        GameFactions.Neutral, CardSize.Small, [GameElements.General]),
                    baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                    {
                        [GameAttributeKeys.CooldownTicks] = 1,
                    })),
                new TagSet(),
                [new AbilityDefinition(new StringName("verification.quest_destroy_attack"),
                    AbilityActivation.Active, AbilityTarget.EnemyHero, 0, 1,
                    [new DestroyCardEffectDefinition(false), new DamageEffectDefinition(1)])],
                quests: [new CardQuestDefinition(
                    new StringName("verification.destroy_win_once"), new BattleVictoryQuestConditionDefinition(), 1,
                    [new AbilityDefinition(new StringName("verification.quest_destroy_echo"),
                        AbilityActivation.EchoOnDamageDealt, AbilityTarget.EnemyHero, 0, 0,
                        [new DamageEffectDefinition(7)])])])
        {
        }
    }

    // 验证用套装卡牌定义（表现层验证模块）。
    // 内部验证定义 VerificationSetCardDefinition，不进入正式内容池。
    internal sealed class VerificationSetCardDefinition : CardDefinition
    {
        public VerificationSetCardDefinition(string key)
            : base(
                new EntityAttributes<CardIdentityAttributes>(
                    new CardIdentityAttributes(
                        new StringName(key), "验证套装卡", GameFactions.Neutral, CardSize.Small,
                        [GameElements.General], new StringName("verification.warrior_set")),
                    baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                    {
                        [GameAttributeKeys.AttackDamage] = 5,
                        [GameAttributeKeys.CooldownTicks] = 10,
                    })),
                new TagSet(),
                [new AbilityDefinition(
                    new StringName("verification.set_attack"), AbilityActivation.Active,
                    AbilityTarget.EnemyHero, 0, 10,
                    [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)])])
        {
        }
    }

    // 验证专用遭遇定义（表现层验证模块）。
    // 内部验证定义 VerificationEncounterDefinition，不进入正式内容池。
    internal sealed class VerificationEncounterDefinition : EncounterDefinition
    {
        public VerificationEncounterDefinition(string key, EncounterKind kind, int baseWeight = 1)
            : base(
                new EntityAttributes<EncounterIdentityAttributes>(
                    new EncounterIdentityAttributes(new StringName(key), "验证遭遇")),
                1,
                99,
                kind,
                baseWeight)
        {
        }
    }

    // 验证专用卡牌定义（表现层验证模块）。
    // 内部验证定义 VerificationCardDefinition，不进入正式内容池。
    internal sealed class VerificationCardDefinition : CardDefinition
    {
        public VerificationCardDefinition()
            : base(
                new EntityAttributes<CardIdentityAttributes>(
                    new CardIdentityAttributes(
                        new StringName("verification.sword"),
                        "验证之剑",
                        new StringName("verification.faction"),
                        CardSize.Small,
                        [GameElements.General]),
                    baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                    {
                        [GameAttributeKeys.AttackDamage] = 25,
                        [GameAttributeKeys.CooldownTicks] = 10,
                    })),
                new TagSet([GameTags.Equipment]),
                [
                    new AbilityDefinition(
                        new StringName("verification.basic_attack"),
                        AbilityActivation.Active,
                        AbilityTarget.EnemyHero,
                        0,
                        10,
                        [new DamageEffectDefinition(25)]),
                ])
        {
        }
    }

    // 内部验证定义 AttackValueVerificationCardDefinition，不进入正式内容池。
    internal sealed class AttackValueVerificationCardDefinition : CardDefinition
    {
        public AttackValueVerificationCardDefinition()
            : base(
                new EntityAttributes<CardIdentityAttributes>(
                    new CardIdentityAttributes(
                        new StringName("verification.attack_value"),
                        "攻击属性验证卡牌",
                        new StringName("paladin"),
                        CardSize.Small,
                        [GameElements.General]),
                    baseCombat: new ModifiableAttributeSet(new Dictionary<StringName, int>
                    {
                        [GameAttributeKeys.AttackDamage] = 30,
                    })),
                new TagSet())
        {
        }
    }

    // 内部验证定义 EconomyCardDefinition，不进入正式内容池。
    internal sealed class EconomyCardDefinition : CardDefinition
    {
        private readonly int _valueCoefficient;

        public EconomyCardDefinition(CardSize size, int valueCoefficient = 2)
            : base(
                new EntityAttributes<CardIdentityAttributes>(
                    new CardIdentityAttributes(
                        new StringName($"verification.economy.{size}.{valueCoefficient}"),
                        "经济验证卡牌",
                        new StringName("verification.faction"),
                        size,
                        [GameElements.General])),
                new TagSet())
        {
            _valueCoefficient = valueCoefficient;
        }

        public override int ValueCoefficient => _valueCoefficient;
    }

    // 内部验证定义 ShopVerificationCardDefinition，不进入正式内容池。
    internal sealed class ShopVerificationCardDefinition : CardDefinition
    {
        public ShopVerificationCardDefinition(CardSize size, StringName factionKey, string suffix = "default", int initialLevel = 1)
            : base(
                new EntityAttributes<CardIdentityAttributes>(
                    new CardIdentityAttributes(
                        new StringName($"verification.shop_card.{factionKey}.{size}.{suffix}"),
                        "商店验证卡牌",
                        factionKey,
                        size,
                        [GameElements.General])),
                new TagSet(), initialLevel: initialLevel,
                levels: initialLevel == 1 ? null : [new CardLevelDefinition(initialLevel, null, [])])
        {
        }
    }

    // 5级默认价值验证卡牌定义（表现层验证模块）。
    // 内部验证定义 FiveLevelEconomyCardDefinition，不进入正式内容池。
    internal sealed class FiveLevelEconomyCardDefinition : CardDefinition
    {
        public FiveLevelEconomyCardDefinition(CardSize size)
            : base(
                new EntityAttributes<CardIdentityAttributes>(
                    new CardIdentityAttributes(
                        new StringName($"verification.economy.level_five.{size}"),
                        "5级经济验证卡牌",
                        new StringName("verification.faction"),
                        size,
                        [GameElements.General])),
                new TagSet(),
                levels:
                [
                    new CardLevelDefinition(1, null, Array.Empty<AbilityDefinition>()),
                    new CardLevelDefinition(2, null, Array.Empty<AbilityDefinition>()),
                    new CardLevelDefinition(3, null, Array.Empty<AbilityDefinition>()),
                    new CardLevelDefinition(4, null, Array.Empty<AbilityDefinition>()),
                    new CardLevelDefinition(5, null, Array.Empty<AbilityDefinition>()),
                ])
        {
        }
    }
}
