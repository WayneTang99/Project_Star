using System.Linq;
using System.Threading.Tasks;
using Aria;
using Chickensoft.GoDotTest;
using Godot;
using Project_Star.Core.Bases;
using Project_Star.Core.Types;
using Project_Star.Entities.Cards;
using Project_Star.Systems;

namespace Project_Star.Tests;

// 卡牌经济规则单测：价值公式、购买折半、出售按现值全额回补、兽皮（特殊价值战利品）。
public class EconomyValueTest : TestClass
{
	private readonly Node _testScene;

	public EconomyValueTest(Node testScene) : base(testScene)
	{
		_testScene = testScene;
	}

	// 初始价值 = 价值系数 * 等级 * 型号系数（小 1 / 中 2 / 大 3）
	[Test]
	public void InitialValue_FollowsFormula()
	{
		CardBase large = new TemplateCard(); // 大型 Lv1 → 2*1*3=6
		Assert.True(Mathf.IsEqualApprox(large.GetInitialValue(), 6f),
			$"模板卡牌(大型 Lv1) 初始价值应为 6，实际 {large.GetInitialValue()}");
		Assert.True(Mathf.IsEqualApprox(large.AttributeSet.Value.CurrentValue, 6f),
			"构造后 Value 属性应同步为初始价值");

		CardBase small = new ShockPistolCard(); // 小型 Lv1 → 2*1*1=2
		Assert.True(Mathf.IsEqualApprox(small.GetInitialValue(), 2f),
			$"电击手枪(小型 Lv1) 初始价值应为 2，实际 {small.GetInitialValue()}");

		BeastHideCard hide = new BeastHideCard(); // 小型 Lv1 系数 4 → 4*1*1=4
		Assert.True(Mathf.IsEqualApprox(hide.GetInitialValue(), 4f),
			$"兽皮(小型 Lv1, 系数 4) 初始价值应为 4，实际 {hide.GetInitialValue()}");
		Assert.True(hide.Abilities.Count == 0, "兽皮不应携带任何能力");
	}

	// 购买成交后加入玩家卡池即视为获得：副本价值折半为初始价值一半；出售按现值全额回补（不二次打折）
	[Test]
	public async Task BuyHalvesValue_SellRefundsCurrentValue()
	{
		CardManager cardManager = await SpawnCardManager();
		try
		{
			CardBase? template = cardManager.CardTemplates.FirstOrDefault(c => c is TemplateCard);
			Assert.True(template is not null, "模板池应含 TemplateCard");

			var wealth = new AriaAttributeData(100f, 0f, 100000f);
			float price = template!.GetInitialValue();

			Assert.True(cardManager.BuyCard(template, wealth), "财富充足应购买成功");
			Assert.True(Mathf.IsEqualApprox(wealth.CurrentValue, 100f - price), "购买应按初始价值扣除财富");
			Assert.True(cardManager.PlayerCards.Count == 1, "购买后玩家应持有 1 张卡");

			CardBase owned = cardManager.PlayerCards[0];
			Assert.True(Mathf.IsEqualApprox(owned.AttributeSet.Value.CurrentValue, price * CardEconomy.OBTAINED_VALUE_RATIO),
				"获得（加入玩家卡池）后副本价值应折半为初始价值一半");

			Assert.True(cardManager.SellCard(owned, wealth), "持有卡应可出售");
			float expectedWealth = 100f - price + price * CardEconomy.OBTAINED_VALUE_RATIO;
			Assert.True(Mathf.IsEqualApprox(wealth.CurrentValue, expectedWealth),
				$"出售应按现值全额回补，期望 {expectedWealth}，实际 {wealth.CurrentValue}");
			Assert.True(cardManager.PlayerCards.Count == 0, "出售后玩家应不再持有该卡");
		}
		finally
		{
			cardManager.QueueFree();
			await WaitOneFrame();
		}
	}

	// 兽皮作为掉落道具：加入玩家池（获得）即折半为初始价值一半，与获得途径无关；出售按现值全额回补
	[Test]
	public async Task BeastHide_LootHalvesOnObtain_SellsAtCurrentValue()
	{
		CardManager cardManager = await SpawnCardManager();
		try
		{
			CardBase? template = cardManager.CardTemplates.FirstOrDefault(c => c is BeastHideCard);
			Assert.True(template is not null, "模板池应含兽皮");

			CardBase hide = cardManager.CreateCard(template!);
			Assert.True(Mathf.IsEqualApprox(hide.AttributeSet.Value.CurrentValue, 4f),
				"加入玩家池前兽皮价值应保持全额初始价值 4");

			cardManager.AddCardToPlayer(hide); // 掉落/奖励途径：加入即获得，同样折半
			Assert.True(Mathf.IsEqualApprox(hide.AttributeSet.Value.CurrentValue, 4f * CardEconomy.OBTAINED_VALUE_RATIO),
				"获得后兽皮价值应折半为 2");

			var wealth = new AriaAttributeData(0f, 0f, 100000f);
			Assert.True(cardManager.SellCard(hide, wealth), "掉落兽皮应可出售");
			Assert.True(Mathf.IsEqualApprox(wealth.CurrentValue, 2f), "出售兽皮应按现值 2 全额回补");
		}
		finally
		{
			cardManager.QueueFree();
			await WaitOneFrame();
		}
	}

	// 延迟挂载 CardManager 到场景树根并等待 _Ready 完成反射模板收集
	private async Task<CardManager> SpawnCardManager()
	{
		var manager = new CardManager();
		_testScene.GetTree().Root.CallDeferred(Node.MethodName.AddChild, manager);
		await WaitOneFrame();
		await WaitOneFrame();
		Assert.True(manager.CardTemplates.Count > 0, "CardManager._Ready 应已完成模板收集");
		return manager;
	}

	// 等待一帧：让节点入树后的帧循环推进一帧
	private async Task WaitOneFrame()
	{
		await _testScene.ToSignal(_testScene.GetTree(), SceneTree.SignalName.ProcessFrame);
	}
}
