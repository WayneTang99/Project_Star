using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 莫娜森林卡牌分级、目标、亡语、充能及纹理验证。
internal static class MonaForestCardsChecks
{
    private static readonly CardDefinition[] Definitions = [new MushroomSpiritCardDefinition(),
        new GiantMawMantrapCardDefinition(), new FrostSpiritDeerCardDefinition(), new GhostEyeCardDefinition()];

    internal static bool Combat()
    {
        var registry = DefinitionRegistry.Scan(typeof(MonaForestCardsChecks).Assembly);
        foreach (var definition in Definitions)
        {
            Require(registry.Cards.ContainsKey(definition.Attributes.Identity.Key)
                && definition.Attributes.Identity.FactionKey == new StringName("mona"), "注册与归属");
            Require(!definition.SupportsLevel(definition.InitialLevel - 1) && !definition.SupportsLevel(5), "等级边界");
        }
        foreach (var reverse in new[] { false, true })
        {
            for (var level = 1; level <= 4; level++)
            {
                var source = Card(new MushroomSpiritCardDefinition(), level, 2);
                var result = Run([source], [Empty(0)], 80, reverse);
                var frame = result.States.Last(s => s.Tick.Value == 40);
                var own = reverse ? frame.Opponent : frame.Player;
                var enemy = reverse ? frame.Player : frame.Opponent;
                Require(own.HealthRegen == 1 << (level - 1) && own.Poison == 0
                    && enemy.Poison == 2 << (level - 1) && enemy.HealthRegen == 0, "蕈灵双向目标");
                Require(result.Events.OfType<StatusChangedEvent>().Count(e => e.Status == BattleStatus.HealthRegen) == 2, "蕈灵周期发动");
                Require(!Run([source with { IsOnBench = true }], [Empty(0)], 80, reverse)
                    .Events.OfType<StatusChangedEvent>().Any(), "备战不发动");
            }
            for (var level = 2; level <= 4; level++)
            foreach (var count in new[] { 0, 1, 2 })
            {
                var plant = Card(new GiantMawMantrapCardDefinition(), level, 2);
                var left = Card(new MushroomSpiritCardDefinition(), level, 1);
                var right = Card(new GhostEyeCardDefinition(), level, 4);
                var distant = Empty(6);
                var medium = Empty(0) with { OccupiedSlots = 2 };
                var bench = Empty(1) with { IsOnBench = true };
                var allies = new[] { plant, distant, bench }.Concat(count > 0 ? new[] { left } : new[] { medium })
                    .Concat(count > 1 ? new[] { right } : Array.Empty<CardBattleSetup>()).ToArray();
                var result = Run(allies, [Empty(0)], 100, reverse, damage: 100);
                var frame = result.States.Last(s => s.Tick.Value == 100);
                var own = reverse ? frame.Opponent : frame.Player;
                var regen = result.Events.OfType<StatusChangedEvent>().Where(e => e.Tick.Value == 100 && e.Status == BattleStatus.HealthRegen).ToArray();
                Require(regen.Length == 1 && regen[0].Amount == 10 << (level - 2), "食人花固定一次再生");
                Require(frame.Cards.Count(c => c.Destroyed) == count
                    && !frame.Cards.Single(c => c.Id == distant.EntityId).Destroyed
                    && !frame.Cards.Single(c => c.Id == bench.EntityId).Destroyed, "仅直接相邻小型目标");
                if (count > 0)
                {
                    var baseline = Run(allies.Select(c => c.EntityId == left.EntityId ? c with
                        { Abilities = c.Abilities!.Where(a => a.Activation != AbilityActivation.EchoOnSourceDestroyed).ToArray() } : c).ToArray(),
                        [Empty(0)], 100, reverse, damage: 100);
                    var baseFrame = baseline.States.Last(s => s.Tick.Value == 100);
                    Require(own.Health - (reverse ? baseFrame.Opponent : baseFrame.Player).Health == 60 << (level - 1), $"蕈灵亡语治疗 level={level} count={count} health={own.Health} baseline={(reverse ? baseFrame.Opponent : baseFrame.Player).Health}");
                }
                if (count > 1)
                    Require(result.Events.OfType<DamageDealtEvent>().Any(e => e.SourceCardId == right.EntityId
                        && e.RawDamage == 120 << (level - 2)), "鬼眼亡语分级伤害");
            }
            for (var level = 2; level <= 4; level++)
            {
                var eye = Card(new GhostEyeCardDefinition(), level, 0);
                var target = Empty(0);
                var result = Run([eye], [target, Empty(1), Empty(2) with { IsOnBench = true }], 80, reverse);
                Require(result.States.Last(s => s.Tick.Value == 70).Cards.Count(c => c.Immobilize > 0) == 1
                    && result.States.Last(s => s.Tick.Value == 80).Cards.All(c => c.Immobilize == 0), "鬼眼随机单目标与到期");
            }
            for (var level = 3; level <= 4; level++)
            foreach (var element in new[] { GameElements.Ice, GameElements.Fire })
            {
                var deer = Card(new FrostSpiritDeerCardDefinition(), level, 0);
                var cooldown = level == 3 ? 80 : 70;
                Require(deer.CooldownTicks == cooldown, "霜灵鹿8/7秒冷却");
                var helper = Empty(3) with { CooldownTicks = 30, ElementKeys = [element], Abilities =
                    [new AbilityDefinition("verification.forest.helper", AbilityActivation.Active, AbilityTarget.AlliedHero,
                        0, 30, [new RestoreManaEffectDefinition(0), new IncreaseSourceCooldownEffectDefinition(10000)])] };
                var setup = Setup([deer, helper], [Empty(0)], cooldown + 10, reverse, 0);
                var result = new CombatSimulator().Simulate(setup);
                var first = element == GameElements.Ice ? cooldown - 10 : cooldown;
                Require(result.States.Last(s => s.Tick.Value == first).Cards.Count(c => c.Immobilize > 0) == 1, "冰发动提前充能");
                var charges = result.Events.OfType<CardChargedEvent>().Where(e => e.TargetCardId == deer.EntityId).ToArray();
                Require(charges.Length == (element == GameElements.Ice ? 2 : 1)
                    && charges.All(e => e.AmountTicks == 10), "包含自身冰发动与回响截断");
                Require(result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events), "随机确定性");
                Require(!Run([deer with { IsOnBench = true }, helper], [Empty(0)], cooldown, reverse)
                    .Events.OfType<CardChargedEvent>().Any(), "备战充能排除");
            }
        }
        var mushroomSnapshot = Project_Star.Application.Match.MatchDisplayQuery.FromOffer(
            Project_Star.Domain.Match.ShopOffer.Create(new MushroomSpiritCardDefinition()));
        var rules = string.Join("\n", Project_Star.Presentation.CardFace.CardDisplayAdapter.Rules(mushroomSnapshot));
        GD.Print(rules);
        rules = rules.Replace(" ", "");
        Require(rules.Contains("己方英雄获得1生命再生") && !rules.Contains("敌方英雄获得1生命再生"), "再生文案目标");
        return CombatTraceChecks.PreservesTraces() && CardDescriptionChecks.AllLevels();
    }

    private static CardBattleSetup Card(CardDefinition definition, int level, int start)
    {
        var card = new EntityFactory().CreateCard(definition, level);
        var values = card.Attributes.BaseCombat.SnapshotFinalValues();
        Require(card.Abilities.All(a => a.ManaCost == 0), "零消耗");
        return new CardBattleSetup(card.Id, start, values.GetValueOrDefault(GameAttributeKeys.AttackDamage),
            values.GetValueOrDefault(GameAttributeKeys.CooldownTicks), card.Abilities, UseLegacyAttack: false,
            Tags: card.Tags, OccupiedSlots: (int)definition.Attributes.Identity.Size,
            ElementKeys: definition.Attributes.Identity.ElementKeys) { Level = level, CombatValues = values };
    }
    private static CardBattleSetup Empty(int start) => new(EntityId.New(), start, 0, 10000, [], UseLegacyAttack: false);
    private static BattleResult Run(CardBattleSetup[] own, CardBattleSetup[] enemy, int ticks, bool reverse, int damage = 0) =>
        new CombatSimulator().Simulate(Setup(own, enemy, ticks, reverse, damage));
    private static BattleSetup Setup(CardBattleSetup[] own, CardBattleSetup[] enemy, int ticks, bool reverse, int damage)
    {
        var a = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0, Poison: damage), own);
        var b = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), enemy);
        return new BattleSetup(reverse ? b : a, reverse ? a : b, 42, new BattleTick(ticks + 1), new BattleTick(ticks + 2));
    }
    internal static async Task Capture(Node owner)
    {
        foreach (var definition in Definitions)
        {
            var identity = definition.Attributes.Identity;
            var adapter = new Project_Star.Presentation.CardFace.CardDisplayAdapter();
            using var image = adapter.Artwork(identity.Illustration).GetImage();
            Require(image.HasMipmaps() && (identity.Size == CardSize.Small
                ? image.GetHeight() == image.GetWidth() * 2 : image.GetHeight() == image.GetWidth()), "原画比例与实际mipmap");
            GD.Print($"{identity.DisplayName} {image.GetWidth()}×{image.GetHeight()} mipmaps={image.GetMipmapCount()}");
            var viewport = new SubViewport { Size = new Vector2I(900, 750), Disable3D = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
            owner.AddChild(viewport);
            var snapshot = Project_Star.Application.Match.MatchDisplayQuery.FromOffer(Project_Star.Domain.Match.ShopOffer.Create(definition));
            foreach (var scale in new[] { 100, 320 })
            {
                var face = new Project_Star.Presentation.Game.CardItemView { Position = new Vector2(scale == 100 ? 20 : 220, 20),
                    Size = new Vector2(scale, identity.Size == CardSize.Small ? scale * 2 : scale) };
                viewport.AddChild(face); face.Render(snapshot, adapter);
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw();
            using var capture = viewport.GetTexture().GetImage();
            capture.SavePng($"res://output/mona-forest/{identity.Key.ToString().Replace("card.", "")}.png");
            viewport.QueueFree();
            var preview = new Project_Star.Presentation.Game.CardPreviewView
            {
                Size = owner.GetViewport().GetVisibleRect().Size,
                Theme = Project_Star.Presentation.Game.MatchTheme.Create(),
            };
            owner.AddChild(preview);
            preview.SetQuery(new Project_Star.Application.Content.CardPreviewQuery(
                DefinitionRegistry.Scan(typeof(MonaForestCardsChecks).Assembly)));
            preview.Open(snapshot, null, null);
            for (var frame = 0; frame < 30; frame++)
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw();
            using var detail = owner.GetViewport().GetTexture().GetImage();
            detail.SavePng($"res://output/mona-forest/{identity.Key.ToString().Replace("card.", "")}-preview.png");
            owner.RemoveChild(preview);
            preview.Free();
        }
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("莫娜森林卡牌验证失败：" + message); }
}
