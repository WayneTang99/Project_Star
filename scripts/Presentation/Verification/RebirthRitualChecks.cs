using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Content;
using Project_Star.Content.Skills;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

// 复生仪式被摧毁事件、等级继承、原位召唤与战斗隔离验证（表现层）。
internal static class RebirthRitualChecks
{
    internal static bool SummonsAndFilters()
    {
        var definition = new RebirthRitualSkillDefinition();
        var identity = definition.Attributes.Identity;
        var registry = DefinitionRegistry.Scan(typeof(RebirthRitualChecks).Assembly);
        var skill = new SkillBattleSetup(EntityId.New(), definition.GetLevel(4)!.Abilities,
            definition.GetLevel(4)!.BaseCombatValues);
        if (identity.FactionKey != new StringName("mona") || definition.InitialLevel != 4
            || definition.SupportsLevel(3) || definition.SupportsLevel(5) || !registry.Skills.ContainsKey(identity.Key)) return false;
        var text = CardDisplayAdapter.SkillDetails(SkillCatalogQuery.Capture(registry).Single(entry => entry.InitialSkill.Key == identity.Key).InitialSkill);
        if (!text.Contains("小型") || !text.Contains("原位置") || !text.Contains("【骷髅】") || text.Contains("己方英雄")) return false;
        GD.Print(text);
        using var image = GD.Load<Texture2D>(identity.Illustration.ToString()).GetImage();
        if (image.GetWidth() != image.GetHeight() || !image.HasMipmaps()) return false;
        GD.Print($"复生仪式原画 {image.GetWidth()}×{image.GetHeight()} mipmaps={image.GetMipmapCount()}");
        foreach (var reverse in new[] { false, true })
        foreach (var level in new[] { 1, 2, 3, 4 })
        {
            var victim = Victim(0, 1, level);
            var setup = Setup([victim], [], [skill], 45, reverse);
            var result = new CombatSimulator().Simulate(setup);
            var summon = result.Events.OfType<CardSummonedEvent>().Single();
            var attack = result.Events.OfType<DamageDealtEvent>().Single(e => e.SourceCardId == summon.SummonedCardId);
            if (summon.Level != level || summon.BoardStart != 0 || summon.CardKey != new StringName("card.skeleton")
                || summon.SourceCardId != skill.EntityId || summon.Tick.Value != 10
                || attack.Tick.Value != 40 || attack.RawDamage != level * 10
                || result.PermanentChanges.Count != 0 || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            foreach (var size in new[] { 2, 3 })
                if (Run([Victim(0, size, level)], [], [skill], 15, reverse).Events.OfType<CardSummonedEvent>().Any()) return false;
            if (Run([], [victim], [skill], 15, reverse).Events.OfType<CardSummonedEvent>().Any()
                || Run([victim with { IsOnBench = true }], [], [skill], 15, reverse).Events.OfType<CardSummonedEvent>().Any()
                || Run([victim], [], [], 15, reverse).Events.OfType<CardSummonedEvent>().Any()) return false;
            // 同一位置两个技能均触发，只有首个召唤能占据位置。
            var duplicate = Run([victim], [], [skill, skill with { EntityId = EntityId.New() }], 15, reverse);
            if (duplicate.Events.OfType<CardSummonedEvent>().Count() != 1) return false;
            var mass = new CardBattleSetup(EntityId.New(), 0, 0, 20,
                [new AbilityDefinition("verification.ritual.mass", AbilityActivation.Active, AbilityTarget.SelfCard, 0, 20,
                    [new DestroyBattlefieldCardsEffectDefinition(Project_Star.Domain.Definitions.CardSize.Small)])],
                UseLegacyAttack: false, OccupiedSlots: 3);
            var repeated = Run([victim], [mass], [skill], 45, reverse);
            if (!repeated.Events.OfType<CardSummonedEvent>().Select(e => e.Tick.Value).SequenceEqual(new long[] { 10, 20, 40 })) return false;
            // 回响导致的自毁不再触发复生回响。
            var echoVictim = victim with { Abilities = [new AbilityDefinition("verification.ritual.active", AbilityActivation.Active,
                AbilityTarget.AlliedHero, 0, 10, [new HealEffectDefinition(1)]),
                new AbilityDefinition("verification.ritual.echo_destroy", AbilityActivation.EchoOnSourceCardActivated,
                    AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])] };
            if (Run([echoVictim], [], [skill], 15, reverse).Events.OfType<CardSummonedEvent>().Any()) return false;
        }
        return true;
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(620, 270), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(620, 270) });
            var snapshot = SkillCatalogQuery.Capture(DefinitionRegistry.Scan(typeof(RebirthRitualChecks).Assembly))
                .Single(entry => entry.InitialSkill.Key == new StringName("skill.rebirth_ritual")).InitialSkill;
            foreach (var compact in new[] { false, true })
            {
                var icon = new SkillItemView { Position = compact ? new Vector2(250, 20) : new Vector2(20, 20),
                    Size = compact ? new Vector2(64, 64) : new Vector2(200, 200) };
                viewport.AddChild(icon); icon.Render(snapshot, new CardDisplayAdapter());
            }
            var label = new RichTextLabel { Position = new Vector2(250, 100), Size = new Vector2(350, 150),
                Text = CardDisplayAdapter.SkillDetails(snapshot) }; viewport.AddChild(label);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            RenderingServer.ForceDraw(false);
            using var image = viewport.GetTexture().GetImage();
            image.SavePng("res://output/rebirth-ritual/skill-preview.png");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardBattleSetup Victim(int start, int size, int level) => new(EntityId.New(), start, 0, 10,
        [new AbilityDefinition("verification.ritual.consume", AbilityActivation.Active, AbilityTarget.SelfCard,
            0, 10, [new DestroyCardEffectDefinition(false)])], UseLegacyAttack: false, OccupiedSlots: size) { Level = level };

    private static BattleResult Run(CardBattleSetup[] allies, CardBattleSetup[] enemies, SkillBattleSetup[] skills, int ticks, bool reverse) =>
        new CombatSimulator().Simulate(Setup(allies, enemies, skills, ticks, reverse));

    private static BattleSetup Setup(CardBattleSetup[] allies, CardBattleSetup[] enemies, SkillBattleSetup[] skills, int ticks, bool reverse) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), reverse ? enemies : allies, reverse ? [] : skills),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), reverse ? allies : enemies, reverse ? skills : []),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
