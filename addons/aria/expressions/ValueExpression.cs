using System;
using Aria;

namespace Aria;

// 动态值表达式：支持常量、属性读取、算术运算，运算符重载链式组合。
// 用于能力/效果中需要动态计算的数值（伤害、治疗、条件阈值等）。
public class ValueExpression
{
	private readonly Func<IAriaEntity?, float> _evaluator;

	public ValueExpression(Func<IAriaEntity?, float> evaluator)
	{
		_evaluator = evaluator;
	}

	// 求值：context 为当前执行主体（Owner），返回计算结果
	public float Evaluate(IAriaEntity? context) => _evaluator(context);

	// --- 工厂方法 ---

	// 常量值
	public static ValueExpression Constant(float value) => new(_ => value);

	// 读取实体的指定属性当前值
	public static ValueExpression Attribute(string key) => new(ctx =>
	{
		if (ctx?.AttributeSet is null) return 0f;
		return ctx.AttributeSet.GetAttribute(key)?.CurrentValue ?? 0f;
	});

	// 读取实体的指定属性基础值
	public static ValueExpression BaseAttribute(string key) => new(ctx =>
	{
		if (ctx?.AttributeSet is null) return 0f;
		return ctx.AttributeSet.GetAttribute(key)?.BaseValue ?? 0f;
	});

	// 取两个表达式的较小值
	public static ValueExpression Min(ValueExpression a, ValueExpression b) =>
		new(ctx => Math.Min(a.Evaluate(ctx), b.Evaluate(ctx)));

	// 取两个表达式的较大值
	public static ValueExpression Max(ValueExpression a, ValueExpression b) =>
		new(ctx => Math.Max(a.Evaluate(ctx), b.Evaluate(ctx)));

	// --- 运算符重载 ---

	public static ValueExpression operator +(ValueExpression left, ValueExpression right) =>
		new(ctx => left.Evaluate(ctx) + right.Evaluate(ctx));

	public static ValueExpression operator +(ValueExpression left, float right) =>
		new(ctx => left.Evaluate(ctx) + right);

	public static ValueExpression operator +(float left, ValueExpression right) =>
		new(ctx => left + right.Evaluate(ctx));

	public static ValueExpression operator -(ValueExpression left, ValueExpression right) =>
		new(ctx => left.Evaluate(ctx) - right.Evaluate(ctx));

	public static ValueExpression operator -(ValueExpression left, float right) =>
		new(ctx => left.Evaluate(ctx) - right);

	public static ValueExpression operator -(float left, ValueExpression right) =>
		new(ctx => left - right.Evaluate(ctx));

	public static ValueExpression operator *(ValueExpression left, ValueExpression right) =>
		new(ctx => left.Evaluate(ctx) * right.Evaluate(ctx));

	public static ValueExpression operator *(ValueExpression left, float right) =>
		new(ctx => left.Evaluate(ctx) * right);

	public static ValueExpression operator *(float left, ValueExpression right) =>
		new(ctx => left * right.Evaluate(ctx));

	public static ValueExpression operator /(ValueExpression left, ValueExpression right) =>
		new(ctx =>
		{
			float r = right.Evaluate(ctx);
			return r == 0f ? 0f : left.Evaluate(ctx) / r;
		});

	public static ValueExpression operator /(ValueExpression left, float right) =>
		new(ctx => right == 0f ? 0f : left.Evaluate(ctx) / right);

	// 隐式转换：float → Constant
	public static implicit operator ValueExpression(float value) => Constant(value);
}
