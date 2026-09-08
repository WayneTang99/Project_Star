using Godot;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;
using Project_Star.Core.Types;
using Project_Star.Entities.Abilities;

namespace Project_Star.Entities.Cards;

// 电击手枪：小型卡，造成等级相关伤害（10/20/40/60/80）并麻痹随机敌方卡牌。
// 伤害随等级变化由卡牌自身处理：等级变化时通过 OnLevelChanged 钩子更新 ATTACK_POWER 属性。
public partial class ShockPistolCard : CardBase
{
	// 伤害等级表（索引 0~4 对应等级 1~5）
	private static readonly float[] DamageTable = { 10f, 20f, 40f, 60f, 80f };

	public ShockPistolCard()
	{
		AttributeSet = new CardAttributeSet(
			new StringName("Shock_Pistol"),
			"电击手枪",
			new StringName("Dome_Enforcer"),
			CardSize.Small);
		TagSet.Add(Tags.FromSize(AttributeSet.Size));
		TagSet.Add(Tags.Weapon);
		InitializeValue();
		CooldownDuration = 5f;
		AttributeSet.Cooldown.SetCurrentValue(5f);
		Abilities.Add(new AttackAbility());
		Abilities.Add(new ParalysisAbility { DurationSeconds = 1f });
		InitializeLevelListener();
	}

	protected override void OnLevelChanged(int newLevel)
	{
		int index = Mathf.Clamp(newLevel - 1, 0, DamageTable.Length - 1);
		AttributeSet.AttackPower.SetCurrentValue(DamageTable[index]);
	}
}
