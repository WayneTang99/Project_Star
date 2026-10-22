using System;
using System.Linq;
using System.Threading.Tasks;
using Project_Star.Application.Content;
using Project_Star.Application.Economy;
using Project_Star.Application.Match;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Content.Sets;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 魔导士装备的发动筛选、公式、支付事件和战斗隔离（表现验证层）。
internal static class MageEquipmentChecks
{
    internal static bool Combat()
    {
        var registry = DefinitionRegistry.Scan(typeof(MageSetDefinition).Assembly);
        Require(registry.Cards.Values.Count(card => card.Attributes.Identity.SetKey == new StringName("set.mage")) == 5, "five distinct members");
        var set = new MageSetDefinition();
        Require(set.Thresholds.Select(t => t.RequiredDistinctCards).SequenceEqual(new[] { 3, 5 }), "thresholds");
        foreach (var reverse in new[] { false, true })
        {
            foreach (var level in new[] { 3, 4 })
            {
                var ring = Card(new MageRingCardDefinition(), level, 0);
                var robe = Card(new WizardRobeCardDefinition(), 1, 1);
                var setup = Setup([ring, robe], 60, reverse);
                var result = new CombatSimulator().Simulate(setup);
                var hero = Hero(result, reverse, 60);
                Require(hero.MaxMana == 200 + (level == 3 ? 20 : 40), "one active regen grant triggers one ring echo");
                Require(hero.ManaRegen == 4 && hero.Mana == 160, "gain max mana does not refill mana");
                Require(result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events), "deterministic events");
                Require(result.PermanentAttributeBonuses.Count == 0, "battle growth isolation");
                var bench = new CombatSimulator().Simulate(Setup([ring with { IsOnBench = true }, robe], 60, reverse));
                Require(Hero(bench, reverse, 60).MaxMana == 200, "bench ring excluded");
                var hat = Card(new WizardHatCardDefinition(), 1, 3);
                var echoes = new CombatSimulator().Simulate(Setup([ring, robe, hat], 60, reverse));
                Require(Hero(echoes, reverse, 60).MaxMana == hero.MaxMana && Hero(echoes, reverse, 60).ManaRegen == 5, "hat echo regen does not trigger ring");
                var circlet = Card(new MageCircletCardDefinition(), level, 0);
                var ticks = level == 3 ? 60 : 50;
                var attack = new CombatSimulator().Simulate(Setup([circlet], ticks, reverse, regen: 7));
                var target = reverse ? attack.States.Last(s => s.Tick.Value == ticks && s.Player.Health > 0 && s.Opponent.Health > 0).Player : attack.States.Last(s => s.Tick.Value == ticks && s.Player.Health > 0 && s.Opponent.Health > 0).Opponent;
                Require(target.Health == 10000 - 200 - 7 * (level == 3 ? 10 : 20), "max mana plus regen formula");
                Require(Hero(attack, reverse, ticks).Mana == 100, "attack pays 100 mana");
                var poor = new CombatSimulator().Simulate(Setup([circlet], ticks, reverse, mana: 99));
                Require(!poor.Events.OfType<AbilityActivatedEvent>().Any(), "insufficient mana blocks attack");
            }
            foreach (var level in new[] { 2, 3, 4 })
            {
                var boots = Card(new MageBootsCardDefinition(), level, 0);
                var result = new CombatSimulator().Simulate(Setup([boots], 100, reverse));
                var hero = Hero(result, reverse, 100);
                Require(hero.MaxMana == 200 + 2 * (20 << (level - 2)) && hero.Mana == 160, "boots stack each activation");
            }
            var aura = new CardSetBattleSetup(EntityId.New(), set.Thresholds[0].Abilities);
            var refund = new CardSetBattleSetup(EntityId.New(), set.Thresholds[1].Abilities);
            var ring3 = Card(new MageRingCardDefinition(), 3, 0);
            var auraResult = new CombatSimulator().Simulate(Setup([ring3], 1, reverse, sets: [aura]));
            Require(Hero(auraResult, reverse, 1).ManaRegen == 40 && Hero(auraResult, reverse, 1).MaxMana == 200, "aura excluded from ring");
            var boots2 = Card(new MageBootsCardDefinition(), 2, 1);
            var full = Setup([ring3, boots2], 50, reverse, sets: [aura, refund]);
            var restored = new CombatSimulator().Simulate(full);
            Require(Hero(restored, reverse, 50).Mana == 200, "five piece refunds exact payment");
            var runtime = new BattleRuntime(Setup([ring3], 1, reverse));
            var source = runtime.Cards.Single();
            var own = runtime.GetHero(source.Side);
            var pending = new PendingAbility(source, source.Abilities[0], false, false, BattleTick.Zero);
            BattleEffectResolver.ApplyEffect(runtime, pending, new ApplyStatusEffectDefinition(BattleStatus.ManaRegen, 4));
            Require(runtime.Queue.Count == 0 && own.ManaRegen == 4, "nonactive status cannot trigger ring");
        }
        return true;
    }

    private static HeroBattleSnapshot Hero(BattleResult result, bool reverse, int tick)
    {
        var frame = result.States.Last(s => s.Tick.Value == tick && s.Player.Health > 0 && s.Opponent.Health > 0);
        return reverse ? frame.Opponent : frame.Player;
    }

    private static BattleSetup Setup(CardBattleSetup[] cards, int ticks, bool reverse, int regen = 0, int mana = 200, CardSetBattleSetup[]? sets = null)
    {
        var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, 200, mana, regen), cards, sets: sets);
        var enemy = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), []);
        return new BattleSetup(reverse ? enemy : own, reverse ? own : enemy, 42, new BattleTick(ticks), new BattleTick(ticks + 1));
    }

    private static CardBattleSetup Card(CardDefinition definition, int level, int start)
    {
        var card = new EntityFactory().CreateCard(definition, level);
        var identity = card.Attributes.Identity;
        return new CardBattleSetup(card.Id, start, 0, 50, card.Abilities, UseLegacyAttack: false,
            Tags: card.Tags, OccupiedSlots: identity.OccupiedSlots, ElementKeys: identity.ElementKeys)
            { Identity = identity, Level = level, CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues() };
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("魔导士验证失败：" + message); }
    internal static async Task Capture(Control owner)
    {
        owner.GetWindow().SetFlag(Window.Flags.NoFocus, true);
        var details = new CardDetailsView { Theme = MatchTheme.Create() }; owner.AddChild(details);
        var registry = DefinitionRegistry.Scan(typeof(MageSetDefinition).Assembly);
        details.SetEntries(CardCatalogQuery.Capture(registry));
        var i = 0;
        foreach (var definition in new CardDefinition[] { new MageRingCardDefinition(), new MageBootsCardDefinition(), new MageCircletCardDefinition() })
        {
            var snapshot = new CardPreviewQuery(registry).Describe(MatchDisplayQuery.FromOffer(ShopOffer.Create(definition)), null);
            using var art = GD.Load<Texture2D>(snapshot.Illustration.ToString()).GetImage();
            Require(art.HasMipmaps() && (i == 2 ? art.GetWidth() == art.GetHeight() : art.GetWidth() * 2 == art.GetHeight()), "art ratio/mipmaps");
            GD.Print($"{snapshot.DisplayName}: {art.GetWidth()}×{art.GetHeight()}, mipmaps={art.GetMipmapCount()}");
            var face = new CardItemView { Position = new Vector2(30, 40), Size = new Vector2(i == 2 ? 200 : 100, 200) };
            owner.AddChild(face); face.Render(snapshot, new CardDisplayAdapter());
            details.ShowCard(snapshot, new Vector2(270, 40), owner.GetViewportRect().Size);
            for (var j = 0; j < 12; j++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw(); using var image = owner.GetViewport().GetTexture().GetImage(); image.SavePng($"res://output/mage-equipment/card-{i++}.png");
            face.QueueFree();
        }
        details.QueueFree();
    }
}
