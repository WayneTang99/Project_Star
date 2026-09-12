using System;
using Aria;

namespace Aria;

// 条件类型枚举
public enum ConditionOp
{
	Equals,
	NotEquals,
	GreaterThan,
	GreaterOrEqual,
	LessThan,
	LessOrEqual,
}

// 条件判断：支持比较、AND/OR 组合，用于能力激活条件、解锁条件等。
public class Condition
{
	private readonly Func<IAriaEntity?, bool> _evaluator;

	public Condition(Func<IAriaEntity?, bool> evaluator)
	{
		_evaluator = evaluator;
	}

	// 求值
	public bool Evaluate(IAriaEntity? context) => _evaluator(context);

	// --- 工厂方法 ---

	// 比较：属性值 vs 常量
	public static Condition Compare(string attributeKey, ConditionOp op, float value) => new(ctx =>
	{
		if (ctx?.AttributeSet is null) return false;
		float current = ctx.AttributeSet.GetAttribute(attributeKey)?.CurrentValue ?? 0f;
		return op switch
		{
			ConditionOp.Equals => Math.Abs(current - value) < 0.001f,
			ConditionOp.NotEquals => Math.Abs(current - value) >= 0.001f,
			ConditionOp.GreaterThan => current > value,
			ConditionOp.GreaterOrEqual => current >= value,
			ConditionOp.LessThan => current < value,
			ConditionOp.LessOrEqual => current <= value,
			_ => false,
		};
	});

	// 比较：属性A vs 属性B
	public static Condition CompareAttributes(string keyA, ConditionOp op, string keyB) => new(ctx =>
	{
		if (ctx?.AttributeSet is null) return false;
		float a = ctx.AttributeSet.GetAttribute(keyA)?.CurrentValue ?? 0f;
		float b = ctx.AttributeSet.GetAttribute(keyB)?.CurrentValue ?? 0f;
		return op switch
		{
			ConditionOp.Equals => Math.Abs(a - b) < 0.001f,
			ConditionOp.NotEquals => Math.Abs(a - b) >= 0.001f,
			ConditionOp.GreaterThan => a > b,
			ConditionOp.GreaterOrEqual => a >= b,
			ConditionOp.LessThan => a < b,
			ConditionOp.LessOrEqual => a <= b,
			_ => false,
		};
	});

	// 永远为真
	public static Condition Always => new(_ => true);

	// 永远为假
	public static Condition Never => new(_ => false);

	// --- 组合 ---

	// AND：两个条件都为真
	public static Condition And(Condition a, Condition b) =>
		new(ctx => a.Evaluate(ctx) && b.Evaluate(ctx));

	// OR：任一条件为真
	public static Condition Or(Condition a, Condition b) =>
		new(ctx => a.Evaluate(ctx) || b.Evaluate(ctx));

	// NOT：取反
	public static Condition Not(Condition a) =>
		new(ctx => !a.Evaluate(ctx));

	// 隐式转换：bool → Condition
	public static implicit operator Condition(bool value) => value ? Always : Never;
}
