using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;

namespace Project_Star.Presentation.Playtest;

// 只读视觉夹具与 HTML 展示同一内容，不进入正式内容池或可变对局。
internal static class HtmlParityCapture
{
    public static async Task Run(Control owner)
    {
        var root = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<MinimalPlaytest>();
        var shell = root.GetNode<MatchShell>("MatchShell"); root.RemoveChild(shell); root.Free(); owner.AddChild(shell);
        shell.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        var registry = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
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
        var scrollbar = shell.GetNode<VScrollBar>("DesktopScroll"); scrollbar.Value = scrollbar.MaxValue - scrollbar.Page; await Save("shop-bottom"); scrollbar.Value = 0;
        var details = shell.GetNode<CardDetailsView>("CardDetails");
        var item = shell.GetNode<BoardZoneView>("BattlefieldRow/Content/Board").GetNode<CardItemView>($"Card_{hammer.Id.Value:N}");
        details.ShowNear(hammer, item.GetGlobalRect(), shell.Size); await Save("details");
        details.ShowNear(cauldron, item.GetGlobalRect(), shell.Size); await Save("details-multiple");
        details.GetNode<Button>("Content/CardContent/InstanceToggle").EmitSignal(Button.SignalName.Pressed);
        await Save("details-instance");
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
            new KeyedAction(new StringName("ui.sample.shop"), new UiAction("旅人集市")) { Illustration = view.ContextIllustration, Subtitle = "购买卡牌，完善你的构筑" },
            new KeyedAction(new StringName("ui.sample.forest"), new UiAction("暮歌丛林")) { Illustration = new StringName("res://art/ui/encounters/artwork/dusk_song_jungle-illustration.png"), Subtitle = "采摘植物，或探寻林间秘密" },
            new KeyedAction(new StringName("ui.sample.boar"), new UiAction("野猪巢穴")) { Illustration = new StringName("res://art/ui/encounters/artwork/boar-illustration.png"), Subtitle = "迎战怪物，获取战斗奖励" },
        };
        shell.Render(view with { Page = MatchPage.EncounterChoice, Title = "林间岔路", Message = "命运藏在每一次选择里。\n下一站，将通往何处？", Choices = Array.AsReadOnly(choices), ContextIllustration = choices[1].Illustration, Refresh = new UiAction("刷新", false) }); await Save("encounter");
        shell.Render(view with { Page = MatchPage.Preparation, Title = "战斗准备", Message = "确认你的阵容。\n让每一张卡牌各尽其用。", EnemyVisible = true, Battle = new UiAction("开始战斗"), Continue = new UiAction("继续", false), Refresh = new UiAction("刷新", false), ContextIllustration = choices[2].Illustration }); await Save("battle");
        shell.Render(view); shell.SetDesktopPalette(true); await Save("blue"); shell.SetDesktopPalette(false);
        owner.GetTree().Quit();
        async Task Save(string name)
        {
            await owner.ToSignal(owner.GetTree().CreateTimer(.2), SceneTreeTimer.SignalName.Timeout);
            for (var frame = 0; frame < 6; frame++) await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            DirAccess.MakeDirRecursiveAbsolute("res://output/ui-html-parity/native");
            using var image = owner.GetViewport().GetTexture().GetImage();
            if (image.SavePng($"res://output/ui-html-parity/native/{name}-{image.GetWidth()}x{image.GetHeight()}.png") != Error.Ok) throw new InvalidOperationException("视觉对照截图保存失败。");
        }
    }
}

