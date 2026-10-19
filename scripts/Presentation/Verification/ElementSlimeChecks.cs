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
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 十种小型史莱姆的分级、周期效果、随机目标与原画验证（表现层）。
internal static class ElementSlimeChecks
{
    private static CardDefinition[] Definitions() =>
    [new FireSlimeCardDefinition(), new WaterSlimeCardDefinition(), new WindSlimeCardDefinition(),
        new EarthSlimeCardDefinition(), new LightningSlimeCardDefinition(), new WoodSlimeCardDefinition(),
        new IceSlimeCardDefinition(), new LightSlimeCardDefinition(), new DarkSlimeCardDefinition(), new SlimeCardDefinition()];

    internal static bool LevelsAndEffects()
    {
        var catalog = DefinitionRegistry.Scan(typeof(ElementSlimeChecks).Assembly);
        var elements = new[] { GameElements.Fire, GameElements.Water, GameElements.Wind, GameElements.Earth,
            GameElements.Lightning, GameElements.Wood, GameElements.Ice, GameElements.Light, GameElements.Dark, GameElements.General };
        var names = new[] { "火", "水", "风", "土", "雷", "木", "冰", "光", "暗", "" };
        var definitions = Definitions();
        for (var index = 0; index < definitions.Length; index++)
        {
            var definition = definitions[index]; var identity = definition.Attributes.Identity;
            var control = index is 7 or 8; var status = index is 0 or 5;
            var initialLevel = control ? 3 : 1;
            var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
            using var image = texture?.GetImage();
            if (!catalog.Cards.ContainsKey(identity.Key) || identity.DisplayName != names[index] + "史莱姆"
                || identity.FactionKey != GameFactions.Neutral || identity.Size != CardSize.Small
                || !identity.ElementKeys.SequenceEqual(new[] { elements[index] })
                || definition.InitialLevel != initialLevel || definition.SupportsLevel(initialLevel - 1)
                || definition.SupportsLevel(5) || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Small)
                || !definition.Tags.Contains(GameTags.Material)
                || image is null || image.GetHeight() != image.GetWidth() * 2 || !image.HasMipmaps()) return Fail();
            for (var level = initialLevel; level <= 4; level++)
            foreach (var reverse in new[] { false, true })
            foreach (var targetCount in control ? new[] { 0, 1, 3 } : new[] { 0 })
            {
                var card = new EntityFactory().CreateCard(definition, level);
                var values = card.Attributes.BaseCombat.SnapshotFinalValues();
                if (card.Abilities.Single().ManaCost != 0 || values[GameAttributeKeys.CooldownTicks] != 30
                    || !card.Tags.Contains(GameTags.Material)) return Fail();
                var source = new CardBattleSetup(card.Id, 0, values.TryGetValue(GameAttributeKeys.AttackDamage, out var attack) ? attack : 0, 30, card.Abilities, UseLegacyAttack: false,
                    Tags: card.Tags, ElementKeys: identity.ElementKeys) { Level = level, CombatValues = values };
                var targets = Enumerable.Range(0, targetCount).Select(slot => new CardBattleSetup(EntityId.New(), slot,
                    0, 1000, [], UseLegacyAttack: false)).ToArray();
                var bench = new CardBattleSetup(EntityId.New(), 0, 0, 1000, [], IsOnBench: true, UseLegacyAttack: false);
                var allies = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), [source]);
                var enemies = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), targets.Append(bench).ToArray());
                var setup = new BattleSetup(reverse ? enemies : allies, reverse ? allies : enemies, 42,
                    new BattleTick(71), new BattleTick(72));
                var result = new CombatSimulator().Simulate(setup);
                var activations = result.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId == card.Id).ToArray();
                var damage = result.Events.OfType<DamageDealtEvent>().Where(e => e.SourceCardId == card.Id).ToArray();
                if (!activations.Select(e => e.Tick.Value).SequenceEqual(new long[] { 30, 60 })
                    || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                    || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any()) return Fail();
                if (control)
                {
                    var statuses = result.Events.OfType<StatusChangedEvent>().ToArray();
                    if (damage.Length != 0 || statuses.Length != 2 * Math.Min(targetCount, level - 2)
                        || statuses.Any(e => e.Status != BattleStatus.ImmobilizeDuration || e.Amount != 10)) return Fail();
                    foreach (var tick in new[] { 30, 60 })
                    {
                        var affected = result.States.Last(s => s.Tick.Value == tick).Cards.Where(c => c.Immobilize > 0).ToArray();
                        if (affected.Length != Math.Min(targetCount, level - 2)
                            || affected.Any(c => c.Immobilize != 10 || !targets.Any(t => t.EntityId == c.Id))) return Fail();
                    }
                    if (result.States.Last(s => s.Tick.Value == 40).Cards.Any(c => c.Immobilize > 0)) return Fail();
                }
                else if (status)
                {
                    var expectedStatus = index == 0 ? BattleStatus.Burn : BattleStatus.Poison;
                    var applications = result.Events.OfType<StatusChangedEvent>().ToArray();
                    if (damage.Length != 0 || applications.Length != 2
                        || applications.Any(e => e.Status != expectedStatus || e.Amount != 2 * (1 << (level - 1)))
                        || !applications.Select(e => e.Tick.Value).SequenceEqual(new long[] { 30, 60 })) return Fail();
                }
                else if (damage.Length != 2 || damage.Any(e => e.RawDamage != 10 * (1 << (level - 1)))
                    || result.Events.OfType<StatusChangedEvent>().Any()) return Fail();
            }
        }
        return true;
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(1660, 600), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(1660, 600) });
            var definitions = Definitions();
            foreach (var definition in definitions.Append(new BigSlimeCardDefinition()))
            {
                using var artwork = GD.Load<Texture2D>(definition.Attributes.Identity.Illustration.ToString()).GetImage();
                GD.Print($"{definition.Attributes.Identity.DisplayName}原画 {artwork.GetWidth()}×{artwork.GetHeight()} mipmaps={artwork.GetMipmapCount()}");
                if (!artwork.HasMipmaps()) throw new InvalidOperationException("史莱姆原画缺少mipmap。");
            }
            for (var index = 0; index < definitions.Length; index++)
            for (var row = 0; row < 2; row++)
            {
                var definition = definitions[index];
                var face = new CardItemView { Position = new Vector2(12 + index * 138, 16 + row * 288), Size = new Vector2(120, 240) };
                viewport.AddChild(face);
                face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, row == 0 ? definition.InitialLevel : 4)), new CardDisplayAdapter());
            }
            foreach (var row in new[] { 0, 1 })
            {
                var face = new CardItemView { Position = new Vector2(1400, 16 + row * 288), Size = new Vector2(240, 240) };
                viewport.AddChild(face);
                face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new BigSlimeCardDefinition(), row == 0 ? 1 : 4)), new CardDisplayAdapter());
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/element-slimes/card-faces.png") != Error.Ok)
                throw new InvalidOperationException("史莱姆卡面截图保存失败。");
            viewport.Size *= 2;
            foreach (var child in viewport.GetChildren().OfType<Control>())
            {
                child.Position *= 2;
                child.Size *= 2;
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw(false);
            using var enlarged = viewport.GetTexture().GetImage();
            if (enlarged.SavePng("res://output/element-slimes/card-faces-large.png") != Error.Ok)
                throw new InvalidOperationException("史莱姆放大卡面截图保存失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static bool Fail([System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        GD.PushError("ElementSlimeChecks 验证失败，行 " + line);
        return false;
    }
}
