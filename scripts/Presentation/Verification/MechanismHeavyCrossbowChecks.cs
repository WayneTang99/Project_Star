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

// 机关重弩分级多重与动态相邻冷却验证（表现层验证模块）。
internal static class MechanismHeavyCrossbowChecks
{
    internal static bool LevelsAndMulticast()
    {
        var definition = new MechanismHeavyCrossbowCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.Key != new StringName("card.mechanism_heavy_crossbow") || identity.DisplayName != "机关重弩"
            || identity.FactionKey != new StringName("harla") || identity.Size != CardSize.Large
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Mechanical)
            || definition.InitialLevel != 2 || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(MechanismHeavyCrossbowCardDefinition).Assembly).Cards.ContainsKey(identity.Key)) return false;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (texture is null || texture.GetWidth() * 2 != texture.GetHeight() * 3) return false;
        foreach (var level in new[] { 2, 3, 4 })
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        foreach (var bonus in new[] { 0, 17 })
        {
            var card = Crossbow(level);
            var damage = 80 << (level - 2);
            if (card.AttackDamage != damage || card.CooldownTicks != 120 || card.Multicast != 2
                || card.Abilities!.Any(ability => ability.ManaCost != 0)) return false;
            var boosted = card with { AttackDamage = damage + bonus };
            var setup = side == SideId.Player ? Battle([boosted], [], 240) : Battle([], [boosted], 240);
            var result = new CombatSimulator().Simulate(setup);
            var hits = result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == card.EntityId).ToArray();
            if (!hits.Select(hit => hit.Tick.Value).SequenceEqual(new long[] { 120, 120, 120, 240, 240, 240 })
                || hits.Any(hit => hit.RawDamage != damage + bonus || hit.TargetSide == side)
                || result.Events.OfType<ManaChangedEvent>().Any() || result.PermanentChanges.Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                || State(result, card, 120).CooldownDurationUnits[0] != 240) return false;
            var offer = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            if (!CardDisplayAdapter.FaceEffects(offer).Any(effect => effect.Kind == CardFaceEffectKind.Damage
                    && effect.Value == damage.ToString())
                || !CardDisplayAdapter.Details(offer).Contains("多重2")) return false;
        }
        return true;
    }

    internal static bool AdjacencyAndTransitions()
    {
        var card = Crossbow(2);
        var left = Neighbor(0, 3);
        var right = Neighbor(6, 2);
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        foreach (var neighbors in new CardBattleSetup[][] { [left], [right], [left, right] })
        {
            var allies = new[] { card }.Concat(neighbors).ToArray();
            var result = new CombatSimulator().Simulate(side == SideId.Player ? Battle(allies, [], 200) : Battle([], allies, 200));
            if (!Hits(result, card).SequenceEqual(new long[] { 100, 100, 100, 200, 200, 200 })
                || State(result, card, 0).CooldownDurationUnits[0] != 200) throw new InvalidOperationException("Crossbow: adjacency baseline");
        }
        foreach (var excluded in new[]
        {
            right with { BoardStart = 7 }, right with { IsOnBench = true },
            right with { Tags = new TagSet([GameTags.Equipment]) },
            right with { Abilities = [Ability(AbilityActivation.PassiveOnBattleStart, 0, new DestroyCardEffectDefinition(false))] },
        })
            if (!Hits(new CombatSimulator().Simulate(Battle([card, excluded], [], 120)), card)
                .SequenceEqual(new long[] { 120, 120, 120 })) throw new InvalidOperationException("Crossbow: excluded neighbor");
        if (!Hits(new CombatSimulator().Simulate(Battle([card], [right], 120)), card)
            .SequenceEqual(new long[] { 120, 120, 120 })) throw new InvalidOperationException("Crossbow: enemy isolation");
        var dying = right with { Abilities = [Ability(AbilityActivation.Active, 50, new DestroyCardEffectDefinition(false))] };
        var lost = new CombatSimulator().Simulate(Battle([card, dying], [], 240));
        if (!Hits(lost, card).SequenceEqual(new long[] { 120, 120, 120, 240, 240, 240 })
            || State(lost, card, 50).CooldownUnits[0] != 140 || State(lost, card, 50).CooldownDurationUnits[0] != 240)
            throw new InvalidOperationException($"Crossbow: lost neighbor hits={string.Join(',', Hits(lost, card))}, remaining={State(lost, card, 50).CooldownUnits[0]}");
        // 一侧失效但另一侧仍有机械时，不撤销相邻条件。
        if (!Hits(new CombatSimulator().Simulate(Battle([card, left, dying], [], 100)), card)
            .SequenceEqual(new long[] { 100, 100, 100 })) throw new InvalidOperationException("Crossbow: surviving neighbor");
        var untagged = Neighbor(6, 1) with
        {
            Tags = new TagSet([]),
            Abilities = [Ability(AbilityActivation.Active, 50, new HealEffectDefinition(1))],
            Quests = [new CardQuestDefinition("verification.crossbow.unlock_tag", new SourceCardActivationQuestConditionDefinition(),
                1, [], unlockedTags: [GameTags.Mechanical])],
        };
        var tagger = Neighbor(0, 1) with
        {
            Abilities = [Ability(AbilityActivation.PassiveAura, 0, new GrantTagToEnemySizeCardsEffectDefinition(CardSize.Small, GameTags.Mechanical))],
        };
        var gained = new CombatSimulator().Simulate(Battle([card, untagged], [], 100));
        if (!Hits(gained, card).SequenceEqual(new long[] { 100, 100, 100 })
            || State(gained, card, 50).CooldownUnits[0] != 100 || State(gained, card, 50).CooldownDurationUnits[0] != 200)
            throw new InvalidOperationException($"Crossbow: gained tag hits={string.Join(',', Hits(gained, card))}, remaining={State(gained, card, 50).CooldownUnits[0]}");
        var granted = untagged with { Abilities = [], Quests = [] };
        if (!Hits(new CombatSimulator().Simulate(Battle([card, granted], [tagger], 100)), card)
            .SequenceEqual(new long[] { 100, 100, 100 })) return false;
        if (Hits(new CombatSimulator().Simulate(Battle([card with { IsOnBench = true }, right], [], 240)), card).Any()) return false;
        var destroyed = card with { Multicast = 0, Abilities = card.Abilities!.Append(
            Ability(AbilityActivation.PassiveOnBattleStart, 0, new DestroyCardEffectDefinition(false))).ToArray() };
        if (Hits(new CombatSimulator().Simulate(Battle([destroyed, right], [], 240)), card).Any()) return false;
        // 同一可复用光环支持其他标签；无相邻卡牌时自身标签不满足条件。
        var generic = card with { Abilities = [card.Abilities![0], new AbilityDefinition("verification.crossbow.other_tag",
            AbilityActivation.PassiveAura, AbilityTarget.SelfCard, 0, 0,
            [new ReduceSourceCooldownWhileAdjacentTaggedCardEffectDefinition(GameTags.Equipment, 20)])] };
        if (!Hits(new CombatSimulator().Simulate(Battle([generic, right with { Tags = new TagSet([GameTags.Equipment]) }], [], 100)), card)
            .SequenceEqual(new long[] { 100, 100, 100 })) return false;
        foreach (var invalid in new[]
        {
            (new StringName(), 20, AbilityActivation.PassiveAura, AbilityTarget.SelfCard, false),
            (GameTags.Mechanical, 0, AbilityActivation.PassiveAura, AbilityTarget.SelfCard, false),
            (GameTags.Mechanical, 20, AbilityActivation.Active, AbilityTarget.SelfCard, false),
            (GameTags.Mechanical, 20, AbilityActivation.PassiveAura, AbilityTarget.EnemyHero, false),
            (GameTags.Mechanical, 20, AbilityActivation.PassiveAura, AbilityTarget.SelfCard, true),
        })
        {
            try
            {
                _ = new AbilityDefinition("verification.crossbow.invalid", invalid.Item3, invalid.Item4, 0, 0,
                    [new ReduceSourceCooldownWhileAdjacentTaggedCardEffectDefinition(invalid.Item1, invalid.Item2)], allowsBench: invalid.Item5);
                return false;
            }
            catch (ArgumentException) { }
        }
        return State(new CombatSimulator().Simulate(Battle([card], [], 120)), card, 0).CooldownDurationUnits[0] == 240;
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(400, 320), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            var background = new ColorRect { Color = new Color("333c41"), Size = new Vector2(400, 320) };
            viewport.AddChild(background);
            var face = new CardItemView { Position = new Vector2(56, 22), Size = new Vector2(288, 276) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new MechanismHeavyCrossbowCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/mechanism-heavy-crossbow/card-face.png") != Error.Ok)
                throw new InvalidOperationException("Crossbow: card capture failed");
            GD.Print($"机关重弩：{LevelsAndMulticast() && AdjacencyAndTransitions()}");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardBattleSetup Crossbow(int level)
    {
        var instance = new EntityFactory().CreateCard(new MechanismHeavyCrossbowCardDefinition(), level);
        return new CardBattleSetup(instance.Id, 3, instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
            instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.CooldownTicks), instance.Abilities,
            Tags: instance.Tags, Multicast: instance.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.Multicast),
            OccupiedSlots: 3, ElementKeys: instance.Attributes.Identity.ElementKeys) { Level = level };
    }

    private static CardBattleSetup Neighbor(int start, int size) => new(EntityId.New(), start, 0, 0, [],
        UseLegacyAttack: false, Tags: new TagSet([GameTags.Mechanical]), OccupiedSlots: size);
    private static AbilityDefinition Ability(AbilityActivation activation, int ticks, EffectDefinition effect) =>
        new("verification.crossbow.transition", activation, AbilityTarget.SelfCard, 0, ticks, [effect]);
    private static long[] Hits(BattleResult result, CardBattleSetup card) => result.Events.OfType<DamageDealtEvent>()
        .Where(hit => hit.SourceCardId == card.EntityId).Select(hit => hit.Tick.Value).ToArray();
    private static CardBattleSnapshot State(BattleResult result, CardBattleSetup card, long tick) =>
        result.States.Last(frame => frame.Tick.Value == tick).Cards.Single(snapshot => snapshot.Id == card.EntityId);
    private static BattleSetup Battle(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), cards),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), enemies),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
