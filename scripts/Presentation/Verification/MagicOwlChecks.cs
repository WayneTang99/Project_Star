using System;
using System.Linq;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 魔枭身份、分级再生与发动飞行验证（表现层）。
internal static class MagicOwlChecks
{
    internal static async System.Threading.Tasks.Task Capture(Godot.Node owner)
    {
        var snapshot = Project_Star.Application.Match.MatchDisplayQuery.FromOffer(
            Project_Star.Domain.Match.ShopOffer.Create(new MagicOwlCardDefinition()));
        var adapter = new Project_Star.Presentation.CardFace.CardDisplayAdapter();
        using var image = adapter.Artwork(snapshot.Illustration).GetImage();
        Require(image.GetHeight() == image.GetWidth() * 2 && image.HasMipmaps(), "原画比例与mipmap");
        Godot.GD.Print($"魔枭原画 {image.GetWidth()}×{image.GetHeight()} mipmaps={image.GetMipmapCount()}");
        var viewport = new Godot.SubViewport { Size = new Godot.Vector2I(500, 460), Disable3D = true,
            RenderTargetUpdateMode = Godot.SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        viewport.AddChild(new Godot.ColorRect { Size = new Godot.Vector2(500, 460), Color = new Godot.Color("333c41") });
        foreach (var (position, size) in new[] { (new Godot.Vector2(20, 20), new Godot.Vector2(100, 200)),
            (new Godot.Vector2(160, 20), new Godot.Vector2(200, 400)) })
        {
            var face = new Project_Star.Presentation.Playtest.CardItemView { Position = position, Size = size };
            viewport.AddChild(face); face.Render(snapshot, adapter);
        }
        await owner.ToSignal(owner.GetTree(), Godot.SceneTree.SignalName.ProcessFrame);
        await owner.ToSignal(owner.GetTree(), Godot.SceneTree.SignalName.ProcessFrame);
        Godot.RenderingServer.ForceDraw();
        using var capture = viewport.GetTexture().GetImage();
        Require(capture.SavePng("res://output/magic-owl/card-face.png") == Godot.Error.Ok, "卡面截图");
        owner.RemoveChild(viewport); viewport.Free();
    }

    internal static bool LevelsAndCombat()
    {
        var definition = new MagicOwlCardDefinition();
        var identity = definition.Attributes.Identity;
        Require(identity.DisplayName == "魔枭" && identity.FactionKey == "mona"
            && identity.Size == CardSize.Small && identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            && definition.Tags.Contains(GameTags.Beast) && definition.InitialLevel == 1
            && DefinitionRegistry.Scan(typeof(MagicOwlChecks).Assembly).Cards.ContainsKey(identity.Key), "身份注册");
        foreach (var level in new[] { 1, 2, 3, 4 })
        foreach (var reverse in new[] { false, true })
        {
            var card = new EntityFactory().CreateCard(definition, level);
            Require(card.Abilities.Single().ManaCost == 0
                && card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) == 40, "冷却与消耗");
            var source = new CardBattleSetup(card.Id, 0, 0, 40, card.Abilities, UseLegacyAttack: false,
                Tags: card.Tags, ElementKeys: identity.ElementKeys) { Identity = identity, Level = level };
            foreach (var bench in new[] { false, true })
            {
                var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0),
                    [source with { IsOnBench = bench }]);
                var enemy = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), []);
                var setup = new BattleSetup(reverse ? enemy : own, reverse ? own : enemy, 42,
                    new BattleTick(81), new BattleTick(82));
                var result = new CombatSimulator().Simulate(setup);
                foreach (var tick in new[] { 39, 40, 79, 80 })
                {
                    var frame = result.States.Last(s => s.Tick.Value == tick);
                    var hero = reverse ? frame.Opponent : frame.Player;
                    var count = bench ? 0 : tick / 40;
                    Require(hero.ManaRegen == count * (2 << (level - 1)), "4秒分级再生累加");
                    Require(frame.Cards.Single(c => c.Id == card.Id).IsFlying == (count > 0), "发动飞行");
                }
                Require(result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                    && !result.PermanentAttributeBonuses.Any() && !result.PermanentChanges.Any(), "确定性与战后隔离");
            }
        }
        return true;
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("魔枭验证失败：" + message); }
}
