using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

internal static class HeavyCavalryChecks
{
    internal static bool LevelsAndNeighbors()
    {
        var definition = new HeavyCavalryCardDefinition(); var identity = definition.Attributes.Identity;
        if (identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Medium
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General }) || !definition.Tags.Contains(GameTags.Human)
            || definition.InitialLevel != 3 || definition.SupportsLevel(2) || definition.SupportsLevel(5)) return false;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (texture is null || texture.GetWidth() != texture.GetHeight()) return false;
        foreach (var level in new[] { 3, 4 })
        foreach (var reverse in new[] { false, true })
        for (var count = 0; count <= 2; count++)
        {
            var cavalry = Cavalry(level); var factor = level == 3 ? 1 : 2;
            var allies = new[] { cavalry }.Concat(Enumerable.Range(0, count).Select(i => Equipment(i == 0 ? 0 : 5, 3))).ToArray();
            var setup = Setup(allies, [], 80, reverse); var simulator = new CombatSimulator(); var result = simulator.Simulate(setup);
            var damage = (80 + count * 60) * factor; var armor = (160 + count * 60) * factor;
            var hits = result.Events.OfType<DamageDealtEvent>().Where(hit => hit.SourceCardId == cavalry.EntityId).ToArray();
            if (hits.Length != 1 || hits[0].Tick.Value != 80 || hits[0].RawDamage != damage
                || Value(result, cavalry, GameAttributeKeys.AttackDamage) != damage || Value(result, cavalry, GameAttributeKeys.Armor) != armor
                || (reverse ? result.States.Last().Opponent.Armor : result.States.Last().Player.Armor) != armor
                || result.PermanentAttributeBonuses.Count != 0 || !result.Events.SequenceEqual(simulator.Simulate(setup).Events)) return false;
        }
        return true;
    }

    internal static bool RemovalAndPreview()
    {
        var cavalry = Cavalry(3);
        foreach (var excluded in new[] { Equipment(0, 2), Equipment(6, 1), Equipment(5, 1) with { IsOnBench = true },
            Equipment(5, 1) with { Tags = new TagSet([GameTags.Human]) } })
            if (Value(Run([cavalry, excluded], [], 1), cavalry, GameAttributeKeys.AttackDamage) != 80) return false;
        if (Value(Run([cavalry], [Equipment(5, 1)], 1), cavalry, GameAttributeKeys.AttackDamage) != 80) return false;
        var left = Equipment(0, 3) with { Abilities = [new AbilityDefinition("verification.cavalry.destroy_equipment",
            AbilityActivation.Active, AbilityTarget.SelfCard, 0, 40, [new DestroyCardEffectDefinition(false)])] };
        var right = Equipment(5, 2); var after = Run([cavalry, left, right], [], 80);
        if (after.States.First().Cards.Single(c => c.Id == cavalry.EntityId).Values[GameAttributeKeys.AttackDamage] != 200
            || Value(after, cavalry, GameAttributeKeys.AttackDamage) != 140 || Value(after, cavalry, GameAttributeKeys.Armor) != 220
            || after.Events.OfType<DamageDealtEvent>().Single(hit => hit.SourceCardId == cavalry.EntityId).RawDamage != 140) return false;
        foreach (var inactive in new[] { cavalry with { IsOnBench = true }, cavalry with
        {
            Abilities = cavalry.Abilities!.Append(new AbilityDefinition("verification.cavalry.destroy_source",
                AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])).ToArray(),
        } })
            if (Value(Run([inactive, right], [], 1), inactive, GameAttributeKeys.AttackDamage) != 80) return false;
        // 原生战场数值投影随区域调整，旧快照保持冻结，实例基础值不变。
        var registry = DefinitionRegistry.Scan(typeof(HeavyCavalryCardDefinition).Assembly);
        var factory = new EntityFactory(); var board = new BoardService(new BoardPlacementSolver());
        var economy = new CardEconomyService(factory, board);
        var session = new CreateMatchService(factory).Create(1, 100, new PaladinHeroDefinition());
        var card = economy.AcquireCard(session, new HeavyCavalryCardDefinition(), 3, CardAcquisitionSource.Reward).Value!.Card;
        var armguard = economy.AcquireCard(session, new ArmguardCardDefinition(), 1, CardAcquisitionSource.Reward).Value!.Card;
        var plate = economy.AcquireCard(session, new OrderPlateArmorCardDefinition(), 1, CardAcquisitionSource.Reward).Value!.Card;
        if (board.PlaceCard(session, card.Id, BoardZone.Battlefield, 3).IsFailure
            || board.PlaceCard(session, armguard.Id, BoardZone.Battlefield, 2).IsFailure
            || board.PlaceCard(session, plate.Id, BoardZone.Battlefield, 5).IsFailure) return false;
        var before = MatchDisplayQuery.Capture(session, registry.Sets).Cards.Single(c => c.Id == card.Id);
        if (before.CurrentValues[GameAttributeKeys.AttackDamage] != 200 || before.CurrentValues[GameAttributeKeys.Armor] != 280) return false;
        if (board.PlaceCard(session, plate.Id, BoardZone.Bench, 0).IsFailure) return false;
        var next = MatchDisplayQuery.Capture(session, registry.Sets).Cards.Single(c => c.Id == card.Id);
        return next.CurrentValues[GameAttributeKeys.AttackDamage] == 140 && next.CurrentValues[GameAttributeKeys.Armor] == 220
            && before.CurrentValues[GameAttributeKeys.AttackDamage] == 200
            && card.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.AttackDamage) == 80;
    }

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(340, 340), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            var face = new CardItemView { Position = new Vector2(20, 20), Size = new Vector2(300, 300) }; viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new HeavyCavalryCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/heavy-cavalry/card-face.png") != Error.Ok) throw new InvalidOperationException("Heavy cavalry capture failed.");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static CardBattleSetup Cavalry(int level)
    {
        var card = new EntityFactory().CreateCard(new HeavyCavalryCardDefinition(), level);
        return new(card.Id, 3, card.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.AttackDamage), 80, card.Abilities,
            UseLegacyAttack: false, Tags: card.Tags, OccupiedSlots: 2, ArmorAmount: card.Attributes.BaseCombat.GetBaseValue(GameAttributeKeys.Armor)) { Level = level };
    }
    private static CardBattleSetup Equipment(int start, int size) => new(EntityId.New(), start, 0, 0, [], UseLegacyAttack: false,
        Tags: new TagSet([GameTags.Equipment]), OccupiedSlots: size);
    private static int Value(BattleResult result, CardBattleSetup card, StringName key) => result.States.Last().Cards.Single(c => c.Id == card.EntityId).Values[key];
    private static BattleResult Run(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks) => new CombatSimulator().Simulate(Setup(allies, enemies, ticks));
    private static BattleSetup Setup(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks, bool reverse = false) => new(
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), reverse ? enemies : allies),
        new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), reverse ? allies : enemies),
        42, new BattleTick(ticks), new BattleTick(ticks + 1));
}
