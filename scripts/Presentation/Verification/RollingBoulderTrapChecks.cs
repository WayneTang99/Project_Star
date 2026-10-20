using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 滚石陷阱尺寸筛选、双方摧毁、疾速充能和本场消耗验证（表现层）。
internal static class RollingBoulderTrapChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new RollingBoulderTrapCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.FactionKey != new StringName("robin") || identity.Size != CardSize.Large
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !definition.Tags.Contains(GameTags.Trap) || !definition.Tags.Contains(GameTags.Consumable)
            || definition.InitialLevel != 3 || definition.SupportsLevel(2) || definition.SupportsLevel(5)) return false;
        var rules = string.Join("\n", CardDisplayAdapter.Rules(MatchDisplayQuery.FromOffer(ShopOffer.Create(definition))));
        if (!rules.Contains("小型和中型") || !rules.Contains("消耗") || !rules.Contains("疾速")) return false;
        using var image = GD.Load<Texture2D>(identity.Illustration.ToString()).GetImage();
        if (image.GetWidth() * 2 != image.GetHeight() * 3 || !image.HasMipmaps()) return false;
        GD.Print($"滚石陷阱原画 {image.GetWidth()}×{image.GetHeight()} mipmaps={image.GetMipmapCount()}");
        foreach (var level in new[] { 3, 4 })
        foreach (var reverse in new[] { false, true })
        {
            var instance = new EntityFactory().CreateCard(definition, level);
            var cooldown = level == 3 ? 300 : 240;
            if (instance.Abilities.Any(a => a.ManaCost != 0)
                || instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != cooldown) return false;
            var trap = new CardBattleSetup(instance.Id, 0, 0, cooldown, instance.Abilities,
                UseLegacyAttack: false, OccupiedSlots: 3, Tags: instance.Tags) { Level = level };
            var small = Idle(3, 1); var medium = Idle(4, 2); var large = Idle(6, 3);
            var bench = Idle(0, 1) with { IsOnBench = true };
            var enemySmall = Idle(0, 1); var enemyMedium = Idle(1, 2); var enemyLarge = Idle(3, 3);
            var enemyBench = Idle(0, 2) with { IsOnBench = true };
            var setup = Setup([trap, small, medium, large, bench], [enemySmall, enemyMedium, enemyLarge, enemyBench], 600, reverse);
            var result = new CombatSimulator().Simulate(setup);
            var destroyed = result.Events.OfType<CardDestroyedEvent>().ToArray();
            var expected = new[] { small.EntityId, medium.EntityId, enemySmall.EntityId, enemyMedium.EntityId, trap.EntityId };
            if (destroyed.Length != 5 || !destroyed.Select(e => e.CardId).ToHashSet().SetEquals(expected)
                || destroyed.Any(e => e.Tick.Value != cooldown || e.SourceCardId != trap.EntityId)
                || destroyed.Last().CardId != trap.EntityId || result.PermanentChanges.Count != 0
                || result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == trap.EntityId) != 1
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            // 同一冻结输入再次战斗仍可发动，消耗不修改定义或对局卡牌。
            if (new CombatSimulator().Simulate(Setup([trap], [], cooldown, reverse)).Events.OfType<CardDestroyedEvent>().Count() != 1) return false;
            var inactive = new CombatSimulator().Simulate(Setup([trap with { IsOnBench = true }], [enemySmall], 600, reverse));
            if (inactive.Events.OfType<CardDestroyedEvent>().Any()) return false;
            foreach (var status in new[] { BattleStatus.HasteDuration, BattleStatus.SlowDuration, BattleStatus.ImmobilizeDuration })
            {
                var giver = Idle(0, 1) with { Abilities = [new AbilityDefinition("verification.boulder.status",
                    AbilityActivation.PassiveOnBattleStart, AbilityTarget.RightAdjacentAlliedCard, 0, 0,
                    [new ApplyStatusEffectDefinition(status, 10), new ApplyStatusEffectDefinition(status, 10)])] };
                var charged = new CombatSimulator().Simulate(Setup([giver, trap with { BoardStart = 1 }], [], 1, reverse));
                var charges = charged.Events.OfType<CardChargedEvent>().ToArray();
                if (charges.Length != (status == BattleStatus.HasteDuration ? 2 : 0)
                    || charges.Any(e => e.TargetCardId != trap.EntityId || e.AmountTicks != 10)) return false;
            }
            // 完整冷却前1秒获得疾速并充能到零，在该Tick摧毁目标并消耗。
            var instant = trap with { Abilities = trap.Abilities!.Append(new AbilityDefinition("verification.boulder.late_haste",
                AbilityActivation.Active, AbilityTarget.SelfCard, 0, cooldown - 10,
                [new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 1)])).ToArray() };
            var immediate = new CombatSimulator().Simulate(Setup([instant], [enemySmall], cooldown, reverse));
            if (immediate.Events.OfType<CardDestroyedEvent>().Any(e => e.Tick.Value != cooldown - 10)
                || immediate.Events.OfType<CardDestroyedEvent>().Count() != 2) return false;
        }
        return true;
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(700, 340), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(700, 340) });
            foreach (var compact in new[] { false, true })
            {
                var face = new CardItemView { Position = compact ? new Vector2(510, 24) : new Vector2(24, 20),
                    Size = compact ? new Vector2(150, 100) : new Vector2(450, 300) };
                viewport.AddChild(face);
                face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new RollingBoulderTrapCardDefinition())), new CardDisplayAdapter());
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/rolling-boulder-trap/card-face.png") != Error.Ok)
                throw new InvalidOperationException("滚石陷阱卡面捕获失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardBattleSetup Idle(int start, int size) => new(EntityId.New(), start, 0, 0, [],
        UseLegacyAttack: false, OccupiedSlots: size);

    private static BattleSetup Setup(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks, bool reverse) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), reverse ? enemies : allies),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), reverse ? allies : enemies),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
