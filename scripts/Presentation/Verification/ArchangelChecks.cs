using System;
using System.Linq;
using System.Threading.Tasks;
using Project_Star.Application.Economy;
using Project_Star.Application.Match;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 大天使号身份、伤害、双方分类充能及回响截断验证（表现层）。
internal static class ArchangelChecks
{
    internal static bool LevelsAndCharge()
    {
        var definition = new ArchangelCardDefinition();
        var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        if (identity.DisplayName != "大天使号" || identity.FactionKey != new StringName("paladin")
            || identity.Size != CardSize.Large || definition.InitialLevel != 3
            || definition.SupportsLevel(2) || definition.SupportsLevel(5)
            || !identity.ElementKeys.ToHashSet().SetEquals(new[] { GameElements.Light, GameElements.Water })
            || !definition.Tags.ToArray().ToHashSet().SetEquals(new[] { GameTags.Large, GameTags.Mechanical, GameTags.Vehicle })
            || TagDisplayNames.Get(GameTags.Vehicle) != "载具"
            || !DefinitionRegistry.Scan(typeof(ArchangelChecks).Assembly).Cards.ContainsKey(identity.Key)
            || texture is null || texture.GetWidth() * 2 != texture.GetHeight() * 3) return false;
        foreach (var level in new[] { 3, 4 })
        foreach (var reverse in new[] { false, true })
        {
            var card = new EntityFactory().CreateCard(definition, level);
            var ship = new CardBattleSetup(card.Id, 0, card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage),
                200, card.Abilities, UseLegacyAttack: false,
                Tags: card.Tags, OccupiedSlots: 3, ElementKeys: identity.ElementKeys)
                { Level = level, CombatValues = card.Attributes.BaseCombat.SnapshotFinalValues() };
            var active = new CardBattleSetup(EntityId.New(), 3, 0, 190,
                [new AbilityDefinition(new StringName("verification.activation"), AbilityActivation.Active,
                    AbilityTarget.SelfCard, 0, 190, [new GainSourceHeroArmorEffectDefinition(1)])],
                UseLegacyAttack: false, ElementKeys: [GameElements.Light]);
            if (!Check(ship, [], [], reverse, level, 200, 1)) return false;
            foreach (var fixture in new[] { active, active with { Multicast = 1 } })
                if (!Check(ship, [fixture], [], reverse, level, 190, fixture.Multicast + 1)) return false;
            foreach (var tags in new[] { new[] { GameTags.Demon }, new[] { GameTags.Undead }, new[] { GameTags.Demon, GameTags.Undead } })
                if (!Check(ship, [], [active with { Tags = new TagSet(tags) }], reverse, level, 190, 1)) return false;
            // 错误归属、非光元素、备战与未支付魔法均不能触发。
            foreach (var fixture in new[] { active with { ElementKeys = [GameElements.Water], Tags = new TagSet([GameTags.Demon]) },
                active with { IsOnBench = true }, active with { Abilities = [new AbilityDefinition(
                    new StringName("verification.unpaid"), AbilityActivation.Active, AbilityTarget.SelfCard,
                    1, 190, [new GainSourceHeroArmorEffectDefinition(1)])] } })
                if (!Check(ship, [fixture], [], reverse, level, 200, 1)) return false;
            if (!Check(ship, [], [active], reverse, level, 200, 1)
                || !Check(ship with { IsOnBench = true }, [active], [], reverse, level, 0, 0)) return false;
        }
        return true;
    }

    internal static async Task Capture(Node owner)
    {
        if (!LevelsAndCharge()) throw new InvalidOperationException("大天使号战斗验证失败。");
        var viewport = new SubViewport { Size = new Vector2I(648, 448), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(648, 448) });
            var face = new CardItemView { Position = new Vector2(24, 24), Size = new Vector2(600, 400) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new ArchangelCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/archangel/card-face.png") != Error.Ok)
                throw new InvalidOperationException("大天使号卡面截图保存失败。");
        }
        finally { owner.RemoveChild(viewport); viewport.Free(); }
    }

    private static bool Check(CardBattleSetup ship, CardBattleSetup[] allies, CardBattleSetup[] enemies,
        bool reverse, int level, int tick, int echoes)
    {
        var own = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), [ship, .. allies]);
        var other = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), enemies);
        var setup = new BattleSetup(reverse ? other : own, reverse ? own : other, 42, new BattleTick(201), new BattleTick(202));
        var result = new CombatSimulator().Simulate(setup);
        var hits = result.Events.OfType<DamageDealtEvent>().Where(e => e.SourceCardId == ship.EntityId).ToArray();
        var charges = result.Events.OfType<CardChargedEvent>().Where(e => e.SourceCardId == ship.EntityId).ToArray();
        var passed = (tick == 0 ? hits.Length == 0 : hits.Length == 1 && hits[0].Tick.Value == tick
                && hits[0].RawDamage == (level == 3 ? 500 : 1000)
                && hits[0].TargetSide == (reverse ? SideId.Player : SideId.Opponent))
            && charges.Length == echoes && charges.All(e => e.TargetCardId == ship.EntityId && e.AmountTicks == 10)
            && result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events);
        if (!passed) GD.Print($"大天使号验证：level={level} reverse={reverse} allies={allies.Length} enemies={enemies.Length} expected={tick}/{echoes} hits={string.Join(',', hits.Select(e => $"{e.Tick.Value}:{e.RawDamage}"))} charges={charges.Length}");
        return passed;
    }
}
