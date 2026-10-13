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

internal static class TrainingDummyChecks
{
    internal static bool LevelsAndGroupBuff()
    {
        var definition = new TrainingDummyCardDefinition(); var identity = definition.Attributes.Identity;
        if (identity.FactionKey != GameFactions.Neutral || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General }) || !definition.Tags.Contains(GameTags.Material)
            || definition.InitialLevel != 1 || definition.SupportsLevel(5)) return false;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (texture is null || texture.GetHeight() != texture.GetWidth() * 2) return false;
        foreach (var level in Enumerable.Range(1, 4))
        foreach (var reverse in new[] { false, true })
        {
            var dummy = Dummy(level); var first = Attacker(1, 50); var zero = Attacker(2, 0);
            var enemy = Attacker(0, 50); var setup = Setup([dummy, first, zero], [enemy], 75, reverse);
            var simulator = new CombatSimulator(); var result = simulator.Simulate(setup);
            if (Attack(result, first) != 50 + level * 10 || Attack(result, zero) != level * 10 || Attack(result, enemy) != 50
                || result.Events.OfType<CardAttributeChangedEvent>().Count(e => e.CardId == first.EntityId) != 2
                || result.Events.OfType<CardAttributeChangedEvent>().Count(e => e.CardId == zero.EntityId) != 2
                || result.Events.OfType<DamageDealtEvent>().Single(e => e.SourceCardId == first.EntityId).RawDamage != 50 + level * 10
                || result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == dummy.EntityId) != 2
                || result.PermanentAttributeBonuses.Count != 0 || !result.Events.SequenceEqual(simulator.Simulate(setup).Events)) return false;
            var multi = dummy with { Multicast = 1 };
            if (Attack(new CombatSimulator().Simulate(Setup([multi, first, zero], [], 60, reverse)), first) != 50 + level * 20) return false;
            if (Attack(new CombatSimulator().Simulate(Setup([first], [], 1, reverse)), first) != 50) return false;
        }
        return true;
    }

    internal static bool FiltersAndSourceLoss()
    {
        var dummy = Dummy(1); var attack = Attacker(1, 50);
        var bench = Attacker(2, 50) with { IsOnBench = true };
        var support = Attacker(3, 50) with { Abilities = [new AbilityDefinition("verification.dummy.heal",
            AbilityActivation.Active, AbilityTarget.AlliedHero, 0, 75, [new HealEffectDefinition(1)])] };
        var destroyed = Attacker(4, 50) with { Abilities = [new AbilityDefinition("verification.dummy.hit",
            AbilityActivation.Active, AbilityTarget.EnemyHero, 0, 75, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)]),
            DestroyAtStart()] };
        var result = new CombatSimulator().Simulate(Setup([dummy, attack, bench, support, destroyed], [], 60));
        if (Attack(result, attack) != 60 || Attack(result, bench) != 50 || Attack(result, support) != 50
            || Attack(result, destroyed) != 50 || Attack(result, dummy) != 0) return false;
        foreach (var inactive in new[] { dummy with { IsOnBench = true }, dummy with { Abilities = dummy.Abilities!.Append(DestroyAtStart()).ToArray() } })
            if (Attack(new CombatSimulator().Simulate(Setup([inactive, attack], [], 60)), attack) != 50) return false;
        var loss = dummy with { Abilities = dummy.Abilities!.Append(new AbilityDefinition("verification.dummy.destroy_after_buff",
            AbilityActivation.Active, AbilityTarget.SelfCard, 0, 40, [new DestroyCardEffectDefinition(false)])).ToArray() };
        if (Attack(new CombatSimulator().Simulate(Setup([loss, attack], [], 75)), attack) != 55) return false;
        var empty = new CombatSimulator().Simulate(Setup([dummy], [], 60));
        if (empty.Events.OfType<CardAttributeChangedEvent>().Any()) return false;
        // 支持其他伤害公式的卡牌也按通用攻击分类接收攻击属性贡献。
        var shieldInstance = new EntityFactory().CreateCard(new LionKingShieldCardDefinition(), 3);
        var shield = new CardBattleSetup(shieldInstance.Id, 1, 0, 50, shieldInstance.Abilities,
            UseLegacyAttack: false, ArmorAmount: 200, OccupiedSlots: 2);
        return Attack(new CombatSimulator().Simulate(Setup([dummy, shield], [], 30)), shield) == 5;
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(260, 440), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            var face = new CardItemView { Position = new Vector2(30, 20), Size = new Vector2(200, 400) }; viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new TrainingDummyCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/training-dummy/card-face.png") != Error.Ok) throw new InvalidOperationException("Training dummy capture failed.");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }
    private static CardBattleSetup Dummy(int level)
    {
        var card = new EntityFactory().CreateCard(new TrainingDummyCardDefinition(), level);
        return new(card.Id, 0, 0, 30, card.Abilities, UseLegacyAttack: false, Tags: card.Tags) { Level = level };
    }
    private static CardBattleSetup Attacker(int start, int attack) => new(EntityId.New(), start, attack, 75,
        [new AbilityDefinition("verification.dummy.attack", AbilityActivation.Active, AbilityTarget.EnemyHero, 0, 75,
            [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)])], UseLegacyAttack: false);
    private static AbilityDefinition DestroyAtStart() => new("verification.dummy.destroy", AbilityActivation.PassiveOnBattleStart,
        AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)]);
    private static int Attack(BattleResult result, CardBattleSetup card) => result.States.Last().Cards.Single(c => c.Id == card.EntityId).Values.TryGetValue(GameAttributeKeys.AttackDamage, out var value) ? value : 0;
    private static BattleSetup Setup(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks, bool reverse = false) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), reverse ? enemies : allies),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), reverse ? allies : enemies),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
