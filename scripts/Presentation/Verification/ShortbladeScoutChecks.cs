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

// 短刃斥候分级、疾速获得回响与连锁截断验证（表现层）。
internal static class ShortbladeScoutChecks
{
    internal static bool LevelsAndCooldown()
    {
        var definition = new ShortbladeScoutCardDefinition();
        var identity = definition.Attributes.Identity;
        if (identity.Key != new StringName("card.shortblade_scout") || identity.DisplayName != "短刃斥候"
            || identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Small
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Human)
            || definition.InitialLevel != 2 || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(ShortbladeScoutCardDefinition).Assembly).Cards.ContainsKey(identity.Key)) return false;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (texture is null || texture.GetWidth() * 2 != texture.GetHeight()) return false;
        foreach (var level in new[] { 2, 3, 4 })
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        {
            var scout = Scout(level);
            var damage = 20 << (level - 2);
            var result = Simulate(scout, side, 55);
            var hits = Hits(result, scout);
            if (scout.AttackDamage != damage || scout.CooldownTicks != 30
                || !hits.Select(hit => hit.Tick.Value).SequenceEqual(new long[] { 30, level == 2 ? 50 : 45 })
                || hits.Any(hit => hit.RawDamage != damage || hit.TargetSide == side)
                || result.Events.OfType<StatusChangedEvent>().Any(status => status.Amount != (level - 1) * 10)
                || result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == scout.EntityId && e.IsEcho) != 2
                || result.Events.OfType<ManaChangedEvent>().Any() || result.PermanentChanges.Any()
                || !result.Events.SequenceEqual(Simulate(scout, side, 55).Events)) return false;
            var offer = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            if (offer.Abilities.Last().TriggerStatus != BattleStatus.HasteDuration
                || !CardDisplayAdapter.FaceEffects(offer).Any(effect => effect.Kind == CardFaceEffectKind.Damage
                    && effect.Value == damage.ToString())) return false;
        }
        return true;
    }

    internal static bool StatusSourcesAndMulticast()
    {
        var scout = Scout(2);
        // 其他卡牌重复施加到已有疾速时，每份正贡献各触发一次。
        var giver = Fixture(0, Ability(AbilityActivation.PassiveOnBattleStart, AbilityTarget.RightAdjacentAlliedCard,
            new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 10),
            new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 20)));
        var repeated = Run([giver, scout], [], 1);
        if (Hits(repeated, scout).Length != 2 || State(repeated, scout, 0).Haste != 30) return false;
        var adjacent = giver with { Abilities = [Ability(AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard,
            new ApplyStatusToAdjacentAlliedCardsEffectDefinition(BattleStatus.HasteDuration, 10, GameTags.Human))] };
        var adjacentResult = Run([adjacent, scout], [], 1);
        if (Hits(adjacentResult, scout).Length != 1 || State(adjacentResult, scout, 0).Haste != 20) return false;
        var randomAllied = scout with { Abilities = scout.Abilities!.Append(Ability(AbilityActivation.PassiveOnBattleStart,
            AbilityTarget.SelfCard, new ApplyStatusToRandomAlliedCardEffectDefinition(BattleStatus.HasteDuration, 10))).ToArray() };
        if (Hits(Run([randomAllied], [], 1), scout).Length != 1) return false;
        var enemy = Fixture(0, Ability(AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard,
            new ApplyStatusToRandomEnemyCardEffectDefinition(BattleStatus.HasteDuration, 10)));
        if (Hits(Run([scout], [enemy], 1), scout).Length != 1) return false;
        var multi = scout with { Multicast = 2, AttackDamage = 37,
            CombatValues = new System.Collections.Generic.Dictionary<StringName, int> { [GameAttributeKeys.HasteDurationBonus] = 5 } };
        var multiResult = Run([multi], [], 34);
        if (Hits(multiResult, scout).Length != 3 || Hits(multiResult, scout).Any(hit => hit.RawDamage != 37)
            || State(multiResult, scout, 34).Haste != 41) return false;
        // 狂暴只提升主动伤害，回响伤害保持当前攻击。
        var berserk = multi with { Abilities = multi.Abilities!.Append(Ability(AbilityActivation.PassiveOnBattleStart,
            AbilityTarget.SelfCard, new SetSourceCardStateEffectDefinition(GameAttributeKeys.Berserk, true))).ToArray() };
        return Hits(Run([berserk], [], 30), scout).All(hit => hit.RawDamage == 37)
            && !Hits(Run([scout with { IsOnBench = true }, giver], [], 40), scout).Any()
            && !Hits(Run([scout with { Abilities = scout.Abilities!.Append(Ability(AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.SelfCard, new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 10),
                new DestroyCardEffectDefinition(false))).ToArray() }], [], 40), scout).Any();
    }

    internal static bool FiltersAndChainCutoff()
    {
        var scout = Scout(2);
        foreach (var status in new[] { BattleStatus.HasteDuration, BattleStatus.SlowDuration, BattleStatus.ImmobilizeDuration })
        {
            var listener = scout with { Abilities = [
                new AbilityDefinition("verification.scout.status", AbilityActivation.EchoOnSourceCardStatusGained,
                    AbilityTarget.EnemyHero, 0, 0, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)], triggerStatus: status),
                Ability(AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, new ApplyStatusEffectDefinition(status, 10))] };
            if (Hits(Run([listener], [], 1), scout).Length != 1) return false;
        }
        var zero = scout with { Abilities = scout.Abilities!.Append(Ability(AbilityActivation.PassiveOnBattleStart,
            AbilityTarget.SelfCard, new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 0),
            new ApplyStatusEffectDefinition(BattleStatus.SlowDuration, 10))).ToArray() };
        if (Hits(Run([zero], [], 1), scout).Any()) return false;
        var giver = Fixture(0,
            new AbilityDefinition("verification.scout.active", AbilityActivation.Active, AbilityTarget.SelfCard,
                0, 1, [new ArmorEffectDefinition(0)]),
            Ability(AbilityActivation.EchoOnSourceCardActivated, AbilityTarget.RightAdjacentAlliedCard,
                new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 10)));
        var cut = Run([giver, scout], [], 10);
        if (State(cut, scout, 10).Haste != 10 || Hits(cut, scout).Any()) return false;
        // 自身回响产生的伤害不得触发另一个伤害回响。
        var damageListener = Fixture(0, Ability(AbilityActivation.EchoOnDamageDealt, AbilityTarget.EnemyHero, new DamageEffectDefinition(99)));
        var result = Run([damageListener, scout], [], 30);
        if (result.Events.OfType<DamageDealtEvent>().Count() != 1) return false;
        foreach (var invalid in new[] { (BattleStatus?)null, BattleStatus.Burn, (BattleStatus)99 })
        {
            try { _ = new AbilityDefinition("verification.scout.invalid", AbilityActivation.EchoOnSourceCardStatusGained,
                AbilityTarget.EnemyHero, 0, 0, [new DamageEffectDefinition(1)], triggerStatus: invalid); return false; }
            catch (ArgumentException) { }
        }
        foreach (var config in new[] { (AbilityActivation.Active, 10, false), (AbilityActivation.EchoOnSourceCardStatusGained, 1, false),
            (AbilityActivation.EchoOnSourceCardStatusGained, 0, true) })
        {
            try { _ = new AbilityDefinition("verification.scout.invalid", config.Item1, AbilityTarget.EnemyHero,
                0, config.Item2, [new DamageEffectDefinition(1)], allowsBench: config.Item3, triggerStatus: BattleStatus.HasteDuration); return false; }
            catch (ArgumentException) { }
        }
        return State(Run([scout], [], 1), scout, 0).Haste == 0;
    }

    internal static async Task Capture(Node owner)
    {
        if (!LevelsAndCooldown() || !StatusSourcesAndMulticast() || !FiltersAndChainCutoff())
            throw new InvalidOperationException("Scout checks failed");
        var viewport = new SubViewport { Size = new Vector2I(320, 300), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(320, 300) });
            var face = new CardItemView { Position = new Vector2(106, 22), Size = new Vector2(108, 256) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new ShortbladeScoutCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/shortblade-scout/card-face.png") != Error.Ok)
                throw new InvalidOperationException("Scout capture failed");
            GD.Print("短刃斥候聚焦验证与卡面捕获：通过");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardBattleSetup Scout(int level)
    {
        var card = new EntityFactory().CreateCard(new ShortbladeScoutCardDefinition(), level);
        return new CardBattleSetup(card.Id, 1, card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
            30, card.Abilities, UseLegacyAttack: false, Tags: card.Tags, ElementKeys: card.Attributes.Identity.ElementKeys) { Level = level };
    }
    private static AbilityDefinition Ability(AbilityActivation activation, AbilityTarget target, params EffectDefinition[] effects) =>
        new("verification.scout.effect", activation, target, 0, 0, effects);
    private static CardBattleSetup Fixture(int start, params AbilityDefinition[] abilities) =>
        new(EntityId.New(), start, 0, 0, abilities, UseLegacyAttack: false);
    private static BattleResult Simulate(CardBattleSetup card, SideId side, int ticks) =>
        side == SideId.Player ? Run([card], [], ticks) : Run([], [card], ticks);
    private static BattleResult Run(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks) =>
        new CombatSimulator().Simulate(new BattleSetup(
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), cards),
            new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), enemies),
            42, new BattleTick(ticks), new BattleTick(ticks + 1)));
    private static DamageDealtEvent[] Hits(BattleResult result, CardBattleSetup card) =>
        result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == card.EntityId).ToArray();
    private static CardBattleSnapshot State(BattleResult result, CardBattleSetup card, long tick) =>
        result.States.Last(frame => frame.Tick.Value == tick).Cards.Single(value => value.Id == card.EntityId);
}
