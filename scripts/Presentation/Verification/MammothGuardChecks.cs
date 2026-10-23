using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Economy;
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
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

// 猛犸近卫身份、分级护甲及敌方发动充能验证（表现层）。
internal static class MammothGuardChecks
{
    internal static bool ArmorAndCharge()
    {
        var definition = new MammothGuardCardDefinition(); var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.Key != new StringName("card.mammoth_guard") || identity.DisplayName != "猛犸近卫"
            || identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Large
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General }) || definition.InitialLevel != 3
            || definition.SupportsLevel(2) || definition.SupportsLevel(5) || definition.Tags.Count != 3
            || !definition.Tags.Contains(GameTags.Human) || !definition.Tags.Contains(GameTags.Mechanical)
            || texture is null || texture.GetWidth() * 2 != texture.GetHeight() * 3
            || !DefinitionRegistry.Scan(typeof(MammothGuardChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        foreach (var level in new[] { 3, 4 })
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        {
            var card = new EntityFactory().CreateCard(definition, level);
            var guard = new CardBattleSetup(card.Id, 0, 0, 100, card.Abilities, UseLegacyAttack: false,
                Tags: card.Tags, OccupiedSlots: 3, ElementKeys: identity.ElementKeys,
                ArmorAmount: card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Armor))
                { Level = level, CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues() };
            var armor = level == 3 ? 180 : 360;
            if (guard.CombatValues[GameAttributeKeys.Armor] != armor || guard.Abilities!.Any(a => a.ManaCost != 0)) return false;
            var enemy = new CardBattleSetup(EntityId.New(), 0, 0, 90,
                [new AbilityDefinition(new StringName("verification.mammoth.support"), AbilityActivation.Active,
                    AbilityTarget.SelfCard, 0, 90, [new GainSourceHeroArmorEffectDefinition(1)])], UseLegacyAttack: false);
            var setup = Battle([guard], [enemy], side);
            var charged = new CombatSimulator().Simulate(setup);
            if (!Check(charged, guard, side, armor, 90, 1)
                || !charged.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            var repeated = new CombatSimulator().Simulate(Battle([guard], [enemy with { Multicast = 1 }], side));
            if (!Check(repeated, guard, side, armor, 90, 2)) return false;
            foreach (var excluded in new[] { enemy with { IsOnBench = true }, enemy with
                { Abilities = [new AbilityDefinition(new StringName("verification.mammoth.unpaid"), AbilityActivation.Active,
                    AbilityTarget.SelfCard, 1, 90, [new GainSourceHeroArmorEffectDefinition(1)])] } })
                if (!Check(new CombatSimulator().Simulate(Battle([guard], [excluded], side)), guard, side, armor, 100, 0)) return false;
            if (!Check(new CombatSimulator().Simulate(Battle([guard, enemy with { BoardStart = 3 }], [], side)),
                guard, side, armor + 1, 100, 0)) return false;
            var bench = new CombatSimulator().Simulate(Battle([guard with { IsOnBench = true }], [enemy], side));
            if (bench.Events.OfType<AbilityActivatedEvent>().Any(e => e.SourceCardId == guard.EntityId)) return false;
            // 同一冻结输入下场不保留充能，护甲读取当前属性加成。
            if (!Check(new CombatSimulator().Simulate(Battle([guard], [], side)), guard, side, armor, 100, 0)
                || !Check(new CombatSimulator().Simulate(Battle([guard with { ArmorAmount = armor + 7 }], [], side)),
                    guard, side, armor + 7, 100, 0)) return false;
        }
        return true;
    }

    private static bool Check(BattleResult result, CardBattleSetup guard, SideId side, int armor, int tick, int echoes)
    {
        var events = result.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId == guard.EntityId).ToArray();
        var frame = result.States.Last(s => s.Player.Health > 0 && s.Opponent.Health > 0);
        return events.Count(e => e.IsEcho) == echoes && events.Count(e => !e.IsEcho) == 1
            && events.Single(e => !e.IsEcho).Tick.Value == tick
            && (side == SideId.Player ? frame.Player : frame.Opponent).Armor == armor;
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(648, 448), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(648, 448) });
            var face = new CardItemView { Position = new Vector2(24, 24), Size = new Vector2(600, 400) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new MammothGuardCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/mammoth-guard/card-face.png") != Error.Ok)
                throw new InvalidOperationException("猛犸近卫卡面截图保存失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
        var preview = new CardPreviewView { Size = owner.GetViewport().GetVisibleRect().Size, Theme = MatchTheme.Create() };
        owner.AddChild(preview);
        try
        {
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(MammothGuardChecks).Assembly)));
            preview.Open(MatchDisplayQuery.FromOffer(ShopOffer.Create(new MammothGuardCardDefinition())), null, null);
            preview.GetNode<Control>("Panel").FindChildren("Level4", "Button", true, false).OfType<Button>().Single()
                .EmitSignal(BaseButton.SignalName.Pressed);
            for (var frame = 0; frame < 4; frame++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = owner.GetViewport().GetTexture().GetImage();
            if (image.SavePng("res://output/preview-armor/mammoth-level4.png") != Error.Ok)
                throw new InvalidOperationException("猛犸近卫等级预览截图保存失败。");
        }
        finally { owner.RemoveChild(preview); preview.Free(); }
    }

    private static BattleSetup Battle(CardBattleSetup[] allies, CardBattleSetup[] enemies, SideId side) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), side == SideId.Player ? allies : enemies),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), side == SideId.Player ? enemies : allies),
        42, new BattleTick(100), new BattleTick(101));
}
