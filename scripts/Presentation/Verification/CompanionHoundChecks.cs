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

// 伙伴猎犬分级伤害、相邻小型卡牌成长与战斗隔离验证（表现层）。
internal static class CompanionHoundChecks
{
    internal static bool LevelsAndDamage()
    {
        var definition = new CompanionHoundCardDefinition();
        var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.Key != new StringName("card.companion_hound") || identity.DisplayName != "伙伴猎犬"
            || identity.FactionKey != new StringName("robin") || identity.Size != CardSize.Medium
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Beast)
            || definition.InitialLevel != 1 || definition.SupportsLevel(0) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(CompanionHoundCardDefinition).Assembly).Cards.ContainsKey(identity.Key)
            || texture is null || texture.GetWidth() != texture.GetHeight() || texture.GetWidth() < 512) return false;
        for (var level = 1; level <= 4; level++)
        {
            var hound = Hound(level);
            var damage = 40 << (level - 1);
            foreach (var side in new[] { SideId.Player, SideId.Opponent })
            {
                var setup = side == SideId.Player ? Battle([hound], [], 140) : Battle([], [hound], 140);
                var result = new CombatSimulator().Simulate(setup);
                var hits = Hits(result, hound);
                if (hound.AttackDamage != damage || hound.CooldownTicks != 70
                    || !hits.Select(hit => hit.Tick.Value).SequenceEqual(new long[] { 70, 140 })
                    || hits.Any(hit => hit.RawDamage != damage || hit.TargetSide == side)
                    || hits[0].ArmorAbsorbed != 1 || hits[0].HealthDamage != damage - 1
                    || result.Events.OfType<ManaChangedEvent>().Any()
                    || result.PermanentAttributeBonuses.Any() || result.PermanentChanges.Any()
                    || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            }
            var boosted = hound with { AttackDamage = damage + 17, Multicast = 1 };
            if (!Hits(Run([boosted], [], 70), hound).Select(hit => hit.RawDamage).SequenceEqual(new[] { damage + 17, damage + 17 })) return false;
            var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            if (!CardDisplayAdapter.FaceEffects(snapshot).Any(effect => effect.Kind == CardFaceEffectKind.Damage && effect.Value == damage.ToString())
                || !snapshot.Abilities.Skip(1).Select(ability => ability.TriggerCardSide).SequenceEqual(new AdjacentCardSide?[] { AdjacentCardSide.Left, AdjacentCardSide.Right })
                || snapshot.Abilities.Skip(1).Any(ability => ability.TriggerCardSize != CardSize.Small || ability.TriggerCardTag is not null
                    || ability.Effects.Single() is not IncreaseSourceCardAttributeEffectDefinition { Permanent: false, Amount: var amount }
                    || amount != (5 << (level - 1)))) return false;
        }
        return true;
    }

    internal static bool AdjacencyAndGrowth()
    {
        for (var level = 1; level <= 4; level++)
        {
            var hound = Hound(level);
            var left = Neighbor(2, 1) with { Multicast = 1 };
            var right = Neighbor(5, 1) with { Tags = new TagSet([GameTags.Beast]) };
            var setup = Battle([hound, left, right], [Neighbor(0, 1)], 70);
            var result = new CombatSimulator().Simulate(setup);
            var growth = result.Events.OfType<CardAttributeChangedEvent>().Where(e => e.CardId == hound.EntityId).ToArray();
            if (growth.Length != 9 || growth.Any(e => e.AttributeKey != GameAttributeKeys.AttackDamage || e.Amount != (5 << (level - 1)))
                || Value(result, hound, 20) != hound.AttackDamage + 3 * (5 << (level - 1))
                || Hits(result, hound).Single().RawDamage != hound.AttackDamage + 9 * (5 << (level - 1))
                || result.PermanentAttributeBonuses.Any() || result.PermanentChanges.Any()) return false;
            var mirrored = new BattleSetup(setup.Opponent, setup.Player, 42, new BattleTick(70), new BattleTick(71));
            if (Value(new CombatSimulator().Simulate(mirrored), hound, 20) != hound.AttackDamage + 3 * (5 << (level - 1))) return false;
            if (Value(Run([hound, left], [], 20), hound, 20) != hound.AttackDamage + 2 * (5 << (level - 1))
                || Value(Run([hound, right], [], 20), hound, 20) != hound.AttackDamage + (5 << (level - 1))) return false;
            // 回放冻结当前攻击，后续成长不修改已捕获的数值。
            var frozen = result.States.Last(frame => frame.Tick.Value == 20).Cards.Single(card => card.Id == hound.EntityId);
            var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new CompanionHoundCardDefinition(), level))
                with { CurrentValues = frozen.Values, EffectAmounts = frozen.EffectAmounts };
            if (!CardDisplayAdapter.FaceEffects(snapshot).Any(effect => effect.Kind == CardFaceEffectKind.Damage
                    && effect.Value == (hound.AttackDamage + 3 * (5 << (level - 1))).ToString())
                || Value(Run([hound], [], 20), hound, 0) != hound.AttackDamage) return false;
        }
        return true;
    }

    internal static bool FiltersAndIsolation()
    {
        var hound = Hound(1);
        var left = Neighbor(2, 1);
        foreach (var excluded in new[]
        {
            left with { BoardStart = 6 },
            left with { BoardStart = 1 },
            left with { OccupiedSlots = 2, BoardStart = 1 },
            left with { OccupiedSlots = 3, BoardStart = 0 },
            left with { IsOnBench = true },
            left with { Abilities = [new AbilityDefinition("verification.hound.opening", AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.SelfCard, 0, 0, [new ArmorEffectDefinition(1)])] },
            left with { Abilities = [new AbilityDefinition("verification.hound.failed", AbilityActivation.Active,
                AbilityTarget.SelfCard, 1, 20, [new ArmorEffectDefinition(1)])] },
            left with { Abilities = [new AbilityDefinition("verification.hound.echo", AbilityActivation.EchoOnAbilityActivated,
                AbilityTarget.SelfCard, 0, 0, [new ArmorEffectDefinition(1)])] },
        })
            if (Value(Run([hound, excluded], [], 20), hound, 20) != hound.AttackDamage) return false;
        if (Value(Run([hound], [left], 20), hound, 20) != hound.AttackDamage) return false;
        var destroyed = hound with { Abilities = hound.Abilities!.Append(new AbilityDefinition("verification.hound.destroy",
            AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])).ToArray() };
        foreach (var inactive in new[] { hound with { IsOnBench = true }, destroyed })
            if (Value(Run([inactive, left], [], 70), hound, 70) != hound.AttackDamage
                || Hits(Run([inactive, left], [], 70), hound).Any()) return false;
        var simulated = Run([hound, left], [], 40);
        return Value(simulated, hound, 20) == 45 && Value(simulated, hound, 40) == 50
            && Value(Run([hound], [], 20), hound, 20) == 40;
    }

    internal static async Task Capture(Node owner)
    {
        if (!LevelsAndDamage() || !AdjacencyAndGrowth() || !FiltersAndIsolation())
            throw new InvalidOperationException("Battle hound checks failed");
        var viewport = new SubViewport { Size = new Vector2I(360, 300), Disable3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(360, 300) });
            var face = new CardItemView { Position = new Vector2(62, 22), Size = new Vector2(236, 256) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new CompanionHoundCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/companion-hound/card-face.png") != Error.Ok) throw new InvalidOperationException("Battle hound capture failed");
            GD.Print("伙伴猎犬聚焦验证与卡面捕获：通过");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardBattleSetup Hound(int level)
    {
        var card = new EntityFactory().CreateCard(new CompanionHoundCardDefinition(), level);
        return new CardBattleSetup(card.Id, 3, card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
            70, card.Abilities, UseLegacyAttack: false, Tags: card.Tags, OccupiedSlots: 2,
            ElementKeys: card.Attributes.Identity.ElementKeys) { Level = level };
    }
    private static CardBattleSetup Neighbor(int start, int size) => new(EntityId.New(), start, 0, 20,
        [new AbilityDefinition("verification.hound.human", AbilityActivation.Active, AbilityTarget.SelfCard,
            0, 20, [new ArmorEffectDefinition(1)])], UseLegacyAttack: false, Tags: new TagSet([GameTags.Human]), OccupiedSlots: size);
    private static BattleSetup Battle(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 7000, 1, ManaRegen: 0), cards),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 7000, 1, ManaRegen: 0), enemies),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
    private static BattleResult Run(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks) => new CombatSimulator().Simulate(Battle(cards, enemies, ticks));
    private static DamageDealtEvent[] Hits(BattleResult result, CardBattleSetup card) => result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == card.EntityId).ToArray();
    private static int Value(BattleResult result, CardBattleSetup card, long tick) => result.States.Last(frame => frame.Tick.Value == tick).Cards.Single(value => value.Id == card.EntityId).Values[GameAttributeKeys.AttackDamage];
}
