using Aria;
using Godot;

namespace Project_Star.Core;

[GlobalClass]
public partial class HeroAttributeSet : AriaAttributeSet
{
	public const string HEALTH = "Health";
	public const string ARMOR = "Armor";
	public const string WEALTH = "Wealth";
	public const string EXPERIENCE = "Experience";
	public const string LEVEL = "Level";

	public AriaAttributeData Health => GetAttribute(HEALTH)!;
	public AriaAttributeData Armor => GetAttribute(ARMOR)!;
	public AriaAttributeData Wealth => GetAttribute(WEALTH)!;
	public AriaAttributeData Experience => GetAttribute(EXPERIENCE)!;
	public AriaAttributeData Level => GetAttribute(LEVEL)!;

	public HeroAttributeSet()
	{
		AddAttribute(HEALTH, new AriaAttributeData(100f, 0f, 100f));
		AddAttribute(ARMOR, new AriaAttributeData(0f, 0f, 100f));
		AddAttribute(WEALTH, new AriaAttributeData(0f, 0f, 100000f));
		AddAttribute(EXPERIENCE, new AriaAttributeData(0f, 0f, 100000f));
		AddAttribute(LEVEL, new AriaAttributeData(1f, 1f, 100f));
	}
}