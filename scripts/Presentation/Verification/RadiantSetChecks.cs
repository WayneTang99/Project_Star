using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Content;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Application.Economy;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Content.Sets;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

internal static class RadiantSetChecks
{
    internal static bool SwordShieldCombat()
    {
        var definition = new RadiantSwordShieldCardDefinition(); var identity = definition.Attributes.Identity;
        var registry = DefinitionRegistry.Scan(typeof(RadiantSetChecks).Assembly);
        using var image = GD.Load<Texture2D>(identity.Illustration.ToString()).GetImage();
        if (definition.InitialLevel != 4 || !definition.SupportsLevel(4) || definition.SupportsLevel(3) || definition.SupportsLevel(5)
            || identity.Size != CardSize.Medium || identity.SetKey != new StringName("set.radiant")
            || identity.FactionKey != new StringName("paladin") || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Light })
            || !definition.Tags.Contains(GameTags.Equipment) || image.GetWidth() != image.GetHeight() || !image.HasMipmaps()
            || new RadiantBeltCardDefinition().Attributes.Identity.DisplayName != "光辉腰带") { GD.Print($"剑盾身份失败 mipmap={image.HasMipmaps()}"); return false; }
        foreach (var reverse in new[] { false, true })
        {
            var source = Sword(); var own = Side([source]); var enemy = Side([]);
            var setup = new BattleSetup(reverse ? enemy : own, reverse ? own : enemy, 42, new BattleTick(48), new BattleTick(100));
            var result = new CombatSimulator().Simulate(setup);
            var activations = result.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId == source.EntityId).ToArray();
            var frame = result.States.Last(state => state.Tick.Value == 47);
            var attacker = reverse ? frame.Opponent : frame.Player; var defender = reverse ? frame.Player : frame.Opponent;
            if (!activations.Select(e => e.Tick.Value).SequenceEqual(new long[] { 45, 47 })
                || attacker.Armor != 400 || defender.Health != 9600 || frame.Cards.Single(c => c.Id == source.EntityId).Values[GameAttributeKeys.Multicast] != 1
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events) || result.PermanentChanges.Count != 0)
            { GD.Print($"剑盾 reverse={reverse}, ticks={string.Join(',',activations.Select(e=>e.Tick.Value))}, armor={attacker.Armor}, health={defender.Health}"); return false; }
        }
        return registry.Sets.ContainsKey(new StringName("set.radiant")) && new RadiantPlateArmorCardDefinition().Attributes.Identity.SetKey == identity.SetKey
            && new RadiantBeltCardDefinition().Attributes.Identity.SetKey == identity.SetKey;
    }

    internal static bool SetCountingAndPreview()
    {
        var registry = DefinitionRegistry.Scan(typeof(RadiantSetChecks).Assembly);
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
        var session = new CreateMatchService(factory).Create(42, 100, new PaladinHeroDefinition());
        CardInstance Add(CardDefinition definition, int level, int start)
        {
            var card = factory.CreateCard(definition, level); session.Player.Inventory.Add(card);
            if (board.PlaceCard(session, card.Id, BoardZone.Battlefield, start).IsFailure) throw new InvalidOperationException("光辉套装验证放置失败。");
            return card;
        }
        var belt = Add(new RadiantBeltCardDefinition(), 4, 0);
        var plate = Add(new RadiantPlateArmorCardDefinition(), 4, 2);
        var duplicate = Add(new RadiantBeltCardDefinition(), 4, 4);
        if (new CardSetEvaluator().Evaluate(session, registry.Sets).Count != 0) return false;
        if (board.PlaceCard(session, duplicate.Id, BoardZone.Bench, 0).IsFailure) return false;
        var sword = Add(new RadiantSwordShieldCardDefinition(), 4, 4);
        var light = Add(new RadiantStandardCardDefinition(), 3, 6);
        if (new CardSetEvaluator().Evaluate(session, registry.Sets).Single().Threshold.RequiredDistinctCards != 3) return false;
        var baseHealth = session.Player.Hero!.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth);
        var snapshot = MatchDisplayQuery.Capture(session, registry.Sets);
        var enemy = new CreateMatchService(factory).Create(43, 100, new PaladinHeroDefinition());
        var state = new CombatSimulator().Preview(new BattleSetupFactory(registry.Sets).Create(session, enemy, 42, new BattleTick(1)));
        if (!state.Cards.Single(c => c.Id == light.Id).QuestTags.Contains(GameTags.Equipment)
            || state.Player.MaxHealth != baseHealth + 400 || snapshot.Hero!.CombatValues[GameAttributeKeys.MaxHealth] != baseHealth + 400
            || light.Tags.Contains(GameTags.Equipment) || session.Player.Hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth) != baseHealth) return false;
        if (board.PlaceCard(session, plate.Id, BoardZone.Bench, 2).IsFailure) return false;
        snapshot = MatchDisplayQuery.Capture(session, registry.Sets);
        return new CardSetEvaluator().Evaluate(session, registry.Sets).Count == 0
            && snapshot.Hero!.CombatValues[GameAttributeKeys.MaxHealth] == baseHealth + 200
            && !snapshot.Cards.Single(c => c.Id == light.Id).Tags.Contains(GameTags.Equipment);
    }

    internal static bool AuraFiltersAndLifetime()
    {
        var source = Sword(); var light = Receiver(2, GameElements.Light, false);
        var equipment = Receiver(3, GameElements.Light, true); var dark = Receiver(4, GameElements.Dark, true);
        var bench = Receiver(0, GameElements.Light, true) with { IsOnBench = true }; var enemy = Receiver(0, GameElements.Light, true);
        var simulator = new CombatSimulator();
        var baseSetup = new BattleSetup(Side([source, light, equipment, dark, bench]), Side([enemy]), 42, new BattleTick(1));
        var frame = simulator.Preview(baseSetup);
        decimal Cooldown(CardBattleSnapshot card) => card.CooldownDurationUnits.Single();
        if (Cooldown(frame.Cards.Single(c => c.Id == light.EntityId)) != 200 || Cooldown(frame.Cards.Single(c => c.Id == equipment.EntityId)) != 180
            || Cooldown(frame.Cards.Single(c => c.Id == dark.EntityId)) != 200 || Cooldown(frame.Cards.Single(c => c.Id == bench.EntityId)) != 200
            || Cooldown(frame.Cards.Single(c => c.Id == enemy.EntityId)) != 200) return false;
        CardBattleSetup TagProvider(int start, int life) => new(EntityId.New(), start, 0, life,
            [new AbilityDefinition(new StringName("verify.element_tag_aura"), AbilityActivation.PassiveAura, AbilityTarget.AlliedHero, 0, 0,
                [new GrantTagToAlliedElementCardsEffectDefinition(GameElements.Light, GameTags.Equipment)]),
             new AbilityDefinition(new StringName("verify.element_tag_expire"), AbilityActivation.Active, AbilityTarget.SelfCard, 0, life,
                [new DestroyCardEffectDefinition(false)])], UseLegacyAttack: false);
        var first = TagProvider(6, 10); var second = TagProvider(7, 20);
        var result = simulator.Simulate(new BattleSetup(Side([source, light, equipment, dark, bench, first, second]), Side([enemy]),
            42, new BattleTick(21), new BattleTick(30)));
        CardBattleSnapshot At(long tick) => result.States.Last(state => state.Tick.Value == tick).Cards.Single(card => card.Id == light.EntityId);
        if (!At(0).QuestTags.Contains(GameTags.Equipment) || Cooldown(At(0)) != 180
            || !At(10).QuestTags.Contains(GameTags.Equipment) || Cooldown(At(10)) != 180
            || At(20).QuestTags.Contains(GameTags.Equipment) || Cooldown(At(20)) != 200) return false;
        if (light.Tags!.Contains(GameTags.Equipment) || !result.States.Last().Cards.Single(c => c.Id == equipment.EntityId).QuestTags.Contains(GameTags.Equipment)) return false;
        // 标签到期仍须保留随机永久本场标签，来源失效须撤销冷却贡献。
        var vanishing = source with { Abilities = source.Abilities!.Concat(new[] {
            new AbilityDefinition(new StringName("verify.sword_expire"), AbilityActivation.Active, AbilityTarget.SelfCard, 0, 10,
                [new DestroyCardEffectDefinition(false)]) }).ToArray() };
        result = simulator.Simulate(new BattleSetup(Side([vanishing, equipment]), Side([]), 42, new BattleTick(11), new BattleTick(30)));
        return Cooldown(result.States.Last().Cards.Single(c => c.Id == equipment.EntityId)) == 200;
    }

    private static CardBattleSetup Sword()
    {
        var definition = new RadiantSwordShieldCardDefinition(); var card = new EntityFactory().CreateCard(definition, 4);
        return new(card.Id, 0, 200, 50, card.Abilities, UseLegacyAttack: false, Tags: card.Tags, Multicast: 1,
            OccupiedSlots: 2, ElementKeys: [GameElements.Light], ArmorAmount: 200)
            { Level = 4, Identity = definition.Attributes.Identity, CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues() };
    }
    private static CardBattleSetup Receiver(int start, StringName element, bool equipment) => new(EntityId.New(), start, 1, 100,
        [new AbilityDefinition(new StringName("verify.radiant_receiver"), AbilityActivation.Active, AbilityTarget.EnemyHero, 0, 100,
            [new DamageEffectDefinition(1)])], UseLegacyAttack: false, Tags: new TagSet(equipment ? [GameTags.Equipment] : [GameTags.Human]), ElementKeys: [element]);
    private static BattleSideSetup Side(CardBattleSetup[] cards) => new(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), cards);

    internal static async Task Capture(Control owner)
    {
        if (!OrdinaryAndLightShopChecks.PoolsAndRefresh() || !OrdinaryAndLightShopChecks.Scheduling()) throw new InvalidOperationException("新商店验证失败。");
        if (!CardFinalValueChecks.NumericFallbacks()) throw new InvalidOperationException("卡面数字验证失败。");
        var combat = SwordShieldCombat(); var counting = SetCountingAndPreview(); var filters = AuraFiltersAndLifetime();
        GD.Print($"光辉验证 combat={combat}, counting={counting}, filters={filters}");
        if (!combat || !counting || !filters) throw new InvalidOperationException("光辉套装行为验证失败。");
        var directory = "res://output/radiant-set-20261018"; DirAccess.MakeDirRecursiveAbsolute(directory);
        var registry = DefinitionRegistry.Scan(typeof(RadiantSetChecks).Assembly); var adapter = new CardDisplayAdapter();
        var viewport = new SubViewport { Size = new Vector2I(1910, 380), Disable3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport); viewport.AddChild(new ColorRect { Color = new Color("293139"), Size = new Vector2(1910, 380) });
        var preview = new CardPreviewView { Size = owner.Size, Theme = MatchTheme.Create() }; owner.AddChild(preview); preview.SetQuery(new CardPreviewQuery(registry));
        try
        {
            CardDefinition[] definitions = [new RadiantBeltCardDefinition(), new RadiantPlateArmorCardDefinition(), new RadiantSwordShieldCardDefinition(), new FallenAngelCardDefinition(), new ManaSourceFirelockCardDefinition(), new ArchbishopLuksaCardDefinition(), new BoarCardDefinition()];
            for (var index = 0; index < definitions.Length; index++)
            {
                var card = MatchDisplayQuery.FromOffer(ShopOffer.Create(definitions[index]));
                using var image = adapter.Artwork(card.Illustration).GetImage();
                if (!image.HasMipmaps() || image.GetWidth() != image.GetHeight()) throw new InvalidOperationException("光辉套装原画比例或mipmap错误。");
                GD.Print($"{card.DisplayName} {image.GetWidth()}×{image.GetHeight()} mipmaps={image.GetMipmapCount()}");
                var tile = new CardItemView { Size = new Vector2(200, 200), Position = new Vector2(20 + index * 270, 28), Theme = MatchTheme.Create() };
                viewport.AddChild(tile); tile.Render(card, adapter);
                var compact = new CardItemView { Size = new Vector2(90, 90), Position = new Vector2(20 + index * 270, 270), Theme = MatchTheme.Create() };
                viewport.AddChild(compact); compact.Render(card, adapter);
                viewport.AddChild(new Label { Text = card.DisplayName, Position = new Vector2(20 + index * 270, 237) });
                preview.Open(card, null, null); await Frame(); await Frame(); await ToDraw();
                using var detail = owner.GetViewport().GetTexture().GetImage();
                detail.SavePng($"{directory}/{card.Key.ToString().Replace('.', '_')}.png");
            }
            await Frame(); await ToDraw(); using var sheet = viewport.GetTexture().GetImage(); sheet.SavePng($"{directory}/set-cards.png");
            GD.Print("光辉套装行为、计数、标签联动、实际mipmap与卡面捕获通过。");
        }
        finally { owner.RemoveChild(preview); preview.Free(); owner.RemoveChild(viewport); viewport.Free(); }
        async Task Frame() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        async Task ToDraw() => await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
    }
}
