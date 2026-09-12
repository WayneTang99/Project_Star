using System;

namespace Project_Star.Combat;

// 战斗时钟：1/30 秒固定步长，供战斗循环使用。
// 管理器每帧推进 accumulator，满一步则回调 Tick。
public class BattleClock
{
	// 固定步长（1/30 秒 ≈ 0.0333s）
	public const float FIXED_STEP = 1f / 30f;

	// 步长倒数（每秒步数，用于乘以 dt）
	public const float STEP_RECIPROCAL = 30f;

	// 累积器（秒）
	private float _accumulator;

	// 步数计数器
	public int StepCount { get; private set; }

	// 每步回调
	public event Action? StepTicked;

	public BattleClock()
	{
		Reset();
	}

	// 重置时钟
	public void Reset()
	{
		_accumulator = 0f;
		StepCount = 0;
	}

	// 推进时间；delta 为帧间隔（秒），返回本帧执行的步数
	public int Advance(float delta)
	{
		_accumulator += delta;
		int steps = 0;
		while (_accumulator >= FIXED_STEP)
		{
			_accumulator -= FIXED_STEP;
			StepCount++;
			steps++;
			StepTicked?.Invoke();
		}

		return steps;
	}

	// 获取累积器当前值（秒，0~FIXED_STEP）
	public float Accumulator => _accumulator;
}
