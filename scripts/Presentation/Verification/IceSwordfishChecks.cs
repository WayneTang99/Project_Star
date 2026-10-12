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

// 冰剑鱼分级、自身回响、随机筛选与战斗隔离验证（表现层）。
internal static class IceSwordfishChecks
{
    internal static bool LevelsAndDamage()
    {
        var definition = new IceSwordfishCardDefinition(); var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.Key != new StringName("card.ice_swordfish") || identity.DisplayName != "冰剑鱼"
            || identity.FactionKey != new StringName("neutral") || identity.Size != CardSize.Medium
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Ice }) || definition.InitialLevel != 2
            || definition.SupportsLevel(1) || definition.SupportsLevel(5) || definition.Tags.Count != 3
            || !definition.Tags.Contains(GameTags.Equipment) || !definition.Tags.Contains(GameTags.Aquatic)
            || texture is null || texture.GetWidth() != texture.GetHeight()
            || !DefinitionRegistry.Scan(typeof(IceSwordfishChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        foreach (var (level, damage, bonus) in new[] { (2, 40, 5), (3, 80, 10), (4, 160, 20) })
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        {
            var fish = Fish(level); var setup = Battle([fish], [], 120, side);
            var result = new CombatSimulator().Simulate(setup);
            var hits = result.Events.OfType<DamageDealtEvent>().Where(item => item.SourceCardId == fish.EntityId).ToArray();
            var changes = result.Events.OfType<CardAttributeChangedEvent>().ToArray();
            if (fish.CooldownTicks != 60 || fish.Abilities!.Any(ability => ability.ManaCost != 0)
                || hits.Length != 2 || hits[0].Tick.Value != 60 || hits[0].RawDamage != damage
                || hits[1].Tick.Value != 120 || hits[1].RawDamage != damage + bonus
                || Echoes(result, fish) != 2 || changes.Length != 2 || changes.Any(change => change.Amount != bonus)
                || Value(result, fish) != damage + bonus * 2 || fish.AttackDamage != damage
                || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            var rules = string.Join("\n", CardDisplayAdapter.Rules(snapshot));
            if (!rules.Contains($"造成 {damage} 点伤害") || !rules.Contains($"攻击 +{bonus}")
                || !rules.Contains("冰属性") || rules.Contains("40/80/160") || rules.Contains("5/10/20")) return false;
        }
        var multicast = Fish(2) with { Multicast = 1 };
        var repeated = new CombatSimulator().Simulate(Battle([multicast], [], 60));
        return Echoes(repeated, multicast) == 2 && Value(repeated, multicast) == 50;
    }

    internal static bool RandomTargetsAndIsolation()
    {
        var fish = EchoOnly(); var first = Attack(2, 0); var second = Attack(3, 10);
        var bench = Attack(0, 20) with { IsOnBench = true };
        var dead = Attack(4, 30) with { Abilities = Attack(4, 30).Abilities!.Append(Opening(new DestroyCardEffectDefinition(false))).ToArray() };
        var support = Support(5); var ice = Support(6) with { ElementKeys = [GameElements.Ice], Multicast = 1 };
        var enemy = Attack(0, 50);
        foreach (var side in new[] { SideId.Player, SideId.Opponent })
        {
            var setup = Battle([fish, first, second, bench, dead, support, ice], [enemy], 40, side);
            var result = new CombatSimulator().Simulate(setup);
            var changes = result.Events.OfType<CardAttributeChangedEvent>().ToArray();
            if (Echoes(result, fish) != 4 || changes.Length != 4
                || changes.Any(change => change.Amount != 5 || change.CardId != first.EntityId && change.CardId != second.EntityId)
                || Value(result, first) + Value(result, second) != 30
                || Value(result, bench) != 20 || Value(result, dead) != 30 || Value(result, support) != 0
                || Value(result, fish) != 40 || Value(result, enemy) != 50
                || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
        }
        // 零攻击能力卡牌仍可获得增量；下一场不继承上一场成长。
        var single = new CombatSimulator().Simulate(Battle([fish, first, ice], [], 40));
        return Value(single, first) == 20 && first.AttackDamage == 0
            && Value(new CombatSimulator().Simulate(Battle([fish, first], [], 40)), first) == 0
            && !new CombatSimulator().Simulate(Battle([fish, ice], [], 40)).Events.OfType<CardAttributeChangedEvent>().Any();
    }

    internal static bool TriggerBoundaries()
    {
        var fish = EchoOnly(); var target = Attack(2, 0);
        var ice = Support(3) with { ElementKeys = [GameElements.Ice] };
        foreach (var excluded in new[]
        {
            ice with { ElementKeys = [GameElements.Fire] }, ice with { IsOnBench = true },
            ice with { Abilities = [Opening(new GainSourceHeroArmorEffectDefinition(1))] },
            ice with { Abilities = [Opening(new DestroyCardEffectDefinition(false))] },
            ice with { Abilities = [new AbilityDefinition("verification.ice.unpaid", AbilityActivation.Active,
                AbilityTarget.SelfCard, 1, 20, [new GainSourceHeroArmorEffectDefinition(1)])] },
        })
        {
            var result = new CombatSimulator().Simulate(Battle([fish, target, excluded], [], 40));
            if (Echoes(result, fish) != 0 || Value(result, target) != 0) return false;
        }
        if (Echoes(new CombatSimulator().Simulate(Battle([fish, target], [ice], 40)), fish) != 0) return false;
        foreach (var source in new[] { fish with { IsOnBench = true }, fish with
            { Abilities = fish.Abilities!.Append(Opening(new DestroyCardEffectDefinition(false))).ToArray() } })
            if (Echoes(new CombatSimulator().Simulate(Battle([source, target, ice], [], 40)), source) != 0) return false;
        // 被动回响不递归；每次冰属性主动发动只触发一次。
        var echoing = ice with { Abilities = ice.Abilities!.Append(new AbilityDefinition("verification.ice.echo",
            AbilityActivation.EchoOnSourceCardActivated, AbilityTarget.SelfCard, 0, 0,
            [new GainSourceHeroArmorEffectDefinition(1)])).ToArray() };
        var echoed = new CombatSimulator().Simulate(Battle([fish, target, echoing], [], 40));
        if (Echoes(echoed, fish) != 2 || Value(echoed, target) != 10) return false;
        try
        {
            _ = new AbilityDefinition("verification.ice.invalid", AbilityActivation.Active, AbilityTarget.SelfCard,
                0, 10, [new ModifyRandomAlliedAttackCardAttributeEffectDefinition(new StringName(""), 5)]);
            return false;
        }
        catch (ArgumentException) { return true; }
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(360, 360), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(360, 360) });
            var face = new CardItemView { Position = new Vector2(24, 24), Size = new Vector2(312, 312) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new IceSwordfishCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/ice-swordfish/card-face.png") != Error.Ok)
                throw new InvalidOperationException("Ice swordfish card capture failed.");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardBattleSetup Fish(int level)
    {
        var card = new EntityFactory().CreateCard(new IceSwordfishCardDefinition(), level);
        return new CardBattleSetup(card.Id, 0, card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
            60, card.Abilities, UseLegacyAttack: false, Tags: card.Tags, OccupiedSlots: 2,
            ElementKeys: card.Attributes.Identity.ElementKeys) { Level = level };
    }
    private static CardBattleSetup EchoOnly()
    {
        var fish = Fish(2); return fish with { Abilities = fish.Abilities!.Where(item => item.Activation != AbilityActivation.Active).ToArray() };
    }
    private static CardBattleSetup Attack(int start, int damage) => new(EntityId.New(), start, damage, 200,
        [new AbilityDefinition("verification.ice.attack", AbilityActivation.Active, AbilityTarget.EnemyHero,
            0, 200, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)])], UseLegacyAttack: false);
    private static CardBattleSetup Support(int start) => new(EntityId.New(), start, 0, 20,
        [new AbilityDefinition("verification.ice.support", AbilityActivation.Active, AbilityTarget.SelfCard,
            0, 20, [new GainSourceHeroArmorEffectDefinition(1)])], UseLegacyAttack: false);
    private static AbilityDefinition Opening(EffectDefinition effect) => new("verification.ice.opening",
        AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [effect]);
    private static int Echoes(BattleResult result, CardBattleSetup source) => result.Events.OfType<AbilityActivatedEvent>()
        .Count(item => item.SourceCardId == source.EntityId && item.IsEcho);
    private static int Value(BattleResult result, CardBattleSetup card) => result.States.Last(frame => frame.Player.Health > 0
        && frame.Opponent.Health > 0).Cards.Single(item => item.Id == card.EntityId).Values[GameAttributeKeys.AttackDamage];
    private static BattleSetup Battle(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks, SideId side = SideId.Player) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), side == SideId.Player ? allies : enemies),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), side == SideId.Player ? enemies : allies),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
