using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

internal static class IceLotusChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new IceLotusCardDefinition(); var identity = definition.Attributes.Identity;
        if (identity.FactionKey != new StringName("jiyun") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Ice }) || !definition.Tags.Contains(GameTags.Plant)
            || definition.InitialLevel != 3 || definition.SupportsLevel(2) || definition.SupportsLevel(5)) return false;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (texture is null || texture.GetHeight() != texture.GetWidth() * 2) return false;
        foreach (var level in new[] { 3, 4 })
        foreach (var reverse in new[] { false, true })
        {
            var lotus = Lotus(level); var ally = Idle(1); var enemy = Idle(0);
            var result = Run([lotus, ally], [enemy], 30, reverse);
            if (EchoCount(result, lotus) != 1 || Slow(result, enemy) != 10 + (level - 2) * 10
                || Slow(result, lotus) + Slow(result, ally) != 10) return false;
            var again = Run([lotus, ally], [enemy], 30, reverse);
            if (!result.Events.SequenceEqual(again.Events)) return false;
            var both = Run([lotus], [Lotus(level)], 30);
            if (both.Events.OfType<AbilityActivatedEvent>().Count(item => item.IsEcho) != 4) return false;
            var single = Run([lotus], [], 30);
            if (EchoCount(single, lotus) != 1 || Slow(single, lotus) != 10) return false;
            var expired = Run([lotus], [enemy], 60);
            if (Slow(expired, lotus) != 0 || Slow(expired, enemy) != 0) return false;
        }
        return true;
    }

    internal static bool ReceivedStatusAndFilters()
    {
        var lotus = Lotus(4);
        var enemy = Caster(new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.SlowDuration, 10));
        foreach (var reverse in new[] { false, true })
        {
            var result = Run([lotus], [enemy], 10, reverse);
            if (EchoCount(result, lotus) != 1 || Slow(result, enemy) != 20) return false;
            var multi = enemy with { Multicast = 1 };
            var repeated = Run([lotus], [multi], 12, reverse);
            if (EchoCount(repeated, lotus) != 2 || Slow(repeated, lotus) != 18 || Slow(repeated, multi) != 38) return false;
        }
        var friendly = Caster(new ApplyStatusToRandomAlliedCardEffectDefinition(BattleStatus.SlowDuration, 10), 1);
        if (EchoCount(Run([lotus, friendly], [Idle(0)], 10), lotus) != 1) return false;
        var haste = Caster(new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.HasteDuration, 10));
        var bind = Caster(new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.ImmobilizeDuration, 10));
        if (EchoCount(Run([lotus], [haste], 10), lotus) != 0 || EchoCount(Run([lotus], [bind], 10), lotus) != 0) return false;
        foreach (var inactive in new[]
        {
            lotus with { IsOnBench = true },
            lotus with { Abilities = lotus.Abilities!.Append(new AbilityDefinition("verification.lotus.destroy",
                AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])).ToArray() },
        })
            if (EchoCount(Run([inactive, Idle(1)], [enemy], 10), lotus) != 0) return false;
        var flying = lotus with { Abilities = lotus.Abilities!.Append(new AbilityDefinition("verification.lotus.fly",
            AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0,
            [new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, true)])).ToArray() };
        var zero = Caster(new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.SlowDuration, 1));
        if (EchoCount(Run([flying], [zero], 10), flying) != 0) return false;
        var half = Run([flying], [enemy], 10);
        if (Slow(half, flying) != 5 || EchoCount(half, flying) != 1) return false;
        // 敌方只迟缓敌方自己的卡牌，不触发本方冰莲。
        var otherSide = Caster(new ApplyStatusToRandomAlliedCardEffectDefinition(BattleStatus.SlowDuration, 10));
        if (EchoCount(Run([lotus], [otherSide], 10), lotus) != 0) return false;
        try
        {
            _ = new AbilityDefinition("verification.lotus.invalid", AbilityActivation.EchoOnAlliedCardStatusGained,
                AbilityTarget.SelfCard, 0, 0, [new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.SlowDuration, 10)],
                triggerStatus: BattleStatus.Burn);
            return false;
        }
        catch (ArgumentException) { }
        return true;
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(260, 440), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            var face = new CardItemView { Position = new Vector2(30, 20), Size = new Vector2(200, 400) }; viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new IceLotusCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/ice-lotus/card-face.png") != Error.Ok) throw new InvalidOperationException("Ice lotus capture failed.");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardBattleSetup Lotus(int level)
    {
        var card = new EntityFactory().CreateCard(new IceLotusCardDefinition(), level);
        return new(card.Id, 0, 0, 30, card.Abilities, UseLegacyAttack: false, Tags: card.Tags) { Level = level };
    }
    private static CardBattleSetup Idle(int start) => new(EntityId.New(), start, 0, 0, [], UseLegacyAttack: false);
    private static CardBattleSetup Caster(EffectDefinition effect, int start = 0) => new(EntityId.New(), start, 0, 10,
        [new AbilityDefinition("verification.lotus.cast", AbilityActivation.Active, AbilityTarget.SelfCard, 0, 10, [effect])], UseLegacyAttack: false);
    private static int Slow(BattleResult result, CardBattleSetup card) => result.States.Last().Cards.Single(c => c.Id == card.EntityId).Slow;
    private static int EchoCount(BattleResult result, CardBattleSetup card) => result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == card.EntityId && e.IsEcho);
    private static BattleResult Run(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks, bool reverse = false) =>
        new CombatSimulator().Simulate(new BattleSetup(
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), reverse ? enemies : allies),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), reverse ? allies : enemies),
            42, new BattleTick(ticks), new BattleTick(ticks + 1)));
}
