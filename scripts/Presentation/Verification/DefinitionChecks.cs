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
        var mona = registry.Heroes[new StringName("hero.mona")];
        var paladin = registry.Heroes[new StringName("hero.paladin")];
        var session = new CreateMatchService(new EntityFactory()).Create(42, 100, mona);
        var snapshot = MatchSnapshot.From(session);
        return mona.Attributes.Identity.DisplayName == "莫娜"
            && mona.Attributes.Identity.Title == "小魔女"
            && mona.Attributes.Identity.FactionKey == new StringName("mona")
            && paladin.Attributes.Identity.DisplayName == "帕拉帝恩"
            && paladin.Attributes.Identity.Title == "圣骑士"
            && paladin.Attributes.Identity.FactionKey == new StringName("paladin")
            && typeof(HeroIdentityAttributes).GetProperty(nameof(HeroIdentityAttributes.Title))!.SetMethod is null
            && snapshot.Hero is { Title: "小魔女", DisplayName: "莫娜", Level: 1 }
            && snapshot.Income == 5
            && snapshot.Hero.CombatValues[GameAttributeKeys.MaxHealth] == 100
            && snapshot.Hero.CombatValues[GameAttributeKeys.Armor] == 0
            && snapshot.Hero.CombatValues[GameAttributeKeys.MaxMana] == 100
            && snapshot.Hero.CombatValues[GameAttributeKeys.Mana] == 0
            && snapshot.Hero.CombatValues[GameAttributeKeys.ManaRegen] == 10
            && snapshot.Hero.CombatValues[GameAttributeKeys.HealthRegen] == 0
            && PlaytestText.FormatHeroName(snapshot.Hero.DisplayName, snapshot.Hero.Title) == "小魔女·莫娜"
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
