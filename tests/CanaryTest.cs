using Chickensoft.GoDotTest;
using Godot;
using Project_Star.Entities.Cards;

namespace Project_Star.Tests;

// 金丝雀测试：验证局内测试环境可实例化引擎耦合实体（Node 派生类），用于证明测试基础设施工作正常。
public class CanaryTest : TestClass
{
	public CanaryTest(Node testScene) : base(testScene) { }

	// 实例化真实引擎耦合卡牌实体并断言其存在，证明 Node/Resource 派生类可在测试内构建。
	[Test]
	public void InstantiateCard_EngineCouplingWorks()
	{
		var card = new TemplateCard();
		Assert.True(card is Node, "TemplateCard 应派生自 Godot Node，可在局内测试中实例化");
	}

	// 金丝雀绿灯测试：断言通过，用于证明运行器能报告 GREEN。
	[Test]
	public void Canary_ShouldPassGreen()
	{
		Assert.True(true, "金丝雀通过验证运行器绿灯路径");
	}
}