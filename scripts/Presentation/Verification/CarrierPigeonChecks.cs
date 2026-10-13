using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 信鸽分级、己方首尾筛选与疾速实际结算验证（表现层）。
internal static class CarrierPigeonChecks
{
    internal static bool LevelsAndFilters()
    {
        var definition = new CarrierPigeonCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.DisplayName != "信鸽" || identity.FactionKey != new StringName("robin")
            || identity.Size != CardSize.Small || definition.InitialLevel != 2
            || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Beast)
            || !DefinitionRegistry.Scan(typeof(CarrierPigeonCardDefinition).Assembly).Cards.ContainsKey(identity.Key)) return false;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (texture is null || texture.GetHeight() != texture.GetWidth() * 2) return false;
        foreach (var level in new[] { 2, 3, 4 })
        foreach (var opposite in new[] { false, true })
        {
            var pigeon = Pigeon(level);
            var left = Fixture(1, 10) with { OccupiedSlots = 2 };
            var right = Fixture(8);
            var middle = Fixture(5, 10);
            var benchLeft = Fixture(0, 10) with { IsOnBench = true };
            var benchRight = Fixture(9) with { IsOnBench = true };
            var enemy = Fixture(0, 10);
            var result = Run([right, middle, pigeon, left, benchLeft, benchRight], [enemy], 10, opposite);
            if (Haste(result, right) != (level - 1) * 10
                || new[] { left, middle, pigeon, benchLeft, benchRight, enemy }.Any(c => Haste(result, c) != 0)
                || result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == pigeon.EntityId) != 1
                || result.PermanentChanges.Any()) return false;
            if (Haste(Run([pigeon with { IsOnBench = true }, left, right], [], 10, opposite), right) != 0) return false;
            // 被动卡牌仍占最左侧，不能把“最左卡牌”误选成“最左主动卡牌”。
            if (Haste(Run([Fixture(0), left, pigeon, right], [], 10, opposite), right) != 0) return false;
            if (Haste(Run([pigeon with { BoardStart = 9 }, left], [], 10, opposite), pigeon) != (level - 1) * 10) return false;
        }
        return true;
    }

    internal static bool StackingAndTransitions()
    {
        var pigeon = Pigeon(2);
        var left = Fixture(0, 10) with { Multicast = 1 };
        var right = Fixture(9);
        var setup = new[] { pigeon, left, right };
        var result = Run(setup, [], 10);
        if (Haste(result, right) != 20 || !result.Events.SequenceEqual(Run(setup, [], 10).Events)) return false;
        if (Haste(Run([pigeon, left with { CooldownTicks = 0, Abilities = [new AbilityDefinition(
            "verification.pigeon.opening", AbilityActivation.PassiveOnBattleStart, AbilityTarget.EnemyHero,
            0, 0, [new DamageEffectDefinition(1)])] }, right], [], 10), right) != 0) return false;
        var once = Fixture(0, 10, consume: true);
        if (Haste(Run([pigeon, once, right], [], 20), right) != 0) return false;
        var activeRight = Fixture(9, 30);
        var accelerated = Run([pigeon, once, activeRight], [], 30);
        if (accelerated.Events.OfType<DamageDealtEvent>().Single(e => e.SourceCardId == activeRight.EntityId).Tick.Value != 20) return false;
        // 首尾均使用当前存活战场卡牌；位置空隙和输入列表顺序不影响结果。
        var deadLeft = Fixture(0, 5, consume: true);
        var nextLeft = Fixture(2, 10);
        var deadRight = Fixture(9, 5, consume: true);
        var nextRight = Fixture(7);
        var changed = Run([deadRight, pigeon, nextRight, nextLeft, deadLeft], [], 10);
        if (Haste(changed, nextRight) != 15) return false;
        var destroyedSource = pigeon with { Abilities = pigeon.Abilities!.Concat(new[] { new AbilityDefinition(
            "verification.pigeon.destroy", AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard,
            0, 0, [new DestroyCardEffectDefinition(false)]) }).ToArray() };
        return Haste(Run([destroyedSource, left, right], [], 10), right) == 0;
    }

    private static CardBattleSetup Pigeon(int level)
    {
        var card = new EntityFactory().CreateCard(new CarrierPigeonCardDefinition(), level);
        return new(card.Id, 4, 0, 0, card.Abilities, UseLegacyAttack: false, Tags: card.Tags) { Level = level };
    }

    // 使用原生卡面检查小型插画及回响描述的实际叠加。
    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(260, 440), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            var face = new CardItemView { Position = new Vector2(30, 20), Size = new Vector2(200, 400) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new CarrierPigeonCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/carrier-pigeon/card-face.png") != Error.Ok)
                throw new System.InvalidOperationException("Carrier pigeon capture failed");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardBattleSetup Fixture(int start, int cooldown = 0, bool consume = false) =>
        new(EntityId.New(), start, 0, cooldown, cooldown == 0 ? [] : [new AbilityDefinition(
            "verification.pigeon.active", AbilityActivation.Active, AbilityTarget.EnemyHero, 0, cooldown,
            consume ? [new DamageEffectDefinition(1), new DestroyCardEffectDefinition(false)] : [new DamageEffectDefinition(1)])],
            UseLegacyAttack: false);

    private static int Haste(BattleResult result, CardBattleSetup card) =>
        result.States.Last().Cards.Single(c => c.Id == card.EntityId).Haste;

    private static BattleResult Run(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks, bool opposite = false) =>
        new CombatSimulator().Simulate(new BattleSetup(
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), opposite ? enemies : cards),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), opposite ? cards : enemies),
            42, new BattleTick(ticks), new BattleTick(ticks + 1)));
}
