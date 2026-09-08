using System.Reflection;
using Chickensoft.GoDotTest;
using Godot;

namespace Project_Star.Tests;

// 局内测试入口：挂载在 TestRunner.tscn 根节点，运行当前程序集内的 GoDotTest 测试。
public partial class TestRunner : Node
{
	public override async void _Ready()
	{
		// 依据命令行参数构建测试环境，识别 --run-tests / --quit-on-finish
		var environment = TestEnvironment.From(OS.GetCmdlineArgs());
		await GoTest.RunTests(Assembly.GetExecutingAssembly(), this, environment);
		// 兜底退出，避免 headless 下残留空主循环挂起
		GetTree().Quit();
	}
}