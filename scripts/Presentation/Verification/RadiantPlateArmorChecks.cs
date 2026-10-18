using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
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

internal static class RadiantPlateArmorChecks
{
    internal static bool LevelsAndEcho()
    {
        var definition = new RadiantPlateArmorCardDefinition();
        var identity = definition.Attributes.Identity;
        using var image = GD.Load<Texture2D>(identity.Illustration.ToString())?.GetImage();
        if (identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Medium
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Light }) || definition.InitialLevel != 3
            || definition.SupportsLevel(2) || definition.SupportsLevel(5)
            || !definition.Tags.ToArray().ToHashSet().SetEquals(new[] { GameTags.Medium, GameTags.Equipment })
            || image is null || image.GetWidth() != image.GetHeight() || !image.HasMipmaps()
            || !DefinitionRegistry.Scan(typeof(RadiantPlateArmorChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        foreach (var (level, armor, growth) in new[] { (3, 160, 20), (4, 320, 40) })
        foreach (var reverse in new[] { false, true })
        {
            var card = new EntityFactory().CreateCard(definition, level);
            var values = card.Attributes.BaseCombat.SnapshotFinalValues();
            var source = new CardBattleSetup(card.Id, 0, 0, 50, card.Abilities, UseLegacyAttack: false,
                Tags: card.Tags, OccupiedSlots: 2, ElementKeys: identity.ElementKeys, ArmorAmount: armor)
                { Level = level, CombatValues = values };
            var trigger = new CardBattleSetup(EntityId.New(), 2, 0, 30,
                [new AbilityDefinition(new StringName("ability.verification_light_trigger"), AbilityActivation.Active,
                    AbilityTarget.AlliedHero, 0, 30, [new HealEffectDefinition(1)])],
                UseLegacyAttack: false, ElementKeys: [GameElements.Light]);
            foreach (var mode in Enumerable.Range(0, 6))
            {
                var ally = mode switch { 2 => trigger with { Multicast = 1 },
                    3 => trigger with { ElementKeys = [GameElements.Fire] },
                    4 => trigger with { IsOnBench = true }, _ => trigger };
                var allies = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0),
                    mode is 0 or 5 ? [source] : [source, ally]);
                var enemies = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0),
                    mode == 5 ? [trigger] : []);
                var setup = new BattleSetup(reverse ? enemies : allies, reverse ? allies : enemies,
                    42, new BattleTick(101), new BattleTick(102));
                var result = new CombatSimulator().Simulate(setup);
                var frame = result.States.Last(state => state.Tick.Value == 100);
                var hero = reverse ? frame.Opponent : frame.Player;
                var events = result.Events.OfType<AbilityActivatedEvent>().Where(e => e.SourceCardId == card.Id).ToArray();
                var extra = mode == 1 ? 3 : mode == 2 ? 6 : 0;
                var armorGrowth = mode == 1 ? 5 : mode == 2 ? 9 : 1;
                if (!events.Where(e => !e.IsEcho).Select(e => e.Tick.Value).SequenceEqual(new long[] { 50, 100 })
                    || events.Count(e => e.IsEcho) != 2 + extra
                    || hero.Armor != 2 * armor + armorGrowth * growth
                    || frame.Cards.Single(c => c.Id == card.Id).Values[GameAttributeKeys.Armor] != armor + (2 + extra) * growth
                    || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any()
                    || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            }
            if (card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Armor) != armor
                || card.Abilities.Single(a => a.Activation == AbilityActivation.Active).ManaCost != 0) return false;
        }
        return true;
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(720, 460), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Size = new Vector2(720, 460), Color = new Color("333c41") });
            foreach (var level in new[] { 3, 4 })
            {
                var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new RadiantPlateArmorCardDefinition(), level));
                var adapter = new CardDisplayAdapter();
                using var art = adapter.Artwork(snapshot.Illustration).GetImage();
                GD.Print($"光辉板甲：{art.GetWidth()}×{art.GetHeight()}，mipmap层数{art.GetMipmapCount()}");
                var position = new Vector2(20 + (level - 3) * 350, 20);
                var face = new CardItemView { Position = position, Size = new Vector2(300, 300) };
                viewport.AddChild(face); face.Render(snapshot, adapter);
                var compact = new CardItemView { Position = position + new Vector2(0, 320), Size = new Vector2(100, 100) };
                viewport.AddChild(compact); compact.Render(snapshot, adapter);
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var capture = viewport.GetTexture().GetImage();
            if (capture.SavePng("res://output/radiant-plate-armor/card-face.png") != Error.Ok)
                throw new InvalidOperationException("光辉板甲卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }
}
