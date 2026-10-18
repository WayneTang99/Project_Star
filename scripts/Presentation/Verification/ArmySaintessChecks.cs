using System;
using System.Threading.Tasks;
using Project_Star.Application.Content;
using Project_Star.Application.Economy;
using Project_Star.Application.Match;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;
using System.Linq;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Content.Cards;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Verification;

// 随军圣女等级、魔法门槛及人类战斗内成长验证（表现层）。
internal static class ArmySaintessChecks
{
    internal static bool LevelsManaAndTargets()
    {
        var definition = new ArmySaintessCardDefinition();
        var identity = definition.Attributes.Identity;
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        using var image = texture?.GetImage();
        if (identity.DisplayName != "随军圣女" || identity.Size != CardSize.Small
            || identity.FactionKey != new StringName("paladin")
            || !identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            || !definition.Tags.Contains(GameTags.Human) || definition.InitialLevel != 1
            || definition.SupportsLevel(0) || definition.SupportsLevel(5)
            || image is null || image.GetHeight() != image.GetWidth() * 2 || !image.HasMipmaps()
            || !DefinitionRegistry.Scan(typeof(ArmySaintessChecks).Assembly).Cards.ContainsKey(identity.Key)) return false;
        foreach (var (level, bonus) in new[] { (1, 5), (2, 10), (3, 20), (4, 40) })
        foreach (var reverse in new[] { false, true })
        foreach (var mana in new[] { 9, 10, 20 })
        {
            var card = new EntityFactory().CreateCard(definition, level);
            var values = card.Attributes.BaseCombat.SnapshotFinalValues();
            if (values[GameAttributeKeys.CooldownTicks] != 60 || card.Abilities.Single().ManaCost != 10) return false;
            var source = new CardBattleSetup(card.Id, 0, 0, 60, card.Abilities, UseLegacyAttack: false,
                Tags: card.Tags, ElementKeys: identity.ElementKeys) { CombatValues = values, Level = level };
            var human = Target(1, GameTags.Human);
            var beast = Target(2, GameTags.Beast);
            var bench = Target(3, GameTags.Human) with { IsOnBench = true };
            var dead = Target(4, GameTags.Human) with { Abilities = [new AbilityDefinition(
                new StringName("ability.verification_saintess_destroy"), AbilityActivation.PassiveOnBattleStart,
                AbilityTarget.SelfCard, 0, 0, [new DestroyCardEffectDefinition(false)])] };
            var enemy = Target(0, GameTags.Human);
            var allies = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0,
                InitialMana: mana, ManaRegen: 0), [source, human, beast, bench, dead]);
            var enemies = new BattleSideSetup(new HeroBattleSetup(EntityId.New(), 10000, 0, ManaRegen: 0), [enemy]);
            var setup = new BattleSetup(reverse ? enemies : allies, reverse ? allies : enemies,
                42, new BattleTick(121), new BattleTick(122));
            var result = new CombatSimulator().Simulate(setup);
            var frame = result.States.Last(state => state.Tick.Value == 120);
            var changes = result.Events.OfType<CardAttributeChangedEvent>().ToArray();
            var activations = result.Events.OfType<AbilityActivatedEvent>().Where(item => item.SourceCardId == card.Id).ToArray();
            var count = mana / 10;
            if (activations.Length != count || (count > 0 && activations[0].Tick.Value != 60)
                || (count == 2 && activations[1].Tick.Value != 120)
                || changes.Length != count * 2 || changes.Any(change => change.Amount != bonus
                    || change.CardId != source.EntityId && change.CardId != human.EntityId)
                || Value(source) != bonus * count || Value(human) != bonus * count
                || Value(beast) != 0 || Value(bench) != 0 || Value(dead) != 0 || Value(enemy) != 0
                || (reverse ? frame.Opponent : frame.Player).Mana != mana - count * 10
                || result.Events.OfType<DamageDealtEvent>().Any()
                || result.PermanentChanges.Any() || result.PermanentAttributeBonuses.Any()
                || !result.Events.SequenceEqual(new CombatSimulator().Simulate(setup).Events)
                || card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) != 0) return false;
            int Value(CardBattleSetup target) => frame.Cards.Single(item => item.Id == target.EntityId)
                .Values[GameAttributeKeys.AttackDamage];
        }
        return true;
    }

    private static CardBattleSetup Target(int start, StringName tag) => new(EntityId.New(), start, 0, 0, [],
        UseLegacyAttack: false, Tags: TagSet.ForCard(CardSize.Small, [tag]));

    internal static async Task Capture(Node owner)
    {
        var viewport = new SubViewport { Size = new Vector2I(240, 420), Disable3D = true,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always }; owner.AddChild(viewport);
        var preview = new CardPreviewView { Size = owner.GetViewport().GetVisibleRect().Size, Theme = MatchTheme.Create() };
        owner.AddChild(preview);
        try
        {
            viewport.AddChild(new ColorRect { Color = new Color("333c41"), Size = new Vector2(240, 420) });
            var face = new CardItemView { Position = new Vector2(24, 24), Size = new Vector2(192, 372) };
            viewport.AddChild(face);
            face.Render(MatchDisplayQuery.FromOffer(ShopOffer.Create(new ArmySaintessCardDefinition())), new CardDisplayAdapter());
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = viewport.GetTexture().GetImage();
            if (image.SavePng("res://output/army-saintess/card-face.png") != Error.Ok)
                throw new InvalidOperationException("随军圣女卡面截图失败。");
            preview.SetQuery(new CardPreviewQuery(DefinitionRegistry.Scan(typeof(ArmySaintessChecks).Assembly)));
            preview.Open(MatchDisplayQuery.FromOffer(ShopOffer.Create(new ArmySaintessCardDefinition())), null, null);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var enlarged = owner.GetViewport().GetTexture().GetImage();
            if (enlarged.SavePng("res://output/army-saintess/preview.png") != Error.Ok)
                throw new InvalidOperationException("随军圣女放大预览截图失败。");
        }
        finally
        {
            owner.RemoveChild(viewport); viewport.Free();
            owner.RemoveChild(preview); preview.Free();
        }
    }
}



