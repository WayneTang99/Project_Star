using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Project_Star.Application.Common;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Infrastructure.Random;
using Project_Star.Presentation.Playtest;

using static Project_Star.Presentation.Verification.VerificationFixtures;

namespace Project_Star.Presentation.Verification;

// 基础类型、不可变定义与实例隔离验证（表现层验证模块）。
internal static class DefinitionChecks
{
    internal static bool CheckTickConversion()
    {
        var tick = BattleTick.FromPeriodicSeconds(0.2m);
        return tick.Value == 2 && tick.ToSeconds() == 0.2m;
    }

    internal static bool CheckInvalidTickConversion()
    {
        try
        {
            _ = BattleTick.FromPeriodicSeconds(0.15m);
            return false;
        }
        catch (ArgumentException)
        {
            return true;
        }
    }

    internal static bool CheckDeterministicRandom()
    {
        var first = new SeededRandom(42);
        var second = new SeededRandom(42);

        for (var index = 0; index < 20; index++)
        {
            if (first.NextInt(-10, 25) != second.NextInt(-10, 25))
            {
                return false;
            }
        }

        return first.State == second.State;
    }

    internal static bool CheckRandomResume()
    {
        var original = new SeededRandom(7);
        _ = original.NextInt(0, 100);
        var resumed = new SeededRandom(original.State);
        return original.NextInt(0, 100) == resumed.NextInt(0, 100);
    }

    internal static bool CheckSuccessfulResult()
    {
        var result = Result<int>.Success(12);
        return result.IsSuccess && result.Value == 12 && result.Failure is null;
    }

    internal static bool CheckFailureCode()
    {
        var expectedCode = new StringName("card.not_found");
        var result = Result.Fail(new Failure(expectedCode, "Card was not found."));
        return result.IsFailure && result.Failure?.Code == expectedCode;
    }

    internal static bool CheckReadonlyIdentity()
    {
        return typeof(CardIdentityAttributes).GetProperty(nameof(CardIdentityAttributes.Key))?.CanWrite == false
            && typeof(CardIdentityAttributes).GetProperty(nameof(CardIdentityAttributes.ElementKeys))?.CanWrite == false;
    }

    internal static bool CheckGeneralElement()
    {
        var identity = CreateCardIdentity([GameElements.General]);
        return identity.ElementKeys.Count == 1 && identity.ElementKeys[0] == GameElements.General;
    }

    internal static bool CheckElementNormalization()
    {
        var identity = CreateCardIdentity([GameElements.Dark, GameElements.Fire]);
        return identity.ElementKeys.Count == 2
            && identity.ElementKeys[0] == GameElements.Fire
            && identity.ElementKeys[1] == GameElements.Dark;
    }

    internal static bool CheckInvalidElements()
    {
        return RejectsElements([])
            && RejectsElements([GameElements.Fire, GameElements.Fire])
            && RejectsElements([GameElements.Fire, GameElements.Water, GameElements.Wind])
            && RejectsElements([new StringName("Unknown")]);
    }

    internal static bool CheckModifierStacking()
    {
        var attack = new StringName("Attack");
        var attributes = new ModifiableAttributeSet(new Dictionary<StringName, int> { [attack] = 10 });
        attributes.ApplyModifier(new StatModifier(ModifierId.New(), EntityId.New(), attack, 4));
        attributes.ApplyModifier(new StatModifier(ModifierId.New(), EntityId.New(), attack, 6));
        return attributes.GetBaseValue(attack) == 10 && attributes.GetFinalValue(attack) == 20;
    }

    internal static bool CheckModifierRemoval()
    {
        var armor = new StringName("Armor");
        var attributes = new ModifiableAttributeSet();
        var first = new StatModifier(ModifierId.New(), EntityId.New(), armor, 5);
        var second = new StatModifier(ModifierId.New(), EntityId.New(), armor, 7);
        attributes.ApplyModifier(first);
        attributes.ApplyModifier(second);
        var removed = attributes.RemoveModifier(first.Id);
        return removed && attributes.GetFinalValue(armor) == 7;
    }

    internal static bool CheckSizeTag()
    {
        var identity = CreateCardIdentity([GameElements.Ice], CardSize.Large);
        var tags = TagSet.ForCard(identity.Size, [GameTags.Equipment]);
        return identity.OccupiedSlots == 3 && tags.Contains(GameTags.Large) && tags.Contains(GameTags.Equipment);
    }

    internal static bool CheckUnknownTagDisplay()
    {
        var unknown = new StringName("Dragon");
        return TagDisplayNames.Get(unknown) == "Dragon";
    }

    internal static bool CheckCardDefinition()
    {
        var definition = new VerificationCardDefinition();
        return definition.Attributes.Identity.DisplayName == "验证之剑"
            && definition.Attributes.Identity.ElementKeys[0] == GameElements.General
            && definition.Tags.Contains(GameTags.Small)
            && definition.Tags.Contains(GameTags.Equipment);
    }

    internal static bool CheckDefinitionDiscovery()
    {
        var registry = CreateVerificationRegistry();
        return registry.Heroes.Count == 1
            && registry.Cards.Count == 1
            && registry.Skills.Count == 1
            && registry.Encounters.Count == 7
            && registry.Cards.ContainsKey(new StringName("verification.sword"));
    }

    internal static bool CheckFormalHeroes()
    {
        var registry = DefinitionRegistry.Scan(typeof(MonaHeroDefinition).Assembly);
        foreach (var (key, name, title) in new[]
        {
            ("mona", "莫娜", "小魔女"), ("paladin", "帕拉帝恩", "圣骑士"),
            ("harla", "哈尔拉", "机械师"), ("jiyun", "极云", "熊猫人"),
            ("robin", "罗宾", "冒险家"), ("valos", "瓦洛斯", "潜行者"),
        })
        {
            var heroKey = new StringName("hero." + key);
            if (!registry.Heroes.TryGetValue(heroKey, out var definition)) return false;
            var identity = definition.Attributes.Identity;
            var session = new CreateMatchService(new EntityFactory()).Create(42, 100, definition);
            var snapshot = MatchSnapshot.From(session);
            var hero = snapshot.Hero!;
            if (identity.DisplayName != name || identity.Title != title || identity.FactionKey != new StringName(key)
                || hero.Key != heroKey || hero.DisplayName != name || hero.Title != title || hero.FactionKey != identity.FactionKey
                || hero.Level != 1 || snapshot.Income != 5 || snapshot.Wealth != 105 || snapshot.Experience != 1
                || hero.CombatValues[GameAttributeKeys.MaxHealth] != 200 || hero.CombatValues[GameAttributeKeys.Armor] != 0
                || hero.CombatValues[GameAttributeKeys.MaxMana] != 100 || hero.CombatValues[GameAttributeKeys.Mana] != 0
                || hero.CombatValues[GameAttributeKeys.ManaRegen] != 10 || hero.CombatValues[GameAttributeKeys.HealthRegen] != 0
                || PlaytestText.FormatHeroName(hero.DisplayName, hero.Title) != title + "·" + name) return false;
        }
        return typeof(HeroIdentityAttributes).GetProperty(nameof(HeroIdentityAttributes.Title))!.SetMethod is null
            && PlaytestText.FormatHeroName("野猪", "") == "野猪";
    }

    internal static bool CheckDuplicateDefinition()
    {
        try
        {
            _ = DefinitionRegistry.Create([new VerificationCardDefinition(), new VerificationCardDefinition()]);
            return false;
        }
        catch (DefinitionValidationException exception)
        {
            return exception.Message.Contains("Duplicate card key", StringComparison.Ordinal);
        }
    }

    internal static bool CheckFrozenDefinition()
    {
        var definition = new VerificationCardDefinition();
        try
        {
            definition.Attributes.Persistent.SetBaseValue(new StringName("Value"), 10);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    internal static bool CheckUnknownCardSet()
    {
        try
        {
            _ = DefinitionRegistry.Create([new VerificationSetCardDefinition("verification.set_card_a")]);
            return false;
        }
        catch (DefinitionValidationException)
        {
            return true;
        }
    }


}
