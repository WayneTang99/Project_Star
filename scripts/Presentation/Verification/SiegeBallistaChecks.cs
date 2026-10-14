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
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

internal static class SiegeBallistaChecks
{
    internal static bool LevelsDamageAndHaste()
    {
        var definition = new SiegeBallistaCardDefinition(); var identity = definition.Attributes.Identity;
        if (identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Large
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General }) || !definition.Tags.Contains(GameTags.Mechanical)
            || definition.InitialLevel != 3 || definition.SupportsLevel(2) || definition.SupportsLevel(5)) return false;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (texture is null || texture.GetWidth() * 2 != texture.GetHeight() * 3) return false;
        foreach (var level in new[] { 3, 4 })
        foreach (var reverse in new[] { false, true })
        {
            var card = Ballista(level); var damage = level == 3 ? 160 : 320; var bonus = level == 3 ? 20 : 40;
            var normal = Run([card], [], 140, reverse);
            var hits = Hits(normal, card);
            if (hits.Length != 2 || !hits.Select(hit => hit.Tick.Value).SequenceEqual(new long[] { 70, 140 })
                || hits.Any(hit => hit.RawDamage != damage)) return false;
            var giver = Giver(0, AbilityActivation.PassiveOnBattleStart, BattleStatus.HasteDuration, 10, 20);
            var haste = Run([giver, card], [], 60, reverse);
            if (Echoes(haste, card) != 2 || Attack(haste, card) != damage + bonus * 2
                || Hits(haste, card).Single().RawDamage != damage + bonus * 2
                || haste.PermanentAttributeBonuses.Count != 0) return false;
            var multi = Giver(0, AbilityActivation.Active, BattleStatus.HasteDuration, 10) with { Multicast = 1 };
            var stacked = Run([multi, card], [], 12, reverse);
            if (Echoes(stacked, card) != 2 || Attack(stacked, card) != damage + bonus * 2) return false;
            if (Attack(Run([card], [], 1, reverse), card) != damage) return false;
        }
        return true;
    }

    internal static bool FiltersAndIsolation()
    {
        var card = Ballista(3);
        foreach (var status in new[] { BattleStatus.SlowDuration, BattleStatus.ImmobilizeDuration })
            if (Echoes(Run([Giver(0, AbilityActivation.PassiveOnBattleStart, status, 10), card], [], 1), card) != 0) return false;
        var zero = Giver(0, AbilityActivation.PassiveOnBattleStart, BattleStatus.HasteDuration, 0);
        if (Echoes(Run([zero, card], [], 1), card) != 0) return false;
        // 疾速给予其他己方卡牌时，攻城弩车不响应。
        var other = Giver(4, AbilityActivation.PassiveOnBattleStart, BattleStatus.HasteDuration, 10);
        var idle = new CardBattleSetup(EntityId.New(), 5, 0, 0, [], UseLegacyAttack: false);
        if (Echoes(Run([card, other, idle], [], 1), card) != 0) return false;
        var echoGiver = Giver(0, AbilityActivation.EchoOnSourceCardActivated, BattleStatus.HasteDuration, 10);
        var echo = echoGiver with { Abilities = echoGiver.Abilities!.Prepend(new AbilityDefinition("verification.ballista.active",
            AbilityActivation.Active, AbilityTarget.SelfCard, 0, 10, [new HealEffectDefinition(1)])).ToArray() };
        if (Echoes(Run([echo, card], [], 10), card) != 0) return false;
        var destroyed = card with { Abilities = card.Abilities!.Append(new AbilityDefinition("verification.ballista.destroy",
            AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])).ToArray() };
        foreach (var inactive in new[] { card with { IsOnBench = true }, destroyed })
            if (Echoes(Run([Giver(0, AbilityActivation.PassiveOnBattleStart, BattleStatus.HasteDuration, 10), inactive], [], 100), card) != 0
                || Hits(Run([inactive], [], 100), card).Length != 0) return false;
        return true;
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(500, 340), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            var face = new CardItemView { Position = new Vector2(25, 20), Size = new Vector2(450, 300) }; viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new SiegeBallistaCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/siege-ballista/card-face.png") != Error.Ok) throw new InvalidOperationException("Siege ballista capture failed.");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardBattleSetup Ballista(int level)
    {
        var instance = new EntityFactory().CreateCard(new SiegeBallistaCardDefinition(), level);
        return new(instance.Id, 1, instance.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.AttackDamage), 70,
            instance.Abilities, UseLegacyAttack: false, OccupiedSlots: 3, Tags: instance.Tags) { Level = level };
    }
    private static CardBattleSetup Giver(int start, AbilityActivation activation, BattleStatus status, params int[] amounts) =>
        new(EntityId.New(), start, 0, activation == AbilityActivation.Active ? 10 : 0,
            [new AbilityDefinition("verification.ballista.give", activation, AbilityTarget.RightAdjacentAlliedCard, 0,
                activation == AbilityActivation.Active ? 10 : 0, amounts.Select(amount => (EffectDefinition)new ApplyStatusEffectDefinition(status, amount)).ToArray())],
            UseLegacyAttack: false);
    private static int Echoes(BattleResult result, CardBattleSetup card) => result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == card.EntityId && e.IsEcho);
    private static int Attack(BattleResult result, CardBattleSetup card) => result.States.Last().Cards.Single(c => c.Id == card.EntityId).Values[GameAttributeKeys.AttackDamage];
    private static DamageDealtEvent[] Hits(BattleResult result, CardBattleSetup card) => result.Events.OfType<DamageDealtEvent>().Where(e => e.SourceCardId == card.EntityId).ToArray();
    private static BattleResult Run(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks, bool reverse = false) =>
        new CombatSimulator().Simulate(new BattleSetup(
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), reverse ? enemies : allies),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), reverse ? allies : enemies),
            42, new BattleTick(ticks), new BattleTick(ticks + 1)));
}
