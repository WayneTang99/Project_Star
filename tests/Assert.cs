using System;

namespace Project_Star.Tests;

// 轻量断言辅助：GoDotTest 未捆绑断言库，测试内抛异常即视为失败。
public static class Assert
{
	// 断言条件成立，否则抛出异常标记测试失败。
	public static void True(bool condition, string message = "")
	{
		if (!condition)
		{
			throw new Exception($"断言失败（期望为真）：{message}");
		}
	}

	// 断言条件不成立，否则抛出异常标记测试失败。
	public static void False(bool condition, string message = "")
	{
		if (condition)
		{
			throw new Exception($"断言失败（期望为假）：{message}");
		}
	}

	// 断言两个引用相等，否则抛出异常标记测试失败。
	public static void Same(object expected, object actual, string message = "")
	{
		if (!ReferenceEquals(expected, actual))
		{
			throw new Exception($"断言失败（期望引用相同）：{message}");
		}
	}

	// 断言两个对象相等（按 Equals），否则抛出异常标记测试失败。
	public static void Equal(object expected, object actual, string message = "")
	{
		if (!expected.Equals(actual))
		{
			throw new Exception($"断言失败（期望 {expected}，实际 {actual}）：{message}");
		}
	}
}