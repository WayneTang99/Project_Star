using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Content;
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

// 战舰炮手分级、全体载具攻击累加与发动回响验证（表现层）。
internal static class WarshipGunnerChecks
{
    internal static bool LevelsAndCombat()
    {
        var definition = new WarshipGunnerCardDefinition(); var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.Key != new StringName("card.warship_gunner") || identity.DisplayName != "战舰炮手"
            || identity.FactionKey != new StringName("paladin") || identity.Size != CardSize.Medium
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.Water }) || definition.InitialLevel != 2
            || definition.SupportsLevel(1) || definition.SupportsLevel(5)
            || !definition.Tags.ToArray().ToHashSet().SetEquals(new[] { GameTags.Medium, GameTags.Human })
            || !DefinitionRegistry.Scan(typeof(WarshipGunnerChecks).Assembly).Cards.ContainsKey(identity.Key)
            || texture is null || texture.GetWidth() != texture.GetHeight()) return false;
        foreach (var level in new[] { 2, 3, 4 })
        foreach (var reverse in new[] { false, true })
        {
            var source = Gunner(level); var bonus = 20 << (level - 2); var damage = bonus * 3;
            var vehicle = Vehicle(2); var zero = Vehicle(3) with { AttackDamage = 0, Abilities = [] };
            var nonVehicle = Vehicle(4) with { Tags = new TagSet([GameTags.Mechanical]) };
            var bench = Vehicle(5) with { IsOnBench = true };
            var dead = Vehicle(6) with { Abilities = [Opening(new DestroyCardEffectDefinition(false))] };
            var enemy = Vehicle(0);
            var setup = Battle([source, vehicle, zero, nonVehicle, bench, dead], [enemy], 100, reverse);
            var result = new CombatSimulator().Simulate(setup);
            var changes = result.Events.OfType<CardAttributeChangedEvent>().ToArray();
            var hits = result.Events.OfType<DamageDealtEvent>().Where(e => e.SourceCardId == source.EntityId).ToArray();
            if (source.Abilities!.Single(a => a.Activation == AbilityActivation.Active).CooldownTicks != 50
                || source.Abilities!.Any(a => a.ManaCost != 0) || changes.Length != 4
                || changes.Any(e => e.Amount != bonus || e.AttributeKey != GameAttributeKeys.AttackDamage
                    || e.CardId != vehicle.EntityId && e.CardId != zero.EntityId)
                || Attack(result, vehicle) != 10 + bonus * 2 || Attack(result, zero) != bonus * 2
                || Attack(result, nonVehicle) != 10 || Attack(result, bench) != 10 || Attack(result, dead) != 10 || Attack(result, enemy) != 10
                || hits.Length != 1 || hits[0].Tick.Value != 70 || hits[0].RawDamage != damage
                || hits[0].TargetSide != (reverse ? SideId.Player : SideId.Opponent)
                || result.Events.OfType<DamageDealtEvent>().Single(e => e.SourceCardId == vehicle.EntityId).RawDamage != 10 + bonus
                || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)) return false;
            var multi = new CombatSimulator().Simulate(Battle([source with { Multicast = 1 }, vehicle with { Multicast = 1 }], [], 72, reverse));
            if (Attack(multi, vehicle) != 10 + bonus * 2
                || multi.Events.OfType<DamageDealtEvent>().Count(e => e.SourceCardId == source.EntityId && e.RawDamage == damage) != 2) return false;
            var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level));
            var rules = CardDisplayAdapter.AbilityDetails(snapshot.Abilities);
            if (CardDisplayAdapter.CooldownSeconds(snapshot) != 5 || !rules.Contains("载具")
                || !CardDisplayAdapter.FaceEffects(snapshot).Any(e => e.Kind == CardFaceEffectKind.Damage && e.Value == damage.ToString())) return false;
        }
        return true;
    }

    internal static bool FiltersAndLifetime()
    {
        var source = Gunner(2); var vehicle = Vehicle(4);
        var other = Gunner(4) with { BoardStart = 2 };
        var stacked = new CombatSimulator().Simulate(Battle([source, other, vehicle], [], 70));
        if (Attack(stacked, vehicle) != 110
            || stacked.Events.OfType<DamageDealtEvent>().Count(e => e.SourceCardId == source.EntityId || e.SourceCardId == other.EntityId) != 2) return false;
        foreach (var inactive in new[] { source with { IsOnBench = true }, source with { Abilities = [Opening(new DestroyCardEffectDefinition(false))] } })
        {
            var result = new CombatSimulator().Simulate(Battle([inactive, vehicle], [], 70));
            if (Attack(result, vehicle) != 10 || result.Events.OfType<DamageDealtEvent>().Any(e => e.SourceCardId == source.EntityId)) return false;
        }
        var dying = source with { Abilities = source.Abilities!.Append(new AbilityDefinition(new StringName("verification.gunner.destroy"),
            AbilityActivation.Active, AbilityTarget.SelfCard, 0, 55, [new DestroyCardEffectDefinition(false)])).ToArray() };
        var stopped = new CombatSimulator().Simulate(Battle([dying, vehicle], [], 70));
        if (Attack(stopped, vehicle) != 30 || stopped.Events.OfType<DamageDealtEvent>().Any(e => e.SourceCardId == source.EntityId)) return false;
        var unpaid = vehicle with { Abilities = [new AbilityDefinition(new StringName("verification.unpaid_vehicle"),
            AbilityActivation.Active, AbilityTarget.EnemyHero, 1, 70, [new DamageEffectDefinition(1)])] };
        if (new CombatSimulator().Simulate(Battle([source, unpaid], [], 70)).Events.OfType<DamageDealtEvent>().Any()) return false;
        var empty = new CombatSimulator().Simulate(Battle([source], [], 100));
        return !empty.Events.OfType<CardAttributeChangedEvent>().Any() && !empty.Events.OfType<DamageDealtEvent>().Any()
            && empty.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == source.EntityId && !e.IsEcho) == 2
            && Attack(new CombatSimulator().Simulate(Battle([source, vehicle], [], 49)), vehicle) == 10;
    }

    private static CardBattleSetup Gunner(int level)
    {
        var card = new EntityFactory().CreateCard(new WarshipGunnerCardDefinition(), level);
        return new CardBattleSetup(card.Id, 0, card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
            50, card.Abilities, UseLegacyAttack: false, Tags: card.Tags, OccupiedSlots: 2,
            ElementKeys: card.Attributes.Identity.ElementKeys)
            { Level = level, CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues() };
    }

    private static CardBattleSetup Vehicle(int position) => new(EntityId.New(), position, 10, 70,
        [new AbilityDefinition(new StringName("verification.vehicle_attack"), AbilityActivation.Active, AbilityTarget.EnemyHero,
            0, 70, [new AttributeDamageEffectDefinition(GameAttributeKeys.AttackDamage)])],
        UseLegacyAttack: false, Tags: new TagSet([GameTags.Vehicle]));

    private static AbilityDefinition Opening(params EffectDefinition[] effects) => new(new StringName("verification.opening"),
        AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, effects);

    private static int Attack(BattleResult result, CardBattleSetup card) => result.States[^1].Cards.Single(c => c.Id == card.EntityId)
        .Values[GameAttributeKeys.AttackDamage];

    private static BattleSetup Battle(CardBattleSetup[] allies, CardBattleSetup[] enemies, int ticks, bool reverse = false)
    {
        var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), allies);
        var other = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), enemies);
        return new BattleSetup(reverse ? other : own, reverse ? own : other, 42, new BattleTick(ticks), new BattleTick(ticks + 1));
    }

    internal static async Task Capture(Node owner)
    {
        if (!LevelsAndCombat() || !FiltersAndLifetime()) throw new InvalidOperationException("战舰炮手验证失败。");
        var snapshot = MatchDisplayQuery.FromOffer(ShopOffer.Create(new WarshipGunnerCardDefinition()));
        using (var textureImage = GD.Load<Texture2D>(snapshot.Illustration.ToString()).GetImage())
        {
            if (!textureImage.HasMipmaps()) throw new InvalidOperationException("战舰炮手原画未实际加载mipmap。");
            GD.Print($"战舰炮手原画：{textureImage.GetWidth()}×{textureImage.GetHeight()}，mipmap层数{textureImage.GetMipmapCount()}。");
        }
        var viewport = new SubViewport { Size = new Vector2I(448, 448), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(448, 448) });
            var face = new CardItemView { Position = new Vector2(24, 24), Size = new Vector2(400, 400) };
            viewport.AddChild(face);
            face.Render(snapshot, new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/warship-gunner/card-face.png") != Error.Ok)
                throw new InvalidOperationException("战舰炮手卡面截图保存失败。");
            face.Size = new Vector2(100, 100); viewport.Size = new Vector2I(148, 148);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var compact = viewport.GetTexture().GetImage();
            if (compact.SavePng("res://output/warship-gunner/compact.png") != Error.Ok)
                throw new InvalidOperationException("战舰炮手紧凑卡面截图保存失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
        var preview = new CardPreviewView { Size = owner.GetViewport().GetVisibleRect().Size, Theme = MatchTheme.Create() };
        owner.AddChild(preview);
        try
        {
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(WarshipGunnerChecks).Assembly)));
            preview.Open(snapshot, null, null);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = owner.GetViewport().GetTexture().GetImage();
            if (image.SavePng("res://output/warship-gunner/preview.png") != Error.Ok)
                throw new InvalidOperationException("战舰炮手放大预览截图保存失败。");
        }
        finally { owner.RemoveChild(preview); preview.Free(); }
    }
}
