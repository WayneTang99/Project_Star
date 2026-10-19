using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Content;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 军械库的分级交易、标签光环、生命周期与原生原画验证（表现层）。
internal static class EquipmentShopChecks
{
    internal static bool LevelPreview()
    {
        var registry = DefinitionRegistry.Scan(typeof(EquipmentShopChecks).Assembly);
        var query = new CardPreviewQuery(registry);
        foreach (var definition in new CardDefinition[] { new EquipmentShopCardDefinition(), new BlackMarketPawnshopCardDefinition() })
        foreach (var start in Enumerable.Range(definition.InitialLevel, 5 - definition.InitialLevel))
        foreach (var end in Enumerable.Range(definition.InitialLevel, 5 - definition.InitialLevel))
        {
            var original = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, start));
            var source = original with { Value = original.Value + 5 };
            var preview = query.Preview(source, end, new StringName?[source.GemSockets.Count]);
            var expected = 5 + MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, end)).Value;
            var aura = CardDisplayAdapter.FaceEffects(preview).Where(effect => effect.Kind is CardFaceEffectKind.Damage or CardFaceEffectKind.Armor).ToArray();
            if (preview.Value != expected || aura.Length != (definition is EquipmentShopCardDefinition ? 2 : 1)
                || aura.Any(effect => effect.Value != $"+{expected}") || source.Value != original.Value + 5) return false;
            if (definition is EquipmentShopCardDefinition && !CardDisplayAdapter.Rules(preview).Any(line =>
                line.StartsWith("交易") && line.Contains($"+{end - 1}"))) return false;
        }
        return true;
    }

    internal static bool AcquisitionAndLevels()
    {
        var definition = new EquipmentShopCardDefinition();
        var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        using var image = texture?.GetImage();
        if (identity.DisplayName != "军械库" || identity.Key != new StringName("card.equipment_shop")
            || identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Medium
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !definition.Tags.Contains(GameTags.Building) || definition.InitialLevel != 2
            || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || texture is null || texture.GetWidth() != texture.GetHeight() || image?.HasMipmaps() != true
            || !DefinitionRegistry.Scan(typeof(EquipmentShopChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        foreach (var level in new[] { 2, 3, 4 })
        {
            var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
            var economy = new CardEconomyService(factory, board);
            var session = new CreateMatchService(factory).Create(42, 100, new PaladinHeroDefinition());
            var field = factory.CreateCard(definition, level); var bench = factory.CreateCard(definition, level);
            var loose = factory.CreateCard(definition, level);
            foreach (var card in new[] { field, bench, loose })
            {
                card.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, 7);
                session.Player.Inventory.Add(card);
            }
            if (board.PlaceCard(session, field.Id, BoardZone.Battlefield, 0).IsFailure
                || board.PlaceCard(session, bench.Id, BoardZone.Bench, 0).IsFailure) return false;
            var offer = ShopOffer.Create(new HandCrossbowCardDefinition());
            if (economy.BuyAndPlace(session, offer).IsFailure || economy.BuyAndPlace(session, offer).IsSuccess) return false;
            var acquired = economy.AcquireAndPlace(session, offer.Definition, 1, CardAcquisitionSource.Reward);
            if (acquired.IsFailure || acquired.Value!.WasCreated || acquired.Value.CurrentLevel != 2) return false;
            if (economy.AcquireCard(session, new BeastHideCardDefinition(), 1, CardAcquisitionSource.Drop).IsFailure) return false;
            var expected = 7 + 2 * (level - 1);
            if (Value(field) != expected || Value(bench) != expected || Value(loose) != 7) return false;
            var rules = CardDisplayAdapter.Rules(MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level)));
            if (!rules.Any(line => line.StartsWith("交易") && line.Contains($"+{level - 1}") && line.Contains("装备"))
                || !rules.Any(line => line.StartsWith("光环") && line.Contains("装备") && line.Contains("价值"))) return false;
            factory.ApplyCardLevel(field, definition, 4);
            if (Value(field) != expected || economy.AcquireCard(session, new GuardianCrossCardDefinition(), 1,
                CardAcquisitionSource.Reward).IsFailure || Value(field) != expected + 3 || Value(bench) != expected + level - 1) return false;
            bench.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, int.MaxValue);
            var failedOffer = ShopOffer.Create(new GuardianCrossCardDefinition());
            var wealth = session.Player.Wealth; var count = session.Player.Inventory.Cards.Count;
            if (economy.BuyCard(session, failedOffer).IsSuccess || failedOffer.IsSold || session.Player.Wealth != wealth
                || session.Player.Inventory.Cards.Count != count || Value(field) != expected + 3
                || economy.AcquireAndPlace(session, failedOffer.Definition, 1, CardAcquisitionSource.Reward).IsSuccess
                || economy.AcquireCard(session, failedOffer.Definition, 1, CardAcquisitionSource.Reward).IsSuccess) return false;
        }
        return true;
    }

    internal static bool AuraFiltersAndLifetime()
    {
        var factory = new EntityFactory();
        foreach (var level in new[] { 2, 3, 4 })
        foreach (var reverse in new[] { false, true })
        {
            var source = factory.CreateCard(new EquipmentShopCardDefinition(), level);
            var shop = new CardBattleSetup(source.Id, 0, 0, 0, source.Abilities, UseLegacyAttack: false,
                Tags: source.Tags, OccupiedSlots: 2)
                { CombatValues = new Dictionary<StringName, int> { [GameAttributeKeys.Value] = 7 } };
            var second = shop with { EntityId = EntityId.New(), BoardStart = 2 };
            var equipment = Fighter(4, true); var other = Fighter(5, false);
            var bench = Fighter(0, true) with { IsOnBench = true };
            var armorOnly = Fighter(6, true) with { AttackDamage = 0, Abilities =
                [new AbilityDefinition(new StringName("ability.verify_shop_armor_only"), AbilityActivation.Active,
                    AbilityTarget.AlliedHero, 0, 10, [new GainSourceHeroArmorFromAttributeEffectDefinition(GameAttributeKeys.Armor)])] };
            var destroyed = Fighter(7, true);
            destroyed = destroyed with { Abilities = destroyed.Abilities!.Append(Destroy()).ToArray() };
            var zero = Fighter(8, true) with { AttackDamage = 0, ArmorAmount = 0 };
            var enemy = Fighter(0, true);
            foreach (var sources in new[] { new[] { shop }, new[] { shop, second },
                new[] { shop with { IsOnBench = true } }, new[] { shop with { Abilities = source.Abilities.Append(Destroy()).ToArray() } } })
            {
                var bonus = sources.Count(item => !item.IsOnBench && !item.Abilities!.SelectMany(ability => ability.Effects)
                    .OfType<DestroyCardEffectDefinition>().Any()) * 7;
                var setup = Battle(sources.Concat(new[] { equipment, other, bench, armorOnly, destroyed, zero }).ToArray(), [enemy], reverse);
                var result = new CombatSimulator().Simulate(setup);
                var state = result.States.Last();
                int Attr(CardBattleSetup card, StringName key) => state.Cards.Single(item => item.Id == card.EntityId).Values[key];
                if (Attr(equipment, GameAttributeKeys.AttackDamage) != 10 + bonus || Attr(equipment, GameAttributeKeys.Armor) != 20 + bonus
                    || Attr(other, GameAttributeKeys.AttackDamage) != 10 || Attr(other, GameAttributeKeys.Armor) != 20
                    || Attr(bench, GameAttributeKeys.AttackDamage) != 10 || Attr(enemy, GameAttributeKeys.AttackDamage) != 10
                    || Attr(armorOnly, GameAttributeKeys.AttackDamage) != 0 || Attr(armorOnly, GameAttributeKeys.Armor) != 20 + bonus
                    || Attr(destroyed, GameAttributeKeys.AttackDamage) != 10 || Attr(destroyed, GameAttributeKeys.Armor) != 20
                    || Attr(zero, GameAttributeKeys.AttackDamage) != bonus || Attr(zero, GameAttributeKeys.Armor) != bonus
                    || Attr(shop, GameAttributeKeys.AttackDamage) != 0
                    || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            }
            var frozen = Battle([shop, equipment], [], reverse);
            source.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, 100);
            var hit = new CombatSimulator().Simulate(frozen).Events.OfType<DamageDealtEvent>().Single();
            if (hit.RawDamage != 17) return false;
        }
        return true;
    }

    internal static async Task Capture(Node owner)
    {
        GD.Print($"军械库等级预览验证：{LevelPreview()}");
        var viewport = new SubViewport { Size = new Vector2I(920, 640), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("303b44"), Size = new Vector2(920, 640) });
            var card = MatchDisplayQuery.FromOffer(ShopOffer.Create(new EquipmentShopCardDefinition()));
            var texture = GD.Load<Texture2D>(card.Illustration.ToString());
            using var artwork = texture.GetImage();
            GD.Print($"军械库原画：{artwork.GetWidth()}×{artwork.GetHeight()}，mipmap层数：{artwork.GetMipmapCount()}。");
            var large = new CardItemView { Position = new Vector2(20, 20), Size = new Vector2(440, 440) };
            var small = new CardItemView { Position = new Vector2(480, 20), Size = new Vector2(100, 100) };
            viewport.AddChild(large); viewport.AddChild(small);
            large.Render(card, new CardDisplayAdapter()); small.Render(card, new CardDisplayAdapter());
            var label = new Label { Position = new Vector2(480, 150), Size = new Vector2(420, 330),
                AutowrapMode = TextServer.AutowrapMode.WordSmart, Text = string.Join("\n\n", CardDisplayAdapter.Rules(card)) };
            viewport.AddChild(label);
            var query = new CardPreviewQuery(DefinitionRegistry.Scan(typeof(EquipmentShopChecks).Assembly));
            foreach (var level in new[] { 3, 4 })
            {
                var preview = query.Preview(card, level, new StringName?[card.GemSockets.Count]);
                var upgraded = new CardItemView { Position = new Vector2(480 + (level - 3) * 200, 330), Size = new Vector2(160, 160) };
                viewport.AddChild(upgraded); upgraded.Render(preview, new CardDisplayAdapter());
                viewport.AddChild(new Label { Position = new Vector2(480 + (level - 3) * 200, 500), Text = $"切换{level}级：攻防各+{preview.Value}" });
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/equipment-shop/card-face.png") != Error.Ok)
                throw new InvalidOperationException("军械库截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static int Value(CardInstance card) => card.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Value);
    private static AbilityDefinition Destroy() => new(new StringName("ability.verify_destroy_shop"),
        AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)]);
    private static CardBattleSetup Fighter(int start, bool equipment) => new(EntityId.New(), start, 10, 10,
        [new AbilityDefinition(new StringName("ability.verify_shop_attack_armor"), AbilityActivation.Active,
            AbilityTarget.EnemyHero, 0, 10, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage),
                new GainSourceHeroArmorFromAttributeEffectDefinition(GameAttributeKeys.Armor)])],
        UseLegacyAttack: false, Tags: new TagSet(equipment ? new[] { GameTags.Equipment } : new[] { GameTags.Human }), ArmorAmount: 20);
    private static BattleSetup Battle(CardBattleSetup[] allies, CardBattleSetup[] enemies, bool reverse) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), reverse ? enemies : allies),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0), reverse ? allies : enemies),
        42, new BattleTick(10), new BattleTick(11));
}
