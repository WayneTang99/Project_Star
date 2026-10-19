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
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 苍穹塔身份、独立回响、本场成长与原生原画验证（表现层）。
internal static class SkyvaultTowerChecks
{
    internal static bool IdentityAndCombat()
    {
        var definition = new SkyvaultTowerCardDefinition(); var identity = definition.Attributes.Identity;
        if (identity.DisplayName != "苍穹塔" || identity.FactionKey != new StringName("paladin")
            || identity.Size != CardSize.Large || definition.InitialLevel != 4
            || Enumerable.Range(1, 5).Any(level => definition.SupportsLevel(level) != (level == 4))
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !definition.Tags.ToArray().ToHashSet().SetEquals(new[] { GameTags.Large, GameTags.Building, GameTags.Location })
            || !DefinitionRegistry.Scan(typeof(SkyvaultTowerChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        var card = new EntityFactory().CreateCard(definition);
        var tower = new CardBattleSetup(card.Id, 0, 0, 70, card.Abilities, UseLegacyAttack: false,
            Tags: card.Tags, OccupiedSlots: 3, ElementKeys: identity.ElementKeys, ArmorAmount: 100)
            { CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues(), Level = 4 };
        var active = new CardBattleSetup(EntityId.New(), 3, 0, 60,
            [new AbilityDefinition(new StringName("verification.tower_trigger"), AbilityActivation.Active,
                AbilityTarget.SelfCard, 0, 60, [new GainSourceHeroArmorEffectDefinition(1)])],
            UseLegacyAttack: false, ElementKeys: [GameElements.General]);
        foreach (var reverse in new[] { false, true })
        {
            if (!Check(tower, [], [], reverse, 70, 100, 0)
                || !Check(tower, [active with { Tags = new TagSet([GameTags.Human]) }], [], reverse, 60, 100, 1)
                || !Check(tower, [active with { ElementKeys = [GameElements.Light] }], [], reverse, 70, 120, 0)
                || !Check(tower, [active with { Tags = new TagSet([GameTags.Human]), ElementKeys = [GameElements.Light] }], [], reverse, 60, 120, 1)
                || !Check(tower, [active with { ElementKeys = [GameElements.Light], Multicast = 1 }], [], reverse, 70, 140, 0)) return false;
            var matching = active with { Tags = new TagSet([GameTags.Human]), ElementKeys = [GameElements.Light] };
            foreach (var excluded in new[] { active, matching with { IsOnBench = true }, matching with
                { Abilities = [new AbilityDefinition(new StringName("verification.unpaid_tower_trigger"),
                    AbilityActivation.Active, AbilityTarget.SelfCard, 1, 60, [new GainSourceHeroArmorEffectDefinition(1)])] } })
                if (!Check(tower, [excluded], [], reverse, 70, 100, 0)) return false;
            if (!Check(tower, [], [matching], reverse, 70, 100, 0)
                || !Check(tower with { IsOnBench = true }, [matching], [], reverse, 0, 100, 0)) return false;
        }
        return card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Armor) == 100
            && card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.HealingBonus) == 100;
    }

    private static bool Check(CardBattleSetup tower, CardBattleSetup[] allies, CardBattleSetup[] enemies,
        bool reverse, int firstTick, int amount, int charges)
    {
        var attacker = new CardBattleSetup(EntityId.New(), 9, 0, 0,
            [new AbilityDefinition(new StringName("verification.tower_opening_damage"), AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.EnemyHero, 0, 0, [new DamageEffectDefinition(1000, true)])], UseLegacyAttack: false);
        var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), [tower, .. allies]);
        var other = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), [attacker, .. enemies]);
        var setup = new BattleSetup(reverse ? other : own, reverse ? own : other, 42, new BattleTick(81), new BattleTick(82));
        var result = new CombatSimulator().Simulate(setup);
        var activations = result.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId == tower.EntityId && !e.IsEcho).ToArray();
        var state = result.States.Last(s => s.Tick.Value <= 80);
        var hero = reverse ? state.Opponent : state.Player;
        var values = state.Cards.Single(c => c.Id == tower.EntityId).Values;
        var passed = (firstTick == 0 ? activations.Length == 0 : activations.Length == 1 && activations[0].Tick.Value == firstTick)
            && result.Events.OfType<CardChargedEvent>().Count(e => e.SourceCardId == tower.EntityId) == charges
            && values[GameAttributeKeys.Armor] == amount && values[GameAttributeKeys.HealingBonus] == amount
            && hero.Health == 9000 + (firstTick == 0 ? 0 : amount)
            && hero.Armor >= (firstTick == 0 ? 0 : amount)
            && !result.PermanentAttributeBonuses.Any() && !result.PermanentChanges.Any()
            && result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events);
        if (!passed) GD.Print($"苍穹塔验证失败：reverse={reverse}, expected={firstTick}/{amount}/{charges}, active={string.Join(',', activations.Select(e => e.Tick.Value))}, hero={hero.Health}/{hero.Armor}, armor={values[GameAttributeKeys.Armor]}, healing={values[GameAttributeKeys.HealingBonus]}");
        return passed;
    }

    internal static async Task Capture(Node owner)
    {
        var definition = new SkyvaultTowerCardDefinition();
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition));
        var adapter = new CardDisplayAdapter();
        using var raw = adapter.Artwork(snapshot.Illustration).GetImage();
        if (!raw.HasMipmaps() || raw.GetWidth() * 2 != raw.GetHeight() * 3)
            throw new InvalidOperationException("苍穹塔原画比例或mipmap不合格。");
        GD.Print($"苍穹塔原画：{raw.GetWidth()}×{raw.GetHeight()}，mipmap层数{raw.GetMipmapCount()}");
        var viewport = new SubViewport { Size = new Vector2I(648, 620), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        var preview = new CardPreviewView { Size = owner.GetViewport().GetVisibleRect().Size, Theme = MatchTheme.Create() };
        owner.AddChild(preview);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(648, 620) });
            foreach (var (position, size) in new[] { (new Vector2(24, 24), new Vector2(600, 400)),
                (new Vector2(24, 450), new Vector2(180, 120)) })
            {
                var face = new CardItemView { Position = position, Size = size };
                viewport.AddChild(face); face.Render(snapshot, adapter);
            }
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(SkyvaultTowerChecks).Assembly)));
            preview.Open(snapshot, null, null);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            using var detail = owner.GetViewport().GetTexture().GetImage();
            if (image.SavePng("res://output/skyvault-tower/card-face.png") != Error.Ok
                || detail.SavePng("res://output/skyvault-tower/preview.png") != Error.Ok)
                throw new InvalidOperationException("苍穹塔卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); owner.RemoveChild(preview); preview.Free(); }
    }
}
