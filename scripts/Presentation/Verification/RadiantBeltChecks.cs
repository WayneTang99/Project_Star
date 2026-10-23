using System.Threading.Tasks;
using Project_Star.Application.Content;
using Project_Star.Presentation.Game;
using System;
using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Application.Board;
using Project_Star.Content.Heroes;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Verification;

// 光辉腰带分级、动态生命贡献及跨战斗隔离验证（表现层）。
internal static class RadiantBeltChecks
{
    internal static bool LevelsAndLifetime()
    {
        var definition = new RadiantBeltCardDefinition();
        var identity = definition.Attributes.Identity;
        using var image = GD.Load<Texture2D>(identity.Illustration.ToString())?.GetImage();
        if (identity.DisplayName != "光辉腰带" || identity.Size != CardSize.Medium
            || identity.FactionKey != new StringName("paladin") || definition.InitialLevel != 2
            || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Light })
            || !definition.Tags.ToArray().ToHashSet().SetEquals(new[] { GameTags.Medium, GameTags.Equipment })
            || image is null || image.GetWidth() != image.GetHeight() || !image.HasMipmaps()
            || !DefinitionRegistry.Scan(typeof(RadiantBeltChecks).Assembly).Cards.ContainsKey(identity.Key))
        { GD.Print($"光辉腰带身份原画：{image?.GetWidth()}×{image?.GetHeight()},mipmap={image?.HasMipmaps()}"); return false; }
        foreach (var (level, amount) in new[] { (2, 30), (3, 60), (4, 100) })
        foreach (var reverse in new[] { false, true })
        {
            var source = Belt(level);
            var equipment = Target(2, GameTags.Equipment);
            var human = Target(3, GameTags.Human);
            var bench = Target(4, GameTags.Equipment) with { IsOnBench = true };
            var dead = Target(5, GameTags.Equipment) with { Abilities = [Opening(new DestroyCardEffectDefinition(false))] };
            var enemy = Target(0, GameTags.Equipment);
            var setup = Battle([source, equipment, human, bench, dead], [enemy], reverse);
            var result = new CombatSimulator().Simulate(setup);
            if (!Health(result.States.First(), reverse, 1000 + amount * 3, 1000 + amount * 3)
                || !Health(result.States.Last(s => s.Tick.Value == 10), reverse, 1000 + amount * 2, 1000 + amount * 2)
                || (reverse ? result.States[0].Player : result.States[0].Opponent).MaxHealth != 1000
                || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            var disappearing = equipment with { Abilities = [Active(new DestroyCardEffectDefinition(false))] };
            var damaged = Target(0, GameTags.Human) with { Abilities = [Opening(new DamageEffectDefinition(50))] };
            var changed = new CombatSimulator().Simulate(Battle([source, disappearing], [damaged], reverse));
            if (!Health(changed.States.Last(s => s.Tick.Value == 9), reverse, 1000 + amount * 2, 950 + amount * 2)
                || !Health(changed.States.Last(s => s.Tick.Value == 10), reverse, 1000 + amount, 950 + amount)) return false;
            foreach (var invalid in new[] { source with { IsOnBench = true }, source with
                { Abilities = source.Abilities!.Append(Active(new DestroyCardEffectDefinition(false))).ToArray() } })
            {
                var stopped = new CombatSimulator().Simulate(Battle([invalid, equipment], [], reverse));
                if (!Health(stopped.States.Last(s => s.Tick.Value == 10), reverse, 1000, 1000)) return false;
            }
            var preview = new CombatSimulator().Preview(Battle([source, equipment], [], reverse));
            if (!Health(preview, reverse, 1000 + amount * 2, 1000 + amount * 2)) return false;
            var details = string.Join("\n", CardDisplayAdapter.Rules(MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level))));
            if (!details.Contains("×" + amount) || !details.Contains("最低保留1生命") || details.Contains("冷却"))
            { GD.Print("光辉腰带详情：" + details); return false; }
        }
        foreach (var reverse in new[] { false, true })
        {
            var stacked = new CombatSimulator().Preview(Battle([Belt(2), Belt(3) with { BoardStart = 2 }], [], reverse));
            if (!Health(stacked, reverse, 1180, 1180)) return false;
            var destroy = Target(2, GameTags.Equipment) with { Abilities = [Active(new DestroyCardEffectDefinition(false))] };
            var hit = Target(0, GameTags.Human) with { Abilities = [Opening(new DamageEffectDefinition(1050))] };
            var clamped = new CombatSimulator().Simulate(Battle([Belt(2), destroy], [hit], reverse));
            if (!Health(clamped.States.Last(s => s.Tick.Value == 10), reverse, 1030, 1)) return false;
            var lethal = hit with { Abilities = [Opening(new DamageEffectDefinition(1060))] };
            var defeated = new CombatSimulator().Simulate(Battle([Belt(2), destroy], [lethal], reverse));
            if ((reverse ? defeated.States.Last().Opponent : defeated.States.Last().Player).Health != 0) return false;
            var repairer = Target(3, GameTags.Human) with { Abilities = [new AbilityDefinition(
                new StringName("ability.verification_belt_repair"), AbilityActivation.Active, AbilityTarget.SelfCard,
                0, 20, [new RepairRandomDestroyedAlliedCardEffectDefinition(GameTags.Equipment)])] };
            var repaired = new CombatSimulator().Simulate(Battle([Belt(2), destroy, repairer], [], reverse, 21));
            if (!Health(repaired.States.Last(s => s.Tick.Value == 10), reverse, 1030, 1030)
                || !Health(repaired.States.Last(s => s.Tick.Value == 20), reverse, 1060, 1060)) return false;
            var generic = Belt(2) with { Abilities = [new AbilityDefinition(new StringName("ability.verification_human_health"),
                AbilityActivation.PassiveAura, AbilityTarget.AlliedHero, 0, 0,
                [new IncreaseHeroMaxHealthPerAlliedTaggedCardEffectDefinition(GameTags.Human, 7)])] };
            if (!Health(new CombatSimulator().Preview(Battle([generic, repairer], [], reverse)), reverse, 1007, 1007)) return false;
        }
        var factory = new EntityFactory();
        var session = new CreateMatchService(factory).Create(42, 100, new PaladinHeroDefinition());
        var belt = factory.CreateCard(definition, 2);
        session.Player.Inventory.Add(belt);
        var board = new BoardService(new BoardPlacementSolver());
        if (board.PlaceCard(session, belt.Id, BoardZone.Battlefield, 0).IsFailure) return false;
        var initial = session.Player.Hero!.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth);
        var shown = MatchDisplayQuery.Capture(session, DefinitionRegistry.Scan(typeof(RadiantBeltChecks).Assembly).Sets);
        return shown.Hero!.CombatValues[GameAttributeKeys.MaxHealth] == initial + 30
            && session.Player.Hero.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.MaxHealth) == initial;
    }

    private static CardBattleSetup Belt(int level)
    {
        var definition = new RadiantBeltCardDefinition();
        var card = new EntityFactory().CreateCard(definition, level);
        return new CardBattleSetup(card.Id, 0, 0, 0, card.Abilities, UseLegacyAttack: false, Tags: card.Tags,
            OccupiedSlots: 2, ElementKeys: definition.Attributes.Identity.ElementKeys) { Level = level };
    }
    private static CardBattleSetup Target(int start, StringName tag) => new(EntityId.New(), start, 0, 0, [],
        UseLegacyAttack: false, Tags: new TagSet([tag]));
    private static AbilityDefinition Opening(EffectDefinition effect) => new(new StringName("ability.verification_belt_opening"),
        AbilityActivation.PassiveOnBattleStart, AbilityTarget.EnemyHero, 0, 0, [effect]);
    private static AbilityDefinition Active(EffectDefinition effect) => new(new StringName("ability.verification_belt_active"),
        AbilityActivation.Active, AbilityTarget.SelfCard, 0, 10, [effect]);
    private static bool Health(BattleStateSnapshot frame, bool reverse, int maximum, int current)
    {
        var hero = reverse ? frame.Opponent : frame.Player;
        if (hero.MaxHealth == maximum && hero.Health == current) return true;
        GD.Print($"光辉腰带生命验证：tick={frame.Tick.Value},reverse={reverse},actual={hero.Health}/{hero.MaxHealth},expected={current}/{maximum}");
        return false;
    }
    private static BattleSetup Battle(CardBattleSetup[] allies, CardBattleSetup[] enemies, bool reverse, int ticks = 11)
    {
        var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), allies);
        var other = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 1000, 0, ManaRegen: 0), enemies);
        return new BattleSetup(reverse ? other : own, reverse ? own : other, 42, new BattleTick(ticks), new BattleTick(ticks + 1));
    }
    internal static async Task Capture(Node owner)
    {
        if (!LevelsAndLifetime()) throw new InvalidOperationException("光辉腰带战斗验证失败。");
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new RadiantBeltCardDefinition()));
        var adapter = new CardDisplayAdapter();
        using var raw = adapter.Artwork(snapshot.Illustration).GetImage();
        if (!raw.HasMipmaps() || raw.GetWidth() != raw.GetHeight())
            throw new InvalidOperationException("光辉腰带原画比例或mipmap不合格。");
        GD.Print($"光辉腰带原画：{raw.GetWidth()}×{raw.GetHeight()}，mipmap层数{raw.GetMipmapCount()}");
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
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(RadiantBeltChecks).Assembly)));
            preview.Open(snapshot, null, null);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            using var detail = owner.GetViewport().GetTexture().GetImage();
            if (image.SavePng("res://output/radiant-belt/card-face.png") != Error.Ok
                || detail.SavePng("res://output/radiant-belt/preview.png") != Error.Ok)
                throw new InvalidOperationException("光辉腰带卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); owner.RemoveChild(preview); preview.Free(); }
    }
}



