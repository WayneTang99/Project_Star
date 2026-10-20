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
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 巫师装备分级、实际魔法支付回响、再生叠加与原画验证（表现层）。
internal static class WizardEquipmentChecks
{
    internal static bool LevelsAndCombat()
    {
        var hat = new WizardHatCardDefinition();
        var robe = new WizardRobeCardDefinition();
        var registry = DefinitionRegistry.Scan(typeof(WizardEquipmentChecks).Assembly);
        foreach (var definition in new CardDefinition[] { hat, robe })
        {
            var identity = definition.Attributes.Identity;
            if (!registry.Cards.ContainsKey(identity.Key) || definition.InitialLevel != 1
                || identity.FactionKey != new StringName("mona") || identity.Size != CardSize.Medium
                || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
                || !definition.Tags.Contains(GameTags.Equipment) || definition.SupportsLevel(5)) return false;
            using var image = GD.Load<Texture2D>(identity.Illustration.ToString()).GetImage();
            if (image.GetWidth() != image.GetHeight() || !image.HasMipmaps()) return false;
            GD.Print($"{identity.DisplayName} {image.GetWidth()}×{image.GetHeight()} mipmaps={image.GetMipmapCount()}");
        }
        foreach (var reverse in new[] { false, true })
        for (var level = 1; level <= 4; level++)
        {
            var mana = 20 << (level - 1); var regeneration = 1 << (level - 1);
            foreach (var definition in new CardDefinition[] { hat, robe })
            {
                var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
                var expected = (definition == hat ? regeneration : regeneration * 4).ToString();
                if (!CardDisplayAdapter.FaceEffects(snapshot).Any(e => e.Kind == CardFaceEffectKind.ManaRegen && e.Value == expected)) return false;
            }
            var hatCard = Card(hat, level, 0); var robeCard = Card(robe, level, 2);
            var restored = Run([hatCard], [], 50, 0, reverse);
            if (Hero(restored, reverse).Mana != mana || Hero(restored, reverse).ManaRegen != 0
                || restored.Events.OfType<StatusChangedEvent>().Any()) return false;
            var capped = Run([hatCard], [], 50, 990, reverse);
            if (Hero(capped, reverse).Mana != 1000) return false;
            var gained = Run([robeCard], [], 120, 80, reverse);
            if (gained.Events.OfType<ManaChangedEvent>().Count(e => e.Amount == -40) != 2
                || gained.Events.OfType<StatusChangedEvent>().Count(e => e.Status == BattleStatus.ManaRegen && e.Amount == regeneration * 4) != 2
                || Hero(gained, reverse).ManaRegen != regeneration * 8
                || Hero(gained, reverse).Mana != regeneration * 24) return false;
            var insufficient = Run([robeCard], [], 60, 39, reverse);
            if (insufficient.Events.OfType<AbilityActivatedEvent>().Any()
                || insufficient.Events.OfType<ManaChangedEvent>().Any() || Hero(insufficient, reverse).ManaRegen != 0) return false;
            var combined = Run([hatCard, robeCard], [], 60, 40, reverse);
            if (Hero(combined, reverse).ManaRegen != regeneration * 5
                || combined.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == hatCard.EntityId && e.IsEcho) != 1) return false;

            // 多重、敌方发动、备战来源及零消耗均不能伪造支付事件；每次真实支付只回响一次。
            var spender = new CardBattleSetup(EntityId.New(), 4, 0, 20,
                [new AbilityDefinition(new StringName("verification.spend_mana"), AbilityActivation.Active,
                    AbilityTarget.AlliedHero, 10, 20, [new RestoreManaEffectDefinition(0)])],
                UseLegacyAttack: false, Multicast: 2);
            var enemy = spender with { EntityId = EntityId.New(), BoardStart = 0 };
            var setup = Setup([hatCard, spender], [enemy], 45, 100, reverse);
            var spending = new CombatSimulator().Simulate(setup);
            if (spending.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == hatCard.EntityId && e.IsEcho) != 2
                || Hero(spending, reverse).ManaRegen != regeneration * 2
                || !spending.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            if (Run([hatCard with { IsOnBench = true }, spender], [], 25, 100, reverse)
                .Events.OfType<StatusChangedEvent>().Any()) return false;
            if (Run([hatCard, spender], [], 25, 9, reverse).Events.OfType<StatusChangedEvent>().Any()) return false;
            var triggered = spender with { Abilities = [new AbilityDefinition(new StringName("verification.trigger_spender"),
                AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0,
                [new ActivateSourceCardEffectDefinition()]), .. spender.Abilities!] };
            if (Run([hatCard, triggered], [], 5, 100, reverse).Events.OfType<StatusChangedEvent>().Any()) return false;
        }
        return true;
    }

    private static HeroBattleSnapshot Hero(BattleResult result, bool reverse) => reverse ? result.States.Last().Opponent : result.States.Last().Player;

    private static CardBattleSetup Card(CardDefinition definition, int level, int start)
    {
        var card = new EntityFactory().CreateCard(definition, level);
        return new CardBattleSetup(card.Id, start, 0,
            card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks), card.Abilities,
            UseLegacyAttack: false, Tags: card.Tags, OccupiedSlots: 2) { Level = level };
    }

    private static BattleResult Run(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks, int mana, bool reverse)
        => new CombatSimulator().Simulate(Setup(allies, enemies, ticks, mana, reverse));

    private static BattleSetup Setup(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks, int mana, bool reverse)
    {
        var allied = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, 1000, mana, 0), allies);
        var enemy = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, 1000, 100, 0), enemies);
        return new BattleSetup(reverse ? enemy : allied, reverse ? allied : enemy, 42, new BattleTick(ticks), new BattleTick(ticks + 1));
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(1050, 460), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(1050, 460) });
            CardDefinition[] definitions = [new WizardHatCardDefinition(), new WizardRobeCardDefinition()];
            for (var index = 0; index < definitions.Length; index++)
            foreach (var compact in new[] { false, true })
            {
                var face = new CardItemView { Position = compact ? new Vector2(860, 24 + index * 200) : new Vector2(20 + index * 420, 20),
                    Size = compact ? new Vector2(150, 150) : new Vector2(400, 400) };
                viewport.AddChild(face);
                face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(definitions[index])), new CardDisplayAdapter());
            }
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/wizard-equipment/card-face.png") != Error.Ok)
                throw new InvalidOperationException("巫师装备卡面捕获失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }
}
