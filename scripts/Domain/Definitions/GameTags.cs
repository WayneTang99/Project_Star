using Godot;

namespace Project_Star.Domain.Definitions;

public static class GameTags
{
    public static readonly StringName Small = new("Small");
    public static readonly StringName Medium = new("Medium");
    public static readonly StringName Large = new("Large");
    public static readonly StringName Equipment = new("Equipment");
    public static readonly StringName Material = new("Material");
    public static readonly StringName Consumable = new("Consumable");
    public static readonly StringName Human = new("Human");
    public static readonly StringName Mechanical = new("Mechanical");
    public static readonly StringName Demon = new("Demon");
    public static readonly StringName Undead = new("Undead");
    public static readonly StringName Location = new("Location");

    public static StringName FromSize(CardSize size) => size switch
    {
        CardSize.Small => Small,
        CardSize.Medium => Medium,
        CardSize.Large => Large,
        _ => throw new System.ArgumentOutOfRangeException(nameof(size), size, null),
    };
}
