using System;
using System.Collections.Generic;
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
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 风刃螳螂与静水蝎的等级、随机疾速、伤害及出售回归（表现验证层）。
internal static class MantisScorpionChecks
{
    internal static bool LevelsAndCombat()
    {
        var selected = new HashSet<EntityId>();
        foreach (var definition in new CardDefinition[] { new WindbladeMantisCardDefinition(), new StillwaterScorpionCardDefinition() })
        {
            var mantis = definition is WindbladeMantisCardDefinition;
            var element = mantis ? GameElements.Wind : GameElements.Water;
            Require(definition.InitialLevel == 1 && definition.Attributes.Identity.Size == CardSize.Small
                && definition.Attributes.Identity.FactionKey == new StringName("mona") && definition.Tags.Contains(GameTags.Insect)
                && definition.Attributes.Identity.ElementKeys.SequenceEqual(new[] { element }) && !definition.SupportsLevel(5), "identity");
            for (var level = 1; level <= 4; level++)
            foreach (var reverse in new[] { false, true })
            {
                var card = new EntityFactory().CreateCard(definition, level);
                Require(card.Abilities.Single().CooldownTicks == 40 && card.Abilities.Single().ManaCost == 0
                    && card.OnSellReward is ReduceLeftmostElementCardCooldownOnSellDefinition reward
                    && reward.ElementKey == element && reward.Percent == level, "level/reward");
                var source = new CardBattleSetup(card.Id, 0, card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage), 40,
                    card.Abilities, UseLegacyAttack: false, Tags: card.Tags, ElementKeys: [element])
                    { CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues(), Identity = card.Attributes.Identity, Level = level };
                var ally = new CardBattleSetup(EntityId.New(), 1, 0, 0, [], UseLegacyAttack: false);
                var bench = ally with { EntityId = EntityId.New(), BoardStart = 2, IsOnBench = true };
                var enemy = ally with { EntityId = EntityId.New() };
                for (ulong seed = 1; seed <= (mantis ? 12UL : 1UL); seed++)
                {
                    var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, MaxMana: 0, ManaRegen: 0), [source, ally, bench]);
                    var other = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, MaxMana: 0, ManaRegen: 0), [enemy]);
                    var setup = new BattleSetup(reverse ? other : own, reverse ? own : other, seed, new BattleTick(40), new BattleTick(41));
                    var result = new CombatSimulator().Simulate(setup);
                    Require(result.Events.OfType<AbilityActivatedEvent>().Single().Tick.Value == 40, "4s zero mana activation");
                    if (mantis)
                    {
                        var affected = result.States[^1].Cards.Where(c => c.Haste > 0).ToArray();
                        Require(affected.Length == 1 && (affected[0].Id == card.Id || affected[0].Id == ally.EntityId)
                            && affected[0].Haste == level * 10, "random allied haste");
                        if (level == 1 && !reverse) selected.Add(affected[0].Id == card.Id ? default : ally.EntityId);
                    }
                    else
                    {
                        var damage = result.Events.OfType<DamageDealtEvent>().Single();
                        Require(damage.RawDamage == 10 << (level - 1) && damage.TargetSide == (reverse ? SideId.Player : SideId.Opponent), "scaled damage");
                    }
                    Require(result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                        && result.PermanentAttributeBonuses.Count == 0, "determinism and battle isolation");
                    var benched = new BattleSideSetup(own.Hero, [source with { IsOnBench = true }]);
                    var disabled = new CombatSimulator().Simulate(new BattleSetup(benched, other, seed, new BattleTick(40), new BattleTick(41)));
                    Require(!disabled.Events.OfType<AbilityActivatedEvent>().Any(), "bench excluded");
                }
            }
        }
        Require(selected.Count == 2, "self and ally reachable");
        return true;
    }

    internal static bool Sales() => BlackRhinocerosBeetleChecks.SaleTargetsAndPersistence(new WindbladeMantisCardDefinition(), GameElements.Wind)
        && BlackRhinocerosBeetleChecks.SaleTargetsAndPersistence(new StillwaterScorpionCardDefinition(), GameElements.Water);

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("螳螂／静水蝎验证失败：" + message); }

    internal static async Task Capture(Control owner)
    {
        var registry = DefinitionRegistry.Scan(typeof(MantisScorpionChecks).Assembly);
        var details = new CardDetailsView { Theme = MatchTheme.Create() }; owner.AddChild(details);
        details.SetEntries(CardCatalogQuery.Capture(registry));
        var index = 0;
        foreach (var definition in new CardDefinition[] { new WindbladeMantisCardDefinition(), new StillwaterScorpionCardDefinition() })
        {
            var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition));
            using var art = GD.Load<Texture2D>(snapshot.Illustration.ToString()).GetImage();
            Require(art.GetWidth() * 2 == art.GetHeight() && art.HasMipmaps(), "portrait/mipmaps");
            GD.Print($"{snapshot.DisplayName} {art.GetWidth()}×{art.GetHeight()} mipmaps={art.GetMipmapCount()}");
            var face = new CardItemView { Position = new Vector2(30, 40), Size = new Vector2(100, 200) }; owner.AddChild(face);
            face.Render(snapshot, new CardDisplayAdapter());
            details.ShowCard(snapshot, new Vector2(270, 40), owner.GetViewportRect().Size);
            for (var i = 0; i < 12; i++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw(); using var image = owner.GetViewport().GetTexture().GetImage(); image.SavePng($"res://output/mantis-scorpion/card-{index++}.png");
            face.QueueFree();
        }
        details.QueueFree();
    }
}
