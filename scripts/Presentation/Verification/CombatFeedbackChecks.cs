using System;
using System.Linq;
using Godot;
using Project_Star.Application.Combat;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Content.Skills;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Match;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Playtest;

namespace Project_Star.Presentation.Verification;

// 补齐获批状态层的冻结语义和等级实体覆盖验收。
internal static class CombatFeedbackChecks
{
    internal static bool CardLayer(Control owner)
    {
        var item = new CardItemView { Size = new Vector2(100, 200) }; owner.AddChild(item);
        try
        {
            var card = MatchDisplayQuery.FromOffer(ShopOffer.Create(new BoarCardDefinition(), 2));
            item.Render(card, new CardDisplayAdapter());
            var state = new CardBattleSnapshot(card.Id, SideId.Player, false, 20, 0, 0, [50m], card.CurrentValues)
                { CooldownDurationUnits = [100m], IsFlying = true, IsBerserk = true };
            var layer = item.GetNode<CardBattleOverlay>("BattleOverlay");
            item.RenderBattle(state, false);
            if (!layer.HasTrack || layer.RemainingSeconds != 2.5m || layer.Progress != .5f || !layer.GetNode<Label>("Haste").Visible) return false;
            item.RenderBattle(state with { Slow = 30 }, false);
            if (!layer.GetNode<Label>("Haste").Visible || !layer.GetNode<Label>("Slow").Visible || layer.ProgressColor != new Color("fae3ab")) return false;
            item.RenderBattle(state with { Immobilize = 15 }, false); layer._Process(1);
            if (layer.Progress != .5f || !layer.GetNode<Label>("Remaining").Text.StartsWith("Ⅱ") || state.CooldownUnits[0] != 50) return false;
            var hero = new HeroBattleSnapshot(100, 100, 0, 0, 100, 0, 0, 0, 0);
            var playback = new BattlePlaybackViewModel(10, true, 1, false, new(new BattleTick(10), 0, false, hero, hero, [state]), [],
                new System.Collections.Generic.Dictionary<EntityId, long> { [card.Id] = 10 })
                { VisualEvents = [new CardChargedEvent(new BattleTick(10), card.Id, card.Id, 5)] };
            item.RenderBattle(state, true, playback);
            var charge = layer.GetNode<Label>("Charge");
            if (!charge.Visible || charge.Text != "充能 +0.5s") return false;
            item.RenderBattle(state with { Destroyed = true }, false, playback);
            if (layer.HasTrack || charge.Visible || !layer.GetNode<Label>("Destroyed").Visible) return false;
            item.RenderBattle(state with { IsOnBench = true }, false);
            if (layer.Visible) return false;
            item.RenderBattle(state with { Haste = 0, IsFlying = false, IsBerserk = false, CooldownDurationUnits = [] }, false);
            if (layer.HasTrack || layer.GetNode<Label>("Flying").Visible || layer.GetNode<Label>("Berserk").Visible) return false;
            item.RenderBattle(null, false);
            return !layer.Visible && state.IsFlying && state.IsBerserk && state.CooldownUnits[0] == 50;
        }
        finally { owner.RemoveChild(item); item.Free(); }
    }

    internal static bool RecordedDamage(Control owner)
    {
        var snapshot = MatchSnapshot.From(new CreateMatchService(new EntityFactory()).Create(42, 100, new PaladinHeroDefinition()));
        var result = new CombatSimulator().Simulate(new BattleSetup(
            new(new HeroBattleSetup(EntityId.New(), 100, 10, ManaRegen: 0, Burn: 4, Poison: 3), []),
            new(new HeroBattleSetup(EntityId.New(), 100, 0, ManaRegen: 0, Burn: 5, Poison: 2), []), 42, new BattleTick(31)));
        var damage = result.Events.OfType<DamageDealtEvent>().ToArray();
        if (damage.Any(item => item.SourceKind != DamageSourceKind.Status || item.StatusOrigin is null)
            || damage.Where(item => item.Tick.Value == 30 && item.TargetSide == SideId.Opponent).Select(item => item.StatusOrigin).Distinct().Count() != 2) return false;
        var presenter = new BattlePlaybackPresenter(new BattleResolution(snapshot, snapshot, result, snapshot));
        var hud = new HeroBattleStatusView(); owner.AddChild(hud);
        try
        {
            presenter.Advance(.6); var frozen = presenter.Capture(); hud.Render(frozen.State.Player, frozen);
            if (hud.GetNode<Label>("Burn/Overlay/Damage").Text != "-0 / 甲-4" || !hud.GetNode<PanelContainer>("Burn").Visible) return false;
            presenter.TogglePause(); presenter.Advance(10); hud.Render(presenter.Capture().State.Player, presenter.Capture());
            if (presenter.Tick != 6 || hud.GetNode<Label>("Burn/Overlay/Damage").Text != "-0 / 甲-4") return false;
            presenter.TogglePause(); presenter.Advance(.4); var current = presenter.Capture(); hud.Render(current.State.Opponent, current, SideId.Opponent);
            if (hud.GetNode<Label>("Poison/Overlay/Damage").Text != "-2" || hud.GetNode<Label>("Poison/Row/Period").Text != "1s"
                || frozen.VisualEvents.OfType<DamageDealtEvent>().Any(item => item.StatusOrigin == BattleStatus.Poison)) return false;
            hud.Render(current.State.Player with { Armor = 0, Burn = 0, Poison = 0 });
            return !hud.GetNode<PanelContainer>("Armor").Visible && !hud.GetNode<PanelContainer>("Burn").Visible && !hud.GetNode<PanelContainer>("Poison").Visible;
        }
        finally { owner.RemoveChild(hud); hud.Free(); }
    }

    internal static bool LevelFrames(Control owner)
    {
        var parent = new Control(); owner.AddChild(parent);
        try
        {
            var icon = new SkillItemView(); parent.AddChild(icon);
            var skill = MatchDisplayQuery.FromSkill(new DefendSkillDefinition(), 1);
            for (var level = 1; level <= 5; level++)
            {
                icon.Render(skill with { Level = level }, new CardDisplayAdapter()); icon.SetSelected(true);
                if (!Frame(icon, level)) return false;
            }
            var encounters = new EncounterSelectionView(); parent.AddChild(encounters);
            encounters.Render("", [new(new StringName("verify.encounter"), new UiAction("遭遇")) { Level = 3 }]);
            if (!Frame(encounters.GetNode<Button>("Choices/Choice0"), 3)) return false;
            var mentor = new KeyedActionView(); parent.AddChild(mentor);
            mentor.Render("导师", [new(new StringName("verify.skill"), new UiAction("传授技能")) { Level = 2 }],
                illustration: new StringName("res://art/ui/encounters/artwork/boar-illustration.png"), level: 4);
            if (!Frame(mentor.GetNode<TextureRect>("Illustration"), 4) || !Frame(mentor.GetNode<Button>("ActionScroll/Actions/Action0"), 2)) return false;
            var stage = new BattleStageView(); parent.AddChild(stage);
            var view = new MatchPageViewModel(MatchPage.Preparation, "怪物", "", null, null, null, false, true, [], [], [], [], 0,
                new UiAction("", false), new UiAction("开始"), new UiAction("", false), new UiAction("", false)) { EncounterLevel = 5 };
            stage.Render(view);
            if (!Frame(stage.GetNode<TextureRect>("EnemyPortrait"), 5)) return false;
            var details = new CardDetailsView { ZIndex = 100 }; parent.AddChild(details);
            details.ShowCard(MatchDisplayQuery.FromOffer(ShopOffer.Create(new BoarCardDefinition(), 2)), Vector2.Zero, new Vector2(1600, 900));
            var detailBorder = details.GetNode<LevelBorderView>("LevelBorder"); detailBorder._Process(0);
            if (detailBorder.GetGlobalRect() != details.GetGlobalRect() || detailBorder.ZAsRelative || detailBorder.ZIndex <= details.ZIndex) return false;
            var hero = new PlayerHeroPanel(); parent.AddChild(hero);
            hero.Render(MatchSnapshot.From(new CreateMatchService(new EntityFactory()).Create(42, 100, new PaladinHeroDefinition())).Hero);
            hero.RenderSkills([skill], 0);
            var result = new ResultView(); parent.AddChild(result);
            result.Render(view with { Rewards = [new RewardItemViewModel(0, 1, "技能 · 4级", null, "") { Level = 4 }] });
            if (!result.FindChildren("LevelBorder", "Panel", true, false).OfType<Panel>().Any(border => ((StyleBoxFlat)border.GetThemeStylebox("panel")).BorderColor == CardLevelGem.LevelColor(4))) return false;
            return Frame(details, 2) && Frame(hero.GetNode<HBoxContainer>("SkillIcons").GetChild<Button>(0), 1)
                && hero.GetNodeOrNull<Panel>("LevelBorder") is null && hero.GetNode<TextureRect>("Identity/Portrait").GetNodeOrNull<Panel>("LevelBorder") is null;
        }
        finally { owner.RemoveChild(parent); parent.Free(); }
        static bool Frame(Control node, int level) => node.GetNodeOrNull<Panel>("LevelBorder") is { Visible: true } border
            && border.GetThemeStylebox("panel") is StyleBoxFlat style && style.BorderWidthLeft == 2 && style.BorderColor == CardLevelGem.LevelColor(level);
    }
}
