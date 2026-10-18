using System.Threading.Tasks;
using Project_Star.Application.Economy;
using Project_Star.Application.Match;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;
using System;
using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Domain.Match;

namespace Project_Star.Presentation.Verification;

// 火枪手分级、周期伤害、灼伤和原画验证（表现层）。
internal static class MusketeerChecks
{
    internal static bool LevelsAndFiring()
    {
        var definition = new MusketeerCardDefinition(); var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.Key != new StringName("card.musketeer") || identity.DisplayName != "火枪手"
            || identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General }) || definition.InitialLevel != 1
            || definition.SupportsLevel(0) || definition.SupportsLevel(5) || definition.Tags.Count != 2
            || !definition.Tags.Contains(GameTags.Small) || !definition.Tags.Contains(GameTags.Human)
            || identity.DescriptionEntries.Single().KeywordKey != CardKeywords.Activate
            || texture is null || texture.GetHeight() != texture.GetWidth() * 2 || !texture.GetImage().HasMipmaps()
            || !DefinitionRegistry.Scan(typeof(MusketeerChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        foreach (var (level, damage, burn) in new[] { (1, 10, 2), (2, 20, 4), (3, 40, 8), (4, 80, 16) })
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        {
            var card = new EntityFactory().CreateCard(definition, level);
            var values = card.Attributes.BaseCombat.SnapshotFinalValues();
            if (values[GameAttributeKeys.AttackDamage] != damage || values[GameAttributeKeys.Burn] != burn
                || values[GameAttributeKeys.CooldownTicks] != 70 || card.Abilities.Single().ManaCost != 0) return false;
            var musketeer = new CardBattleSetup(card.Id, 0, damage, 70, card.Abilities, UseLegacyAttack: false,
                Tags: card.Tags, ElementKeys: identity.ElementKeys) { Level = level, CombatValues = values };
            var allies = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), [musketeer]);
            var enemies = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), []);
            var setup = new BattleSetup(side == SideId.Player ? allies : enemies,
                side == SideId.Player ? enemies : allies, 42, new BattleTick(150), new BattleTick(151));
            var result = new CombatSimulator().Simulate(setup);
            var hits = result.Events.OfType<DamageDealtEvent>().Where(item => item.SourceCardId == card.Id).ToArray();
            var statuses = result.Events.OfType<StatusChangedEvent>().Where(item => item.Status == BattleStatus.Burn).ToArray();
            var destroyed = result.Events.OfType<CardDestroyedEvent>().ToArray();
            if (hits.Length != 2 || hits[0].Tick.Value != 70 || hits[0].RawDamage != damage
                || statuses.Length != 2 || statuses[0].Tick.Value != 70 || statuses[0].Amount != burn
                || destroyed.Length != 0 || hits[1].Tick.Value != 140 || hits[1].RawDamage != damage || statuses[1].Tick.Value != 140 || statuses[1].Amount != burn
                || result.Events.ToList().IndexOf(hits[0]) >= result.Events.ToList().IndexOf(statuses[0])
                || result.Events.OfType<AbilityActivatedEvent>().Count(item => item.SourceCardId == card.Id) != 2
                || result.States[^1].Cards.Single(item => item.Id == card.Id).Destroyed
                || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any()) return false;
            var next = new CombatSimulator().Simulate(setup);
            if (next.States[0].Cards.Single(item => item.Id == card.Id).Destroyed
                || !result.Events.SequenceEqual(next.Events) || card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Burn) != burn)
                return false;
        }
        return true;
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(240, 420), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(240, 420) });
            var face = new CardItemView { Position = new Vector2(24, 24), Size = new Vector2(192, 372) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new MusketeerCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/musketeer/card-face.png") != Error.Ok)
                throw new InvalidOperationException("Incendiary bomb card capture failed.");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }
}

