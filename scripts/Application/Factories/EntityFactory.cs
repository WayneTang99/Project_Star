using System;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Application.Factories;

/// <summary>Creates independent match instances from immutable definitions.</summary>
public sealed class EntityFactory
{
    public HeroInstance CreateHero(HeroDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var attributes = Copy(definition.Attributes);
        if (attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) < 1)
            attributes.Persistent.SetBaseValue(GameAttributeKeys.Level, 1);
        return new HeroInstance(EntityId.New(), attributes);
    }

    // 从怪物定义创建复用英雄战斗模型的独立实例。
    public HeroInstance CreateMonsterCombatant(MonsterDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var attributes = new EntityAttributes<HeroIdentityAttributes>(
            new HeroIdentityAttributes(
                definition.Attributes.Identity.Key,
                definition.Attributes.Identity.DisplayName,
                new Godot.StringName("monster")),
            baseCombat: definition.Attributes.BaseCombat.CreateMutableCopy());
        return new HeroInstance(EntityId.New(), attributes);
    }

    public CardInstance CreateCard(CardDefinition definition, int? level = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var selectedLevel = level ?? definition.InitialLevel;
        if (!definition.SupportsLevel(selectedLevel))
        {
            throw new ArgumentOutOfRangeException(nameof(level), $"Card does not provide level {selectedLevel}.");
        }

        var attributes = Copy(definition.Attributes);
        attributes.Persistent.SetBaseValue(GameAttributeKeys.Level, selectedLevel);
        var levelDefinition = definition.GetLevel(selectedLevel);
        if (levelDefinition is not null)
        {
            foreach (var (key, value) in levelDefinition.BaseCombatValues)
                attributes.BaseCombat.SetBaseValue(key, value);
        }
        return new CardInstance(
            EntityId.New(),
            attributes,
            definition.Tags,
            levelDefinition?.Abilities ?? definition.Abilities,
            definition.OnSellReward);
    }

    // 将已有卡牌实例刷新为定义中的指定等级，同时保留实例身份与 Modifier。
    public void ApplyCardLevel(CardInstance card, CardDefinition definition, int level)
    {
        ArgumentNullException.ThrowIfNull(card);
        ArgumentNullException.ThrowIfNull(definition);
        if (!definition.SupportsLevel(level))
            throw new ArgumentOutOfRangeException(nameof(level), $"Card does not provide level {level}.");
        var baseCombat = definition.Attributes.BaseCombat.CreateMutableCopy();
        var levelDefinition = definition.GetLevel(level);
        if (levelDefinition is not null)
            foreach (var (key, value) in levelDefinition.BaseCombatValues) baseCombat.SetBaseValue(key, value);
        card.Attributes.BaseCombat.ReplaceBaseValues(baseCombat);
        card.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Level, level);
        card.ReplaceAbilities(levelDefinition?.Abilities ?? definition.Abilities);
    }

    private static EntityAttributes<TIdentity> Copy<TIdentity>(EntityAttributes<TIdentity> source)
        where TIdentity : class =>
        new(source.Identity, source.Persistent.CreateMutableCopy(), source.BaseCombat.CreateMutableCopy());
}
