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

// 肩甲相邻多重强化、铁蒺藜敌方疾速回响及原画验证（表现层）。
internal static class PauldronCaltropsChecks
{
    internal static bool Pauldron()
    {
        var definition = new PauldronCardDefinition();
        if (!Identity(definition, "肩甲", GameTags.Equipment)) return Fail();
        for (var level = 1; level <= 4; level++)
        foreach (var reverse in new[] { false, true })
        foreach (var slots in new[] { 1, 2, 3 })
        {
            var amount = 5 * (1 << (level - 1));
            var source = Source(definition, level, 3);
            if (source.Multicast != 1 || source.CooldownTicks != 60 || source.Abilities!.Single().ManaCost != 0) return Fail();
            var left = Neighbor(3 - slots, slots, GameTags.Human);
            var right = Neighbor(4, 1, GameTags.Equipment);
            var far = Neighbor(6, 1, GameTags.Human, GameTags.Equipment);
            var bench = Neighbor(0, 1, GameTags.Human, GameTags.Equipment) with { IsOnBench = true };
            var enemy = Neighbor(0, 1, GameTags.Human, GameTags.Equipment);
            var setup = Battle([source, left, right, far, bench], [enemy], reverse, 130);
            var result = new CombatSimulator().Simulate(setup);
            var changes = result.Events.OfType<CardAttributeChangedEvent>().ToArray();
            if (changes.Length != 8 || changes.Any(e => e.Amount != amount
                    || e.CardId == left.EntityId && e.AttributeKey != GameAttributeKeys.AttackDamage
                    || e.CardId == right.EntityId && e.AttributeKey != GameAttributeKeys.Armor
                    || e.CardId != left.EntityId && e.CardId != right.EntityId)
                || !changes.Where(e => e.CardId == left.EntityId).Select(e => e.Tick.Value).SequenceEqual(new long[] { 60, 62, 120, 122 })
                || !changes.Where(e => e.CardId == right.EntityId).Select(e => e.Tick.Value).SequenceEqual(new long[] { 60, 62, 120, 122 })
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                || result.PermanentAttributeBonuses.Any()) return Fail();
            var hits = result.Events.OfType<DamageDealtEvent>().Where(e => e.SourceCardId == left.EntityId).ToArray();
            if (hits.Single().RawDamage != 20 + 2 * amount) return Fail();
            var dual = left with { Tags = new TagSet([GameTags.Human, GameTags.Equipment]) };
            var both = new CombatSimulator().Simulate(Battle([source, dual], [], reverse, 64));
            if (both.Events.OfType<CardAttributeChangedEvent>().Count(e => e.CardId == dual.EntityId) != 4) return Fail();
            foreach (var excluded in new[] { left with { BoardStart = left.BoardStart - 1 }, left with { IsOnBench = true },
                left with { Tags = new TagSet([GameTags.Material]) }, left with { Abilities = [Destroy()] } })
                if (new CombatSimulator().Simulate(Battle([source, excluded], [], reverse, 64))
                    .Events.OfType<CardAttributeChangedEvent>().Any()) return Fail();
            if (new CombatSimulator().Simulate(Battle([source with { IsOnBench = true }, left], [], reverse, 64))
                .Events.OfType<CardAttributeChangedEvent>().Any()) return Fail();
        }
        return true;
    }

    internal static bool Caltrops()
    {
        var definition = new CaltropsCardDefinition();
        if (!Identity(definition, "铁蒺藜", GameTags.Trap) || TagDisplayNames.Get(GameTags.Trap) != "陷阱") return Fail();
        for (var level = 1; level <= 4; level++)
        foreach (var reverse in new[] { false, true })
        {
            var source = Source(definition, level, 0);
            if (source.Abilities!.Any(a => a.Activation == AbilityActivation.Active)) return Fail();
            var first = Haste(0); var second = Haste(2);
            var own = Haste(2);
            var setup = Battle([source, own, Receiver(3)], [first, Receiver(1), second, Receiver(3)], reverse, 65);
            var result = new CombatSimulator().Simulate(setup);
            var hits = result.Events.OfType<DamageDealtEvent>().Where(e => e.SourceCardId == source.EntityId).ToArray();
            if (hits.Length != 12 || hits.Any(e => e.RawDamage != 10 * (1 << (level - 1))
                    || e.TargetSide != (reverse ? SideId.Player : SideId.Opponent))
                || !hits.Select(e => e.Tick.Value).SequenceEqual(new long[] { 20, 20, 22, 22, 40, 40, 42, 42, 60, 60, 62, 62 })
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return Fail();
            foreach (var excluded in new[] { first with { IsOnBench = true },
                first with { Abilities = [Status(BattleStatus.SlowDuration, 10)] },
                first with { Abilities = [Status(BattleStatus.HasteDuration, 0)] },
                first with { Abilities = [Destroy(), Status(BattleStatus.HasteDuration, 10)] } })
                if (new CombatSimulator().Simulate(Battle([source, own, Receiver(3)], [excluded, Receiver(1)], reverse, 65))
                    .Events.OfType<DamageDealtEvent>().Any(e => e.SourceCardId == source.EntityId)) return Fail();
            foreach (var inactive in new[] { source with { IsOnBench = true }, source with { Abilities = [.. source.Abilities!, Destroy()] } })
                if (new CombatSimulator().Simulate(Battle([inactive], [first, Receiver(1)], reverse, 65))
                    .Events.OfType<DamageDealtEvent>().Any(e => e.SourceCardId == source.EntityId)) return Fail();
            var alliedApplier = Receiver(2) with { Abilities = [new AbilityDefinition(new StringName("ability.verification_enemy_haste"),
                AbilityActivation.Active, AbilityTarget.SelfCard, 0, 20,
                [new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.HasteDuration, 10)])] };
            var granted = new CombatSimulator().Simulate(Battle([source, alliedApplier], [Receiver(0)], reverse, 65));
            if (granted.Events.OfType<DamageDealtEvent>().Count(e => e.SourceCardId == source.EntityId) != 3) return Fail();
        }
        return true;
    }

    internal static bool Portcullis()
    {
        var definition = new SpikedPortcullisCardDefinition(); var identity = definition.Attributes.Identity;
        using var image = GD.Load<Texture2D>(identity.Illustration.ToString()).GetImage();
        if (identity.DisplayName != "尖桩吊闸" || identity.Size != CardSize.Medium || definition.InitialLevel != 2
            || identity.FactionKey != new StringName("paladin") || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.SupportsLevel(1) || definition.SupportsLevel(5) || definition.Tags.Count != 3
            || !definition.Tags.Contains(GameTags.Trap) || !definition.Tags.Contains(GameTags.Building)
            || image.GetWidth() != image.GetHeight() || !image.HasMipmaps()) return Fail();
        foreach (var level in new[] { 2, 3, 4 })
        foreach (var reverse in new[] { false, true })
        {
            var source = Source(definition, level, 0) with { OccupiedSlots = 2 };
            if (source.CooldownTicks != 100 || source.Abilities!.Single(a => a.Activation == AbilityActivation.Active).ManaCost != 0) return Fail();
            var damage = 120 * (1 << (level - 2));
            var idle = new CombatSimulator().Simulate(Battle([source], [], reverse, 205));
            if (!idle.Events.OfType<DamageDealtEvent>().Where(e => e.SourceCardId == source.EntityId)
                .Select(e => (e.Tick.Value, e.RawDamage)).SequenceEqual(new[] { (100L, damage), (200L, damage) })) return Fail();
            var human = Receiver(2) with { Tags = new TagSet([GameTags.Human]), Abilities = [new AbilityDefinition(
                new StringName("ability.verification_human"), AbilityActivation.Active, AbilityTarget.SelfCard, 0, 20,
                [new GainSourceHeroArmorEffectDefinition(1)])] };
            foreach (var enemyHuman in new[] { false, true })
            foreach (var multicast in new[] { 0, 1 })
            {
                var helper = human with { Multicast = multicast };
                var setup = Battle(enemyHuman ? [source] : [source, helper], enemyHuman ? [helper] : [], reverse, 105);
                var result = new CombatSimulator().Simulate(setup);
                var hits = result.Events.OfType<DamageDealtEvent>().Where(e => e.SourceCardId == source.EntityId).ToArray();
                if (hits[0].Tick.Value != (multicast == 0 ? 70 : 60) || hits.Any(e => e.RawDamage != damage)
                    || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return Fail();
            }
            var both = new CombatSimulator().Simulate(Battle([source, human], [human with { EntityId = EntityId.New() }], reverse, 105));
            if (both.Events.OfType<DamageDealtEvent>().First(e => e.SourceCardId == source.EntityId).Tick.Value != 60) return Fail();
            foreach (var excluded in new[] { human with { IsOnBench = true }, human with { Tags = new TagSet([GameTags.Beast]) },
                human with { Abilities = [Destroy()] } })
            {
                var result = new CombatSimulator().Simulate(Battle([source], [excluded], reverse, 105));
                if (result.Events.OfType<DamageDealtEvent>().Single(e => e.SourceCardId == source.EntityId).Tick.Value != 100) return Fail();
            }
        }
        return true;
    }

    private static bool Identity(CardDefinition definition, string name, StringName tag)
    {
        var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString()); using var image = texture?.GetImage();
        return identity.DisplayName == name && identity.FactionKey == new StringName("paladin") && identity.Size == CardSize.Small
            && identity.ElementKeys.SequenceEqual(new[] { GameElements.General }) && definition.InitialLevel == 1
            && !definition.SupportsLevel(0) && !definition.SupportsLevel(5) && definition.Tags.Count == 2
            && definition.Tags.Contains(tag) && definition.Tags.Contains(GameTags.Small)
            && DefinitionRegistry.Scan(typeof(PauldronCaltropsChecks).Assembly).Cards.ContainsKey(identity.Key)
            && image is not null && image.GetHeight() == image.GetWidth() * 2 && image.HasMipmaps();
    }

    private static CardBattleSetup Source(CardDefinition definition, int level, int start)
    {
        var card = new EntityFactory().CreateCard(definition, level);
        var values = card.Attributes.BaseCombat.SnapshotFinalValues();
        return new CardBattleSetup(card.Id, start, card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
            card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks), card.Abilities,
            UseLegacyAttack: false, Tags: card.Tags, Multicast: card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Multicast))
            { Level = level, Identity = card.Attributes.Identity, CombatValues = values };
    }

    private static CardBattleSetup Neighbor(int start, int slots, params StringName[] tags) => new(EntityId.New(), start, 20, 80,
        [new AbilityDefinition(new StringName("ability.verification_attack"), AbilityActivation.Active, AbilityTarget.EnemyHero,
            0, 80, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)])], UseLegacyAttack: false,
        Tags: new TagSet(tags), OccupiedSlots: slots);

    private static CardBattleSetup Haste(int start) => new(EntityId.New(), start, 0, 20,
        [Status(BattleStatus.HasteDuration, 1)], UseLegacyAttack: false, Multicast: 1);

    private static CardBattleSetup Receiver(int start) => new(EntityId.New(), start, 0, 0, [], UseLegacyAttack: false);

    private static AbilityDefinition Status(BattleStatus status, int amount) => new(new StringName("ability.verification_status"),
        AbilityActivation.Active, AbilityTarget.RightAdjacentAlliedCard, 0, 20, [new ApplyStatusEffectDefinition(status, amount)]);

    private static AbilityDefinition Destroy() => new(new StringName("ability.verification_destroy"),
        AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)]);

    private static BattleSetup Battle(CardBattleSetup[] allies, CardBattleSetup[] enemies, bool reverse, int ticks)
    {
        var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), allies);
        var other = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), enemies);
        return new BattleSetup(reverse ? other : own, reverse ? own : other, 42, new BattleTick(ticks), new BattleTick(ticks + 1));
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(1120, 900), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(1120, 900) });
            var definitions = new CardDefinition[] { new PauldronCardDefinition(), new CaltropsCardDefinition() };
            for (var index = 0; index < 2; index++)
            {
                using var image = GD.Load<Texture2D>(definitions[index].Attributes.Identity.Illustration.ToString()).GetImage();
                GD.Print($"{definitions[index].Attributes.Identity.DisplayName} {image.GetWidth()}×{image.GetHeight()} mipmaps={image.GetMipmapCount()}");
                foreach (var (level, x, width) in new[] { (1, 10, 200), (4, 230, 200), (1, 450, 100) })
                {
                    var face = new CardItemView { Position = new Vector2(index * 560 + x, 20), Size = new Vector2(width, width * 2) };
                    viewport.AddChild(face);
                    face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(definitions[index], level)), new CardDisplayAdapter());
                }
            }
            var mediumDefinitions = new CardDefinition[] { new SpikedPortcullisCardDefinition(), new EquipmentShopCardDefinition() };
            for (var index = 0; index < 2; index++)
            {
                using var image = GD.Load<Texture2D>(mediumDefinitions[index].Attributes.Identity.Illustration.ToString()).GetImage();
                GD.Print($"{mediumDefinitions[index].Attributes.Identity.DisplayName} {image.GetWidth()}×{image.GetHeight()} mipmaps={image.GetMipmapCount()}");
                foreach (var (level, x, width) in new[] { (2, 10, 340), (4, 370, 170) })
                {
                    var face = new CardItemView { Position = new Vector2(index * 560 + x, 470), Size = new Vector2(width, width) };
                    viewport.AddChild(face);
                    face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(mediumDefinitions[index], level)), new CardDisplayAdapter());
                }
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw(false);
            using var capture = viewport.GetTexture().GetImage();
            if (capture.SavePng("res://output/pauldron-caltrops/card-faces.png") != Error.Ok)
                throw new InvalidOperationException("肩甲与铁蒺藜卡面保存失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static bool Fail([System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    { GD.PushError("肩甲／铁蒺藜验证失败，行 " + line); return false; }
}
