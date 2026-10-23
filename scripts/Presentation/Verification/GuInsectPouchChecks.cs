using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Content;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

// 虫蛊袋等级、标签筛选、双回响及原画验证（表现验证层）。
internal static class GuInsectPouchChecks
{
    internal static bool Combat()
    {
        var definition = new GuInsectPouchCardDefinition();
        Require(definition.InitialLevel == 2 && definition.Attributes.Identity.Size == CardSize.Medium
            && definition.Attributes.Identity.FactionKey == new StringName("mona")
            && definition.Attributes.Identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            && !definition.SupportsLevel(1) && !definition.SupportsLevel(5), "identity");
        foreach (var level in new[] { 2, 3, 4 })
        foreach (var reverse in new[] { false, true })
        foreach (var insect in new[] { false, true })
        foreach (var size in new[] { 1, 2, 3 })
        {
            var card = new EntityFactory().CreateCard(definition, level);
            Require(card.Abilities.All(a => a.ManaCost == 0 && a.CooldownTicks == 0 && a.Activation != AbilityActivation.Active), "no active");
            var pouch = new CardBattleSetup(card.Id, 0, card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage), 0,
                card.Abilities, UseLegacyAttack: false, OccupiedSlots: 2)
                { Identity = card.Attributes.Identity, Level = level, CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues() };
            var victim = new CardBattleSetup(EntityId.New(), 2, 0, 10,
                [new AbilityDefinition(new StringName("verification.insect.consume"), AbilityActivation.Active, AbilityTarget.SelfCard, 0, 10,
                    [new DestroyCardEffectDefinition(false)])], UseLegacyAttack: false,
                Tags: new TagSet(insect ? new[] { GameTags.Insect } : Array.Empty<StringName>()), OccupiedSlots: size);
            var enemy = new CardBattleSetup(EntityId.New(), 0, 0, 0, [], UseLegacyAttack: false);
            var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, MaxMana: 0, ManaRegen: 0), [pouch, victim]);
            var other = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, MaxMana: 0, ManaRegen: 0), [enemy]);
            var setup = new BattleSetup(reverse ? other : own, reverse ? own : other, 42, new BattleTick(10), new BattleTick(11));
            var result = new CombatSimulator().Simulate(setup);
            var hits = result.Events.OfType<DamageDealtEvent>().Where(e => e.SourceCardId == pouch.EntityId).ToArray();
            Require(hits.Length == (insect ? 1 : 0), "destroy tag filter");
            if (insect) Require(hits[0].RawDamage == 40 << (level - 2) && hits[0].TargetSide == (reverse ? SideId.Player : SideId.Opponent), "damage");
            Require(result.States[^1].Cards.Single(c => c.Id == enemy.EntityId).Slow == (insect ? 10 : 0), "activation tag filter and enemy slow");
            Require(result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events) && result.PermanentAttributeBonuses.Count == 0, "determinism/isolation");
            var disabled = new BattleSideSetup(own.Hero, [pouch with { IsOnBench = true }, victim]);
            var excluded = new CombatSimulator().Simulate(new BattleSetup(disabled, other, 42, new BattleTick(10), new BattleTick(11)));
            Require(!excluded.Events.OfType<DamageDealtEvent>().Any() && excluded.States[^1].Cards.Single(c => c.Id == enemy.EntityId).Slow == 0, "bench source excluded");
            var offer = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            Require(CardDisplayAdapter.Rules(offer).All(rule => rule.Contains("虫族")), "both descriptions include tag");
            if (level == 2 && !reverse && insect && size == 1)
            {
                var taggedPouch = pouch with { Abilities = pouch.Abilities!.Concat(new[] {
                    new AbilityDefinition(new StringName("verification.insect.tag_aura"), AbilityActivation.PassiveAura,
                        AbilityTarget.AlliedHero, 0, 0, [new GrantTagToAlliedElementCardsEffectDefinition(GameElements.Fire, GameTags.Insect)]) }).ToArray() };
                var temporaryInsect = victim with { Tags = new TagSet(), ElementKeys = [GameElements.Fire] };
                var tagged = new CombatSimulator().Simulate(new BattleSetup(new BattleSideSetup(own.Hero, [taggedPouch, temporaryInsect]), other,
                    42, new BattleTick(10), new BattleTick(11)));
                Require(tagged.Events.OfType<DamageDealtEvent>().Single().RawDamage == 40
                    && tagged.States[^1].Cards.Single(c => c.Id == enemy.EntityId).Slow == 10, "frozen temporary tag");
                var destroyedPouch = pouch with { Abilities = pouch.Abilities!.Concat(new[] {
                    new AbilityDefinition(new StringName("verification.insect.destroy_source"), AbilityActivation.PassiveOnBattleStart,
                        AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)]) }).ToArray() };
                var inactive = new CombatSimulator().Simulate(new BattleSetup(new BattleSideSetup(own.Hero, [destroyedPouch, victim]), other,
                    42, new BattleTick(10), new BattleTick(11)));
                Require(!inactive.Events.OfType<DamageDealtEvent>().Any()
                    && inactive.States[^1].Cards.Single(c => c.Id == enemy.EntityId).Slow == 0, "destroyed provider excluded");
            }
        }
        return true;
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("虫蛊袋验证失败：" + message); }

    internal static async Task Capture(Control owner)
    {
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new GuInsectPouchCardDefinition()));
        using var art = GD.Load<Texture2D>(snapshot.Illustration.ToString()).GetImage();
        Require(art.GetWidth() == art.GetHeight() && art.HasMipmaps(), "art/mipmaps");
        GD.Print($"虫蛊袋 {art.GetWidth()}×{art.GetHeight()} mipmaps={art.GetMipmapCount()}");
        var face = new CardItemView { Position = new Vector2(30, 40), Size = new Vector2(200, 200) }; owner.AddChild(face);
        face.Render(snapshot, new CardDisplayAdapter());
        var details = new CardDetailsView { Theme = MatchTheme.Create() }; owner.AddChild(details);
        details.SetEntries(CardCatalogQuery.Capture(DefinitionRegistry.Scan(typeof(GuInsectPouchChecks).Assembly)));
        details.SetPinned(false); details.ShowCard(snapshot, new Vector2(270, 40), owner.GetViewportRect().Size);
        for (var i = 0; i < 12; i++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        RenderingServer.ForceDraw(); using var image = owner.GetViewport().GetTexture().GetImage(); image.SavePng("res://output/gu-insect-pouch/card.png");
        details.QueueFree(); face.QueueFree();
    }
}
