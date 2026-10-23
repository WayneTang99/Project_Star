using System;
using System.Linq;
using System.Text.Json.Nodes;
using Godot;
using Project_Star.Application.Board;
using Project_Star.Application.Economy;
using Project_Star.Application.Encounters;
using Project_Star.Application.Factories;
using Project_Star.Application.Match;
using Project_Star.Domain.Common;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Definitions;
using Project_Star.Domain.Match;
using Project_Star.Infrastructure.Server;
using Project_Star.Infrastructure.Encounters;

namespace Project_Star.Presentation.Verification;

// 持久化字段与恢复后行为验证（表现层验证模块）；使用正式定义，不依赖图形资源。
internal static class MatchSaveChecks
{
    // 验证商品售罄、随机、身份、独立贡献与继续购买，拒绝不兼容或损坏存档。
    internal static void Run(IDefinitionCatalog definitions)
    {
        var codec = new MatchSaveCodec(definitions, new string('A', 64));
        var service = new ServerMatchService(definitions, new LocalTestOpponentProvider(definitions));
        var run = service.Create(new StringName("hero.paladin"), ulong.MaxValue - 17).Value!;
        Require(run.Match.Player.Wealth == 5 && run.Match.Player.Experience == 1, "formal starting economy");
        var first = codec.Encode(run);
        var restored = codec.Decode(first);
        Require(codec.Encode(restored) == first && restored.Match.Id == run.Match.Id, "initial round trip");
        var shop = restored.Match.EncounterSchedule.CurrentChoices.First(choice => definitions.Encounters[choice.Key] is CardShopEncounterDefinition);
        Require(service.SelectShop(restored, shop.Key).IsSuccess, "select real shop");
        var offer = restored.Shop!.Offers.Select((value, index) => (value, index)).First(pair => pair.value.Price <= restored.Match.Player.Wealth);
        Require(service.Buy(restored, offer.index).IsSuccess, "buy real offer");
        var afterBuy = codec.Encode(restored);
        var loaded = codec.Decode(afterBuy);
        Require(codec.Encode(loaded) == afterBuy && loaded.Shop!.Offers[offer.index].IsSold, "sold flag and pool round trip");
        var beforeFailed = codec.Encode(loaded);
        Require(service.Buy(loaded, offer.index).IsFailure && codec.Encode(loaded) == beforeFailed, "sold cannot rebuy or mutate");
        Require(loaded.Match.Player.Inventory.Cards.Single().Id == restored.Match.Player.Inventory.Cards.Single().Id, "stable card ID");
        Require(loaded.Match.Random.State == restored.Match.Random.State, "random state retained");

        CheckLeaveShop(codec, service, definitions, afterBuy);
        ServerConstructionChecks.Run(definitions, codec, service, afterBuy);
        ServerEncounterChecks.Run(definitions, codec, service);
        ServerPvpChecks.Run(definitions, codec, service);

        // 扩充数据夹具覆盖首次商店以外的可序列化聚合字段，不宣称这些 HTTP 命令已接入。
        var factory = new EntityFactory();
        var cardDefinition = definitions.Cards.Values.First(card => card.Quests.Count > 0);
        var card = factory.CreateCard(cardDefinition);
        loaded.Match.Player.Inventory.Add(card);
        var board = new BoardService(new BoardPlacementSolver(), definitions.Sets);
        _ = board.PlaceCard(loaded.Match, card.Id, BoardZone.Bench, 0);
        var modifier = new StatModifier(ModifierId.New(), card.Id, GameAttributeKeys.AttackDamage, 17);
        card.Attributes.BaseCombat.ApplyModifier(modifier);
        card.SetQuestProgress(card.Quests[0].Key, card.Quests[0].RequiredCount);
        card.ReduceCooldown(25);
        if (card.GemSockets.Count > 0) card.SocketGem(0, definitions.Gems.Values.First());
        var skill = factory.CreateSkill(definitions.Skills.Values.First());
        skill.Attributes.BaseCombat.ApplyModifier(new StatModifier(ModifierId.New(), card.Id, GameAttributeKeys.AttackDamage, 9));
        loaded.Match.Player.Skills.Add(skill);
        loaded.Match.Player.Hero!.Attributes.BaseCombat.ApplyModifier(new StatModifier(ModifierId.New(), card.Id, GameAttributeKeys.MaxHealth, 61));
        loaded.Match.AddPendingBattleMaxHealthBonus(13);
        loaded.Match.AddPendingMonsterReward(new PendingMonsterReward(MonsterRewardKind.Card, cardDefinition.Attributes.Identity.Key,
            cardDefinition.Attributes.Identity.DisplayName, cardDefinition.InitialLevel));
        loaded.Match.Progress.PvpWins = 2;
        loaded.Match.GemShopRevision = 19;
        // 贡献顺序参与 checked 累加的中间值，不能为了排序而改变恢复顺序。
        var resource = loaded.Match.Player.Resources;
        resource.SetBaseValue(GameAttributeKeys.Armor, int.MaxValue - 1);
        resource.ApplyModifier(new StatModifier(new ModifierId(Guid.Parse("00000000-0000-0000-0000-000000000002")), card.Id, GameAttributeKeys.Armor, -5));
        resource.ApplyModifier(new StatModifier(new ModifierId(Guid.Parse("00000000-0000-0000-0000-000000000001")), card.Id, GameAttributeKeys.Armor, 5));
        var complex = codec.Encode(loaded);
        var clone = codec.Decode(complex);
        Require(codec.Encode(clone) == complex, "complex canonical round trip");
        Require(clone.Match.Player.Resources.GetFinalValue(GameAttributeKeys.Armor) == int.MaxValue - 1,
            "modifier accumulation order retained");
        var copied = clone.Match.Player.Inventory.Find(card.Id)!;
        Require(copied.CooldownMultiplier == 0.75m && copied.GetQuestProgress(card.Quests[0].Key) == card.Quests[0].RequiredCount,
            "quest and decimal restore");
        Require(copied.Attributes.Identity.ElementKeys.SequenceEqual(card.Attributes.Identity.ElementKeys)
            && copied.Tags.OrderBy(key => key.ToString()).SequenceEqual(card.Tags.OrderBy(key => key.ToString())), "earned identity restore");
        Require(copied.Attributes.BaseCombat.RemoveModifier(modifier.Id)
            && copied.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) == card.Attributes.BaseCombat.GetFinalValue(GameAttributeKeys.AttackDamage) - 17,
            "modifier ID remains removable");
        Require(codec.Encode(loaded) == complex, "restored instance isolation");
        var corruption = JsonNode.Parse(complex)!;
        corruption["formatVersion"] = 99;
        Require(Rejects(() => codec.Decode(corruption.ToJsonString())), "unknown format rejected");
        Require(Rejects(() => new MatchSaveCodec(definitions, new string('B', 64)).Decode(complex)), "different rules rejected");
        var gemShop = definitions.Encounters.Values.OfType<GemShopDefinition>().First();
        _ = new GemShopService(definitions).Open(loaded.Match, gemShop);
        Require(Rejects(() => codec.Encode(loaded)), "unsupported active visit fails closed");
    }

    private static bool Rejects(Action action)
    {
        try { action(); return false; }
        catch (InvalidOperationException) { return true; }
        catch (NotSupportedException) { return true; }
    }

    private static void CheckLeaveShop(MatchSaveCodec codec, ServerMatchService service,
        IDefinitionCatalog definitions, string afterBuy)
    {
        var next = codec.Decode(afterBuy);
        var sameInput = codec.Decode(afterBuy);
        var wealth = next.Match.Player.Wealth;
        var cardId = next.Match.Player.Inventory.Cards.Single().Id;
        var seen = next.Match.EncounterSchedule.SeenKeys.ToHashSet();
        Require(service.LeaveShop(next).IsSuccess && next.Phase == ServerMatchPhase.EncounterChoice
            && next.Shop is null && next.ShopKey is null, "leave closes shop");
        Require(next.Match.Progress.Round == 1 && next.Match.Progress.Turn == 2
            && next.Match.Player.Experience == 2 && next.Match.Player.Wealth == wealth,
            "leave advances once with one experience and no repeated income");
        Require(next.Match.EncounterSchedule.CurrentChoices.Count > 0
            && seen.IsSubsetOf(next.Match.EncounterSchedule.SeenKeys)
            && next.Match.Player.Inventory.Cards.Single().Id == cardId, "leave retains history and owned card");
        Require(service.LeaveShop(sameInput).IsSuccess && codec.Encode(next) == codec.Encode(sameInput),
            "same committed input produces same next choices");
        var saved = codec.Encode(next);
        next = codec.Decode(saved);
        Require(codec.Encode(next) == saved, "next choices survive save restore");
        Require(service.LeaveShop(next).IsFailure && codec.Encode(next) == saved,
            "leave wrong phase cannot reroll or grant experience");
        Require(service.Buy(next, 0).IsFailure && codec.Encode(next) == saved,
            "closed shop cannot purchase");
        for (var turn = 2; turn <= 3; turn++)
        {
            var shop = next.Match.EncounterSchedule.CurrentChoices.First(choice =>
                definitions.Encounters[choice.Key] is CardShopEncounterDefinition);
            Require(service.SelectShop(next, shop.Key).IsSuccess, "select subsequent shop " + turn);
            next = codec.Decode(codec.Encode(next));
            Require(service.LeaveShop(next).IsSuccess && next.Match.Progress.Turn == turn + 1
                && next.Match.Player.Experience == turn + 1 && next.Match.Player.Wealth == wealth,
                "continue without duplicate income " + turn);
            next = codec.Decode(codec.Encode(next));
        }
        Require(next.Match.EncounterSchedule.CurrentChoices.Count == 3
            && next.Match.EncounterSchedule.CurrentChoices.All(choice => choice.Kind == EncounterKind.Monster),
            "turn four keeps strict monster scheduling");
        CheckMonster(codec, service, definitions, codec.Encode(next));
    }

    private static void CheckMonster(MatchSaveCodec codec, ServerMatchService service,
        IDefinitionCatalog definitions, string turnFour)
    {
        var run = codec.Decode(turnFour);
        Require(service.SelectMonster(run, new StringName("monster.missing")).IsFailure
            && codec.Encode(run) == turnFour, "unissued monster cannot advance");
        // 只在验证夹具提高己方数值以稳定覆盖胜利，不修改正式内容或服务器经济。
        var factory = new EntityFactory();
        var board = new BoardService(new BoardPlacementSolver(), definitions.Sets);
        var attacker = factory.CreateCard(definitions.Cards[new StringName("card.assassin_dagger")]);
        attacker.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.AttackDamage, 10000);
        attacker.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.CooldownTicks, 10);
        run.Match.Player.Inventory.Add(attacker);
        Require(board.PlaceCard(run.Match, attacker.Id, BoardZone.Battlefield, 5).IsSuccess, "battle victory fixture placement");
        run.Match.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, 10000);
        run.Match.AddPendingBattleMaxHealthBonus(17);
        Require(service.SelectMonster(run, new StringName("monster.fire_slime")).IsSuccess
            && run.Phase == ServerMatchPhase.MonsterPreparation && run.Battle is { Turn: 4, Round: 1 }
            && run.Match.Progress.Turn == 5, "select fixed monster advances once");
        var prepared = codec.Encode(run);
        var restored = codec.Decode(prepared);
        var id = restored.Battle!.Id;
        Require(codec.Encode(restored) == prepared && restored.Battle.Opponent.Player.Hero!.Id == run.Battle!.Opponent.Player.Hero!.Id
            && restored.Battle.Opponent.Player.Inventory.Cards.Select(card => card.Id).SequenceEqual(run.Battle.Opponent.Player.Inventory.Cards.Select(card => card.Id)),
            "monster preparation preserves opponent entity IDs and input");
        Require(ReferenceEquals(restored.Battle.Opponent.Definitions, run.Battle!.Opponent.Definitions),
            "monster definition catalog remains available before and after restore");
        var edited = codec.Decode(prepared);
        Require(service.MoveCard(edited, attacker.Id, BoardZone.Bench, 0).IsSuccess
            && edited.Battle!.Id == id && edited.Battle.Seed == restored.Battle.Seed
            && edited.Battle.Opponent.Player.Hero!.Id == restored.Battle.Opponent.Player.Hero!.Id,
            "preparation permits player move without changing fixed opponent and seed");
        Require(service.SellCard(edited, attacker.Id).IsSuccess && codec.Encode(codec.Decode(codec.Encode(edited))) == codec.Encode(edited),
            "preparation sale survives restore without changing battle identity");
        Require(service.SelectMonster(restored, new StringName("monster.fire_slime")).IsFailure
            && service.Buy(restored, 0).IsFailure && service.FinishMonster(restored, id).IsFailure
            && codec.Encode(restored) == prepared, "preparation rejects redraw purchase and premature finish");
        Require(service.ResolveMonster(restored, Guid.NewGuid()).IsFailure && codec.Encode(restored) == prepared,
            "wrong battle ID cannot settle");
        var wealth = restored.Match.Player.Wealth;
        var experience = restored.Match.Player.Experience;
        var reputation = restored.Match.Player.Reputation;
        Require(service.ResolveMonster(restored, id).IsSuccess && restored.Phase == ServerMatchPhase.MonsterResult
            && restored.Battle!.Result!.Outcome == BattleOutcome.PlayerVictory, "server simulation settles monster victory");
        Require(restored.Match.Player.Wealth == wealth + 3 && restored.Match.Player.Experience == experience + 2
            && restored.Match.Player.Reputation == reputation && restored.Match.PendingMonsterRewards.Count == 1
            && restored.Match.PendingBattleMaxHealthBonus == 0, "victory economy drop and one battle health bonus");
        var resultSave = codec.Encode(restored);
        var result = codec.Decode(resultSave);
        Require(codec.Encode(result) == resultSave, "settled result and pending reward survive restore");
        Require(service.ResolveMonster(result, id).IsFailure && codec.Encode(result) == resultSave,
            "restored result cannot settle a second time");
        var repeated = codec.Decode(prepared);
        Require(service.ResolveMonster(repeated, id).IsSuccess && repeated.Battle!.Result == result.Battle!.Result
            && repeated.Match.Random.State == result.Match.Random.State
            && repeated.Match.PendingMonsterRewards.SequenceEqual(result.Match.PendingMonsterRewards),
            "same frozen battle input repeats outcome and drop");
        Require(service.FinishMonster(result, id).IsFailure && codec.Encode(result) == resultSave,
            "pending reward prevents finish without losing drop");
        Require(service.ClaimMonsterReward(result, Guid.NewGuid(), 0).IsFailure
            && service.ClaimMonsterReward(result, id, -1).IsFailure && codec.Encode(result) == resultSave,
            "reward claim validates battle and index");
        Require(service.ClaimMonsterReward(result, id, 0).IsSuccess && result.Match.PendingMonsterRewards.Count == 0,
            "claim real dropped card or skill");
        var claimedSave = codec.Encode(result);
        result = codec.Decode(claimedSave);
        Require(service.ClaimMonsterReward(result, id, 0).IsFailure && codec.Encode(result) == claimedSave,
            "restored claimed reward cannot be claimed twice");
        Require(service.FinishMonster(result, id).IsSuccess && result.Phase == ServerMatchPhase.EncounterChoice
            && result.Battle is null && result.Match.Progress.Turn == 5
            && result.Match.Player.Experience == experience + 3 && result.Match.Player.Wealth == wealth + 3,
            "finish generates turn five with one turn experience");
        var finished = codec.Encode(result);
        Require(service.FinishMonster(result, id).IsFailure && codec.Encode(codec.Decode(finished)) == finished,
            "finished battle cannot advance twice and next choices restore");

        // 空战场、1生命的失败夹具验证怪物战不扣声望，且不发胜利掉落。
        var loss = codec.Decode(turnFour);
        foreach (var card in loss.Match.Player.Inventory.Cards.ToArray())
        {
            _ = board.RemoveFromBoard(loss.Match, card.Id);
            loss.Match.Player.Inventory.Remove(card.Id);
        }
        loss.Match.Player.Hero!.Attributes.BaseCombat.SetBaseValue(GameAttributeKeys.MaxHealth, 1);
        Require(service.SelectMonster(loss, new StringName("monster.fire_slime")).IsSuccess
            && service.ResolveMonster(loss, loss.Battle!.Id).IsSuccess
            && loss.Battle!.Result!.Outcome == BattleOutcome.OpponentVictory
            && loss.Match.Player.Reputation == reputation && loss.Match.PendingMonsterRewards.Count == 0,
            "monster defeat keeps reputation and grants no victory drop");
        Require(service.FinishMonster(loss, loss.Battle!.Id).IsSuccess, "defeat can continue to next turn");

        // 满盘不吞奖励；技能领取也通过原有获得用例，均只属于验证夹具。
        var full = codec.Decode(resultSave);
        while (full.Match.PendingMonsterRewards.Count > 0) full.Match.RemovePendingMonsterReward(0);
        full.Match.AddPendingMonsterReward(new PendingMonsterReward(MonsterRewardKind.Card,
            new StringName("card.silver_needle_grass"), "银针草", 1));
        foreach (var card in full.Match.Player.Inventory.Cards.ToArray())
        {
            _ = board.RemoveFromBoard(full.Match, card.Id);
            full.Match.Player.Inventory.Remove(card.Id);
        }
        foreach (var zone in new[] { BoardZone.Battlefield, BoardZone.Bench })
        for (var slot = 0; slot < full.Match.Board.GetZone(zone).Capacity; slot++)
        {
            var card = factory.CreateCard(definitions.Cards[new StringName("card.silver_needle_grass")], 4);
            full.Match.Player.Inventory.Add(card);
            if (board.PlaceCard(full.Match, card.Id, zone, slot).IsFailure) throw new InvalidOperationException("Full board fixture failed.");
        }
        var fullSave = codec.Encode(full);
        Require(service.ClaimMonsterReward(full, id, 0).IsFailure && codec.Encode(full) == fullSave,
            "full boards retain unclaimed card and state");
        var released = full.Match.Board.Bench.Placements.First().CardId;
        var pending = full.Match.PendingMonsterRewards.Single();
        Require(service.SellCard(full, released).IsSuccess && !full.Match.Board.Contains(released)
            && full.Match.PendingMonsterRewards.Single() == pending, "result sale frees reward space without changing pending drop");
        full = codec.Decode(codec.Encode(full));
        Require(service.ClaimMonsterReward(full, id, 0).IsSuccess && full.Match.PendingMonsterRewards.Count == 0
            && full.Match.Player.Inventory.Cards.Count == 20, "full board reward can be claimed after selling and restoring");
        full = codec.Decode(fullSave);
        full.Match.RemovePendingMonsterReward(0);
        var skill = definitions.Skills.Values.First();
        full.Match.AddPendingMonsterReward(new PendingMonsterReward(MonsterRewardKind.Skill,
            skill.Attributes.Identity.Key, skill.Attributes.Identity.DisplayName, skill.InitialLevel));
        Require(service.ClaimMonsterReward(full, id, 0).IsSuccess && full.Match.Player.Skills.Items.Count > 0
            && codec.Encode(codec.Decode(codec.Encode(full))) == codec.Encode(full), "skill reward claim survives save restore");
        var damaged = JsonNode.Parse(prepared)!;
        damaged["battle"] = null;
        Require(Rejects(() => codec.Decode(damaged.ToJsonString())), "missing preparation payload rejected");
    }

    private static void Require(bool condition, string name)
    {
        if (!condition) throw new InvalidOperationException("save verification: " + name);
        Console.WriteLine("SAVE_CHECK=" + name);
    }
}
