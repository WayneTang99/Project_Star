using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Mentors;
using Project_Star.Application.Match;
using Project_Star.Application.Content;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// 只读视觉夹具与 HTML 展示同一内容，不进入正式内容池或可变对局。
internal static class HtmlParityCapture
{
    public static async Task Run(Control owner)
    {
        var root = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<MinimalPlaytest>();
        var shell = root.GetNode<MatchShell>("MatchShell"); root.RemoveChild(shell); root.Free(); owner.AddChild(shell);
        shell.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        GD.Print($"16:9捕获：窗口 {owner.GetWindow().Size}，逻辑视口 {owner.GetViewportRect().Size}，伸缩 {ProjectSettings.GetSetting("display/window/stretch/aspect")}");
        var registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        shell.SetCatalog(CardCatalogQuery.Capture(registry)); shell.SetSkillCatalog(SkillCatalogQuery.Capture(registry));
        var snapshot = MatchSnapshot.From(new CreateMatchService(new EntityFactory()).Create(42, 34, new PaladinHeroDefinition()));
        CardSnapshot Sample(CardDefinition definition, int level, int value) => MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level)) with { Id = EntityId.New(), Value = value };
        var hammer = Sample(new JudgmentHammerCardDefinition(), 3, 24) with { DescriptionEntries = Array.AsReadOnly(new[] { new CardDescriptionEntry(CardKeywords.Activate, "对敌方英雄造成其最大生命 20% 的普通伤害。") }) };
        var arm = Sample(new ArmguardCardDefinition(), 2, 4);
        var boar = Sample(new BoarCardDefinition(), 2, 8);
        var toad = Sample(new JadeToadCardDefinition(), 2, 16);
        var mace = Sample(new FlangedMaceCardDefinition(), 3, 8);
        var potion = Sample(new SmallRedPotionCardDefinition(), 2, 4);
        var berries = Sample(new BerriesCardDefinition(), 1, 2);
        var cauldron = Sample(new AlchemyCauldronCardDefinition(), 3, 16) with
        {
            DisplayName = "炼金坩埚", ElementKeys = Array.AsReadOnly(new[] { GameElements.Water, GameElements.Wood }),
            GemSockets = Array.AsReadOnly(new GemSnapshot?[] { null, new(new StringName("ui.sample.gem"), "示例宝石", "视觉对照夹具"), null }),
            Abilities = Array.AsReadOnly(new[] { new AbilityDefinition(new StringName("ui.sample.cauldron"), AbilityActivation.Active, AbilityTarget.EnemyHero, 0, 50,
                new EffectDefinition[] { new ApplyStatusEffectDefinition(BattleStatus.Poison, 12), new RestoreManaEffectDefinition(8) }) }),
        };
        var cards = new[] { hammer, arm, boar, toad, mace, potion, potion with { Id = EntityId.New() }, berries, cauldron };
        var placements = new List<BoardPlacementSnapshot>(); var start = 0;
        for (var index = 0; index < cards.Length; index++)
        {
            if (index == 6) start = 0;
            placements.Add(new(cards[index].Id, index < 6 ? BoardZone.Battlefield : BoardZone.Bench, start, start + (int)cards[index].Size)); start += (int)cards[index].Size;
        }
        var heroValues = new Dictionary<StringName, int>(snapshot.Hero!.CombatValues) { [GameAttributeKeys.MaxHealth] = 600, [GameAttributeKeys.Mana] = 80, [GameAttributeKeys.MaxMana] = 100, [GameAttributeKeys.Armor] = 25, [GameAttributeKeys.HealthRegen] = 10 };
        snapshot = snapshot with { Cards = Array.AsReadOnly(cards), BoardPlacements = placements.AsReadOnly(), BattlefieldCount = 6, BenchCount = 3,
            Round = 2, Turn = 3, Wealth = 34, Income = 5, Experience = 6, Reputation = 18, PvpWins = 3,
            Hero = snapshot.Hero with { Level = 4, CombatValues = heroValues },
            Skills = Array.AsReadOnly(new[] { "skill.divine_smite", "skill.defend", "skill.charge" }.Where(key => registry.Skills.ContainsKey(new StringName(key))).Select(key => { var definition = registry.Skills[new StringName(key)]; return MatchDisplayQuery.FromSkill(definition, definition.InitialLevel); }).ToArray()) };
        var offers = new[] { mace, toad, hammer }.Select((card, index) => new ShopItemViewModel(index, 1, new UiAction("购买"), card) { Price = new[] { 8, 12, 20 }[index] }).ToArray();
        var view = new MatchPageViewModel(MatchPage.Shop, "旅人集市", "沿着星光寻找，\n为下一场战斗添一份胜算。", snapshot, snapshot, null, true, false,
            Array.Empty<KeyedAction>(), Array.Empty<KeyedAction>(), Array.AsReadOnly(offers), Array.Empty<KeyedAction>(), 1,
            new UiAction("刷新 · 8"), new UiAction("开始战斗", false), new UiAction("继续旅程　→"), new UiAction("奖励", false))
        { ShopLevel = 4, DisplayRound = 2, DisplayTurn = 3, ContextIllustration = new StringName("res://art/ui/encounters/artwork/large_shop-illustration.png") };
        shell.Render(view);
        shell.GetNode<PlayerHeroPanel>("BenchRow/Hero/PlayerHeroPanel").GetNode<ResourceBar>("HealthBar").Render("生命", 450, 600);
        await Save("shop");
        shell.GetNode<Button>("OpenGameMenu").EmitSignal(Button.SignalName.Pressed); await Save("menu");
        var menu = shell.GetNode<MatchMenuView>("GameMenu");
        menu.GetNode<Button>("ItemsPanel/Items/Abandon").EmitSignal(Button.SignalName.Pressed); await Save("abandon"); menu.Close();
        shell.GetNode<Button>("GameMenu/ItemsPanel/Items/OpenCardCatalog").EmitSignal(Button.SignalName.Pressed); await Save("catalog");
        shell.GetNode<CardCatalogView>("CardCatalog").Close();
        var attackFace = shell.GetNode<BoardZoneView>("BattlefieldRow/Content/Board").GetNode<CardItemView>($"Card_{arm.Id.Value:N}")
            .GetChildren().OfType<Project_Star.Presentation.CardFace.CardFace>().Single();
        var attackRow = attackFace.GetNode<Control>("Effects").GetChild<CardEffectRow>(0);
        var attackValue = attackRow.GetNode<Label>("Value");
        var textWidth = attackValue.GetThemeFont("font").GetStringSize(attackValue.Text, fontSize: attackValue.GetThemeFontSize("font_size")).X;
        if (arm.CurrentValues.GetValueOrDefault(GameAttributeKeys.AttackDamage) != 10 || attackValue.Text != "10"
            || !attackValue.IsVisibleInTree() || attackValue.Size.X + .1f < textWidth
            || !attackRow.GetNode<TextureRect>("Icon").Texture.ResourcePath.EndsWith("damage.svg", StringComparison.Ordinal))
            throw new InvalidOperationException($"小型卡攻击10被裁切：{attackValue.Text}，可用{textWidth}/{attackValue.Size.X}。");
        GD.Print($"小型护腕攻击10：字宽{textWidth:0.00}，可用{attackValue.Size.X:0.00}，实际窗口{owner.GetWindow().Size}。");
        await Save("attack10");
        var scrollbar = shell.GetNode<VScrollBar>("DesktopScroll"); scrollbar.Value = scrollbar.MaxValue - scrollbar.Page; await Save("shop-bottom"); scrollbar.Value = 0;
        var details = shell.GetNode<CardDetailsView>("CardDetails");
        var item = shell.GetNode<BoardZoneView>("BattlefieldRow/Content/Board").GetNode<CardItemView>($"Card_{hammer.Id.Value:N}");
        item._GuiInput(new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true }); await Save("details");
        var physicalDetailSize = details.Size * details.Scale * owner.GetViewport().GetStretchTransform().Scale;
        if (!Mathf.IsEqualApprox(physicalDetailSize.X, 380)) throw new InvalidOperationException($"详情屏幕宽度错误：{physicalDetailSize}。");
        GD.Print($"详情屏幕尺寸{physicalDetailSize}，正文宽{details.GetNode<CardDetailsContent>("Content/CardContent").Size.X}，伸缩{details.Scale}。");
        var originalWindowSize = owner.GetWindow().Size;
        owner.GetWindow().Size = originalWindowSize.X == 1280 ? new Vector2I(1920, 1080) : new Vector2I(1280, 720);
        await Save($"details-resized-from-{originalWindowSize.X}x{originalWindowSize.Y}");
        var resizedScale = owner.GetViewport().GetStretchTransform().Scale.X;
        var resizedWidth = details.GetGlobalRect().Size.X * resizedScale;
        var resizedGap = (details.GlobalPosition.X - item.GetGlobalRect().End.X) * resizedScale;
        if (Mathf.Abs(resizedWidth - 380) > .05f || Mathf.Abs(resizedGap - 16) > .05f
            || details.CurrentCardId != hammer.Id || !details.Visible)
            throw new InvalidOperationException($"固定详情缩放后未跟随：宽{resizedWidth}，间距{resizedGap}，可见{details.Visible}，身份{details.CurrentCardId}/{hammer.Id}。");
        owner.GetWindow().Size = originalWindowSize;
        await Save("details-restored");
        GD.Print("固定详情分辨率变化及恢复：380px宽度、16px相邻间距与卡牌身份保持。");
        details.ShowNear(cauldron, item.GetGlobalRect(), shell.Size); await Save("details-multiple");
        details.GetNode<Button>("Content/CardContent/InstanceToggle").EmitSignal(Button.SignalName.Pressed);
        await Save("details-instance");
        cauldron = cauldron with { QuestDefinitions = Array.AsReadOnly(new[] { new CardQuestDefinition(new StringName("ui.sample.quest"),
            new BattleVictoryQuestConditionDefinition(), 5, [], [new ArmorEffectDefinition(1)]) }) };
        var state = new CardBattleSnapshot(cauldron.Id, SideId.Player, true, 15, 20, 30, Array.Empty<decimal>(), cauldron.CurrentValues)
        {
            IsFlying = true, IsBerserk = true,
            Quests = Array.AsReadOnly(new[] { new CardQuestBattleSnapshot(new StringName("ui.sample.quest"), 3, 5, false) }),
        };
        details.ShowNear(cauldron, item.GetGlobalRect(), shell.Size, state); await Save("details-status");
        details.RefreshCard(cauldron, shell.Size, state with { Destroyed = false, Haste = 0, Slow = 0, Immobilize = 0, IsFlying = false, IsBerserk = false,
            Quests = Array.AsReadOnly(new[] { new CardQuestBattleSnapshot(new StringName("ui.sample.quest"), 5, 5, true) }) }); await Save("details-quest");
        var longCard = cauldron with { Value = 0, Abilities = Array.Empty<AbilityDefinition>(),
            DescriptionEntries = Array.AsReadOnly(Enumerable.Range(0, 20).Select(_ => new CardDescriptionEntry(CardKeywords.Passive,
                "当前数值 0；攻击公式 = 英雄最大生命 × 20% + 护甲 × 1.5。长说明保留全部文字，展开宝石和当前属性后正文仍可滚动阅读。")).ToArray()) };
        details.ShowNear(longCard, new Rect2(new Vector2(shell.Size.X - 100, shell.Size.Y - 80), new Vector2(70, 60)), shell.Size);
        details.GetNode<Button>("Content/CardContent/InstanceToggle").EmitSignal(Button.SignalName.Pressed);
        await Save("details-long"); details.Hide();
        var choices = new[]
        {
            new KeyedAction(new StringName("ui.sample.shop"), new UiAction("旅人集市")) { Level = 4, ShopLevel = 4, Illustration = view.ContextIllustration, Subtitle = "购买卡牌，完善你的构筑" },
            new KeyedAction(new StringName("ui.sample.forest"), new UiAction("暮歌丛林")) { Level = 3, Illustration = new StringName("res://art/ui/encounters/artwork/dusk_song_jungle-illustration.png"), Subtitle = "采摘植物，或探寻林间秘密" },
            new KeyedAction(new StringName("ui.sample.boar"), new UiAction("野猪巢穴")) { Level = 1, Illustration = new StringName("res://art/ui/encounters/artwork/boar-illustration.png"), Subtitle = "迎战怪物，获取战斗奖励" },
        };
        shell.Render(view with { Page = MatchPage.EncounterChoice, Title = "林间岔路", Message = "命运藏在每一次选择里。\n下一站，将通往何处？", Choices = Array.AsReadOnly(choices), ContextIllustration = choices[1].Illustration, Refresh = new UiAction("刷新", false) }); await Save("encounter");
        var monster = MatchSnapshot.From(new Project_Star.Infrastructure.Encounters.LocalTestOpponentProvider(registry).CreateMonsterOpponent(42, registry.Monsters["monster.boar"]));
        var battleView = view with { Enemy = monster, Page = MatchPage.Preparation, Title = "战斗准备", EncounterLevel = 1, Message = "确认你的阵容。\n让每一张卡牌各尽其用。", EnemyVisible = true, Battle = new UiAction("开始战斗"), Continue = new UiAction("继续", false), Refresh = new UiAction("刷新", false), ContextIllustration = choices[2].Illustration };
        shell.Render(battleView); await Save("battle");
        // 只读夹具使用真实模拟器生成状态和事件，再由正式回放器播放；不向对局写入示例能力。
        var simulated = new CombatSimulator().Simulate(new BattleSetup(Side(snapshot, true), Side(monster, false), 42, new BattleTick(100)));
        var replay = new BattlePlaybackPresenter(new BattleResolution(snapshot, monster, simulated, snapshot));
        replay.Advance(1); replay.TogglePause();
        shell.Render(battleView with { Page = MatchPage.BattlePlayback, Battle = new UiAction("开始战斗", false), BoardEnabled = false, Playback = replay.Capture(), Player = replay.Project(SideId.Player), Enemy = replay.Project(SideId.Opponent) });
        await Save("combat-status");
        replay.TogglePause(); replay.Advance(2); replay.TogglePause();
        shell.Render(battleView with { Page = MatchPage.BattlePlayback, Battle = new UiAction("开始战斗", false), BoardEnabled = false, Playback = replay.Capture(), Player = replay.Project(SideId.Player), Enemy = replay.Project(SideId.Opponent) });
        await Save("combat-activation");
        var mentor = registry.Mentors.Values.First();
        var visitSession = new CreateMatchService(new EntityFactory()).Create(42, 34, new PaladinHeroDefinition());
        var visit = new MentorService(registry, new SkillAcquisitionService(new EntityFactory())).Open(visitSession, mentor.Attributes.Identity.Key, 2).Value!;
        shell.Render(view with { Page = MatchPage.Event, Title = mentor.Attributes.Identity.DisplayName, Message = "选择一个技能。", EncounterLevel = 2,
            Refresh = new UiAction("刷新", false), Continue = new UiAction("继续", false), ContextIllustration = mentor.Attributes.Identity.Illustration,
            EventOptions = visit.Offers.Select(offer =>
            {
                var skill = MatchDisplayQuery.FromSkill(registry.Skills[offer.SkillKey], offer.Level);
                return new KeyedAction(skill.Key, new UiAction($"{skill.DisplayName} · {skill.Level}级"))
                    { Level = skill.Level, Subtitle = CardDisplayAdapter.AbilityDetails(skill.Abilities, skill.CurrentValues) };
            }).ToArray() });
        await Save("mentor");
        shell.Render(view with { Page = MatchPage.HeroSelection, Heroes = registry.Heroes.Values.Select(definition => new KeyedAction(definition.Attributes.Identity.Key, new UiAction(definition.Attributes.Identity.DisplayName)) { Illustration = definition.Attributes.Identity.Illustration, Hero = HeroSelectionDetails.From(definition) }).ToArray() });
        shell.GetNode<Button>("GameMenu/ItemsPanel/Items/OpenDisplaySettings").EmitSignal(Button.SignalName.Pressed); await Save("settings");
        shell.GetNode<DisplaySettingsView>("DisplaySettings").Hide();
        shell.Render(view); shell.SetDesktopPalette(true); await Save("blue"); shell.SetDesktopPalette(false);
        owner.GetTree().Quit();
        BattleSideSetup Side(MatchSnapshot side, bool player)
        {
            var identity = side.Hero!; var values = identity.CombatValues;
            var setups = side.BoardPlacements.Where(place => place.Zone == BoardZone.Battlefield).Select((place, index) =>
            {
                var card = side.Cards.Single(value => value.Id == place.CardId);
                var effects = new List<EffectDefinition>();
                if (player && index == 0) effects.Add(new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 60));
                if (player && index == 1) { effects.Add(new SetSourceCardStateEffectDefinition(GameAttributeKeys.Flying, true)); effects.Add(new SetSourceCardStateEffectDefinition(GameAttributeKeys.Berserk, true)); }
                if (player && index == 2) effects.Add(new ApplyStatusEffectDefinition(BattleStatus.ImmobilizeDuration, 25));
                if (player && index == 3) { effects.Add(new ApplyStatusEffectDefinition(BattleStatus.HasteDuration, 60)); effects.Add(new ApplyStatusEffectDefinition(BattleStatus.SlowDuration, 60)); }
                if (player && index == 4 || !player && index == 0) effects.Add(new ApplyStatusEffectDefinition(BattleStatus.SlowDuration, 60));
                var abilities = card.Abilities.ToList();
                if (effects.Count > 0) abilities.Add(new(new StringName($"ui.capture.status.{index}"), AbilityActivation.PassiveOnBattleStart, AbilityTarget.SelfCard, 0, 0, effects));
                if (player && index == 3) abilities.Add(new(new StringName("ui.capture.charge"), AbilityActivation.EchoOnFirstAlliedCardActivated,
                    AbilityTarget.SelfCard, 0, 0, [new ChargeSourceCardEffectDefinition(10)]));
                return new CardBattleSetup(card.Id, place.Start, card.CurrentValues.GetValueOrDefault(GameAttributeKeys.AttackDamage), 0,
                    abilities, UseLegacyAttack: false, Tags: new TagSet(card.Tags), Multicast: card.CurrentValues.GetValueOrDefault(GameAttributeKeys.Multicast),
                    OccupiedSlots: (int)card.Size, ElementKeys: card.ElementKeys, ArmorAmount: card.CurrentValues.GetValueOrDefault(GameAttributeKeys.Armor))
                    { CombatValues = new Dictionary<StringName, int>(card.CurrentValues) { [GameAttributeKeys.Value] = card.Value },
                        CooldownMultiplier = card.CooldownMultiplier, Level = card.Level };
            }).ToArray();
            return new(new HeroBattleSetup(EntityId.New(), values.GetValueOrDefault(GameAttributeKeys.MaxHealth), 25,
                MaxMana: 100, InitialMana: 80, ManaRegen: 0, Burn: 8, Poison: player ? 3 : 5), setups);
        }
        async Task Save(string name)
        {
            await owner.ToSignal(owner.GetTree().CreateTimer(.2), SceneTreeTimer.SignalName.Timeout);
            for (var frame = 0; frame < 6; frame++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            var economy = shell.GetNode<EconomyView>("Economy").GetGlobalRect();
            var menuButton = shell.GetNode<Button>("OpenGameMenu").GetGlobalRect();
            if (economy.Position.Y < 0 || economy.End.Y > shell.Size.Y || economy.End.X >= menuButton.Position.X
                || menuButton.End.X > shell.Size.X || menuButton.Position.Y < 0)
                throw new InvalidOperationException("经济区或菜单入口溢出固定画布。");
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            DirAccess.MakeDirRecursiveAbsolute("res://output/ui-html-parity/native");
            using var image = owner.GetViewport().GetTexture().GetImage();
            var window = owner.GetWindow().Size;
            var suffix = window == new Vector2I(image.GetWidth(), image.GetHeight()) ? "" : $"-window-{window.X}x{window.Y}";
            if (image.SavePng($"res://output/ui-html-parity/native/{name}-{image.GetWidth()}x{image.GetHeight()}{suffix}.png") != Error.Ok) throw new InvalidOperationException("视觉对照截图保存失败。");
        }
    }
}
