using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

// 燃爆弹分级伤害、灼伤、消耗顺序与战斗间恢复验证（表现层）。
internal static class IncendiaryBombChecks
{
    internal static bool LevelsAndConsumption()
    {
        var definition = new IncendiaryBombCardDefinition(); var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.Key != new StringName("card.incendiary_bomb") || identity.DisplayName != "燃爆弹"
            || identity.FactionKey != new StringName("harla") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Fire }) || definition.InitialLevel != 2
            || definition.SupportsLevel(1) || definition.SupportsLevel(5) || definition.Tags.Count != 2
            || !definition.Tags.Contains(GameTags.Small) || !definition.Tags.Contains(GameTags.Consumable)
            || identity.DescriptionEntries[1].KeywordKey != CardKeywords.Consume
            || texture is null || texture.GetHeight() != texture.GetWidth() * 2
            || !DefinitionRegistry.Scan(typeof(IncendiaryBombChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        foreach (var (level, damage, burn) in new[] { (2, 40, 4), (3, 80, 8), (4, 160, 16) })
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        {
            var card = new EntityFactory().CreateCard(definition, level);
            var values = card.Attributes.BaseCombat.SnapshotFinalValues();
            if (values[GameAttributeKeys.AttackDamage] != damage || values[GameAttributeKeys.Burn] != burn
                || values[GameAttributeKeys.CooldownTicks] != 70 || card.Abilities.Single().ManaCost != 0) return false;
            var bomb = new CardBattleSetup(card.Id, 0, damage, 70, card.Abilities, UseLegacyAttack: false,
                Tags: card.Tags, ElementKeys: identity.ElementKeys) { Level = level, CombatValues = values };
            var allies = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), [bomb]);
            var enemies = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), []);
            var setup = new BattleSetup(side == SideId.Player ? allies : enemies,
                side == SideId.Player ? enemies : allies, 42, new BattleTick(150), new BattleTick(151));
            var result = new CombatSimulator().Simulate(setup);
            var hits = result.Events.OfType<DamageDealtEvent>().Where(item => item.SourceCardId == card.Id).ToArray();
            var statuses = result.Events.OfType<StatusChangedEvent>().Where(item => item.Status == BattleStatus.Burn).ToArray();
            var destroyed = result.Events.OfType<CardDestroyedEvent>().ToArray();
            if (hits.Length != 1 || hits[0].Tick.Value != 70 || hits[0].RawDamage != damage
                || statuses.Length != 1 || statuses[0].Tick.Value != 70 || statuses[0].Amount != burn
                || destroyed.Length != 1 || destroyed[0].Tick.Value != 70 || destroyed[0].CardId != card.Id
                || result.Events.ToList().IndexOf(hits[0]) >= result.Events.ToList().IndexOf(statuses[0])
                || result.Events.ToList().IndexOf(statuses[0]) >= result.Events.ToList().IndexOf(destroyed[0])
                || result.Events.OfType<AbilityActivatedEvent>().Count(item => item.SourceCardId == card.Id) != 1
                || !result.States[^1].Cards.Single(item => item.Id == card.Id).Destroyed
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
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new IncendiaryBombCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/incendiary-bomb/card-face.png") != Error.Ok)
                throw new InvalidOperationException("Incendiary bomb card capture failed.");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }
}
