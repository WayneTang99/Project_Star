using System;
using System.Threading.Tasks;
using Project_Star.Application.Economy;
using Project_Star.Application.Match;
using Project_Star.Domain.Match;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Game;
using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 弓箭塔四级伤害、敌方发动充能及建筑分类验证（表现层）。
internal static class ArcherTowerChecks
{
    internal static bool LevelsAndEcho()
    {
        var definition = new ArcherTowerCardDefinition();
        var identity = definition.Attributes.Identity;
        var texture = ResourceLoader.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.DisplayName != "弓箭塔" || identity.FactionKey != new StringName("paladin")
            || identity.Size != CardSize.Medium || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.InitialLevel != 1 || definition.SupportsLevel(5)
            || !definition.Tags.Contains(GameTags.Building) || definition.Tags.Contains(GameTags.Location)
            || texture is null || texture.GetWidth() != texture.GetHeight() || !texture.GetImage().HasMipmaps()
            || !DefinitionRegistry.Scan(typeof(ArcherTowerChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        GD.Print($"弓箭塔原画 {texture.GetWidth()}×{texture.GetHeight()} mipmaps={texture.GetImage().GetMipmapCount()}");
        for (var level = 1; level <= 4; level++)
        foreach (var mirrored in new[] { false, true })
        {
            var instance = new EntityFactory().CreateCard(definition, level);
            var damage = 20 << (level - 1);
            if (instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != damage
                || instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) != 80
                || instance.Abilities.Any(a => a.ManaCost != 0)) return false;
            var tower = new CardBattleSetup(instance.Id, 0, damage, 80, instance.Abilities, UseLegacyAttack: false,
                Tags: instance.Tags, ElementKeys: identity.ElementKeys) { Level = level };
            var trigger = new CardBattleSetup(EntityId.New(), 3, 0, 20,
                [new AbilityDefinition("verification.tower.trigger", AbilityActivation.Active, AbilityTarget.AlliedHero,
                    0, 20, [new HealEffectDefinition(1)])], UseLegacyAttack: false);
            BattleSetup Setup(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks) => new(
                new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), mirrored ? enemies : allies),
                new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), mirrored ? allies : enemies),
                42, new BattleTick(ticks), new BattleTick(ticks + 1));
            foreach (var enemy in new[] { false, true })
            {
                var setup = Setup(enemy ? [tower] : [tower, trigger], enemy ? [trigger] : [], 80);
                var result = new CombatSimulator().Simulate(setup);
                var hit = result.Events.OfType<DamageDealtEvent>().Single(e => e.SourceCardId == tower.EntityId);
                if (hit.RawDamage != damage || hit.Tick.Value != (enemy ? 60 : 80)
                    || result.Events.OfType<CardChargedEvent>().Count() != (enemy ? 4 : 0)
                    || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            }
            var multi = new CombatSimulator().Simulate(Setup([tower], [trigger with { Multicast = 1 }], 22));
            if (multi.Events.OfType<CardChargedEvent>().Count() != 2
                || multi.Events.OfType<CardChargedEvent>().Any(e => e.TargetCardId != tower.EntityId || e.AmountTicks != 10)) return false;
            var bench = new CombatSimulator().Simulate(Setup([tower], [trigger with { IsOnBench = true }], 80));
            if (bench.Events.OfType<CardChargedEvent>().Any()) return false;
        }
        return true;
    }

    internal static bool BuildingTags()
    {
        CardDefinition[] buildings = [new BlackMarketPawnshopCardDefinition(), new BlacksmithCardDefinition(),
            new EquipmentShopCardDefinition(), new ArcherTowerCardDefinition()];
        CardDefinition[] places = [new SkyvaultTowerCardDefinition(), new CathedralCardDefinition(), new ConquerorFortressCardDefinition(),
            new TrainingCampCardDefinition(), new WarhammerFortressCardDefinition(), new ZenLightTempleCardDefinition()];
        CardDefinition[] locations = [new DawnlightGateCardDefinition(), new BoneBarrowCardDefinition(), new HiddenBambooGroveCardDefinition(), new WheatFieldCardDefinition()];
        return TagDisplayNames.Get(GameTags.Building) == "建筑"
            && buildings.All(d => d.Tags.Contains(GameTags.Building) && !d.Tags.Contains(GameTags.Location))
            && places.All(d => d.Tags.Contains(GameTags.Building) && d.Tags.Contains(GameTags.Location))
            && locations.All(d => !d.Tags.Contains(GameTags.Building) && d.Tags.Contains(GameTags.Location));
    }
    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(460, 300), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(460, 300) });
            var face = new CardItemView { Position = new Vector2(24, 24), Size = new Vector2(240, 240) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new ArcherTowerCardDefinition())), new CardDisplayAdapter());
            var compact = new CardItemView { Position = new Vector2(310, 24), Size = new Vector2(100, 100) };
            viewport.AddChild(compact);
            compact.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new ArcherTowerCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/archer-tower/card-face.png") != Error.Ok)
                throw new InvalidOperationException("Archer tower card capture failed.");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }
}
