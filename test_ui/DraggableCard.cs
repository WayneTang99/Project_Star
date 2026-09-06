using Godot;

namespace Project_Star.TestUI;

// 可拖拽卡牌面板：列表项，拖拽时传卡牌名称。
public sealed partial class DraggableCard : PanelContainer
{
	public string CardName { get; set; } = "";

	public DraggableCard() { }

	public DraggableCard(string cardName)
	{
		CardName = cardName;
	}

	public override Variant _GetDragData(Vector2 atPosition)
	{
		var preview = new PanelContainer
		{
			CustomMinimumSize = CustomMinimumSize,
			Modulate = new Color(1f, 1f, 1f, 0.6f),
		};
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.18f, 0.22f, 0.28f, 1f),
			BorderColor = new Color(0.5f, 0.55f, 0.65f, 1f),
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
		};
		preview.AddThemeStyleboxOverride("panel", style);
		var lbl = new Label { Text = CardName };
		preview.AddChild(lbl);
		SetDragPreview(preview);
		return new Godot.Collections.Array { CardName };
	}
}
