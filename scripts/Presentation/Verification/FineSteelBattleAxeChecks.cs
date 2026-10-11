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

// 精钢战斧分级伤害、相邻人类成长与战斗隔离验证（表现层）。
internal static class FineSteelBattleAxeChecks
{
    internal static bool LevelsAndDamage()
    {
        var definition = new FineSteelBattleAxeCardDefinition();
        var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.Key != new StringName("card.fine_steel_battle_axe") || identity.DisplayName != "精钢战斧"
            || identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Medium
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || definition.Tags.Count != 2 || !definition.Tags.Contains(GameTags.Equipment)
            || definition.InitialLevel != 1 || definition.SupportsLevel(0) || definition.SupportsLevel(5)
            || !DefinitionRegistry.Scan(typeof(FineSteelBattleAxeCardDefinition).Assembly).Cards.ContainsKey(identity.Key)
            || texture is null || texture.GetWidth() != texture.GetHeight() || texture.GetWidth() < 512) return false;
        for (var level = 1; level <= 4; level++)
        {
            var axe = Axe(level);
            var damage = 40 << (level - 1);
            foreach (var side in new[] { SideId.Player, SideId.Opponent })
            {
                var setup = side == SideId.Player ? Battle([axe], [], 200) : Battle([], [axe], 200);
                var result = new CombatSimulator().Simulate(setup);
                var hits = Hits(result, axe);
                if (axe.AttackDamage != damage || axe.CooldownTicks != 100
                    || !hits.Select(hit => hit.Tick.Value).SequenceEqual(new long[] { 100, 200 })
                    || hits.Any(hit => hit.RawDamage != damage || hit.TargetSide == side)
                    || hits[0].ArmorAbsorbed != 1 || hits[0].HealthDamage != damage - 1
                    || result.Events.OfType<ManaChangedEvent>().Any()
                    || result.PermanentAttributeBonuses.Any() || result.PermanentChanges.Any()
                    || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            }
            var boosted = axe with { AttackDamage = damage + 17, Multicast = 1 };
            if (!Hits(Run([boosted], [], 100), axe).Select(hit => hit.RawDamage).SequenceEqual(new[] { damage + 17, damage + 17 })) return false;
            var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            if (!CardDisplayAdapter.FaceEffects(snapshot).Any(effect => effect.Kind == CardFaceEffectKind.Damage && effect.Value == damage.ToString())
                || !snapshot.Abilities.Skip(1).Select(ability => ability.TriggerCardSide).SequenceEqual(new AdjacentCardSide?[] { AdjacentCardSide.Left, AdjacentCardSide.Right })
                || snapshot.Abilities.Skip(1).Any(ability => ability.TriggerCardTag != GameTags.Human
                    || ability.Effects.Single() is not IncreaseSourceCardAttributeEffectDefinition { Permanent: false, Amount: var amount }
                    || amount != level * 10)) return false;
        }
        return true;
    }

    internal static bool AdjacencyAndGrowth()
    {
        for (var level = 1; level <= 4; level++)
        {
            var axe = Axe(level);
            var left = Human(0, 3) with { Multicast = 1 };
            var right = Human(5, 2);
            var setup = Battle([axe, left, right], [Human(0, 1)], 100);
            var result = new CombatSimulator().Simulate(setup);
            var growth = result.Events.OfType<CardAttributeChangedEvent>().Where(e => e.CardId == axe.EntityId).ToArray();
            if (growth.Length != 15 || growth.Any(e => e.AttributeKey != GameAttributeKeys.AttackDamage || e.Amount != level * 10)
                || Value(result, axe, 20) != axe.AttackDamage + 3 * level * 10
                || Hits(result, axe).Single().RawDamage != axe.AttackDamage + 12 * level * 10
                || result.PermanentAttributeBonuses.Any() || result.PermanentChanges.Any()) return false;
            var mirrored = new BattleSetup(setup.Opponent, setup.Player, 42, new BattleTick(100), new BattleTick(101));
            if (Value(new CombatSimulator().Simulate(mirrored), axe, 20) != axe.AttackDamage + 3 * level * 10) return false;
            if (Value(Run([axe, left], [], 20), axe, 20) != axe.AttackDamage + 2 * level * 10
                || Value(Run([axe, right], [], 20), axe, 20) != axe.AttackDamage + level * 10) return false;
            // 回放冻结当前攻击，后续成长不修改已捕获的数值。
            var frozen = result.States.Last(frame => frame.Tick.Value == 20).Cards.Single(card => card.Id == axe.EntityId);
            var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new FineSteelBattleAxeCardDefinition(), level))
                with { CurrentValues = frozen.Values, EffectAmounts = frozen.EffectAmounts };
            if (!CardDisplayAdapter.FaceEffects(snapshot).Any(effect => effect.Kind == CardFaceEffectKind.Damage
                    && effect.Value == (axe.AttackDamage + 3 * level * 10).ToString())
                || Value(Run([axe], [], 20), axe, 0) != axe.AttackDamage) return false;
        }
        return true;
    }

    internal static bool FiltersAndIsolation()
    {
        var axe = Axe(1);
        var left = Human(0, 3);
        foreach (var excluded in new[]
        {
            left with { BoardStart = 6 },
            left with { Tags = new TagSet([GameTags.Beast]) },
            left with { IsOnBench = true },
            left with { Abilities = [new AbilityDefinition("verification.axe.opening", AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.SelfCard, 0, 0, [new ArmorEffectDefinition(1)])] },
            left with { Abilities = [new AbilityDefinition("verification.axe.failed", AbilityActivation.Active,
                AbilityTarget.SelfCard, 1, 20, [new ArmorEffectDefinition(1)])] },
            left with { Abilities = [new AbilityDefinition("verification.axe.echo", AbilityActivation.EchoOnAbilityActivated,
                AbilityTarget.SelfCard, 0, 0, [new ArmorEffectDefinition(1)])] },
        })
            if (Value(Run([axe, excluded], [], 20), axe, 20) != axe.AttackDamage) return false;
        if (Value(Run([axe], [left], 20), axe, 20) != axe.AttackDamage) return false;
        var destroyed = axe with { Abilities = axe.Abilities!.Append(new AbilityDefinition("verification.axe.destroy",
            AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])).ToArray() };
        foreach (var inactive in new[] { axe with { IsOnBench = true }, destroyed })
            if (Value(Run([inactive, left], [], 100), axe, 100) != axe.AttackDamage
                || Hits(Run([inactive, left], [], 100), axe).Any()) return false;
        var simulated = Run([axe, left], [], 40);
        return Value(simulated, axe, 20) == 50 && Value(simulated, axe, 40) == 60
            && Value(Run([axe], [], 20), axe, 20) == 40;
    }

    internal static async Task Capture(Node owner)
    {
        if (!LevelsAndDamage() || !AdjacencyAndGrowth() || !FiltersAndIsolation())
            throw new InvalidOperationException("Battle axe checks failed");
        var viewport = new SubViewport { Size = new Vector2I(360, 300), Disable3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(360, 300) });
            var face = new CardItemView { Position = new Vector2(62, 22), Size = new Vector2(236, 256) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new FineSteelBattleAxeCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/fine-steel-battle-axe/card-face.png") != Error.Ok) throw new InvalidOperationException("Battle axe capture failed");
            GD.Print("精钢战斧聚焦验证与卡面捕获：通过");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardBattleSetup Axe(int level)
    {
        var card = new EntityFactory().CreateCard(new FineSteelBattleAxeCardDefinition(), level);
        return new CardBattleSetup(card.Id, 3, card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
            100, card.Abilities, UseLegacyAttack: false, Tags: card.Tags, OccupiedSlots: 2,
            ElementKeys: card.Attributes.Identity.ElementKeys) { Level = level };
    }
    private static CardBattleSetup Human(int start, int size) => new(EntityId.New(), start, 0, 20,
        [new AbilityDefinition("verification.axe.human", AbilityActivation.Active, AbilityTarget.SelfCard,
            0, 20, [new ArmorEffectDefinition(1)])], UseLegacyAttack: false, Tags: new TagSet([GameTags.Human]), OccupiedSlots: size);
    private static BattleSetup Battle(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 1, ManaRegen: 0), cards),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 1, ManaRegen: 0), enemies),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
    private static BattleResult Run(CardBattleSetup[] cards, CardBattleSetup[] enemies, int ticks) => new CombatSimulator().Simulate(Battle(cards, enemies, ticks));
    private static DamageDealtEvent[] Hits(BattleResult result, CardBattleSetup card) => result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == card.EntityId).ToArray();
    private static int Value(BattleResult result, CardBattleSetup card, long tick) => result.States.Last(frame => frame.Tick.Value == tick).Cards.Single(value => value.Id == card.EntityId).Values[GameAttributeKeys.AttackDamage];
}
