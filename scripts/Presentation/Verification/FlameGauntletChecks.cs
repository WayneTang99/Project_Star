using Project_Star.Domain.Match;
using System;
using System.Threading.Tasks;
using Project_Star.Application.Economy;
using Project_Star.Application.Match;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;
using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 火焰护腕四级、双方多重伤害与灼伤累加验证（表现层）。
internal static class FlameGauntletChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new FlameGauntletCardDefinition();
        var identity = definition.Attributes.Identity;
        if (!DefinitionRegistry.Scan(typeof(FlameGauntletChecks).Assembly).Cards.ContainsKey(identity.Key)
            || identity.FactionKey != new StringName("mona") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Fire })
            || !definition.Tags.Contains(GameTags.Equipment) || definition.InitialLevel != 1
            || definition.SupportsLevel(5)) return false;
        using var image = GD.Load<Texture2D>(identity.Illustration.ToString()).GetImage();
        if (image.GetWidth() * 2 != image.GetHeight() || !image.HasMipmaps()) return false;
        GD.Print($"火焰护腕原画 {image.GetWidth()}×{image.GetHeight()} mipmaps={image.GetMipmapCount()}");
        for (var level = 1; level <= 4; level++)
        foreach (var reverse in new[] { false, true })
        {
            var card = new EntityFactory().CreateCard(definition, level);
            var values = card.Attributes.BaseCombat;
            var damage = 20 << (level - 1); var burn = 6 << (level - 1);
            if (values.GetFinalValue(GameAttributeKeys.AttackDamage) != damage
                || values.GetFinalValue(GameAttributeKeys.Burn) != burn
                || values.GetFinalValue(GameAttributeKeys.CooldownTicks) != 70
                || values.GetFinalValue(GameAttributeKeys.Multicast) != 1
                || card.Abilities.Any(a => a.ManaCost != 0)) return false;
            var source = new CardBattleSetup(card.Id, 0, damage, 70, card.Abilities,
                UseLegacyAttack: false, Tags: card.Tags, Multicast: 1)
            { CombatValues = values.SnapshotFinalValues(), Level = level };
            BattleSetup Setup(bool bench, int ticks)
            {
                var ally = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), [source with { IsOnBench = bench }]);
                var enemy = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), []);
                return new BattleSetup(reverse ? enemy : ally, reverse ? ally : enemy, 42, new BattleTick(ticks), new BattleTick(ticks + 1));
            }
            var setup = Setup(false, 72);
            var result = new CombatSimulator().Simulate(setup);
            var hits = result.Events.OfType<DamageDealtEvent>().Where(e => e.SourceCardId == card.Id && e.SourceKind == DamageSourceKind.Card).ToArray();
            var burns = result.Events.OfType<StatusChangedEvent>().Where(e => e.Status == BattleStatus.Burn && e.Amount > 0).ToArray();
            if (hits.Length != 2 || hits[0].Tick.Value != 70 || hits[1].Tick.Value != 72
                || hits.Any(e => e.RawDamage != damage) || burns.Length != 2 || burns.Any(e => e.Amount != burn)
                || (reverse ? result.States.Last().Player : result.States.Last().Opponent).Burn != burn * 2 - 1
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                || new CombatSimulator().Simulate(Setup(false, 69)).Events.OfType<AbilityActivatedEvent>().Any()
                || new CombatSimulator().Simulate(Setup(true, 72)).Events.OfType<AbilityActivatedEvent>().Any()) return false;
        }
        return true;
    }
    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(560, 460), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(1050, 460) });
            CardDefinition[] definitions = [new FlameGauntletCardDefinition()];
            for (var index = 0; index < definitions.Length; index++)
            foreach (var compact in new[] { false, true })
            {
                var face = new CardItemView { Position = compact ? new Vector2(260, 24) : new Vector2(20, 20),
                    Size = compact ? new Vector2(100, 200) : new Vector2(200, 400) };
                viewport.AddChild(face);
                face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(definitions[index])), new CardDisplayAdapter());
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw(false);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/flame-gauntlet/card-face.png") != Error.Ok)
                throw new InvalidOperationException("火焰护腕卡面捕获失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }
}
