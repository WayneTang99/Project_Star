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
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.CardFace;
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

// 宝石手套身份、攻击、英雄升级渠道与宝石冻结行为验证（表现层）。
internal static class GemGloveChecks
{
    internal static bool DefinitionAndAttack()
    {
        var definition = new GemGloveCardDefinition(); var identity = definition.Attributes.Identity;
        Require(identity.Size == CardSize.Small && identity.GemSocketCount == 5
            && identity.FactionKey == new StringName("robin") && identity.ElementKeys.SequenceEqual(new[] { GameElements.General })
            && definition.InitialLevel == 2 && !definition.SupportsLevel(1) && !definition.SupportsLevel(5)
            && definition.Tags.Contains(GameTags.Equipment), "身份与等级");
        Require(DefinitionRegistry.Scan(typeof(GemGloveChecks).Assembly).Cards.ContainsKey(identity.Key), "正式注册");
        var texture = GD.Load<Texture2D>(identity.Illustration.ToString());
        Require(texture is not null && texture.GetHeight() == texture.GetWidth() * 2, "小型原画比例");
        foreach (var (level, damage) in new[] { (2, 20), (3, 40), (4, 80) })
        {
            var (match, card, board) = Fixture(level);
            var enemy = new CreateMatchService(new EntityFactory()).Create(43, 100, new RobinHeroDefinition());
            var setup = new BattleSetupFactory().Create(match, enemy, 42, new BattleTick(60), new BattleTick(61));
            var result = new CombatSimulator().Simulate(setup);
            Require(result.Events.OfType<DamageDealtEvent>().Any(e => e.SourceCardId == card.Id && e.RawDamage == damage && e.Tick.Value == 60), "实际六秒发动");
            Require(result.Events.OfType<AbilityActivatedEvent>().Count(e => e.SourceCardId == card.Id) == 1, "升级能力不进入战斗");
            var text = string.Join("\n", CardDisplayAdapter.Rules(MatchSnapshot.From(match).Cards.Single()));
            Require(text.Contains("升级") && text.Contains("每升一级") && text.Contains(damage.ToString()), "当前文案");
        }
        return true;
    }

    internal static bool UpgradeAndSockets()
    {
        var first = Fixture(2); var second = Fixture(2);
        foreach (var fixture in new[] { first, second })
        {
            var (match, card, board) = fixture;
            var bench = new EntityFactory().CreateCard(new GemGloveCardDefinition(), 4);
            var loose = new EntityFactory().CreateCard(new GemGloveCardDefinition(), 4);
            match.Player.Inventory.Add(bench); match.Player.Inventory.Add(loose);
            Require(board.PlaceCard(match, bench.Id, BoardZone.Bench, 0).IsSuccess, "备战摆放");
            var frozen = MatchSnapshot.From(match);
            var previous = match.Random.State;
            HeroExperienceService.Grant(match, 8);
            Require(card.GemSockets.All(g => g is null) && match.Random.State == previous, "不足十经验不触发");
            var turn = new TurnExperienceService(); match.Progress.Turn = 2;
            Require(turn.SettleCurrentTurn(match).IsSuccess && card.GemSockets.Count(g => g is not null) == 1
                && bench.GemSockets.Count(g => g is not null) == 1, "回合经验战场备战各一次");
            var state = match.Random.State;
            Require(turn.SettleCurrentTurn(match).Value == 0 && match.Random.State == state, "回合幂等");
            HeroExperienceService.Grant(match, 30);
            Require(card.GemSockets.Count(g => g is not null) == 4 && bench.GemSockets.Count(g => g is not null) == 4
                && loose.GemSockets.All(g => g is null), "连续三级与游离卡排除");
            Require(frozen.Cards.All(c => c.GemSockets.All(g => g is null)), "旧快照保持");
            var existing = card.GemSockets.ToArray(); state = match.Random.State;
            var economy = new CardEconomyService(new EntityFactory(), board);
            Require(economy.AcquireAndPlace(match, new GemGloveCardDefinition(), 2, CardAcquisitionSource.Reward).IsSuccess
                && card.Attributes.Persistent.GetBaseValue(GameAttributeKeys.Level) == 3 && card.GemSockets.SequenceEqual(existing)
                && match.Random.State == state, "卡牌合并不触发且保留宝石");
            HeroExperienceService.Grant(match, 10);
            Require(card.GemSockets.All(g => g is not null), "第五孔");
            var fullSockets = card.GemSockets.ToArray();
            state = match.Random.State; HeroExperienceService.Grant(match, 20);
            Require(match.Random.State == state && card.GemSockets.SequenceEqual(fullSockets), "满孔不替换且不消耗随机数");
        }
        Require(first.Match.Random.State == second.Match.Random.State
            && first.Card.GemSockets.Select(g => g?.Attributes.Identity.Key).SequenceEqual(second.Card.GemSockets.Select(g => g?.Attributes.Identity.Key)), "固定种子复现");
        var empty = Fixture(2, false); var before = empty.Match.Random.State;
        HeroExperienceService.Grant(empty.Match, 10);
        Require(empty.Card.GemSockets.All(g => g is null) && empty.Match.Random.State == before, "空池跳过");
        empty.Match.Status = MatchStatus.Won; var xp = empty.Match.Player.Experience;
        HeroExperienceService.Grant(empty.Match, 10); Require(empty.Match.Player.Experience == xp, "终局不触发");
        return true;
    }

    internal static bool ExperienceChannels()
    {
        foreach (var channel in new[] { "round", "victory", "monster", "sale" })
        {
            var (match, card, board) = Fixture(2); HeroExperienceService.Grant(match, 8);
            if (channel == "round")
            {
                var factory = new EntityFactory();
                var source = factory.CreateCard(new ExperienceCard()); match.Player.Inventory.Add(source);
                Require(board.PlaceCard(match, source.Id, BoardZone.Battlefield, 1).IsSuccess, "经验来源摆放");
                var registry = DefinitionRegistry.Scan(typeof(GemGloveChecks).Assembly);
                MatchAbilityService.ApplyRoundCards(match, registry, board);
            }
            else if (channel == "sale")
            {
                var sold = new EntityFactory().CreateCard(new ExperienceCard()); match.Player.Inventory.Add(sold);
                sold.Attributes.Persistent.SetBaseValue(GameAttributeKeys.Value, 10);
                Require(new CardEconomyService(new EntityFactory(), board).SellCard(match, sold.Id).IsSuccess, "出售经验");
            }
            else
            {
                var enemy = new CreateMatchService(new EntityFactory()).Create(43, 0, new RobinHeroDefinition());
                var battle = new BattleResult(BattleOutcome.PlayerVictory, BattleEndReason.HeroDefeated,
                    new BattleTick(1), 200, 0, Array.Empty<BattleEvent>())
                {
                    MatchRewardSources = channel == "victory" ?
                        new[] { new MatchRewardSource(card.Id, 2, true,
                            new[] { new AbilityDefinition(new StringName("verification.gem.experience_victory"),
                                AbilityActivation.PassiveOnBattleVictory, AbilityTarget.AlliedHero, 0, 0,
                                [new GrantExperienceEffectDefinition(1)]) }) } : Array.Empty<MatchRewardSource>(),
                };
                Require(new MatchResultService(board).Apply(match, battle,
                    channel == "monster" ? MatchBattleKind.Monster : MatchBattleKind.Pvp, opponent: enemy).IsSuccess, "战斗经验");
            }
            Require(card.GemSockets.Count(g => g is not null) == 1, channel + "触发一次");
            var frozen = new BattleSetupFactory().Create(match,
                new CreateMatchService(new EntityFactory()).Create(43, 0, new RobinHeroDefinition()), 42,
                new BattleTick(60), new BattleTick(61));
            var abilities = frozen.Player.Cards.Single(c => c.EntityId == card.Id).Abilities!;
            Require(abilities.Single(a => a.Activation == AbilityActivation.Active).Effects.Count == 2, "宝石追加发动效果");
            var result = new CombatSimulator().Simulate(frozen);
            Require(result.Events.OfType<StatusChangedEvent>().Any(e => e.Amount == 2), "宝石按攻击20的10%施加");
            HeroExperienceService.Grant(match, 10);
            Require(abilities.Single(a => a.Activation == AbilityActivation.Active).Effects.Count == 2, "旧战斗能力保持冻结");
        }
        return true;
    }

    // 可复用经验渠道夹具，不进入正式内容注册池。
    private sealed class ExperienceCard : CardDefinition
    {
        public ExperienceCard() : base(new EntityAttributes<CardIdentityAttributes>(new CardIdentityAttributes(
            new StringName("verification.gem.experience"), "经验夹具", new StringName("robin"), CardSize.Small, [GameElements.General])),
            new TagSet(), [new AbilityDefinition(new StringName("verification.gem.experience_round"),
                AbilityActivation.PassiveOnRoundStart, AbilityTarget.AlliedHero, 0, 0, [new GrantExperienceEffectDefinition(1)])],
            onSellReward: new GrantValuePercentExperienceOnSellDefinition(10)) { }
    }

    internal static async Task Capture(Control owner)
    {
        owner.Theme = MatchTheme.Create();
        var preview = new CardPreviewView { Size = owner.Size }; owner.AddChild(preview);
        var registry = DefinitionRegistry.Scan(typeof(GemGloveChecks).Assembly);
        preview.SetQuery(new Project_Star.Application.Content.CardPreviewQuery(registry));
        preview.Open(MatchDisplayQuery.FromOffer(ShopOffer.Create(new GemGloveCardDefinition())), null, null);
        await owner.ToSignal(owner.GetTree().CreateTimer(.25), SceneTreeTimer.SignalName.Timeout);
        await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        DirAccess.MakeDirRecursiveAbsolute("res://output/gem-glove");
        using var image = owner.GetViewport().GetTexture().GetImage();
        Require(image.SavePng("res://output/gem-glove/card-preview.png") == Error.Ok, "原生截图");
        owner.RemoveChild(preview); preview.Free();
    }

    private static (MatchSession Match, CardInstance Card, BoardService Board) Fixture(int level, bool gems = true)
    {
        var registry = DefinitionRegistry.Scan(typeof(GemGloveChecks).Assembly); var factory = new EntityFactory();
        var match = new CreateMatchService(factory, gems ? registry.Gems.Values : null).Create(42, 100, new RobinHeroDefinition());
        var board = new BoardService(new BoardPlacementSolver()); var card = factory.CreateCard(new GemGloveCardDefinition(), level);
        match.Player.Inventory.Add(card); Require(board.PlaceCard(match, card.Id, BoardZone.Battlefield, 0).IsSuccess, "战场摆放");
        return (match, card, board);
    }
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException("宝石手套：" + message); }
}
