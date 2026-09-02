using Aria;
using Godot;

namespace Project_Star.Core.AttributeSets;

// 英雄属性集：身份字段不可变，数值属性走 AriaAttributeData 流转。
[GlobalClass]
public partial class HeroAttributeSet : AriaAttributeSet
{
	// 生命属性 key
	public const string HEALTH = "Health";
	// 最大生命属性 key
	public const string MAX_HEALTH = "MaxHealth";
	// 护甲属性 key
	public const string ARMOR = "Armor";
	// 能量属性 key
	public const string ENERGY = "Energy";
	// 最大能量属性 key
	public const string MAX_ENERGY = "MaxEnergy";
	// 财富属性 key
	public const string WEALTH = "Wealth";
	// 经验属性 key
	public const string EXPERIENCE = "Experience";
	// 等级属性 key
	public const string LEVEL = "Level";
	// 声望属性 key
	public const string REPUTATION = "Reputation";

	// 英雄标识 key（不可变）
	public StringName HeroKey { get; }

	// 英雄展示名（不可变）
	public string HeroDisplayName { get; }

	// 阵营 key（不可变）
	public StringName FactionKey { get; }

	// 生命属性
	public AriaAttributeData Health => GetAttribute(HEALTH)!;
	// 最大生命属性
	public AriaAttributeData MaxHealth => GetAttribute(MAX_HEALTH)!;
	// 护甲属性
	public AriaAttributeData Armor => GetAttribute(ARMOR)!;
	// 能量属性
	public AriaAttributeData Energy => GetAttribute(ENERGY)!;
	// 最大能量属性
	public AriaAttributeData MaxEnergy => GetAttribute(MAX_ENERGY)!;
	// 财富属性
	public AriaAttributeData Wealth => GetAttribute(WEALTH)!;
	// 经验属性
	public AriaAttributeData Experience => GetAttribute(EXPERIENCE)!;
	// 等级属性
	public AriaAttributeData Level => GetAttribute(LEVEL)!;
	// 声望属性
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
		AddAttribute(MAX_HEALTH, new AriaAttributeData(100f, 0f, 10000f));
		AddAttribute(ARMOR, new AriaAttributeData(0f, 0f, 100f));
		AddAttribute(ENERGY, new AriaAttributeData(0f, 0f, 100f));
		AddAttribute(MAX_ENERGY, new AriaAttributeData(100f, 0f, 10000f));
		AddAttribute(WEALTH, new AriaAttributeData(0f, 0f, 100000f));
		AddAttribute(EXPERIENCE, new AriaAttributeData(0f, 0f, 100000f));
		AddAttribute(LEVEL, new AriaAttributeData(1f, 1f, 100f));
		AddAttribute(REPUTATION, new AriaAttributeData(100f, 0f, 100f));

		// 游戏侧联动：MaxHealth 变化同步 Health.MaxValue、MaxEnergy 同步 Energy.MaxValue（不纳入 Aria 框架）
		MaxHealth.OnValueChanged += (_, _) => Health.SetMaxValue(MaxHealth.CurrentValue);
		MaxEnergy.OnValueChanged += (_, _) => Energy.SetMaxValue(MaxEnergy.CurrentValue);
	}
}