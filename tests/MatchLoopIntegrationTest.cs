using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Chickensoft.GoDotTest;
using Godot;
using Project_Star.Board.Manager;
using Project_Star.Core;
using Project_Star.Core.Bases;
using Project_Star.Core.Types;
using Project_Star.Entities.Cards;
using Project_Star.Entities.Events;

namespace Project_Star.Tests;

// 全对局闭环集成测试：实例化真实 Main（全部管理器生产装配），
// 选英雄 → 自动推进回合 → 怪物/PvP 战斗 → 奖励/惩罚 → 轮次推进 → 最终到达 Result，
// 证明"可玩闭环"在真实接线下确实闭合。
public class MatchLoopIntegrationTest : TestClass
{
	// GoDotTest 注入的测试场景节点（用于挂载与等待信号）
	private readonly Node _testScene;

	// 对局最长等待秒数：超过视为闭环未在限时内闭合
	// 健全生命恢复后 PvP 战斗需要更多回合；完整 6 轮约需 10-15 分钟
	private const double MATCH_TIMEOUT_SECONDS = 1800;

	public MatchLoopIntegrationTest(Node testScene) : base(testScene)
	{
		_testScene = testScene;
	}

	// 全对局自动打至 Result：限定时间内对局状态必须到达 Result，
	// 且结束原因为真实胜负（Victory/Defeat），证明闭环走完整条流程。
	[Test]
	public async Task FullMatch_AutoPlayReachesResult()
	{
		// 实例化真实主节点并挂入场景树：触发 GameManager/HeroManager/CardManager/BoardManager/
		// RoundTurnManager/EventManager/CombatManager/MatchFlow 全部 _Ready 装配
		Main main = await SpawnMain();

		// 反射模板池应排除 internal 测试英雄，验证不污染生产英雄池
		Assert.True(main.HeroManager.AvailableHeroes.All(h => h.AttributeSet.HeroKey != new StringName("Fast_Test")),
			"FastTestHero 为 internal，不应被反射收集进 AvailableHeroes");

		// 先放卡再选英雄：把 2 张电击手枪放入战场区（提供进攻能力，同时覆盖棋盘/战斗接线）；
		// 我方英雄低生命，战斗仍以失败告终，闭环照常推进
		int placed = 0;
		foreach (CardBase template in main.CardManager.CardTemplates)
		{
			if (template is not ShockPistolCard)
			{
				continue;
			}

			CardBase card = main.CardManager.CreateCard(template);
			Assert.True(main.BoardManager.CommitMove(card, main.BoardManager.Battlefield, placed),
				"卡牌应能放置到战场区");
			placed++;
			if (placed >= 2)
			{
				break;
			}
		}

		// 先开自动对局再选英雄：第 1 回合事件组一经生成即被协调器接管推进
		main.MatchFlow.FlowLog += msg => GD.Print($"[MatchLoop] {msg}");
		main.MatchFlow.AutoPlay = true;
		main.HeroManager.SelectHero(new FastTestHero());
		Assert.True(main.GameManager.CurrentState == GameState.InMatch, "选英雄后应进入 InMatch");

		// 限时轮询等待闭环推进到对局结算（每 0.2s 检查一次，超时即失败，绝不无限挂起）
		var stopwatch = Stopwatch.StartNew();
		while (main.GameManager.CurrentState != GameState.Result
			&& stopwatch.Elapsed.TotalSeconds < MATCH_TIMEOUT_SECONDS)
		{
			await _testScene.ToSignal(_testScene.GetTree().CreateTimer(0.2f), SceneTreeTimer.SignalName.Timeout);
		}

		// 主断言：对局必须到达 Result，结束原因为真实胜负（非认输/未知）
		Assert.True(main.GameManager.CurrentState == GameState.Result,
			$"对局应在 {MATCH_TIMEOUT_SECONDS}s 内到达 Result，实际状态={main.GameManager.CurrentState}");
		Assert.True(main.GameManager.LastMatchEndReason is MatchEndReason.Victory or MatchEndReason.Defeat,
			$"结束原因应为 Victory/Defeat，实际={main.GameManager.LastMatchEndReason}");

		// 清理：释放主节点，让各管理器 _ExitTree 注销静态总线订阅，避免影响后续测试
		main.QueueFree();
		await WaitOneFrame();
	}

	// 默认回合事件组应至少包含 1 个商店事件：验证排程规则（默认三选一、至少 1 商店）。
	[Test]
	public async Task EventsGenerated_DefaultTurnHasShop()
	{
		Main main = await SpawnMain();
		try
		{
			// 选英雄即进入局内并生成第 1 回合事件组
			main.HeroManager.SelectHero(new FastTestHero());
			Assert.True(main.EventManager.CurrentEvents.Count > 0, "第 1 回合应生成事件组");
			Assert.True(main.EventManager.CurrentEvents.Any(evt => evt is ShopEvent),
				"默认回合事件组应至少包含 1 个商店事件");
		}
		finally
		{
			main.QueueFree();
			await WaitOneFrame();
		}
	}

	// 延迟挂载真实 Main 到场景树根并等待 _Ready 装配完成。
	// 测试执行期间场景树可能处于"子节点就绪"阶段，直接 AddChild 会报
	// "Parent node is busy setting up children"，须用 CallDeferred 延迟到空闲帧执行。
	private async Task<Main> SpawnMain()
	{
		var main = new Main();
		_testScene.GetTree().Root.CallDeferred(Node.MethodName.AddChild, main);
		// 等待延迟挂载执行并完成全部管理器 _Ready 装配（两帧保险）
		await WaitOneFrame();
		await WaitOneFrame();
		Assert.True(main.GameManager is not null, "Main._Ready 应已完成管理器装配");
		return main;
	}

	// 等待一帧：让节点入树后的帧循环推进一帧
	private async Task WaitOneFrame()
	{
		await _testScene.ToSignal(_testScene.GetTree(), SceneTree.SignalName.ProcessFrame);
	}
}