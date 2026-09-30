using System;
using System.Linq;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Combat;
using Project_Star.Application.Economy;
using Project_Star.Application.Encounters;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Content.Cards;
using Project_Star.Content.Encounters;
using Project_Star.Content.Heroes;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;

namespace Project_Star.Presentation.Playtest;

// Meaningful query and flow regression checks, executed by the existing Godot verification scene.
internal static class PlaytestVerification
{
    public static bool RenderedScene(Control owner)
    {
        var scene = GD.Load<PackedScene>("res://Playtest.tscn").Instantiate<MinimalPlaytest>();
        owner.AddChild(scene);
        try
        {
            // Read the coordinator to compare actual controls against the same captured domain state.
            var presenter = (MatchPresenter)typeof(MinimalPlaytest).GetField("_presenter",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(scene)!;
            const string root = "Margin/Frame/Margin/Content";
            const string main = root + "/MainPanel/Margin/MainContent";
            void Press(Button button) => button.EmitSignal(Button.SignalName.Pressed);
            var sawMonster = false; var sawPvp = false; var sawCardEvent = false; var sawModifier = false;
            for (var turn = 0; turn < 40; turn++)
            {
                if (presenter.View.Page == MatchPage.HeroSelection)
                {
                    var candidates = scene.GetNode<Control>(main).GetChildren().OfType<VBoxContainer>().Last();
                    Press(candidates.GetChild<Button>(0));
                }
                var choices = presenter.View.Choices;
                var chosen = choices.FirstOrDefault(choice => !sawCardEvent && choice.Key == new StringName("encounter.landfill"))
                    ?? choices.FirstOrDefault(choice => !sawModifier && choice.Key == new StringName("encounter.training_ground"))
                    ?? choices.First();
                var choiceIndex = choices.ToList().IndexOf(chosen);
                Press(scene.GetNode<Button>($"{main}/Choices/Choice{choiceIndex + 1}"));
                switch (presenter.View.Page)
                {
                    case MatchPage.Shop:
                        var offer = presenter.View.Offers.FirstOrDefault(item => item.Action.Enabled
                            && item.Card.Abilities.Any(ability => ability.Effects.Any(effect => effect is AttributeDamageEffectDefinition)))
                            ?? presenter.View.Offers.FirstOrDefault(item => item.Action.Enabled);
                        if (offer is not null) Press(scene.GetNode<Button>($"{main}/Actions/Buy{offer.Index + 1}"));
                        break;
                    case MatchPage.Event:
                        if (presenter.View.EventOptions.Count == 0) break;
                        var options = presenter.View.EventOptions;
                        var selected = options.FirstOrDefault(option => option.Key == new StringName("encounter.landfill.material_small_card"))
                            ?? options.FirstOrDefault(option => option.Key == new StringName("encounter.training_ground.sparring"))
                            ?? options[0];
                        var before = presenter.View.Player!;
                        Press(scene.GetNode<VBoxContainer>($"{main}/EventOptions").GetChild<Button>(options.ToList().IndexOf(selected)));
                        var after = presenter.View.Player!;
                        if (selected.Key == new StringName("encounter.landfill.material_small_card") && after.Cards.Count > before.Cards.Count)
                        {
                            var added = after.Cards.Single(card => !before.Cards.Any(old => old.Id == card.Id));
                            var placement = after.BoardPlacements.Single(item => item.CardId == added.Id);
                            var gridPath = placement.Zone == BoardZone.Battlefield
                                ? $"{root}/BattlefieldPanel/Margin/Area/BattlefieldSlots"
                                : $"{root}/BenchPanel/Margin/Area/BenchSlots";
                            if (!scene.GetNode<GridContainer>(gridPath).GetChild<Button>(placement.Start).TooltipText.Contains(added.DisplayName)) return false;
                            sawCardEvent = true;
                        }
                        if (selected.Key == new StringName("encounter.training_ground.sparring"))
                            foreach (var card in after.Cards)
                            {
                                var old = before.Cards.First(item => item.Id == card.Id);
                                if (card.CurrentValues.TryGetValue(GameAttributeKeys.AttackDamage, out var damage)
                                    && old.CurrentValues.TryGetValue(GameAttributeKeys.AttackDamage, out var previous) && damage > previous)
                                {
                                    var placement = after.BoardPlacements.Single(item => item.CardId == card.Id);
                                    var slot = scene.GetNode<GridContainer>($"{root}/BattlefieldPanel/Margin/Area/BattlefieldSlots")
                                        .GetChild<Button>(placement.Start);
                                    if (!slot.TooltipText.Contains($"当前 {damage}")) return false;
                                    sawModifier = true;
                                }
                            }
                        break;
                    case MatchPage.Preparation:
                        if (!scene.GetNode<Control>($"{root}/EnemyBattlefieldPanel").Visible) return false;
                        if (!scene.GetNode<GridContainer>($"{root}/EnemyBattlefieldPanel/Margin/Area/EnemyBattlefieldSlots")
                            .GetChildren().OfType<Button>().Any(button => button.TooltipText.Length > 0)) return false;
                        sawPvp |= presenter.View.Player!.Round > 1 && presenter.View.Player.Turn == 1;
                        sawMonster |= presenter.View.Player!.Turn == 5;
                        Press(scene.GetNode<Button>($"{main}/Actions/Battle"));
                        break;
                }
                Press(scene.GetNode<Button>($"{main}/EncounterActions/Generate"));
                if (sawMonster && sawPvp && sawCardEvent && sawModifier) break;
            }
            Press(scene.GetNode<Button>($"{root}/Footer/Reset"));
            return sawMonster && sawPvp && sawCardEvent && sawModifier && presenter.View.Player is null
                && !scene.GetNode<Control>($"{root}/EnemyBattlefieldPanel").Visible
                && !scene.GetNode<Control>($"{root}/BattlefieldPanel").Visible;
        }
        finally { owner.RemoveChild(scene); scene.Free(); }
    }

    private static DefinitionRegistry Registry(bool mediumShop = false)
    {
        var all = DefinitionRegistry.Scan(typeof(MinimalPlaytest).Assembly);
        // Keep the real card/monster pool; exactly three normal encounters make event checks reproducible.
        return DefinitionRegistry.Create(all.Heroes.Values.Cast<object>().Concat(all.Cards.Values)
            .Concat(all.Skills.Values).Concat(all.Sets.Values).Concat(all.Monsters.Values)
            .Concat(new object[] { mediumShop ? (object)new MediumShopEncounterDefinition() : new SmallShopEncounterDefinition(), new LandfillEncounterDefinition(),
                new TrainingGroundEncounterDefinition(), new PvpEncounterDefinition() }));
    }

    internal static MatchPresenter CreatePresenter(bool mediumShop = false)
    {
        var registry = Registry(mediumShop);
        var factory = new EntityFactory();
        var board = new BoardService(new BoardPlacementSolver(), registry.Sets);
        var economy = new CardEconomyService(factory, board, registry.Cards.Values);
        var game = new GameCoordinator(new CreateMatchService(factory),
            new EncounterScheduler(registry, allowIncompleteMonsterChoices: true),
            new StartBattleService(new BattleSetupFactory(registry.Sets), new CombatSimulator()),
            new MatchResultService(board));
        var presenter = new MatchPresenter(registry, board, economy, new ShopCardPoolService(),
            new ResolveEncounterOptionService(factory, board, registry.Cards.Values),
            new MonsterRewardClaimService(registry, economy, new SkillAcquisitionService(factory), board), game);
        presenter.Reset();
        presenter.SelectHero(presenter.View.Heroes[0].Key);
        return presenter;
    }

    public static bool SnapshotIsolation()
    {
        var factory = new EntityFactory();
        var session = new CreateMatchService(factory).Create(42, 100, new PaladinHeroDefinition());
        var card = new CardEconomyService(factory).AcquireCard(session, new ArmguardCardDefinition(), 1,
            CardAcquisitionSource.Reward).Value!.Card;
        var before = MatchSnapshot.From(session).Cards.Single();
        card.Attributes.BaseCombat.ApplyModifier(new StatModifier(ModifierId.New(), card.Id, GameAttributeKeys.AttackDamage, 7));
        card.Attributes.Persistent.ApplyModifier(new StatModifier(ModifierId.New(), card.Id, GameAttributeKeys.Value, 9));
        var after = MatchSnapshot.From(session).Cards.Single();
        var sold = new CardEconomyService(factory).SellCard(session, card.Id);
        return before.CurrentValues[GameAttributeKeys.AttackDamage] == 5
            && after.BaseValues[GameAttributeKeys.AttackDamage] == 5
            && after.CurrentValues[GameAttributeKeys.AttackDamage] == 12
            && after.Value == before.Value + 9 && sold.Value == after.Value
            && CardDisplayAdapter.FaceEffects(after).Any(effect => effect.Kind == CardFaceEffectKind.Damage && effect.Value == "12");
    }

    public static bool EffectSemanticsAndArtwork()
    {
        var registry = Registry();
        foreach (var definition in registry.Cards.Values)
            foreach (var level in definition.Levels.Keys.DefaultIfEmpty(definition.InitialLevel))
                _ = CardDisplayAdapter.Details(MatchDisplayQuery.FromOffer(ShopOffer.Create(definition, level)));
        var nun = MatchDisplayQuery.FromOffer(ShopOffer.Create(new NunCardDefinition()));
        var hammer = MatchDisplayQuery.FromOffer(ShopOffer.Create(new JudgmentHammerCardDefinition()));
        var adapter = new CardDisplayAdapter();
        return CardDisplayAdapter.FaceEffects(nun).Any(effect => effect.Kind == CardFaceEffectKind.Healing && effect.Value == "10")
            && CardDisplayAdapter.FaceEffects(hammer).Any(effect => effect.Kind == CardFaceEffectKind.Damage && effect.Value == "20%")
            && CardDisplayAdapter.Details(hammer).Contains("目标最大生命的 20%")
            && adapter.Artwork(new StringName("card.armguard")).ResourcePath.EndsWith("armguard.png")
            && adapter.Artwork(new StringName("card.boar")).ResourcePath.EndsWith("boar.png")
            && adapter.Artwork(new StringName("card.judgment_hammer")).ResourcePath.EndsWith("judgment_hammer.png")
            && adapter.Artwork(new StringName("card.nun")) == adapter.Artwork(new StringName("card.unknown"));
    }

    public static bool RefreshAndStaleOffers()
    {
        var first = CreatePresenter(mediumShop: true);
        var control = CreatePresenter(mediumShop: true);
        first.ChooseEncounter(new StringName("encounter.shop.medium"));
        control.ChooseEncounter(new StringName("encounter.shop.medium"));
        var old = first.View.Offers[0];
        for (var count = 0; count < 5; count++) first.RefreshView();
        if (!first.View.Offers.Select(offer => offer.Card.Key).SequenceEqual(control.View.Offers.Select(offer => offer.Card.Key))) return false;
        first.RefreshShop(); control.RefreshShop();
        if (first.View.Offers[0].Revision == old.Revision) return false;
        var wealth = first.View.Player!.Wealth;
        first.BuyCard(old.Index, old.Revision);
        if (first.View.Player!.Wealth != wealth || first.View.Player.Cards.Count != 0) return false;
        var current = first.View.Offers[0];
        first.BuyCard(current.Index, current.Revision);
        var after = first.View.Player!;
        first.BuyCard(current.Index, current.Revision);
        return first.View.Player!.Wealth == after.Wealth && first.View.Player.Cards.Count == after.Cards.Count
            && first.View.Offers.Select(offer => offer.Card.Key).SequenceEqual(control.View.Offers.Select(offer => offer.Card.Key));
    }

    public static bool EventAcquisitionRefresh()
    {
        var presenter = CreatePresenter();
        presenter.ChooseEncounter(new StringName("encounter.landfill"));
        var revision = presenter.View.EventRevision;
        presenter.ResolveEventOption(new StringName("encounter.landfill.material_small_card"), revision);
        var snapshot = presenter.View.Player!;
        presenter.ResolveEventOption(new StringName("encounter.landfill.material_small_card"), revision);
        return snapshot.Cards.Count == 1 && snapshot.BoardPlacements.Count == 1
            && snapshot.BoardPlacements[0].CardId == snapshot.Cards[0].Id
            && presenter.View.Player!.Cards.Count == 1 && presenter.View.EventOptions.Count == 0
            && presenter.View.Continue.Visible;
    }

    public static bool EventModifierRefresh()
    {
        var presenter = CreatePresenter();
        presenter.ChooseEncounter(new StringName("encounter.shop.small"));
        var offer = presenter.View.Offers.FirstOrDefault(item => item.Card.Abilities
            .Any(ability => ability.Effects.Any(effect => effect is AttributeDamageEffectDefinition)));
        if (offer is null) return false;
        presenter.BuyCard(offer.Index, offer.Revision);
        var before = presenter.View.Player!.Cards.Single();
        presenter.ContinueMatch();
        presenter.ChooseEncounter(new StringName("encounter.training_ground"));
        presenter.ResolveEventOption(new StringName("encounter.training_ground.sparring"), presenter.View.EventRevision);
        var after = presenter.View.Player!.Cards.Single();
        return after.CurrentValues[GameAttributeKeys.AttackDamage] == before.CurrentValues[GameAttributeKeys.AttackDamage] + 5
            && CardDisplayAdapter.FaceEffects(after).Any(effect => effect.Kind == CardFaceEffectKind.Damage
                && effect.Value == after.CurrentValues[GameAttributeKeys.AttackDamage].ToString());
    }

    public static bool EnemyVisibilityAndReset()
    {
        var presenter = CreatePresenter();
        var monsterSeen = false;
        var pvpSeen = false;
        for (var count = 0; count < 8; count++)
        {
            var choice = presenter.View.Player!.EncounterChoices[0];
            presenter.ChooseEncounter(choice.Key);
            if (presenter.View.Page == MatchPage.Shop)
            {
                var offer = presenter.View.Offers.FirstOrDefault(item => item.Action.Enabled);
                if (offer is not null) presenter.BuyCard(offer.Index, offer.Revision);
            }
            else if (presenter.View.Page == MatchPage.Preparation)
            {
                if (!presenter.View.EnemyVisible || presenter.View.Enemy?.BoardPlacements.Count == 0) return false;
                monsterSeen |= choice.Kind == EncounterKind.Monster;
                pvpSeen |= choice.Kind == EncounterKind.Pvp;
                presenter.StartBattle();
                var after = presenter.View.Player!;
                presenter.StartBattle();
                if (presenter.View.Player!.Wealth != after.Wealth || presenter.View.Player.PvpWins != after.PvpWins) return false;
            }
            presenter.ContinueMatch();
        }
        presenter.Reset();
        if (presenter.View.Player is not null || presenter.View.Enemy is not null || presenter.View.SelectedCardId is not null
            || presenter.View.EnemyVisible || presenter.View.Offers.Count != 0) return false;
        presenter.SelectHero(presenter.View.Heroes[0].Key);
        return monsterSeen && pvpSeen && presenter.View.Player!.Wealth == 999
            && presenter.View.Player.Cards.Count == 0 && presenter.View.Player.BoardPlacements.Count == 0;
    }
}
