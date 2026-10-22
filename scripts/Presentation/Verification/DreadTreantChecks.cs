using System;
using System.Linq;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

internal static class DreadTreantChecks
{
    internal static async System.Threading.Tasks.Task Capture(Godot.Node owner)
    {
        var snapshot = Project_Star.Application.Match.MatchDisplayQuery.FromOffer(
            Project_Star.Domain.Match.ShopOffer.Create(new DreadTreantCardDefinition()));
        var adapter = new Project_Star.Presentation.CardFace.CardDisplayAdapter();
        using var image = adapter.Artwork(snapshot.Illustration).GetImage();
        Require(image.GetWidth() == image.GetHeight() && image.HasMipmaps(), "方形原画与mipmap");
        Godot.GD.Print($"恐惧树精原画 {image.GetWidth()}×{image.GetHeight()} mipmaps={image.GetMipmapCount()}");
        var viewport = new Godot.SubViewport { Size = new Godot.Vector2I(620, 460), Disable3D = true,
            RenderTargetUpdateMode = Godot.SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        foreach (var (position, size) in new[] { (new Godot.Vector2(20, 20), new Godot.Vector2(140, 140)),
            (new Godot.Vector2(200, 20), new Godot.Vector2(400, 400)) })
        {
            var face = new Project_Star.Presentation.Playtest.CardItemView { Position = position, Size = size };
            viewport.AddChild(face); face.Render(snapshot, adapter);
        }
        await owner.ToSignal(owner.GetTree(), Godot.SceneTree.SignalName.ProcessFrame);
        await owner.ToSignal(owner.GetTree(), Godot.SceneTree.SignalName.ProcessFrame);
        Godot.RenderingServer.ForceDraw();
        using var capture = viewport.GetTexture().GetImage();
        Require(capture.SavePng("res://output/dread-treant/card-face.png") == Godot.Error.Ok, "卡面截图");
        owner.RemoveChild(viewport); viewport.Free();
    }

    internal static bool LevelsAndCombat()
    {
        var definition = new DreadTreantCardDefinition();
        var identity = definition.Attributes.Identity;
        Require(identity.DisplayName == "恐惧树精" && identity.FactionKey == "mona"
            && identity.Size == CardSize.Medium && identity.ElementKeys.SequenceEqual(new[] { GameElements.Wood })
            && definition.Tags.Contains(GameTags.Plant) && definition.Tags.Contains(GameTags.Spirit)
            && definition.InitialLevel == 2 && !definition.SupportsLevel(1)
            && DefinitionRegistry.Scan(typeof(DreadTreantChecks).Assembly).Cards.ContainsKey(identity.Key), "身份注册");
        foreach (var level in new[] { 2, 3, 4 })
        foreach (var reverse in new[] { false, true })
        foreach (var bench in new[] { false, true })
        {
            var card = new EntityFactory().CreateCard(definition, level);
            Require(card.Abilities.Single().ManaCost == 0
                && card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks) == 70, "冷却与消耗");
            var source = new CardBattleSetup(card.Id, 0, 0, 70, card.Abilities,
                IsOnBench: bench, UseLegacyAttack: false, OccupiedSlots: 2);
            var targets = new[] { new CardBattleSetup(EntityId.New(), 0, 300, 10),
                new CardBattleSetup(EntityId.New(), 1, 0, 1000, UseLegacyAttack: false),
                new CardBattleSetup(EntityId.New(), 2, 0, 1000, IsOnBench: true, UseLegacyAttack: false) };
            var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0,
                MaxMana: 1000, ManaRegen: 0), [source]);
            var enemy = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), targets);
            var setup = new BattleSetup(reverse ? enemy : own, reverse ? own : enemy, 42,
                new BattleTick(81), new BattleTick(82));
            var result = new CombatSimulator().Simulate(setup);
            var before = result.States.Last(s => s.Tick.Value == 69);
            var after = result.States.Last(s => s.Tick.Value == 70);
            var beforeHero = reverse ? before.Opponent : before.Player;
            var afterHero = reverse ? after.Opponent : after.Player;
            var amount = bench ? 0 : 40 << (level - 2);
            Require(beforeHero.Mana == 0 && afterHero.Mana == amount, "7秒分级恢复魔法");
            Require(afterHero.Health == beforeHero.Health - 300 + amount, "实际分级治疗");
            Require(after.Cards.Count(c => c.Slow > 0) == (bench ? 0 : 1)
                && after.Cards.Single(c => c.Id == targets[2].EntityId).Slow == 0, "随机单目标与备战排除");
            Require(result.States.Last(s => s.Tick.Value == 80).Cards.All(c => c.Slow == 0), "1秒迟缓到期");
            Require(result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events), "确定性");
        }
        return true;
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("恐惧树精验证失败：" + message); }
}
