using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Match;
using Project_Star.Application.Factories;
using Project_Star.Content.Heroes;
using Project_Star.Content.Skills;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

// 四种元素联动的等级、窗口边界、消费、刷新、多重及确定性验证（表现层）。
internal static class ElementSequenceChecks
{
    internal static SkillDefinition[] Definitions() =>
        [new FlashFreezeSkillDefinition(), new VaporizeSkillDefinition(), new MeltSkillDefinition(), new ConductivitySkillDefinition()];

    internal static bool WindowsAndResults()
    {
        var catalog = DefinitionRegistry.Scan(typeof(ElementSequenceChecks).Assembly);
        foreach (var definition in Definitions())
        foreach (var level in new[] { 3, 4 })
        foreach (var reverse in new[] { false, true })
        {
            var identity = definition.Attributes.Identity;
            if (identity.FactionKey != new StringName("mona") || definition.InitialLevel != 3
                || definition.SupportsLevel(2) || definition.SupportsLevel(5) || !catalog.Skills.ContainsKey(identity.Key)) return Fail();
            var rule = (ElementSequenceEffectDefinition)definition.GetLevel(level)!.Abilities.Single().Effects.Single();
            var details = CardDisplayAdapter.SkillDetails(MatchDisplayQuery.FromSkill(definition, level));
            if (!details.Contains("下一次") || !details.Contains("触发后消耗窗口") || details.Contains("光环 · 敌方英雄")) return Fail();
            if (rule.WindowTicks != (definition is FlashFreezeSkillDefinition ? level - 2 : level - 1) * 10
                || rule.DamageMultiplier != (definition is VaporizeSkillDefinition or MeltSkillDefinition ? 1.5m : 1m)) return Fail();
            var inside = 10 + rule.WindowTicks - 1;
            var (setup, target, _) = Setup(definition, level, inside, reverse);
            var result = new CombatSimulator().Simulate(setup);
            if (!Check(result, target, rule, inside, 1)
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any()) return Fail();
            // 严格到期边界、前置支付失败、敌方前置、备战前置均不得开启有效窗口。
            foreach (var mode in new[] { "expiry", "unpaid", "enemy", "bench", "wrong", "wrong_follow", "opening", "echo", "source_destroyed" })
            {
                var at = mode == "expiry" ? 10 + rule.WindowTicks : inside;
                var (excluded, card, _) = Setup(definition, level, at, reverse, mode);
                if (!Check(new CombatSimulator().Simulate(excluded), card, rule, at, 0)) return Fail();
            }
            // 两次前置刷新，或前置多重刷新，允许旧窗口到期后的下一次发动触发。
            foreach (var mode in new[] { "refresh", "prefix_multi" })
            {
                var at = 10 + rule.WindowTicks + 1;
                var (refreshed, card, _) = Setup(definition, level, at, reverse, mode);
                if (!Check(new CombatSimulator().Simulate(refreshed), card, rule, at, 1)) return Fail();
            }
            // 同刻FIFO：前置在左则生效，在右则不追溯加成已完成的发动。
            foreach (var mode in new[] { "same_before", "same_after" })
            {
                var (same, card, _) = Setup(definition, level, 10, reverse, mode);
                if (!Check(new CombatSimulator().Simulate(same), card, rule, 10, mode == "same_before" ? 1 : 0)) return Fail();
            }
            var (multi, multiTarget, _) = Setup(definition, level, inside, reverse, "follow_multi");
            if (!Check(new CombatSimulator().Simulate(multi), multiTarget, rule, inside, 1)) return Fail();
            var (sacrificed, sacrificedTarget, _) = Setup(definition, level, inside, reverse, "sacrifice");
            if (!Check(new CombatSimulator().Simulate(sacrificed), sacrificedTarget, rule, inside, 1)) return Fail();
            var (unpaidFollow, freeTarget, _) = Setup(definition, level, inside, reverse, "unpaid_follow");
            if (!Check(new CombatSimulator().Simulate(unpaidFollow), freeTarget, rule, inside, 1)) return Fail();
            var (stacked, stackedTarget, _) = Setup(definition, level, inside, reverse, "stacked");
            if (!Check(new CombatSimulator().Simulate(stacked), stackedTarget, rule, inside, 2)) return Fail();
            var (dual, dualTarget, _) = Setup(definition, level, 10, reverse, "dual");
            var dualResult = new CombatSimulator().Simulate(dual);
            if (!Check(dualResult, dualTarget, rule, 12, 1)) return Fail();
            if (rule.Status is null)
            {
                var (nonAttack, attackTarget, _) = Setup(definition, level, inside, reverse, "nonattack");
                if (!Check(new CombatSimulator().Simulate(nonAttack), attackTarget, rule, inside, 1)) return Fail();
                if (!result.States.Any(s => s.Tick.Value == inside && s.Cards.Single(c => c.Id == target).EffectAmounts[0] == 4)) return Fail();
            }
        }
        return true;
    }

    private static bool Check(BattleResult result, EntityId target, ElementSequenceEffectDefinition rule, int at, int triggers)
    {
        if (rule.Status is { } status)
        {
            var applied = result.Events.OfType<StatusChangedEvent>().Where(e => e.Status == status).ToArray();
            if (applied.Length != triggers || applied.Any(e => e.Tick.Value != at || e.Amount != 10))
            { GD.Print($"状态{status}期望{at}次{triggers}，实际{string.Join(',', applied.Select(e => $"{e.Tick.Value}:{e.Amount}"))}"); return Fail(); }
        }
        else
        {
            var hits = result.Events.OfType<DamageDealtEvent>().Where(e => e.SourceCardId == target).ToArray();
            var expected = triggers == 2 ? 6 : triggers == 1 ? 4 : 3;
            if (!hits.Any(e => e.Tick.Value == at && e.RawDamage == expected)
                || hits.Any(e => e.Tick.Value != at && e.RawDamage != 3))
            { GD.Print($"伤害期望{at}:{expected}，实际{string.Join(',', hits.Select(e => $"{e.Tick.Value}:{e.RawDamage}"))}"); return Fail(); }
        }
        return true;
    }

    private static (BattleSetup Setup, EntityId Target, EntityId Enemy) Setup(SkillDefinition definition, int level,
        int followAt, bool reverse, string mode = "normal")
    {
        var abilities = definition.GetLevel(level)!.Abilities;
        var rule = (ElementSequenceEffectDefinition)abilities.Single().Effects.Single();
        var prefix = Card(0, 10, [rule.PrerequisiteElement], [new RestoreManaEffectDefinition(0)], 100)
            with { IsOnBench = mode == "bench", Multicast = mode == "prefix_multi" ? 1 : 0 };
        if (mode == "wrong") prefix = prefix with { ElementKeys = [GameElements.General] };
        if (mode is "opening" or "echo") prefix = prefix with { Abilities = [new AbilityDefinition(
            new StringName("verification.sequence.excluded"), mode == "opening" ? AbilityActivation.PassiveOnBattleStart : AbilityActivation.EchoOnAbilityActivated,
            AbilityTarget.AlliedHero, 0, 0, [new RestoreManaEffectDefinition(0)])] };
        if (mode == "sacrifice") prefix = prefix with { Abilities = [new AbilityDefinition(
            new StringName("verification.sequence.sacrifice"), AbilityActivation.Active, AbilityTarget.SelfCard,
            100, 10, [new DestroyCardEffectDefinition(false)])] };
        var follow = Card(mode == "same_after" ? 0 : 1, followAt, [rule.FollowupElement], [new DamageEffectDefinition(3)])
            with { Multicast = mode is "follow_multi" or "dual" ? 1 : 0 };
        if (mode == "same_after") prefix = prefix with { BoardStart = 1 };
        if (mode == "wrong_follow") follow = follow with { ElementKeys = [GameElements.General] };
        var enemyCard = Card(0, 1000, [GameElements.General], [new RestoreManaEffectDefinition(0)]);
        var ownCards = new List<CardBattleSetup> { follow };
        var enemyCards = new List<CardBattleSetup> { enemyCard };
        if (mode == "dual") follow = follow with { ElementKeys = [rule.PrerequisiteElement, rule.FollowupElement] };
        if (mode == "dual") ownCards[0] = follow;
        else if (mode == "enemy") enemyCards.Add(prefix with { BoardStart = 1 });
        else ownCards.Add(prefix);
        if (mode == "nonattack") ownCards.Add(Card(2, 11, [rule.FollowupElement], [new RestoreManaEffectDefinition(0)]));
        var target = follow.EntityId;
        if (mode == "unpaid_follow")
        {
            ownCards[0] = follow with { Abilities = [new AbilityDefinition(new StringName("verification.sequence.unpaid_follow"),
                AbilityActivation.Active, AbilityTarget.EnemyHero, 1, followAt, [new DamageEffectDefinition(3)])] };
            var free = Card(2, followAt, [rule.FollowupElement], [new DamageEffectDefinition(3)]);
            ownCards.Add(free); target = free.EntityId;
        }
        var skills = new List<SkillBattleSetup> { new(EntityId.New(), abilities, new Dictionary<StringName, int>()) };
        if (mode == "stacked") skills.Add(new SkillBattleSetup(EntityId.New(), abilities, new Dictionary<StringName, int>()));
        if (mode == "source_destroyed")
        {
            skills.Clear();
            ownCards.Add(Card(4, 11, [GameElements.General], [new DestroyCardEffectDefinition(false)]) with
                { Abilities = [.. abilities, new AbilityDefinition(new StringName("verification.sequence.destroy_source"),
                    AbilityActivation.Active, AbilityTarget.SelfCard, 0, 11, [new DestroyCardEffectDefinition(false)])] });
        }
        var own = new BattleSideSetup(new HeroBattleSetup(new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            10000, 0, MaxMana: 200, InitialMana: mode == "unpaid" ? 99 : mode == "refresh" ? 200 : 100, ManaRegen: 0),
            ownCards, skills);
        var other = new BattleSideSetup(new HeroBattleSetup(new EntityId(Guid.Parse("00000000-0000-0000-0000-000000000002")),
            10000, 0, MaxMana: 200, InitialMana: 100, ManaRegen: 0), enemyCards);
        var end = mode == "dual" ? 14 : mode is "same_before" or "same_after" ? followAt + 1 : followAt * 2 + 1;
        return (new BattleSetup(reverse ? other : own, reverse ? own : other, 42, new BattleTick(end), new BattleTick(end + 1)),
            target, enemyCard.EntityId);
    }

    private static CardBattleSetup Card(int start, int cooldown, StringName[] elements, EffectDefinition[] effects, int mana = 0) =>
        new(EntityId.New(), start, 0, cooldown,
            [new AbilityDefinition(new StringName("verification.sequence.active"), AbilityActivation.Active,
                AbilityTarget.EnemyHero, mana, cooldown, effects)], UseLegacyAttack: false, ElementKeys: elements);
    private static bool Fail([System.Runtime.CompilerServices.CallerLineNumber] int line = 0)
    { GD.Print($"元素联动验证失败，行{line}"); return false; }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(660, 340), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        var root = new Control { Size = new Vector2(660, 340), Theme = MatchTheme.Create() }; viewport.AddChild(root);
        root.AddChild(new ColorRect { Size = root.Size, Color = new Color("33302c") });
        var details = new HeroDetailsView { Theme = MatchTheme.Create() }; owner.AddChild(details);
        var session = new CreateMatchService(new EntityFactory()).Create(42, 0, new MonaHeroDefinition());
        var hidden = new UiAction("", false);
        details.Render(new MatchPageViewModel(MatchPage.EncounterChoice, "元素技能详情", "", MatchSnapshot.From(session), null,
            null, false, false, Array.Empty<KeyedAction>(), Array.Empty<KeyedAction>(), Array.Empty<ShopItemViewModel>(),
            Array.Empty<KeyedAction>(), 0, hidden, hidden, hidden, hidden));
        try
        {
            var definitions = Definitions(); var adapter = new CardDisplayAdapter();
            for (var i = 0; i < definitions.Length; i++)
            {
                var definition = definitions[i];
                using var image = adapter.Artwork(definition.Attributes.Identity.Illustration).GetImage();
                if (image.GetWidth() != image.GetHeight() || !image.HasMipmaps()) throw new InvalidOperationException("元素技能原画比例或mipmap错误。");
                GD.Print($"{definition.Attributes.Identity.DisplayName}原画 {image.GetWidth()}×{image.GetHeight()} mipmaps={image.GetMipmapCount()}");
                foreach (var level in new[] { 3, 4 })
                {
                    var snapshot = MatchDisplayQuery.FromSkill(definition, level);
                    var icon = new SkillItemView { Position = new Vector2(16 + i * 160, 16 + (level - 3) * 160), Size = new Vector2(128, 128) };
                    root.AddChild(icon); icon.Render(snapshot, adapter);
                    details.ShowSkill(snapshot, owner.GetViewport().GetVisibleRect().Size);
                    await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                    await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
                    RenderingServer.ForceDraw(false);
                    using var screenshot = owner.GetViewport().GetTexture().GetImage();
                    screenshot.SavePng($"res://output/element-sequences/{definition.Attributes.Identity.Key.ToString()[6..]}-{level}.png");
                    details.Hide();
                }
            }
            RenderingServer.ForceDraw(false);
            using var grid = viewport.GetTexture().GetImage(); grid.SavePng("res://output/element-sequences/icons.png");
        }
        finally { owner.RemoveChild(details); details.Free(); owner.RemoveChild(viewport); viewport.Free(); }
    }
}
