using System;
using Godot;

namespace Project_Star.TestUI;

// 可放置战场区域：接收拖入的卡牌，回调通知 BattleTestUI。
public sealed partial class DropZone : HBoxContainer
{
	public string Side { get; }
	public Action<string, string> OnCardDropped { get; }

	public DropZone(string side, Action<string, string> onCardDropped)
	{
		Side = side;
		OnCardDropped = onCardDropped;
	}

	public override bool _CanDropData(Vector2 position, Variant data)
	{
		return data.VariantType == Variant.Type.Array;
	}

	public override void _DropData(Vector2 position, Variant data)
	{
		if (data.AsGodotArray() is { Count: > 0 } arr)
		{
			string cardName = arr[0].AsString();
			OnCardDropped.Invoke(Side, cardName);
		}
	}
}
