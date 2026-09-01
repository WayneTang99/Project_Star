using Aria;
using Godot;
using Project_Star.Core.Types;

namespace Project_Star.Core.Bases;

[GlobalClass]
public partial class CardAttributeSet : AriaAttributeSet
{
	public const string LEVEL = "Level";

	public StringName CardKey { get; }

	public string DisplayName { get; }

	public StringName FactionKey { get; }

	public CardSize Size { get; }

	public AriaAttributeData Level => GetAttribute(LEVEL)!;

	public CardAttributeSet(StringName cardKey, string displayName, StringName factionKey, CardSize size)
	{
		CardKey = cardKey;
		DisplayName = displayName;
		FactionKey = factionKey;
		Size = size;
		AddAttribute(LEVEL, new AriaAttributeData(1f, 1f, 100f));
	}
}