using Aria;
using Godot;
using Project_Star.Core.Types;

namespace Project_Star.Core.AttributeSets;

// 卡牌属性集：身份字段不可变，等级走 AriaAttributeData 流转。
[GlobalClass]
public partial class CardAttributeSet : AriaAttributeSet
{
	// 等级属性 key
	public const string LEVEL = "Level";
	// 价值属性 key（动态，可被购买/出售等事件修改）
	public const string VALUE = "Value";
	// 攻击力属性 key
	public const string ATTACK_POWER = "AttackPower";
	// 治疗量属性 key
	public const string HEAL_POWER = "HealPower";
	// 叠甲量属性 key
	public const string ARMOR_POWER = "ArmorPower";
	// 辐射值属性 key
	public const string RADIATION_POWER = "RadiationPower";
	// 腐蚀值属性 key
	public const string CORROSION_POWER = "CorrosionPower";
	// 超频时长属性 key
	public const string OVERCLOCK_DURATION = "OverclockDuration";
	// 冷却时间属性 key
	public const string COOLDOWN = "Cooldown";
	// 麻痹时长属性 key
	public const string PARALYSIS_DURATION = "ParalysisDuration";


	// 卡牌标识 key（不可变）
	public StringName CardKey { get; }

	// 卡牌展示名（不可变）
	public string DisplayName { get; }

	// 英雄 key（不可变）
	public StringName HeroKey { get; }

	// 卡牌尺寸（不可变）
	public CardSize Size { get; }

	// 等级属性
	public AriaAttributeData Level => GetAttribute(LEVEL)!;
	// 价值属性
	public AriaAttributeData Value => GetAttribute(VALUE)!;
	// 攻击力属性
	public AriaAttributeData AttackPower => GetAttribute(ATTACK_POWER)!;
	// 治疗量属性
	public AriaAttributeData HealPower => GetAttribute(HEAL_POWER)!;
	// 叠甲量属性
	public AriaAttributeData ArmorPower => GetAttribute(ARMOR_POWER)!;
	// 辐射值属性
	public AriaAttributeData RadiationPower => GetAttribute(RADIATION_POWER)!;
	// 腐蚀值属性
	public AriaAttributeData CorrosionPower => GetAttribute(CORROSION_POWER)!;
	// 超频时长属性
	public AriaAttributeData OverclockDuration => GetAttribute(OVERCLOCK_DURATION)!;
	// 冷却时间属性
	public AriaAttributeData Cooldown => GetAttribute(COOLDOWN)!;
	// 麻痹时长属性
	public AriaAttributeData ParalysisDuration => GetAttribute(PARALYSIS_DURATION)!;
	
	// 构造：注入不可变身份字段并初始化等级属性
	public CardAttributeSet(StringName cardKey, string displayName, StringName heroKey, CardSize size)
	{
		CardKey = cardKey;
		DisplayName = displayName;
		HeroKey = heroKey;
		Size = size;
		AddAttribute(LEVEL, new AriaAttributeData(1f, 1f, 5f));
		AddAttribute(ATTACK_POWER, new AriaAttributeData(0f, 0f, 9999f));
		AddAttribute(HEAL_POWER, new AriaAttributeData(0f, 0f, 9999f));
		AddAttribute(ARMOR_POWER, new AriaAttributeData(0f, 0f, 9999f));
		AddAttribute(RADIATION_POWER, new AriaAttributeData(0f, 0f, 9999f));
		AddAttribute(CORROSION_POWER, new AriaAttributeData(0f, 0f, 9999f));
		AddAttribute(OVERCLOCK_DURATION, new AriaAttributeData(0f, 0f, 9999f));
		AddAttribute(COOLDOWN, new AriaAttributeData(0f, 0f, 9999f));
		AddAttribute(PARALYSIS_DURATION, new AriaAttributeData(0f, 0f, 9999f));
		AddAttribute(VALUE, new AriaAttributeData(0f, 0f, 99999f));
	}
}