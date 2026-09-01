using Aria;
using Godot;

namespace Project_Star.Core.Bases;

[GlobalClass]
public partial class HeroAttributeSet : AriaAttributeSet
{
	public const string HEALTH = "Health";
	public const string ARMOR = "Armor";
	public const string WEALTH = "Wealth";
	public const string EXPERIENCE = "Experience";
	public const string LEVEL = "Level";
	public const string REPUTATION = "Reputation";

	public StringName HeroKey { get; }

	public string HeroDisplayName { get; }

	public StringName FactionKey { get; }

	public AriaAttributeData Health => GetAttribute(HEALTH)!;
	public AriaAttributeData Armor => GetAttribute(ARMOR)!;
	public AriaAttributeData Wealth => GetAttribute(WEALTH)!;
	public AriaAttributeData Experience => GetAttribute(EXPERIENCE)!;
	public AriaAttributeData Level => GetAttribute(LEVEL)!;
	public AriaAttributeData Reputation => GetAttribute(REPUTATION)!;

	public HeroAttributeSet()
		: this(new StringName("Hero"), "Hero", new StringName("Default"))
	{
	}

	public HeroAttributeSet(StringName heroKey, string heroDisplayName, StringName factionKey)
	{
		HeroKey = heroKey;
		HeroDisplayName = heroDisplayName;
		FactionKey = factionKey;
		AddAttribute(HEALTH, new AriaAttributeData(100f, 0f, 100f));
		AddAttribute(ARMOR, new AriaAttributeData(0f, 0f, 100f));
		AddAttribute(WEALTH, new AriaAttributeData(0f, 0f, 100000f));
		AddAttribute(EXPERIENCE, new AriaAttributeData(0f, 0f, 100000f));
		AddAttribute(LEVEL, new AriaAttributeData(1f, 1f, 100f));
		AddAttribute(REPUTATION, new AriaAttributeData(100f, 0f, 100f));
	}
}