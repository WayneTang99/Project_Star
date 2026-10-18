using System;
using System.Threading.Tasks;
using Project_Star.Application.Content;
using Project_Star.Application.Match;
using Project_Star.Presentation.Playtest;
using Project_Star.Presentation.CardFace;
using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 护腿分级护甲、相邻装备疾速与回响范围验证（表现层）。
internal static class GreavesChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new GreavesCardDefinition();
        var identity = definition.Attributes.Identity;
        using var image = GD.Load<Texture2D>(identity.Illustration.ToString())?.GetImage();
        if (identity.DisplayName != "护腿" || identity.FactionKey != new StringName("paladin")
            || identity.Size != CardSize.Medium || definition.InitialLevel != 1
            || definition.SupportsLevel(0) || definition.SupportsLevel(5)
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !definition.Tags.ToArray().ToHashSet().SetEquals(new[] { GameTags.Medium, GameTags.Equipment })
            || image is null || image.GetWidth() != image.GetHeight() || !image.HasMipmaps()
            || !DefinitionRegistry.Scan(typeof(GreavesChecks).Assembly).Cards.ContainsKey(identity.Key))
        { GD.Print($"护腿身份或原画验证失败：{image?.GetWidth()}×{image?.GetHeight()},mipmap={image?.HasMipmaps()}"); return false; }
        for (var level = 1; level <= 4; level++)
        foreach (var reverse in new[] { false, true })
        {
            var card = new EntityFactory().CreateCard(definition, level);
            var amount = 20 << (level - 1);
            var values = card.Attributes.BaseCombat.SnapshotFinalValues();
            if (values[GameAttributeKeys.Armor] != amount || values[GameAttributeKeys.CooldownTicks] != 40
                || card.Abilities.Single(a => a.Activation == AbilityActivation.Active).ManaCost != 0) return false;
            var source = new CardBattleSetup(card.Id, 3, 0, 40, card.Abilities, UseLegacyAttack: false,
                Tags: card.Tags, OccupiedSlots: 2, ArmorAmount: amount, ElementKeys: identity.ElementKeys)
                { CombatValues = values, Level = level };
            var left = Neighbor(0, 3, GameTags.Equipment);
            var right = Neighbor(5, 2, GameTags.Human);
            foreach (var (candidate, eligible, haste) in new[]
            {
                (left, true, true), (right, true, false),
                (left with { OccupiedSlots = 2 }, false, false),
                (right with { BoardStart = 6 }, false, false),
                (left with { IsOnBench = true }, false, false),
                (left with { Abilities = [Opening(new DestroyCardEffectDefinition(false))] }, false, false),
            })
            {
                var setup = Battle([source, candidate], [], reverse);
                var result = new CombatSimulator().Simulate(setup);
                var frame = result.States.Last(s => s.Tick.Value == 40);
                var own = reverse ? frame.Opponent : frame.Player;
                var echoes = result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == card.Id && e.IsEcho);
                if (own.Armor != (eligible ? amount * 2 : 0) || echoes != (eligible ? 2 : 0)
                    || frame.Cards.Single(c => c.Id == candidate.EntityId).Haste != (haste ? 10 : 0)
                    || frame.Cards.Single(c => c.Id == card.Id).Haste != 0
                    || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any()
                    || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events))
                {
                    GD.Print($"护腿目标验证：level={level},reverse={reverse},start={candidate.BoardStart},slots={candidate.OccupiedSlots},eligible={eligible},armor={own.Armor},echoes={echoes},haste={frame.Cards.Single(c => c.Id == candidate.EntityId).Haste}");
                    return false;
                }
            }
            var stacked = new CombatSimulator().Simulate(Battle([source, left with { Multicast = 1 }, right], [], reverse));
            // 多重额外发动在正常发动后2Tick结算，等待第二轮多重完成。
            var end = stacked.States.Last(s => s.Tick.Value == 42);
            if ((reverse ? end.Opponent : end.Player).Armor != amount * 6)
            { GD.Print($"护腿叠加验证：armor={(reverse ? end.Opponent : end.Player).Armor},expected={amount * 6}"); return false; }
            foreach (var invalid in new[] { source with { IsOnBench = true }, source with
                { Abilities = source.Abilities!.Append(Opening(new DestroyCardEffectDefinition(false))).ToArray() } })
            {
                var result = new CombatSimulator().Simulate(Battle([invalid, left], [], reverse));
                var frame = result.States.Last(s => s.Tick.Value == 40);
                if ((reverse ? frame.Opponent : frame.Player).Armor != 0
                    || frame.Cards.Single(c => c.Id == left.EntityId).Haste != 0) return false;
            }
            var enemyResult = new CombatSimulator().Simulate(Battle([source], [left], reverse));
            var enemyFrame = enemyResult.States.Last(s => s.Tick.Value == 40);
            if ((reverse ? enemyFrame.Opponent : enemyFrame.Player).Armor != 0
                || enemyFrame.Cards.Single(c => c.Id == left.EntityId).Haste != 0) return false;
        }
        return true;
    }

    private static CardBattleSetup Neighbor(int start, int slots, StringName tag) => new(EntityId.New(), start, 0, 20,
        [new AbilityDefinition(new StringName("ability.verification_greaves_neighbor"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, 20, [new RestoreManaEffectDefinition(1)])],
        UseLegacyAttack: false, Tags: new TagSet([tag]), OccupiedSlots: slots);
    private static AbilityDefinition Opening(EffectDefinition effect) => new(new StringName("ability.verification_greaves_opening"),
        AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [effect]);
    private static BattleSetup Battle(CardBattleSetup[] allies, CardBattleSetup[] enemies, bool reverse)
    {
        var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), allies);
        var other = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), enemies);
        return new BattleSetup(reverse ? other : own, reverse ? own : other, 42, new BattleTick(43), new BattleTick(44));
    }
    internal static async Task Capture(Node owner)
    {
        if (!LevelsAndCombat()) throw new InvalidOperationException("护腿战斗验证失败。");
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new GreavesCardDefinition()));
        var adapter = new CardDisplayAdapter();
        using var raw = adapter.Artwork(snapshot.Illustration).GetImage();
        if (!raw.HasMipmaps() || raw.GetWidth() != raw.GetHeight())
            throw new InvalidOperationException("护腿原画比例或mipmap不合格。");
        GD.Print($"护腿原画：{raw.GetWidth()}×{raw.GetHeight()}，mipmap层数{raw.GetMipmapCount()}");
        var viewport = new SubViewport { Size = new Vector2I(420, 250), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        var preview = new CardPreviewView { Size = owner.GetViewport().GetVisibleRect().Size, Theme = MatchTheme.Create() };
        owner.AddChild(preview);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(420, 250) });
            foreach (var (position, size) in new[] { (new Vector2(24, 24), new Vector2(200, 200)),
                (new Vector2(260, 24), new Vector2(100, 100)) })
            {
                var face = new CardItemView { Position = position, Size = size };
                viewport.AddChild(face); face.Render(snapshot, adapter);
            }
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(GreavesChecks).Assembly)));
            preview.Open(snapshot, null, null);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            using var detail = owner.GetViewport().GetTexture().GetImage();
            if (image.SavePng("res://output/greaves/card-face.png") != Error.Ok
                || detail.SavePng("res://output/greaves/preview.png") != Error.Ok)
                throw new InvalidOperationException("护腿卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); owner.RemoveChild(preview); preview.Free(); }
    }
}

