using Aria;
using Godot;
using Project_Star.Core.Types;

namespace Project_Star.Core.Bases;

[GlobalClass]
public partial class CardAttributeSet : AriaAttributeSet
{
	public const string LEVEL = "Level";

	public string CardKey { get; }

	public string DisplayName { get; }

	public string HeroKey { get; }

	public CardSize Size { get; }

	public AriaAttributeData Level => GetAttribute(LEVEL)!;

	public CardAttributeSet(string cardKey, string displayName, string heroKey, CardSize size)
	{
		CardKey = cardKey;
		DisplayName = displayName;
		HeroKey = heroKey;
		Size = size;
		AddAttribute(LEVEL, new AriaAttributeData(1f, 1f, 100f));
	}
}