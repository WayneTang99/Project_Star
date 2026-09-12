using Godot;

namespace Project_Star.Core.Bases;

// 管理器基类：统一初始化 / 释放生命周期。_Ready 自动触发 Initialize，_ExitTree 自动触发 Shutdown（幂等）。
// 引用注入由上层在挂载前通过属性注入，或由子类在 OnInitialize 中通过兄弟节点获取。
public abstract partial class ManagerBase : Node
{
	// 是否已初始化；Initialize / Shutdown 幂等驱动
	public bool IsInitialized { get; private set; }

	public override void _Ready()
	{
		base._Ready();
		Initialize();
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		Shutdown();
	}

	// 初始化：执行引用注入与事件订阅；仅首次生效
	public void Initialize()
	{
		if (IsInitialized)
		{
			return;
		}

		IsInitialized = true;
		OnInitialize();
	}

	// 释放：取消事件订阅与清理；仅初始化过才生效
	public void Shutdown()
	{
		if (!IsInitialized)
		{
			return;
		}

		IsInitialized = false;
		OnShutdown();
	}

	// 初始化钩子，子类覆写
	protected virtual void OnInitialize()
	{
	}

	// 释放钩子，子类覆写
	protected virtual void OnShutdown()
	{
	}
}