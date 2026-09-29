using Godot;

namespace Project_Star.Presentation.CardFace;

public sealed partial class CardValueBadge : HBoxContainer
{
    private Label _value = null!;

    public override void _Ready() => _value = GetNode<Label>("Value");

    public void SetValue(int value)
    {
        _value.Text = value.ToString();
    }
}
