using System;
using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Application.Content;
using Project_Star.Application.Match;
using Project_Star.Domain.Match;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

internal static class LightningConductorStaffChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new LightningConductorStaffCardDefinition();
        var identity = definition.Attributes.Identity;
        Require(identity.DisplayName == "引雷手杖" && identity.FactionKey == new StringName("mona")
            && identity.Size == CardSize.Medium && identity.ElementKeys.SequenceEqual(new[] { GameElements.Lightning })
            && definition.Tags.Contains(GameTags.Equipment) && definition.InitialLevel == 2 && !definition.SupportsLevel(1)
            && DefinitionRegistry.Scan(typeof(LightningConductorStaffChecks).Assembly).Cards.ContainsKey(identity.Key), "身份注册");
        foreach (var level in new[] { 2, 3, 4 })
        foreach (var reverse in new[] { false, true })
        {
            var instance = new EntityFactory().CreateCard(definition, level);
            var damage = 60 << (level - 2);
            var values = instance.Attributes.BaseCombat.SnapshotFinalValues();
            Require(values[GameAttributeKeys.AttackDamage] == damage && values[GameAttributeKeys.CooldownTicks] == 60
                && instance.Abilities.Single(a => a.Activation == AbilityActivation.Active).ManaCost == 40, "等级与消耗");
            var source = new CardBattleSetup(instance.Id, 0, damage, 60, instance.Abilities, UseLegacyAttack: false,
                Tags: instance.Tags, OccupiedSlots: 2, ElementKeys: identity.ElementKeys)
                { Level = level, Identity = identity, CombatValues = values };
            var equipment = new CardBattleSetup(EntityId.New(), 0, 0, 0, [], UseLegacyAttack: false, Tags: new TagSet([GameTags.Equipment]));
            foreach (var count in new[] { 0, 1, 2 })
            {
                var enemies = Enumerable.Range(0, count).Select(i => equipment with { EntityId = EntityId.New(), BoardStart = i }).ToArray();
                var setup = Battle(source, enemies, reverse, 120, 80);
                var result = new CombatSimulator().Simulate(setup);
                var hits = result.Events.OfType<DamageDealtEvent>().Where(e => e.SourceCardId == source.EntityId).ToArray();
                Require(hits.Length == 2 && hits.All(e => e.RawDamage == damage * (count > 0 ? 2 : 1))
                    && hits.Select(e => e.Tick.Value).SequenceEqual(new long[] { 60, 120 }), "6秒伤害与单次翻倍");
                Require(result.Events.OfType<ManaChangedEvent>().Count(e => e.Amount == -40) == 2, "两次支付");
                Require(result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                    && !result.PermanentAttributeBonuses.Any(), "确定性与无永久写回");
            }
            foreach (var excluded in new[] { equipment with { IsOnBench = true }, equipment with { Tags = new TagSet([]) } })
                Require(Hits(source, [excluded], reverse).Single().RawDamage == damage, "备战与非装备排除");
            Require(Hits(source, [], reverse, 39).Length == 0 && Hits(source, [], reverse, 40).Length == 1, "魔法不足不发动");
            Require(Hits(source with { IsOnBench = true }, [equipment], reverse).Length == 0, "来源备战");
            var dying = equipment with { Abilities = [new AbilityDefinition("verification.equipment.destroy", AbilityActivation.Active,
                AbilityTarget.SelfCard, 0, 90, [new DestroyCardEffectDefinition(false)])] };
            var dynamicHits = new CombatSimulator().Simulate(Battle(source, [dying], reverse, 120, 80)).Events
                .OfType<DamageDealtEvent>().Where(e => e.SourceCardId == source.EntityId).ToArray();
            Require(dynamicHits.Select(e => e.RawDamage).SequenceEqual(new[] { damage * 2, damage }), "装备摧毁后恢复");
            var allyOnly = source with { Abilities = source.Abilities!.Append(new AbilityDefinition("verification.own.equipment",
                AbilityActivation.Active, AbilityTarget.SelfCard, 0, 90, [new DestroyCardEffectDefinition(false)])).ToArray() };
            Require(Hits(allyOnly, [], reverse).Single().RawDamage == damage, "己方装备不满足敌方条件");
        }
        return true;
    }

    private static DamageDealtEvent[] Hits(CardBattleSetup source, CardBattleSetup[] enemy, bool reverse, int mana = 80) =>
        new CombatSimulator().Simulate(Battle(source, enemy, reverse, 60, mana)).Events.OfType<DamageDealtEvent>()
            .Where(e => e.SourceCardId == source.EntityId).ToArray();

    private static BattleSetup Battle(CardBattleSetup source, CardBattleSetup[] enemy, bool reverse, int ticks, int mana)
    {
        var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, InitialMana: mana, ManaRegen: 0), [source]);
        var other = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), enemy);
        return new BattleSetup(reverse ? other : own, reverse ? own : other, 42, new BattleTick(ticks), new BattleTick(ticks + 1));
    }

    private static void Require(bool value, string message)
    { if (!value) throw new InvalidOperationException("引雷手杖验证失败：" + message); }
    internal static async System.Threading.Tasks.Task Capture(Node owner)
    {
        foreach (var definition in new CardDefinition[] { new LightningConductorStaffCardDefinition() })
        {
            var directory = "res://output/lightning-conductor-staff";
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
                var registry = DefinitionRegistry.Scan(typeof(LightningConductorStaffChecks).Assembly);
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
