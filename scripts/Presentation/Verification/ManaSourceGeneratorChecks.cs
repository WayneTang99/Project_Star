using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Application.Content;
using Project_Star.Application.Economy;
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

// 魔源发电机分级、随机充能与支付边界验证（表现层）。
internal static class ManaSourceGeneratorChecks
{
    internal static bool LevelsAndCharge()
    {
        var definition = new ManaSourceGeneratorCardDefinition();
        var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.DisplayName != "魔源发电机" || identity.FactionKey != new StringName("harla")
            || identity.Size != CardSize.Medium || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Lightning })
            || definition.InitialLevel != 2 || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || !definition.Tags.Contains(GameTags.Mechanical) || texture is null || texture.GetWidth() != texture.GetHeight()
            || !DefinitionRegistry.Scan(typeof(ManaSourceGeneratorChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        foreach (var level in new[] { 2, 3, 4 })
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        foreach (var count in new[] { 0, 1, 4 })
        foreach (var mana in new[] { 79, 80 })
        {
            var card = new EntityFactory().CreateCard(definition, level);
            if (card.Abilities.Single().CooldownTicks != 50 || card.Abilities.Single().ManaCost != 80) return false;
            var source = new CardBattleSetup(card.Id, 0, 0, 50, card.Abilities, UseLegacyAttack: false, OccupiedSlots: 2);
            var targets = Enumerable.Range(0, count).Select(i => Support(i + 2)).ToArray();
            var bench = Support(7) with { IsOnBench = true };
            var passive = Support(8) with { Abilities = Array.Empty<AbilityDefinition>() };
            var enemy = Support(0);
            var allies = new[] { source }.Concat(targets).Concat(new[] { bench, passive }).ToArray();
            var hero = new HeroBattleSetup(EntityId.New(), 10000, 0, InitialMana: mana, ManaRegen: 0);
            var setup = new BattleSetup(new BattleSideSetup(hero, side == SideId.Player ? allies : [enemy]),
                new BattleSideSetup(hero with { EntityId = EntityId.New() }, side == SideId.Player ? [enemy] : allies),
                42, new BattleTick(61), new BattleTick(62));
            var result = new CombatSimulator().Simulate(setup);
            var charged = result.Events.OfType<CardChargedEvent>().Where(e => e.SourceCardId == source.EntityId).ToArray();
            var expected = mana == 80 ? Math.Min(level - 1, count) : 0;
            if (charged.Length != expected || charged.Select(e => e.TargetCardId).Distinct().Count() != expected
                || charged.Any(e => e.AmountTicks != 10 || e.Tick.Value != 50
                    || !targets.Any(t => t.EntityId == e.TargetCardId))
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            foreach (var target in targets)
            {
                var tick = charged.Any(e => e.TargetCardId == target.EntityId) ? 50 : 60;
                if (result.Events.OfType<AbilityActivatedEvent>().Single(e => e.SourceCardId == target.EntityId).Tick.Value != tick)
                    return false;
            }
            var frame = result.States.Last(s => s.Player.Health > 0 && s.Opponent.Health > 0);
            if ((side == SideId.Player ? frame.Player : frame.Opponent).Mana != (mana == 80 ? 0 : 79)) return false;
        }
        return true;
    }

    private static CardBattleSetup Support(int start) => new(EntityId.New(), start, 0, 60,
        [new AbilityDefinition(new StringName("verification.generator.support"), AbilityActivation.Active,
            AbilityTarget.SelfCard, 0, 60, [new GainSourceHeroArmorEffectDefinition(1)])], UseLegacyAttack: false);

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(448, 448), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(448, 448) });
            var face = new CardItemView { Position = new Vector2(24, 24), Size = new Vector2(400, 400) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new ManaSourceGeneratorCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/mana-source-generator/card-face.png") != Error.Ok)
                throw new InvalidOperationException("魔源发电机卡面截图保存失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }
}
