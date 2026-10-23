using System.Threading.Tasks;
using Project_Star.Presentation.Game;
using System;
using System.Collections.Generic;
using System.Linq;
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

namespace Project_Star.Presentation.Verification;

// 元素法典分级、双目标结算、动态去重多重与魔法门控验证（表现层）。
internal static class ElementalCodexChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new ElementalCodexCardDefinition(); var identity = definition.Attributes.Identity;
        if (identity.DisplayName != "元素法典" || identity.FactionKey != new StringName("mona")
            || identity.Size != CardSize.Medium || definition.InitialLevel != 2
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !definition.Tags.ToArray().ToHashSet().SetEquals(new[] { GameTags.Medium, GameTags.Equipment })
            || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(ElementalCodexChecks).Assembly).Cards.ContainsKey(identity.Key)) return Fail();
        foreach (var level in new[] { 2, 3, 4 })
        foreach (var reverse in new[] { false, true })
        foreach (var variant in new[] { 0, 1, 2, 3 })
        foreach (var mana in new[] { 59, 120 })
        {
            var card = new EntityFactory().CreateCard(definition, level); var amount = 40 << (level - 2);
            var values = card.Attributes.BaseCombat.SnapshotFinalValues().ToDictionary(p => p.Key, p => p.Value);
            values[GameAttributeKeys.AttackDamage] += 5; values[GameAttributeKeys.HealingBonus] = 7;
            var source = new CardBattleSetup(card.Id, 0, amount + 5, 70, card.Abilities, UseLegacyAttack: false,
                Tags: card.Tags, OccupiedSlots: 2, ElementKeys: identity.ElementKeys) { Level = level, CombatValues = values };
            var supporters = variant == 0 ? Array.Empty<CardBattleSetup>() : variant == 2
                ? GameElements.All.Chunk(2).Select((elements, i) => Peer(i + 2, elements)).ToArray()
                : new[] { Peer(2, [GameElements.Fire, GameElements.Water]), Peer(3, [GameElements.Fire, GameElements.Water]) };
            if (variant == 3) supporters = [supporters[0] with { Abilities = [Destroy(71)] }];
            var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, MaxMana: 120,
                InitialMana: mana, ManaRegen: 0),
                [source, .. supporters, Peer(7, [GameElements.Wind]) with { IsOnBench = true },
                    Peer(8, [GameElements.Dark]) with { Abilities = [Destroy(0)] }]);
            var attack = new AbilityDefinition(new StringName("verification.codex_opening"), AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.EnemyHero, 0, 0, [new DamageEffectDefinition(5000)]);
            var enemy = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0),
                [Peer(0, [GameElements.Light, GameElements.Wood]) with { Abilities = [attack] }]);
            var setup = new BattleSetup(reverse ? enemy : own, reverse ? own : enemy, 42, new BattleTick(165), new BattleTick(166));
            var result = new CombatSimulator().Simulate(setup);
            var first = variant == 0 ? 1 : variant == 2 ? GameElements.All.Count : 3;
            var second = variant == 3 ? 1 : first;
            var expectedTicks = mana < 60 ? Array.Empty<long>() : Enumerable.Range(0, first + 1).Select(i => 70L + i * 2)
                .Concat(Enumerable.Range(0, second + 1).Select(i => 140L + i * 2)).ToArray();
            var frame = result.States.Last(s => s.Tick.Value == 164);
            var hero = reverse ? frame.Opponent : frame.Player; var other = reverse ? frame.Player : frame.Opponent;
            var damages = result.Events.OfType<DamageDealtEvent>().Where(e => e.SourceCardId == card.Id).ToArray();
            if (!damages.Select(e => e.Tick.Value).SequenceEqual(expectedTicks)
                || hero.Health != 5000 + (amount + 7) * expectedTicks.Length
                || other.Health != 10000 - (amount + 5) * expectedTicks.Length
                || hero.Mana != (mana < 60 ? mana : 0)
                || frame.Cards.Single(c => c.Id == card.Id).Values[GameAttributeKeys.Multicast] != second
                || result.Events.OfType<ManaChangedEvent>().Count(e => e.Amount == -60) != (mana < 60 ? 0 : 2)
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any())
            {
                GD.Print($"level={level},reverse={reverse},variant={variant},mana={mana},ticks={string.Join(',', damages.Select(e => e.Tick.Value))},health={hero.Health},enemy={other.Health},manaLeft={hero.Mana},multicast={frame.Cards.Single(c => c.Id == card.Id).Values[GameAttributeKeys.Multicast]}");
                return Fail();
            }
            var offer = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            var frozenCard = frame.Cards.Single(c => c.Id == card.Id);
            var frozenFace = new CardDisplayAdapter().Build(offer with
                { CurrentValues = frozenCard.Values, EffectAmounts = frozenCard.EffectAmounts }).Effects;
            if (!frozenFace.Contains(new CardFaceEffect(CardFaceEffectKind.Damage, (amount + 5).ToString()))
                || !frozenFace.Contains(new CardFaceEffect(CardFaceEffectKind.Healing, (amount + 7).ToString()))
                || !frozenFace.Contains(new CardFaceEffect(CardFaceEffectKind.Multicast, second.ToString()))) return Fail();
            var face = CardDisplayAdapter.FaceEffects(offer);
            if (!face.Contains(new CardFaceEffect(CardFaceEffectKind.Damage, amount.ToString()))
                || !face.Contains(new CardFaceEffect(CardFaceEffectKind.Healing, amount.ToString()))
                || CardDisplayAdapter.Rules(offer).Count != 2
                || !CardDisplayAdapter.Rules(offer).Any(rule => rule.Contains("不同元素种类数"))
                || !new CardDisplayAdapter().Build(offer).Effects.Any(e => e.Kind == CardFaceEffectKind.ManaCost && e.Value == "−60"))
            { GD.Print($"face={string.Join(';', face)},rules={string.Join(';', CardDisplayAdapter.Rules(offer))}"); return Fail(); }
        }
        return true;
    }

    private static CardBattleSetup Peer(int start, StringName[] elements) => new(EntityId.New(), start, 0, 0, [],
        UseLegacyAttack: false, ElementKeys: elements);
    private static AbilityDefinition Destroy(int cooldown) => new(new StringName("verification.codex_destroy"),
        cooldown == 0 ? AbilityActivation.PassiveOnBattleStart : AbilityActivation.Active, AbilityTarget.SelfCard,
        0, cooldown, [new DestroyCardEffectDefinition(false)]);
    private static bool Fail([System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    { GD.Print($"元素法典验证失败，行{line}"); return false; }
    internal static async Task Capture(Node owner)
    {
        if (!LevelsAndCombat()) throw new InvalidOperationException("元素法典战斗验证失败。");
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new ElementalCodexCardDefinition()));
        var adapter = new CardDisplayAdapter();
        using var raw = adapter.Artwork(snapshot.Illustration).GetImage();
        if (!raw.HasMipmaps() || raw.GetWidth() != raw.GetHeight())
            throw new InvalidOperationException("元素法典原画比例或mipmap不合格。");
        GD.Print($"元素法典原画：{raw.GetWidth()}×{raw.GetHeight()}，mipmap层数{raw.GetMipmapCount()}");
        var viewport = new SubViewport { Size = new Vector2I(420, 460), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        var preview = new CardPreviewView { Size = owner.GetViewport().GetVisibleRect().Size, Theme = MatchTheme.Create() };
        owner.AddChild(preview);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(420, 460) });
            foreach (var (position, size) in new[] { (new Vector2(24, 24), new Vector2(200, 200)),
                (new Vector2(260, 24), new Vector2(100, 100)) })
            {
                var face = new CardItemView { Position = position, Size = size };
                viewport.AddChild(face); face.Render(snapshot, adapter);
            }
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(ElementalCodexChecks).Assembly)));
            preview.Open(snapshot, null, null);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            using var detail = owner.GetViewport().GetTexture().GetImage();
            if (image.SavePng("res://output/elemental-codex/card-face.png") != Error.Ok
                || detail.SavePng("res://output/elemental-codex/preview.png") != Error.Ok)
                throw new InvalidOperationException("元素法典卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); owner.RemoveChild(preview); preview.Free(); }
    }
}
