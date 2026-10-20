using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Content;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Skills;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 玩具熊半血献祭召唤、空位与暴怒熊状态倍率验证（表现层）。
internal static class TeddyBearChecks
{
    internal static bool LevelsAndCombat()
    {
        var registry = DefinitionRegistry.Scan(typeof(TeddyBearChecks).Assembly);
        foreach (var definition in new CardDefinition[] { new TeddyBearCardDefinition(), new RagingBearCardDefinition() })
        {
            var identity = definition.Attributes.Identity;
            var large = definition is RagingBearCardDefinition;
            using var image = GD.Load<Texture2D>(identity.Illustration.ToString()).GetImage();
            if (!registry.Cards.ContainsKey(identity.Key) || identity.FactionKey != new StringName("mona")
                || identity.Size != (large ? CardSize.Large : CardSize.Small)
                || !identity.ElementKeys.SequenceEqual(new[] { large ? GameElements.Fire : GameElements.General })
                || definition.InitialLevel != 2 || definition.SupportsLevel(1) || definition.SupportsLevel(5)
                || definition.Tags.Count != (large ? 2 : 1) || large && !definition.Tags.Contains(GameTags.Beast)
                || !image.HasMipmaps() || (large ? image.GetWidth() * 2 != image.GetHeight() * 3 : image.GetWidth() * 2 != image.GetHeight())) return Fail();
            GD.Print($"{identity.DisplayName} {image.GetWidth()}×{image.GetHeight()} mipmaps={image.GetMipmapCount()}");
        }
        foreach (var reverse in new[] { false, true })
        for (var level = 2; level <= 4; level++)
        {
            var toy = Card(new TeddyBearCardDefinition(), level, 4);
            var damage = 20 << (level - 2); var bearDamage = damage * 4; var burn = 8 << (level - 2);
            var idle = Run([toy], [], 80, reverse);
            var toyHits = idle.Events.OfType<DamageDealtEvent>().Where(e => e.SourceCardId == toy.EntityId).ToArray();
            if (!toyHits.Select(e => (e.Tick.Value, e.RawDamage)).SequenceEqual(new[] { (40L, damage), (80L, damage) })
                || toy.Abilities!.Any(a => a.ManaCost != 0)) return Fail();
            // 精确半血不触发；跨过阈值立即回响，不等待下次4秒发动。
            var left = Idle(2, 2); var right = Idle(5, 3); var bench = Idle(2, 2) with { IsOnBench = true };
            var enemy = Striker(5001, 41);
            var setup = Setup([toy with { Multicast = 2 }, left, right, bench], [enemy], 103, reverse);
            var result = new CombatSimulator().Simulate(setup);
            var destroyed = result.Events.OfType<CardDestroyedEvent>().ToArray();
            var summon = result.Events.OfType<CardSummonedEvent>().SingleOrDefault();
            if (destroyed.Length != 3 || !destroyed.Select(e => e.CardId).ToHashSet().SetEquals(new[] { toy.EntityId, left.EntityId, right.EntityId })
                || destroyed.Any(e => e.Tick.Value != 41) || summon is null || summon.Tick.Value != 41
                || summon.Level != level || summon.BoardStart != 1 || summon.CardKey != new StringName("card.raging_bear")
                || !result.Events.OfType<AbilityActivatedEvent>().Any(e => e.SourceCardId == toy.EntityId && e.IsEcho && e.Tick.Value == 41)
                || result.Events.OfType<DamageDealtEvent>().Any(e => e.SourceCardId == toy.EntityId && e.Tick.Value > 40)
                || result.States.Last().Cards.Single(c => c.Id == bench.EntityId).Destroyed
                || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return Fail();
            var summoned = result.States.Last().Cards.Single(c => c.Id == summon.SummonedCardId);
            var bearHit = result.Events.OfType<DamageDealtEvent>().SingleOrDefault(e => e.SourceCardId == summon.SummonedCardId);
            var bearBurn = result.Events.OfType<StatusChangedEvent>().Where(e => e.Status == BattleStatus.Burn && e.Amount > 0).ToArray();
            if (!summoned.IsBerserk || summoned.SummonedCard?.Identity.Size != CardSize.Large
                || bearHit is null || bearHit.Tick.Value != 101 || bearHit.RawDamage != bearDamage * 24 / 10
                || bearBurn.Length != 1 || bearBurn[0].Amount != burn * 24 / 10
                || !result.Events.OfType<CardStateChangedEvent>().Any(e => e.CardId == summon.SummonedCardId
                    && e.StateKey == GameAttributeKeys.Berserk && e.Enabled)) return Fail();
            if (Run([toy, left, right], [Striker(5000, 41)], 41, reverse).Events.OfType<CardDestroyedEvent>().Any()
                || Run([toy with { IsOnBench = true }], [enemy], 60, reverse).Events.OfType<CardSummonedEvent>().Any()
                || Run([toy], [Striker(10000, 41)], 41, reverse).Events.OfType<CardSummonedEvent>().Any()) return Fail();
            // 独立暴怒熊默认不狂暴；开战设置、重复设置与清除均可动态求值，含额外加成。
            foreach (var berserk in new[] { false, true })
            {
                var bear = Card(new RagingBearCardDefinition(), level, 0);
                var values = new Dictionary<StringName, int>(bear.CombatValues)
                { [GameAttributeKeys.AttackDamage] = bearDamage + 3, [GameAttributeKeys.BurnBonus] = 3 };
                bear = bear with { AttackDamage = bearDamage + 3, CombatValues = values, Multicast = 1, Abilities = bear.Abilities!.Append(
                    new AbilityDefinition("ability.verification.bear_state", AbilityActivation.PassiveOnBattleStart,
                        AbilityTarget.SelfCard, 0, 0,
                        [new SetSourceCardStateEffectDefinition(GameAttributeKeys.Berserk, true),
                            new SetSourceCardStateEffectDefinition(GameAttributeKeys.Berserk, true),
                            new SetSourceCardStateEffectDefinition(GameAttributeKeys.Berserk, berserk)])).ToArray() };
                var actual = Run([bear], [], 62, reverse);
                var expectedDamage = berserk ? (bearDamage + 3) * 24 / 10 : bearDamage + 3;
                var expectedBurn = berserk ? (burn + 3) * 24 / 10 : burn + 3;
                var hits = actual.Events.OfType<DamageDealtEvent>().Where(e => e.SourceCardId == bear.EntityId).ToArray();
                var statuses = actual.Events.OfType<StatusChangedEvent>().Where(e => e.Status == BattleStatus.Burn && e.Amount > 0).ToArray();
                if (hits.Length != 2 || hits.Any(e => e.RawDamage != expectedDamage)
                    || statuses.Length != 2 || statuses.Any(e => e.Amount != expectedBurn)
                    || !actual.States.Last().Cards.Single(c => c.Id == bear.EntityId).EffectAmounts.Take(2)
                        .SequenceEqual(new int?[] { expectedDamage, expectedBurn })) return Fail();
            }
        }
        return PlacementAndEchoes() && CardDescriptionChecks.AllLevels();
    }

    private static bool PlacementAndEchoes()
    {
        foreach (var reverse in new[] { false, true })
        {
            var toy = Card(new TeddyBearCardDefinition(), 2, 4);
            var hit = Striker(5001, 41);
            var leftBlock = Idle(1, 1);
            var rightOnly = Run([toy, leftBlock, Idle(3, 1), Idle(5, 1)], [hit], 42, reverse);
            if (rightOnly.Events.OfType<CardSummonedEvent>().SingleOrDefault()?.BoardStart != 5
                || rightOnly.States.Last().Cards.Single(c => c.Id == leftBlock.EntityId).Destroyed) return Fail();
            var failed = Run([toy, leftBlock, Idle(3, 1), Idle(5, 1), Idle(7, 1)], [hit], 42, reverse);
            if (failed.Events.OfType<CardSummonedEvent>().Any() || failed.Events.OfType<CardDestroyedEvent>().Count() != 3
                || failed.Events.OfType<CardStateChangedEvent>().Any()) return Fail();
            var gap = Idle(2, 1);
            var gapped = Run([toy, gap], [hit], 42, reverse);
            if (gapped.States.Last().Cards.Single(c => c.Id == gap.EntityId).Destroyed
                || gapped.Events.OfType<CardSummonedEvent>().SingleOrDefault()?.BoardStart != 5) return Fail();
            foreach (var (start, expected) in new[] { (0, 1), (9, 6) })
            {
                var edge = Run([toy with { BoardStart = start }], [hit], 42, reverse);
                if (edge.Events.OfType<CardSummonedEvent>().SingleOrDefault()?.BoardStart != expected) return Fail();
            }
            var neighbor = Idle(3, 1) with { Abilities = [new AbilityDefinition("ability.verification.bear_deathrattle",
                AbilityActivation.EchoOnSourceDestroyed, AbilityTarget.SelfCard, 0, 0, [new GainSourceHeroArmorEffectDefinition(7)])] };
            var skill = new RebirthRitualSkillDefinition();
            var skillInstance = new EntityFactory().CreateSkill(skill, 4);
            var rebirth = new SkillBattleSetup(skillInstance.Id, skillInstance.Abilities, skillInstance.Attributes.BaseCombat.SnapshotFinalValues());
            var deathSetup = Setup([toy, neighbor], [hit], 42, reverse, [rebirth]);
            var death = new CombatSimulator().Simulate(deathSetup);
            if (death.Events.OfType<CardSummonedEvent>().Count() != 1
                || (reverse ? death.States.Last().Opponent : death.States.Last().Player).Armor != 7) return Fail();
        }
        return true;
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(1280, 520), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        var preview = new CardPreviewView { Size = owner.GetViewport().GetVisibleRect().Size, Theme = MatchTheme.Create() };
        owner.AddChild(preview);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(1280, 520) });
            CardDefinition[] definitions = [new TeddyBearCardDefinition(), new RagingBearCardDefinition()];
            for (var index = 0; index < definitions.Length; index++)
            foreach (var compact in new[] { false, true })
            {
                var large = index == 1;
                var face = new CardItemView
                {
                    Position = large ? new Vector2(360, compact ? 355 : 20) : new Vector2(compact ? 20 : 130, 20),
                    Size = large ? (compact ? new Vector2(150, 100) : new Vector2(450, 300))
                        : (compact ? new Vector2(100, 200) : new Vector2(200, 400)),
                };
                viewport.AddChild(face);
                face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(definitions[index])), new CardDisplayAdapter());
            }
            var label = new RichTextLabel { Position = new Vector2(850, 20), Size = new Vector2(410, 480) };
            viewport.AddChild(label);
            label.Text = string.Join("\n\n", definitions.Select(definition => definition.Attributes.Identity.DisplayName + "\n"
                + string.Join("\n", CardDisplayAdapter.Rules(MatchDisplayQuery.FromOffer(ShopOffer.Create(definition))))));
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw(false);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/teddy-bear/card-face.png") != Error.Ok) throw new InvalidOperationException("玩具熊卡面捕获失败。");
            var registry = DefinitionRegistry.Scan(typeof(TeddyBearChecks).Assembly);
            preview.SetQuery(new CardPreviewQuery(registry));
            preview.SetEntries(CardCatalogQuery.Capture(registry));
            foreach (var definition in definitions)
            {
                preview.Open(MatchDisplayQuery.FromOffer(ShopOffer.Create(definition)), null, null);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                RenderingServer.ForceDraw(false);
                using var detail = owner.GetViewport().GetTexture().GetImage();
                var name = definition is TeddyBearCardDefinition ? "teddy-preview" : "raging-preview";
                if (detail.SavePng($"res://output/teddy-bear/{name}.png") != Error.Ok) throw new InvalidOperationException("熊卡牌详情捕获失败。");
            }
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); owner.RemoveChild(preview); preview.Free(); }
    }

    private static CardBattleSetup Card(CardDefinition definition, int level, int start)
    {
        var card = new EntityFactory().CreateCard(definition, level);
        var identity = card.Attributes.Identity;
        var values = card.Attributes.BaseCombat.SnapshotFinalValues();
        return new CardBattleSetup(card.Id, start, values[GameAttributeKeys.AttackDamage], values[GameAttributeKeys.CooldownTicks],
            card.Abilities, UseLegacyAttack: false, Tags: card.Tags, OccupiedSlots: identity.OccupiedSlots, ElementKeys: identity.ElementKeys)
        { Identity = identity, Level = level, CombatValues = values };
    }

    private static CardBattleSetup Idle(int start, int size) => new(EntityId.New(), start, 0, 0, [], UseLegacyAttack: false, OccupiedSlots: size);
    private static CardBattleSetup Striker(int damage, int cooldown) => new(EntityId.New(), 0, damage, cooldown,
        [new AbilityDefinition("ability.verification.toy_hit", AbilityActivation.Active, AbilityTarget.EnemyHero, 0, cooldown,
            [new DamageEffectDefinition(damage), new IncreaseSourceCooldownEffectDefinition(10000)])], UseLegacyAttack: false);
    private static BattleResult Run(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks, bool reverse) =>
        new CombatSimulator().Simulate(Setup(allies, enemies, ticks, reverse));
    private static BattleSetup Setup(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks, bool reverse, SkillBattleSetup[]? skills = null)
    {
        var ally = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), allies, skills);
        var enemy = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 100000, 0, ManaRegen: 0), enemies);
        return new BattleSetup(reverse ? enemy : ally, reverse ? ally : enemy, 42, new BattleTick(ticks), new BattleTick(ticks + 1));
    }
    private static bool Fail([System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    {
        GD.PushError("TeddyBearChecks 验证失败，行 " + line);
        return false;
    }
}
