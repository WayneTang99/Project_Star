using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Content;
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
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

// 双头骷髅分级多重、原位分裂及原画接入验证（表现层）。
internal static class TwoHeadedSkeletonChecks
{
    internal static bool LevelsAndDestruction()
    {
        var definition = new TwoHeadedSkeletonCardDefinition();
        var identity = definition.Attributes.Identity;
        Require(identity.DisplayName == "双头骷髅" && identity.FactionKey == new StringName("mona")
            && identity.Size == CardSize.Medium && identity.ElementKeys.SequenceEqual(new[] { GameElements.Dark })
            && definition.Tags.Contains(GameTags.Undead) && definition.InitialLevel == 2
            && !definition.SupportsLevel(1) && !definition.SupportsLevel(5)
            && DefinitionRegistry.Scan(typeof(TwoHeadedSkeletonChecks).Assembly).Cards.ContainsKey(identity.Key), "身份");
        foreach (var level in new[] { 2, 3, 4 })
        foreach (var reverse in new[] { false, true })
        {
            var instance = new EntityFactory().CreateCard(definition, level);
            var values = instance.Attributes.BaseCombat.SnapshotFinalValues();
            var damage = 60 << (level - 2);
            Require(values[GameAttributeKeys.AttackDamage] == damage && values[GameAttributeKeys.CooldownTicks] == 70
                && values[GameAttributeKeys.Multicast] == 1 && instance.Abilities.All(a => a.ManaCost == 0), "等级属性");
            var source = new CardBattleSetup(instance.Id, 3, damage, 70, instance.Abilities,
                UseLegacyAttack: false, Tags: instance.Tags, Multicast: 1, OccupiedSlots: 2,
                ElementKeys: identity.ElementKeys) { Level = level, Identity = identity, CombatValues = values };
            var active = Battle([source], reverse, 72);
            var hits = new CombatSimulator().Simulate(active).Events.OfType<DamageDealtEvent>().ToArray();
            Require(hits.Length == 2 && hits.All(hit => hit.RawDamage == damage)
                && hits.Select(hit => hit.Tick.Value).SequenceEqual(new long[] { 70, 72 }), "7秒多重");
            Require(!new CombatSimulator().Simulate(Battle([source], reverse, 69)).Events.OfType<DamageDealtEvent>().Any(), "冷却前");
            var dying = source with { Abilities = source.Abilities!.Append(new AbilityDefinition("verification.split.destroy",
                AbilityActivation.Active, AbilityTarget.SelfCard, 0, 71, [new DestroyCardEffectDefinition(false)])).ToArray() };
            var setup = Battle([dying], reverse, 103);
            var result = new CombatSimulator().Simulate(setup);
            var summons = result.Events.OfType<CardSummonedEvent>().ToArray();
            Require(summons.Length == 2 && summons.Select(e => e.BoardStart).SequenceEqual(new[] { 3, 4 })
                && summons.All(e => e.CardKey == new StringName("card.skeleton") && e.Level == level && e.SourceCardId == source.EntityId)
                && summons.Select(e => e.SummonedCardId).Distinct().Count() == 2, "两张同级原位召唤");
            Require(result.Events.OfType<DamageDealtEvent>().Count(e => e.SourceCardId == source.EntityId) == 1, "摧毁取消后续多重");
            Require(summons.All(s => result.Events.OfType<DamageDealtEvent>().Any(hit => hit.SourceCardId == s.SummonedCardId
                && hit.RawDamage == level * 10 && hit.Tick.Value == 101)), "召唤物实际发动");
            Require(result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                && !result.PermanentChanges.Any() && !result.PermanentAttributeBonuses.Any(), "确定性与战后隔离");
            Require(!new CombatSimulator().Simulate(Battle([dying with { IsOnBench = true }], reverse, 103))
                .Events.OfType<CardSummonedEvent>().Any(), "备战不触发");
            // 同一位置先被其他召唤占据时不覆盖；后一格仍可独立放置。
            var contested = dying with { Abilities = dying.Abilities!.Select(a =>
                a.Activation == AbilityActivation.EchoOnSourceDestroyed
                    ? new AbilityDefinition("verification.split.contested", AbilityActivation.EchoOnSourceDestroyed,
                        AbilityTarget.SelfCard, 0, 0,
                        [new SummonAtDestroyedCardEffectDefinition(new SkeletonCardDefinition()),
                         new SummonAtDestroyedCardEffectDefinition(new SkeletonCardDefinition(), Count: 2)]) : a).ToArray() };
            Require(new CombatSimulator().Simulate(Battle([contested], reverse, 103)).Events.OfType<CardSummonedEvent>()
                .Select(e => e.BoardStart).SequenceEqual(new[] { 3, 4 }), "占位不覆盖且后格独立");
            var sweeper = new CardBattleSetup(EntityId.New(), 0, 0, 10,
                [new AbilityDefinition("verification.split.sweep", AbilityActivation.Active, AbilityTarget.SelfCard,
                    0, 10, [new DestroyBattlefieldCardsEffectDefinition(CardSize.Medium)])], UseLegacyAttack: false);
            Require(new CombatSimulator().Simulate(Battle([source, sweeper], reverse, 15)).Events
                .OfType<CardSummonedEvent>().Count() == 2, "外部摧毁只分裂一次");
        }
        return true;
    }

    private static BattleSetup Battle(CardBattleSetup[] own, bool reverse, int ticks)
    {
        var ally = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), own);
        var enemy = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), []);
        return new BattleSetup(reverse ? enemy : ally, reverse ? ally : enemy, 42, new BattleTick(ticks), new BattleTick(ticks + 1));
    }

    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException("双头骷髅验证失败：" + message); }

    internal static async Task Capture(Node owner)
    {
        foreach (var definition in new CardDefinition[] { new BonebreakerMageCardDefinition(), new TwoHeadedSkeletonCardDefinition() })
        {
            var directory = definition is BonebreakerMageCardDefinition ? "res://output/bonebreaker-mage" : "res://output/two-headed-skeleton";
            DirAccess.MakeDirRecursiveAbsolute(directory);
            var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition));
            var adapter = new CardDisplayAdapter();
            using var raw = adapter.Artwork(snapshot.Illustration).GetImage();
            Require(raw.HasMipmaps() && raw.GetWidth() == raw.GetHeight(), "原画比例及实际mipmap");
            GD.Print($"{snapshot.DisplayName} {raw.GetWidth()}×{raw.GetHeight()} mipmaps={raw.GetMipmapCount()}");
            var viewport = new SubViewport { Size = new Vector2I(420, 260), Disable3D = true,
                RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
            var preview = new CardPreviewView { Size = owner.GetViewport().GetVisibleRect().Size, Theme = MatchTheme.Create() };
            owner.AddChild(preview);
            try
            {
                viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(420, 260) });
                foreach (var (position, size) in new[] { (new Vector2(20, 20), new Vector2(200, 200)),
                    (new Vector2(270, 20), new Vector2(100, 100)) })
                {
                    var face = new CardItemView { Position = position, Size = size };
                    viewport.AddChild(face); face.Render(snapshot, adapter);
                }
                var registry = DefinitionRegistry.Scan(typeof(TwoHeadedSkeletonChecks).Assembly);
                preview.SetQuery(new CardPreviewQuery(registry));
                preview.SetEntries(CardCatalogQuery.Capture(registry));
                preview.Open(snapshot, null, null);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                RenderingServer.ForceDraw();
                using var compact = viewport.GetTexture().GetImage();
                using var detail = owner.GetViewport().GetTexture().GetImage();
                Require(compact.SavePng($"{directory}/card-face.png") == Error.Ok
                    && detail.SavePng($"{directory}/preview.png") == Error.Ok, "原生截图");
            }
            finally { owner.RemoveChild(viewport); viewport.Free(); owner.RemoveChild(preview); preview.Free(); }
        }
    }
}
