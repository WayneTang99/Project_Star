using System;
using System.Threading.Tasks;
using Project_Star.Application.Economy;
using Project_Star.Application.Match;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Game;
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

internal static class OrderArcanistChecks
{
    internal static bool LevelsManaAndHealing()
    {
        var definition = new OrderArcanistCardDefinition();
        var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        using var image = texture?.GetImage();
        if (identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Light })
            || !definition.Tags.Contains(GameTags.Human) || definition.InitialLevel != 2
            || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || image is null || image.GetHeight() != image.GetWidth() * 2 || !image.HasMipmaps()
            || !DefinitionRegistry.Scan(typeof(OrderArcanistChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        foreach (var (level, damage, healing) in new[] { (2, 80, 40), (3, 160, 80), (4, 320, 160) })
        foreach (var reverse in new[] { false, true })
        foreach (var mana in new[] { 29, 30, 60 })
        {
            var card = new EntityFactory().CreateCard(definition, level);
            var values = card.Attributes.BaseCombat.SnapshotFinalValues();
            if (values[GameAttributeKeys.AttackDamage] != damage || values[GameAttributeKeys.HealingBonus] != healing
                || values[GameAttributeKeys.CooldownTicks] != 50 || card.Abilities.Single().ManaCost != 30) return false;
            var source = new CardBattleSetup(card.Id, 0, damage, 50, card.Abilities, UseLegacyAttack: false,
                Tags: card.Tags, ElementKeys: identity.ElementKeys) { CombatValues = values, Level = level };
            var allies = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0,
                InitialMana: mana, ManaRegen: 0, Poison: 100), [source]);
            var enemies = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), []);
            var setup = new BattleSetup(reverse ? enemies : allies, reverse ? allies : enemies,
                42, new BattleTick(101), new BattleTick(102));
            var result = new CombatSimulator().Simulate(setup);
            var frame = result.States.Last(state => state.Tick.Value == 100);
            var own = reverse ? frame.Opponent : frame.Player;
            var enemy = reverse ? frame.Player : frame.Opponent;
            var count = mana / 30;
            var hits = result.Events.OfType<DamageDealtEvent>().Where(item => item.SourceCardId == card.Id).ToArray();
            if (hits.Length != count || hits.Any(hit => hit.RawDamage != damage)
                || (count > 0 && hits[0].Tick.Value != 50) || (count == 2 && hits[1].Tick.Value != 100)
                || own.Health != 9000 + healing * count || enemy.Health != 10000 - damage * count
                || own.Mana != mana - 30 * count
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
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
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new OrderArcanistCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/order-arcanist/card-face.png") != Error.Ok)
                throw new InvalidOperationException("教团秘法师卡面截图失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }
}


