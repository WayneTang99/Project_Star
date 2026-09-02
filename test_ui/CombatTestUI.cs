using System;
using System.Collections.Generic;
using System.Text;
using Aria;
using Godot;
using Project_Star.Combat.Events;
using Project_Star.Combat.Managers;
using Project_Star.Core.Interfaces;

namespace Project_Star.TestUI;

// 战斗管理器独立测试 UI：敌我双英雄 + 各自棋盘卡牌；卡牌无生命，伤害/治疗指向英雄，英雄死亡判负。
public partial class CombatTestUI : Control
{
	// 日志最大行数
	private const int MAX_LOG_LINES = 300;

	private CombatManager _combatManager = null!;

	private CombatTestHero _friendlyHero = null!;
	private CombatTestHero _enemyHero = null!;

	private readonly List<CombatTestCard> _friendlies = new();
	private readonly List<CombatTestCard> _enemies = new();

	private UnitCell _friendlyHeroCell = null!;
	private UnitCell _enemyHeroCell = null!;
	private readonly List<UnitCell> _friendlyCells = new();
	private readonly List<UnitCell> _enemyCells = new();

	private readonly List<string> _logLines = new();

	private VBoxContainer _friendlyHeroBox = null!;
	private VBoxContainer _enemyHeroBox = null!;
	private HBoxContainer _friendlyRow = null!;
	private HBoxContainer _enemyRow = null!;
	private Label _statusLabel = null!;
	private RichTextLabel _log = null!;

	private string _outcomeText = "未开战：点击 [开始战斗]";
	private bool _logDirty;

	// 单位格：棋盘槽位（英雄含 HP 条，卡牌仅能力）
	private sealed class UnitCell
	{
		public PanelContainer Panel = null!;
		public Label NameLabel = null!;
		public ProgressBar? HpBar;
		public Label? HpTextLabel;
		public Label? ArmorLabel;
		public readonly List<Label> AbilityLabels = new();
	}

	public override void _Ready()
	{
		base._Ready();
		_combatManager = new CombatManager();
		AddChild(_combatManager);
		BuildUI();
		SubscribeBus();
		SetupRoster();
		Rebuild();
		Refresh();
		AppendLog("就绪：点击 [开始战斗] 启动对战；[重建阵容] 重置双方");
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		UnsubscribeBus();
	}

	public override void _Process(double delta)
	{
		FlushLog();
		CheckOutcome();
		Refresh();
	}

	// 构建界面骨架（单位槽位在阵容就绪后填充）
	private void BuildUI()
	{
		var root = new VBoxContainer();
		root.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(root);

		var controlRow = new HBoxContainer();
		root.AddChild(controlRow);

		var startButton = new Button { Text = "开始战斗" };
		startButton.Pressed += OnStartBattle;
		controlRow.AddChild(startButton);

		var endButton = new Button { Text = "结束战斗" };
		endButton.Pressed += OnEndBattle;
		controlRow.AddChild(endButton);

		var resetButton = new Button { Text = "重建阵容" };
		resetButton.Pressed += OnResetRoster;
		controlRow.AddChild(resetButton);

		var heroTitle = new Label();
		heroTitle.AddThemeFontSizeOverride("font_size", 18);
		heroTitle.Text = "我方英雄";
		root.AddChild(heroTitle);
		_friendlyHeroBox = new VBoxContainer();
		root.AddChild(_friendlyHeroBox);

		var friendlyBoardTitle = new Label();
		friendlyBoardTitle.AddThemeFontSizeOverride("font_size", 16);
		friendlyBoardTitle.Text = "我方棋盘（左侧为前排，按序参战）";
		root.AddChild(friendlyBoardTitle);
		_friendlyRow = new HBoxContainer();
		root.AddChild(_friendlyRow);

		var enemyHeroTitle = new Label();
		enemyHeroTitle.AddThemeFontSizeOverride("font_size", 18);
		enemyHeroTitle.Text = "敌方英雄";
		root.AddChild(enemyHeroTitle);
		_enemyHeroBox = new VBoxContainer();
		root.AddChild(_enemyHeroBox);

		var enemyBoardTitle = new Label();
		enemyBoardTitle.AddThemeFontSizeOverride("font_size", 16);
		enemyBoardTitle.Text = "敌方棋盘";
		root.AddChild(enemyBoardTitle);
		_enemyRow = new HBoxContainer();
		root.AddChild(_enemyRow);

		_log = new RichTextLabel
		{
			CustomMinimumSize = new Vector2(0, 200),
			BbcodeEnabled = true,
			ScrollFollowing = true,
		};
		_log.AddThemeFontSizeOverride("normal_font_size", 14);
		root.AddChild(_log);

		_statusLabel = new Label();
		_statusLabel.AddThemeFontSizeOverride("font_size", 15);
		root.AddChild(_statusLabel);
	}

	// 依据当前阵容重建英雄槽位与两行卡牌槽位
	private void Rebuild()
	{
		ClearBox(_friendlyHeroBox);
		ClearBox(_enemyHeroBox);
		ClearRow(_friendlyRow, _friendlyCells);
		ClearRow(_enemyRow, _enemyCells);

		_friendlyHeroCell = CreateUnitCell(_friendlyHero.AttributeSet.HeroDisplayName,
			_friendlyHero.MaxHealth, _friendlyHero.Abilities, isHero: true);
		_friendlyHeroBox.AddChild(_friendlyHeroCell.Panel);

		_enemyHeroCell = CreateUnitCell(_enemyHero.AttributeSet.HeroDisplayName,
			_enemyHero.MaxHealth, _enemyHero.Abilities, isHero: true);
		_enemyHeroBox.AddChild(_enemyHeroCell.Panel);

		foreach (CombatTestCard card in _friendlies)
		{
			UnitCell cell = CreateUnitCell(card.AttributeSet.DisplayName, 0f, card.Abilities, isHero: false);
			_friendlyRow.AddChild(cell.Panel);
			_friendlyCells.Add(cell);
		}

		foreach (CombatTestCard card in _enemies)
		{
			UnitCell cell = CreateUnitCell(card.AttributeSet.DisplayName, 0f, card.Abilities, isHero: false);
			_enemyRow.AddChild(cell.Panel);
			_enemyCells.Add(cell);
		}
	}

	// 清空容器子节点
	private static void ClearBox(VBoxContainer box)
	{
		foreach (Node child in box.GetChildren())
		{
			box.RemoveChild(child);
			child.QueueFree();
		}
	}

	// 清空一行槽位
	private static void ClearRow(HBoxContainer row, List<UnitCell> cells)
	{
		foreach (Node child in row.GetChildren())
		{
			row.RemoveChild(child);
			child.QueueFree();
		}

		cells.Clear();
	}

	// 创建单位槽位面板（英雄带 HP 条 / 护甲，卡牌仅能力）
	private static UnitCell CreateUnitCell(string name, float maxHealth, Godot.Collections.Array<AriaAbilityBase> abilities, bool isHero)
	{
		var panel = new PanelContainer { CustomMinimumSize = new Vector2(190, isHero ? 170 : 150) };
		var style = new StyleBoxFlat
		{
			BgColor = isHero ? new Color(0.22f, 0.24f, 0.34f, 1f) : new Color(0.2f, 0.22f, 0.3f, 1f),
			BorderColor = new Color(0.45f, 0.5f, 0.6f, 1f),
			ContentMarginLeft = 8,
			ContentMarginTop = 6,
			ContentMarginRight = 8,
			ContentMarginBottom = 6,
			CornerRadiusTopLeft = 4,
			CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4,
			CornerRadiusBottomRight = 4,
			BorderWidthLeft = 1,
			BorderWidthTop = 1,
			BorderWidthRight = 1,
			BorderWidthBottom = 1,
		};
		panel.AddThemeStyleboxOverride("panel", style);

		var vbox = new VBoxContainer();
		panel.AddChild(vbox);

		var nameLabel = new Label();
		nameLabel.AddThemeFontSizeOverride("font_size", 20);
		vbox.AddChild(nameLabel);

		var cell = new UnitCell { Panel = panel, NameLabel = nameLabel };

		if (isHero)
		{
			var hpBar = new ProgressBar
			{
				MinValue = 0,
				MaxValue = maxHealth,
				Value = maxHealth,
				ShowPercentage = false,
				CustomMinimumSize = new Vector2(0, 20),
			};
			vbox.AddChild(hpBar);
			cell.HpBar = hpBar;

			var hpText = new Label();
			hpText.AddThemeFontSizeOverride("font_size", 13);
			vbox.AddChild(hpText);
			cell.HpTextLabel = hpText;

			var armor = new Label();
			armor.AddThemeFontSizeOverride("font_size", 13);
			vbox.AddChild(armor);
			cell.ArmorLabel = armor;
		}

		foreach (AriaAbilityBase ability in abilities)
		{
			var abilityLabel = new Label();
			abilityLabel.AddThemeFontSizeOverride("font_size", 13);
			if (ability is IPassiveAbility)
			{
				abilityLabel.AddThemeColorOverride("font_color", new Color(0.6f, 0.85f, 1f));
			}

			vbox.AddChild(abilityLabel);
			cell.AbilityLabels.Add(abilityLabel);
		}

		return cell;
	}

	// 配置默认测试阵容：双方英雄 + 各自棋盘卡牌（首个卡牌为前排），并挂载为节点
	private void SetupRoster()
	{
		ClearUnits();

		_friendlyHero = new CombatTestHero("A", 150f, 10f,
			//new CombatTestAttackAbility(1f, 10f),
			new CombatTestLifedrainAbility(2f),
			new CombatTestThornsAbility(3f));
		AddChild(_friendlyHero);

		_friendlies.Add(new CombatTestCard("CardA",
			new CombatTestAttackAbility(2f, 8f),
			new CombatTestSynergyAbility(5f)));
		// _friendlies.Add(new CombatTestCard("游侠射手",
		// 	new CombatTestPierceAbility(3f, 15f)));
		// _friendlies.Add(new CombatTestCard("烈焰法师",
		// 	new CombatTestRadiationAbility(4f, 4f)));
		// _friendlies.Add(new CombatTestCard("医疗兵",
		// 	new CombatTestHealAbility(3f, 15f)));
		// foreach (CombatTestCard card in _friendlies)
		// {
		// 	AddChild(card);
		// }

		_enemyHero = new CombatTestHero("B", 180f, 15f,
			//new CombatTestAttackAbility(1.5f, 12f),
			new CombatTestBerserkAbility(40f));
		AddChild(_enemyHero);

		// _enemies.Add(new CombatTestCard("敌兵甲",
		// 	new CombatTestAttackAbility(2f, 6f)));
		// _enemies.Add(new CombatTestCard("敌兵乙",
		// 	new CombatTestAttackAbility(2f, 6f)));
		// _enemies.Add(new CombatTestCard("敌方射手",
		// 	new CombatTestPierceAbility(3f, 10f),
		// 	new CombatTestSynergyAbility(4f)));
		// foreach (CombatTestCard card in _enemies)
		// {
		// 	AddChild(card);
		// }
		_enemies.Add(new CombatTestCard("CardB",
			new CombatTestHealAbility(3f, 15f)));
	}

	// 释放并清空单位节点
	private void ClearUnits()
	{
		_friendlyHero?.QueueFree();
		_enemyHero?.QueueFree();
		foreach (CombatTestCard card in _friendlies)
		{
			card.QueueFree();
		}

		foreach (CombatTestCard card in _enemies)
		{
			card.QueueFree();
		}

		_friendlies.Clear();
		_enemies.Clear();
	}

	// 开始战斗：双方英雄 + 棋盘卡牌
	private void OnStartBattle()
	{
		if (_friendlyHero is null || _enemyHero is null)
		{
			SetStatus("阵容为空，无法开战");
			return;
		}

		_combatManager.EndBattle();
		var friendlyCards = new List<ICombatant>();
		foreach (CombatTestCard card in _friendlies)
		{
			friendlyCards.Add(card);
		}

		var enemyCards = new List<ICombatant>();
		foreach (CombatTestCard card in _enemies)
		{
			enemyCards.Add(card);
		}

		_combatManager.StartBattle(_friendlyHero, friendlyCards, _enemyHero, enemyCards);
		_outcomeText = "";
		AppendLog("战斗开始");
	}

	// 结束战斗
	private void OnEndBattle()
	{
		if (_combatManager.IsInBattle)
		{
			AppendLog("战斗结束（手动）");
		}

		_combatManager.EndBattle();
		_outcomeText = "已手动结束";
	}

	// 重建阵容：结束当前战斗并重置双方
	private void OnResetRoster()
	{
		_combatManager.EndBattle();
		SetupRoster();
		Rebuild();
		_outcomeText = "未开战：点击 [开始战斗]";
		AppendLog("阵容已重建");
	}

	// 检测战斗结局：英雄死亡即判负
	private void CheckOutcome()
	{
		if (!_combatManager.IsInBattle)
		{
			return;
		}

		if (!_friendlyHero.IsAlive)
		{
			_combatManager.EndBattle();
			_outcomeText = "战斗结束：失败（我方英雄阵亡）";
			AppendLog("【失败】我方英雄阵亡");
		}
		else if (!_enemyHero.IsAlive)
		{
			_combatManager.EndBattle();
			_outcomeText = "战斗结束：胜利（敌方英雄阵亡）";
			AppendLog("【胜利】敌方英雄阵亡");
		}
	}

	// 刷新全部槽位与整体状态
	private void Refresh()
	{
		UpdateUnitCell(_friendlyHeroCell, _friendlyHero);
		UpdateUnitCell(_enemyHeroCell, _enemyHero);

		for (int i = 0; i < _friendlyCells.Count && i < _friendlies.Count; i++)
		{
			UpdateCardCell(_friendlyCells[i], _friendlies[i]);
		}

		for (int i = 0; i < _enemyCells.Count && i < _enemies.Count; i++)
		{
			UpdateCardCell(_enemyCells[i], _enemies[i]);
		}

		SetStatus(_combatManager.IsInBattle ? "战斗中（每 0.2s 一帧结算）" : _outcomeText);
	}

	// 更新英雄槽位：名称 / HP 条 / 护甲 / 能力与冷却
	private void UpdateUnitCell(UnitCell cell, CombatTestHero hero)
	{
		cell.NameLabel.Text = hero.AttributeSet.HeroDisplayName + (hero.IsAlive ? "" : " 【阵亡】");
		cell.NameLabel.Modulate = hero.IsAlive ? Colors.White : Colors.Gray;

		if (cell.HpBar is not null)
		{
			cell.HpBar.MaxValue = hero.MaxHealth;
			cell.HpBar.Value = Mathf.Clamp(hero.Health, 0f, hero.MaxHealth);
		}

		if (cell.HpTextLabel is not null)
		{
			cell.HpTextLabel.Text = $"HP {hero.Health:F0} / {hero.MaxHealth:F0}";
		}

		if (cell.ArmorLabel is not null)
		{
			cell.ArmorLabel.Text = $"护甲 {hero.Armor:F0}";
		}

		UpdateAbilityLabels(cell, hero.Abilities, hero);
	}

	// 更新卡牌槽位：名称 / 能力与冷却（无生命）
	private void UpdateCardCell(UnitCell cell, CombatTestCard card)
	{
		cell.NameLabel.Text = card.AttributeSet.DisplayName;
		cell.NameLabel.Modulate = Colors.White;
		UpdateAbilityLabels(cell, card.Abilities, card);
	}

	// 更新能力与冷却文本
	private void UpdateAbilityLabels(UnitCell cell, Godot.Collections.Array<AriaAbilityBase> abilities, ICombatant owner)
	{
		for (int i = 0; i < cell.AbilityLabels.Count && i < abilities.Count; i++)
		{
			AriaAbilityBase ability = abilities[i];
			string tag = ability is IPassiveAbility ? "[被动] " : "";
			float cd = _combatManager.GetCooldownRemaining(owner, ability);
			string cdText = cd <= 0f ? "就绪" : $"冷却 {cd:F1}s";
			cell.AbilityLabels[i].Text = $"{tag}{ability.DisplayName}  {cdText}";
		}
	}

	// 订阅战斗事件总线（观察者通道）
	private void SubscribeBus()
	{
		CombatEventBus.DamageDealt += OnDamageDealt;
		CombatEventBus.AbilityActivated += OnAbilityActivated;
		CombatEventBus.HealthBelowHalf += OnHealthBelowHalf;
		CombatEventBus.NearDeath += OnNearDeath;
	}

	// 断开总线订阅
	private void UnsubscribeBus()
	{
		CombatEventBus.DamageDealt -= OnDamageDealt;
		CombatEventBus.AbilityActivated -= OnAbilityActivated;
		CombatEventBus.HealthBelowHalf -= OnHealthBelowHalf;
		CombatEventBus.NearDeath -= OnNearDeath;
	}

	// 伤害结算事件处理
	private void OnDamageDealt(DamageInfo info)
	{
		string type = info.IsPiercing ? "穿透" : "普通";
		AppendLog($"[{type}伤害] {NameOf(info.Source)} → {NameOf(info.Target)}：{info.Amount:F0}（护甲吸收 {info.AbsorbedByArmor:F0}，扣血 {info.DamageToHealth:F0}）");
	}

	// 能力发动事件处理（含被动）
	private void OnAbilityActivated(ICombatant? combatant, AriaAbilityBase ability)
	{
		AppendLog($"[能力] {NameOf(combatant)} 发动 {ability.DisplayName}");
	}

	// 生命跌破半血事件处理
	private void OnHealthBelowHalf(ICombatant combatant)
	{
		AppendLog($"[半血] {NameOf(combatant)} 生命跌破一半");
	}

	// 濒临死亡事件处理
	private void OnNearDeath(ICombatant combatant)
	{
		AppendLog($"[濒死] {NameOf(combatant)} 濒临死亡");
	}

	// 在已注册的单位中查找名称
	private string NameOf(ICombatant? combatant)
	{
		if (combatant is null)
		{
			return "?";
		}

		if (ReferenceEquals(combatant, _friendlyHero))
		{
			return _friendlyHero.AttributeSet.HeroDisplayName;
		}

		if (ReferenceEquals(combatant, _enemyHero))
		{
			return _enemyHero.AttributeSet.HeroDisplayName;
		}

		foreach (CombatTestCard card in _friendlies)
		{
			if (ReferenceEquals(card, combatant))
			{
				return card.AttributeSet.DisplayName;
			}
		}

		foreach (CombatTestCard card in _enemies)
		{
			if (ReferenceEquals(card, combatant))
			{
				return card.AttributeSet.DisplayName;
			}
		}

		return "?";
	}

	// 追加日志行（超限移除最旧）
	private void AppendLog(string line)
	{
		_logLines.Add(line);
		if (_logLines.Count > MAX_LOG_LINES)
		{
			_logLines.RemoveAt(0);
		}

		_logDirty = true;
	}

	// 将待写日志刷入显示
	private void FlushLog()
	{
		if (!_logDirty)
		{
			return;
		}

		_logDirty = false;
		_log.Text = string.Join("\n", _logLines);
	}

	private void SetStatus(string text)
	{
		_statusLabel.Text = text;
	}
}