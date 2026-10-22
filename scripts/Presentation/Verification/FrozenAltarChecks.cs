using System;
using System.Linq;
using Godot;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 冰封祭坛等级、半血边界、全体禁锢、自毁与资源验证。
internal static class FrozenAltarChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new FrozenAltarCardDefinition();
        var identity = definition.Attributes.Identity;
        Require(identity.FactionKey == new StringName("mona") && identity.Size == CardSize.Large
            && identity.ElementKeys.SequenceEqual(new[] { GameElements.Ice }) && definition.Tags.Contains(GameTags.Building)
            && definition.InitialLevel == 3 && !definition.SupportsLevel(2)
            && DefinitionRegistry.Scan(typeof(FrozenAltarChecks).Assembly).Cards.ContainsKey(identity.Key), "身份");
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        using var image = texture.GetImage();
        Require(image.GetWidth() * 2 == image.GetHeight() * 3 && image.HasMipmaps(), "原画比例与mipmap");
        foreach (var level in new[] { 3, 4 })
        foreach (var reverse in new[] { false, true })
        foreach (var bench in new[] { false, true })
        {
            var source = new CardBattleSetup(EntityId.New(), 0, 0, 1, definition.GetLevel(level)!.Abilities,
                UseLegacyAttack: false, OccupiedSlots: 3, IsOnBench: bench);
            var attacker = new CardBattleSetup(EntityId.New(), 0, 250, 5,
                [new AbilityDefinition("verification.altar.attack", AbilityActivation.Active, AbilityTarget.EnemyHero,
                    0, 5, [new DamageEffectDefinition(250)])], UseLegacyAttack: false);
            var idle = new CardBattleSetup(EntityId.New(), 1, 0, 100, [], UseLegacyAttack: false);
            var reserve = idle with { EntityId = EntityId.New(), BoardStart = 2, IsOnBench = true };
            var allied = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0), [source]);
            var enemy = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0), [attacker, idle, reserve]);
            var setup = new BattleSetup(reverse ? enemy : allied, reverse ? allied : enemy, 42,
                new BattleTick(40), new BattleTick(50));
            var result = new CombatSimulator().Simulate(setup);
            var statuses = result.Events.OfType<StatusChangedEvent>().Where(e => e.Status == BattleStatus.ImmobilizeDuration).ToArray();
            Require(statuses.Length == (bench ? 0 : 2) && statuses.All(e => e.Tick.Value == 30 && e.Amount == (level - 2) * 10), $"严格半血与全体目标 level={level} reverse={reverse} bench={bench} statuses={string.Join(",", statuses.Select(e => e.ToString()))}");
            var destroyed = result.Events.OfType<CardDestroyedEvent>().Where(e => e.CardId == source.EntityId).ToArray();
            Require(destroyed.Length == (bench ? 0 : 1) && destroyed.All(e => e.Tick.Value == 30), "自毁一次");
            Require(!result.PermanentChanges.Any() && result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events), "战后隔离与确定性");
        }
        return true;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException($"冰封祭坛验证失败：{message}");
    }
}
