using System;
using System.Collections.Generic;
using System.Linq;
using Aria;
using Godot;
using Project_Star.Board;
using Project_Star.Board.Algo;
using Project_Star.Board.Data;
using Project_Star.Board.Manager;
using Project_Star.Combat.Events;
using Project_Star.Combat.Managers;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;
using Project_Star.Core.Interfaces;
using Project_Star.Core.Types;
using Project_Star.Entities.Abilities;
using Project_Star.Entities.Cards;
using Project_Star.Entities.Heroes;

namespace Project_Star.TestUI;

public partial class BattleTestUI : Control
{
	private const int CELL_WIDTH = 40;
	private const int CARD_HEIGHT = CELL_WIDTH * 2;

	private CombatManager _combatManager = null!;
	private BoardManager _boardManager = null!;
	private BoardManager _enemyBoardManager = null!;

	private DomeEnforcerHero _enemyHero = null!;
	private DomeEnforcerHero _playerHero = null!;

	private readonly List<CardBase> _playerCards = new();
	private readonly List<CardBase> _enemyCards = new();
	private readonly Dictionary<string, Func<CardBase>> _cardFactories = new();

	private HBoxContainer _enemyBoardRow = null!;
	private HBoxContainer _playerBoardRow = null!;
	private VBoxContainer _enemyHeroBox = null!;
	private VBoxContainer _playerHeroBox = null!;
	private HBoxContainer _cardListBox = null!;
	private RichTextLabel _log = null!;
	private Label _statusLabel = null!;

	// 英雄面板就地更新引用
	private ProgressBar _enemyHpBar = null!;
	private Label _enemyHpLabel = null!;
	private ProgressBar _playerHpBar = null!;
	private Label _playerHpLabel = null!;
	private readonly List<Label> _enemyAbilityLabels = new();
	private readonly List<Label> _playerAbilityLabels = new();
	private Label _enemyDotLabel = null!;
	private Label _playerDotLabel = null!;

	// 棋盘卡牌 → PanelContainer 引用
	private readonly Dictionary<CardBase, PanelContainer> _playerCardPanels = new();
	private readonly Dictionary<CardBase, PanelContainer> _enemyCardPanels = new();
	// 棋盘卡牌 → CD label + 能力 label 引用（创建时缓存，战斗中就地更新）
	private readonly Dictionary<CardBase, Label> _cardCdLabels = new();
	private readonly Dictionary<CardBase, List<Label>> _cardAbilityLabels = new();
	private bool _boardDirty = true;

	// 交互状态
	private CardBase? _selectedCard;
	private bool _isPlacingFromList;
	private GameBoard? _selectedBoard;

	private readonly List<string> _logLines = new();
	private const int MAX_LOG_LINES = 200;
	private bool _logDirty;

	public override void _Ready()
	{
		base._Ready();
		_combatManager = new CombatManager();
		AddChild(_combatManager);
		_boardManager = new BoardManager();
		AddChild(_boardManager);
		_enemyBoardManager = new BoardManager();
		AddChild(_enemyBoardManager);

		_cardFactories["电击手枪"] = () => new ShockPistolCard();
		_cardFactories["模板卡牌"] = () => new TemplateCard();

		BuildUI();
		CreateHeroes();
		PopulateCardList();
		BuildHeroPanels();
		SubscribeBoardEvents();
		SubscribeBus();

		RefreshBoards();
		AppendLog("就绪：点选底部卡牌 → 点棋盘空格放置；点已放卡牌 → 点空格移动");
	}

	public override void _ExitTree()
	{
		base._ExitTree();
		UnsubscribeBus();
		UnsubscribeBoardEvents();
	}

	public override void _Process(double _)
	{
		FlushLog();
		CheckOutcome();
		UpdateHeroLabels();
		UpdateBoardCardLabels();
		if (_boardDirty)
		{
			_boardDirty = false;
			RefreshBoards();
		}
	}

	// ────────────────── UI 骨架 ──────────────────

	private void BuildUI()
	{
		var root = new VBoxContainer();
		root.SetAnchorsPreset(LayoutPreset.FullRect);
		root.AddThemeConstantOverride("separation", 4);
		AddChild(root);

		var btnRow = new HBoxContainer();
		root.AddChild(btnRow);

		var startBtn = new Button { Text = "开始战斗" };
		startBtn.Pressed += OnStartBattle;
		btnRow.AddChild(startBtn);

		var endBtn = new Button { Text = "结束战斗" };
		endBtn.Pressed += OnEndBattle;
		btnRow.AddChild(endBtn);

		var clearBtn = new Button { Text = "清空战场" };
		clearBtn.Pressed += OnClearBattlefield;
		btnRow.AddChild(clearBtn);

		var cancelBtn = new Button { Text = "取消选择" };
		cancelBtn.Pressed += OnCancelSelection;
		btnRow.AddChild(cancelBtn);

		var enemyHeroLabel = new Label { Text = "── 敌方英雄 ──" };
		enemyHeroLabel.AddThemeFontSizeOverride("font_size", 14);
		enemyHeroLabel.AddThemeColorOverride("font_color", new Color(1f, 0.5f, 0.5f));
		root.AddChild(enemyHeroLabel);
		_enemyHeroBox = new VBoxContainer();
		root.AddChild(_enemyHeroBox);

		var enemyBfLabel = new Label { Text = "── 敌方战场 ──" };
		enemyBfLabel.AddThemeFontSizeOverride("font_size", 13);
		enemyBfLabel.AddThemeColorOverride("font_color", new Color(1f, 0.7f, 0.7f));
		root.AddChild(enemyBfLabel);
		_enemyBoardRow = new HBoxContainer();
		_enemyBoardRow.AddThemeConstantOverride("separation", 0);
		root.AddChild(_enemyBoardRow);

		root.AddChild(new HSeparator());

		var playerBfLabel = new Label { Text = "── 我方战场 ──" };
		playerBfLabel.AddThemeFontSizeOverride("font_size", 13);
		playerBfLabel.AddThemeColorOverride("font_color", new Color(0.7f, 1f, 0.7f));
		root.AddChild(playerBfLabel);
		_playerBoardRow = new HBoxContainer();
		_playerBoardRow.AddThemeConstantOverride("separation", 0);
		root.AddChild(_playerBoardRow);

		var playerHeroLabel = new Label { Text = "── 我方英雄 ──" };
		playerHeroLabel.AddThemeFontSizeOverride("font_size", 14);
		playerHeroLabel.AddThemeColorOverride("font_color", new Color(0.5f, 1f, 0.5f));
		root.AddChild(playerHeroLabel);
		_playerHeroBox = new VBoxContainer();
		root.AddChild(_playerHeroBox);

		var cardListLabel = new Label { Text = "── 卡牌列表（点选后点棋盘放置） ──" };
		cardListLabel.AddThemeFontSizeOverride("font_size", 13);
		root.AddChild(cardListLabel);

		var cardListScroll = new ScrollContainer
		{
			CustomMinimumSize = new Vector2(0, 140),
			SizeFlagsVertical = SizeFlags.ExpandFill,
		};
		root.AddChild(cardListScroll);

		var cardListBox = new HBoxContainer();
		cardListBox.AddThemeConstantOverride("separation", 8);
		cardListScroll.AddChild(cardListBox);
		_cardListBox = cardListBox;

		_log = new RichTextLabel
		{
			CustomMinimumSize = new Vector2(0, 100),
			SizeFlagsVertical = SizeFlags.ExpandFill,
			BbcodeEnabled = true,
			ScrollFollowing = true,
		};
		_log.AddThemeFontSizeOverride("normal_font_size", 12);
		root.AddChild(_log);

		_statusLabel = new Label();
		_statusLabel.AddThemeFontSizeOverride("font_size", 13);
		root.AddChild(_statusLabel);
	}

	// ────────────────── 英雄 ──────────────────

	private void CreateHeroes()
	{
		_enemyHero = new DomeEnforcerHero();
		_playerHero = new DomeEnforcerHero();
	}

	private void BuildHeroPanels()
	{
		BuildHeroPanel(_enemyHeroBox, _enemyHero, "敌方",
			out _enemyHpBar, out _enemyHpLabel,
			_enemyAbilityLabels, out _enemyDotLabel);
		BuildHeroPanel(_playerHeroBox, _playerHero, "我方",
			out _playerHpBar, out _playerHpLabel,
			_playerAbilityLabels, out _playerDotLabel);
	}

	private void BuildHeroPanel(VBoxContainer box, DomeEnforcerHero hero, string side,
		out ProgressBar hpBar, out Label hpLabel,
		List<Label> abilityLabels, out Label dotLabel)
	{
		abilityLabels.Clear();

		var panel = new PanelContainer { CustomMinimumSize = new Vector2(0, 80) };
		var style = new StyleBoxFlat
		{
			BgColor = side == "敌方"
				? new Color(0.3f, 0.18f, 0.18f, 1f)
				: new Color(0.18f, 0.3f, 0.18f, 1f),
			BorderColor = side == "敌方"
				? new Color(0.8f, 0.4f, 0.4f, 1f)
				: new Color(0.4f, 0.8f, 0.4f, 1f),
			BorderWidthLeft = 1, BorderWidthTop = 1,
			BorderWidthRight = 1, BorderWidthBottom = 1,
			CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
			ContentMarginLeft = 10, ContentMarginTop = 6,
			ContentMarginRight = 10, ContentMarginBottom = 6,
		};
		panel.AddThemeStyleboxOverride("panel", style);

		var hbox = new HBoxContainer();
		hbox.AddThemeConstantOverride("separation", 12);
		panel.AddChild(hbox);

		var left = new VBoxContainer();
		left.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		hbox.AddChild(left);

		var nameLabel = new Label { Text = hero.AttributeSet.HeroDisplayName };
		nameLabel.AddThemeFontSizeOverride("font_size", 18);
		left.AddChild(nameLabel);

		hpBar = new ProgressBar
		{
			MinValue = 0,
			MaxValue = hero.AttributeSet.MaxHealth.CurrentValue,
			Value = hero.AttributeSet.Health.CurrentValue,
			ShowPercentage = false,
			CustomMinimumSize = new Vector2(0, 16),
		};
		left.AddChild(hpBar);

		hpLabel = new Label
		{
			Text = $"HP {hero.AttributeSet.Health.CurrentValue:F0}/{hero.AttributeSet.MaxHealth.CurrentValue:F0}  护甲 {hero.AttributeSet.Armor.CurrentValue:F0}",
		};
		hpLabel.AddThemeFontSizeOverride("font_size", 12);
		left.AddChild(hpLabel);

		var right = new VBoxContainer();
		right.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		hbox.AddChild(right);

		foreach (AriaAbilityBase ability in hero.Abilities)
		{
			string tag = ability is IPassiveAbility ? "[被动] " : "";
			var abLabel = new Label { Text = $"{tag}{ability.DisplayName}" };
			abLabel.AddThemeFontSizeOverride("font_size", 12);
			right.AddChild(abLabel);
			abilityLabels.Add(abLabel);
		}

		dotLabel = new Label { Text = "" };
		dotLabel.AddThemeFontSizeOverride("font_size", 11);
		dotLabel.AddThemeColorOverride("font_color", new Color(1f, 0.75f, 0.5f));
		right.AddChild(dotLabel);

		box.AddChild(panel);
	}

	private void UpdateHeroLabels()
	{
		UpdateSingleHero(_enemyHero, _enemyHpBar, _enemyHpLabel, _enemyAbilityLabels, _enemyDotLabel);
		UpdateSingleHero(_playerHero, _playerHpBar, _playerHpLabel, _playerAbilityLabels, _playerDotLabel);
	}

	private void UpdateSingleHero(DomeEnforcerHero hero, ProgressBar hpBar, Label hpLabel,
		List<Label> abilityLabels, Label dotLabel)
	{
		float hp = hero.AttributeSet.Health.CurrentValue;
		float maxHp = hero.AttributeSet.MaxHealth.CurrentValue;
		float armor = hero.AttributeSet.Armor.CurrentValue;

		hpBar.Value = Mathf.Clamp(hp, 0f, maxHp);
		hpLabel.Text = $"HP {hp:F0}/{maxHp:F0}  护甲 {armor:F0}";

		for (int i = 0; i < abilityLabels.Count && i < hero.Abilities.Count; i++)
		{
			AriaAbilityBase ability = hero.Abilities[i];
			string tag = ability is IPassiveAbility ? "[被动] " : "";
			string cdText = "";
			if (_combatManager.IsInBattle)
			{
				float cd = _combatManager.GetCooldownRemaining(hero, ability);
				cdText = cd <= 0f ? "就绪" : $"冷却 {cd:F1}s";
			}
			abilityLabels[i].Text = $"{tag}{ability.DisplayName}  {cdText}";
		}

		float rad = hero.AttributeSet.Radiation.CurrentValue;
		float corr = hero.AttributeSet.Corrosion.CurrentValue;
		if (rad > 0.01f || corr > 0.01f)
		{
			var parts = new List<string>();
			if (rad > 0.01f) parts.Add($"辐射 {rad:F1}");
			if (corr > 0.01f) parts.Add($"腐蚀 {corr:F1}");
			dotLabel.Text = string.Join("  ", parts);
			dotLabel.Visible = true;
		}
		else
		{
			dotLabel.Visible = false;
		}
	}

	// ────────────────── 卡牌列表 ──────────────────

	private void PopulateCardList()
	{
		foreach ((string name, Func<CardBase> factory) in _cardFactories)
		{
			CardBase template = factory();
			_cardListBox.AddChild(CreateCardListItem(template, name));
		}
	}

	private PanelContainer CreateCardListItem(CardBase card, string displayName)
	{
		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(130, CARD_HEIGHT),
			MouseFilter = Control.MouseFilterEnum.Stop,
		};
		panel.GuiInput += (@event) =>
		{
			if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				return;
			_selectedCard = card;
			_isPlacingFromList = true;
			_selectedBoard = null;
			SetStatus($"已选中 {card.AttributeSet.DisplayName}，点击任意棋盘空格放置");
		};

		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.18f, 0.22f, 0.28f, 1f),
			BorderColor = new Color(0.5f, 0.55f, 0.65f, 1f),
			BorderWidthLeft = 1, BorderWidthTop = 1,
			BorderWidthRight = 1, BorderWidthBottom = 1,
			CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
			ContentMarginLeft = 6, ContentMarginTop = 4,
			ContentMarginRight = 6, ContentMarginBottom = 4,
		};
		panel.AddThemeStyleboxOverride("panel", style);

		var vbox = new VBoxContainer();
		panel.AddChild(vbox);

		var nameLabel = new Label { Text = displayName };
		nameLabel.AddThemeFontSizeOverride("font_size", 13);
		nameLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 1f));
		vbox.AddChild(nameLabel);

		var factionLabel = new Label
		{
			Text = $"归属: {card.AttributeSet.HeroKey}",
			Modulate = new Color(0.7f, 0.7f, 0.8f),
		};
		factionLabel.AddThemeFontSizeOverride("font_size", 10);
		vbox.AddChild(factionLabel);

		var cdLabel = new Label
		{
			Text = $"CD: {card.CooldownDuration:F1}s",
			Modulate = new Color(0.6f, 0.8f, 1f),
		};
		cdLabel.AddThemeFontSizeOverride("font_size", 10);
		vbox.AddChild(cdLabel);

		if (!card.TagSet.IsEmpty)
		{
			var tagParts = new List<string>();
			foreach (StringName tag in card.TagSet.All)
				tagParts.Add(Tags.GetDisplayName(tag));
			var tagLabel = new Label
			{
				Text = string.Join(" ", tagParts),
				Modulate = new Color(1f, 0.85f, 0.6f),
			};
			tagLabel.AddThemeFontSizeOverride("font_size", 10);
			vbox.AddChild(tagLabel);
		}

		foreach (AriaAbilityBase ability in card.Abilities)
		{
			string tag = ability is IPassiveAbility ? "[被] " : "";
			var abLabel = new Label { Text = $"• {tag}{ability.DisplayName}" };
			abLabel.AddThemeFontSizeOverride("font_size", 10);
			abLabel.AddThemeColorOverride("font_color", new Color(0.8f, 0.9f, 0.8f));
			vbox.AddChild(abLabel);
		}

		return panel;
	}

	// ────────────────── 棋盘事件订阅 ──────────────────

	private void SubscribeBoardEvents()
	{
		_boardManager.Battlefield.LayoutChangedEvent += OnPlayerLayoutChanged;
		_enemyBoardManager.Battlefield.LayoutChangedEvent += OnEnemyLayoutChanged;
	}

	private void UnsubscribeBoardEvents()
	{
		_boardManager.Battlefield.LayoutChangedEvent -= OnPlayerLayoutChanged;
		_enemyBoardManager.Battlefield.LayoutChangedEvent -= OnEnemyLayoutChanged;
	}

	private void OnPlayerLayoutChanged() => _boardDirty = true;
	private void OnEnemyLayoutChanged() => _boardDirty = true;

	// ────────────────── 棋盘网格渲染 ──────────────────

	private void RefreshBoards()
	{
		_playerCardPanels.Clear();
		_enemyCardPanels.Clear();
		_cardCdLabels.Clear();
		_cardAbilityLabels.Clear();
		RefreshBoardRow(_playerBoardRow, _boardManager.Battlefield, _playerCardPanels);
		RefreshBoardRow(_enemyBoardRow, _enemyBoardManager.Battlefield, _enemyCardPanels);
	}

	private void RefreshBoardRow(HBoxContainer row, GameBoard board,
		Dictionary<CardBase, PanelContainer> panelMap)
	{
		foreach (Node child in row.GetChildren())
		{
			row.RemoveChild(child);
			child.QueueFree();
		}

		List<(CardBase Card, int StartCell)> layout = board.GetLayout();
		layout.Sort((a, b) => a.StartCell.CompareTo(b.StartCell));

		int currentCell = 0;
		foreach ((CardBase card, int startCell) in layout)
		{
			while (currentCell < startCell)
			{
				row.AddChild(CreateEmptyCell(board, currentCell));
				currentCell++;
			}

			int span = card.AttributeSet.Size.GetCells();
			PanelContainer cardPanel = CreateBoardCard(card, board, span);
			row.AddChild(cardPanel);
			panelMap[card] = cardPanel;
			currentCell += span;
		}

		while (currentCell < BoardManager.BOARD_CAPACITY)
		{
			row.AddChild(CreateEmptyCell(board, currentCell));
			currentCell++;
		}
	}

	private Control CreateEmptyCell(GameBoard board, int cellIndex)
	{
		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(CELL_WIDTH, CARD_HEIGHT),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
		};
		var style = new StyleBoxFlat
		{
			BgColor = new Color(0.12f, 0.12f, 0.15f, 0.6f),
			BorderColor = new Color(0.3f, 0.3f, 0.35f, 0.5f),
			BorderWidthLeft = 1, BorderWidthTop = 1,
			BorderWidthRight = 1, BorderWidthBottom = 1,
		};
		panel.AddThemeStyleboxOverride("panel", style);

		int capturedCell = cellIndex;
		panel.GuiInput += (InputEvent @event) =>
		{
			if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				return;
			OnCellClicked(board, capturedCell);
		};

		return panel;
	}

	private PanelContainer CreateBoardCard(CardBase card, GameBoard board, int span)
	{
		int width = span * CELL_WIDTH;
		var panel = new PanelContainer
		{
			CustomMinimumSize = new Vector2(width, CARD_HEIGHT),
			SizeFlagsHorizontal = SizeFlags.ShrinkCenter,
			MouseFilter = Control.MouseFilterEnum.Stop,
		};

		bool isSelected = ReferenceEquals(_selectedCard, card);
		var style = new StyleBoxFlat
		{
			BgColor = isSelected
				? new Color(0.25f, 0.32f, 0.45f, 1f)
				: new Color(0.2f, 0.25f, 0.32f, 1f),
			BorderColor = isSelected
				? new Color(0.8f, 0.8f, 1f, 1f)
				: new Color(0.5f, 0.6f, 0.7f, 1f),
			BorderWidthLeft = isSelected ? 2 : 1,
			BorderWidthTop = isSelected ? 2 : 1,
			BorderWidthRight = isSelected ? 2 : 1,
			BorderWidthBottom = isSelected ? 2 : 1,
			CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4,
			CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4,
			ContentMarginLeft = 4, ContentMarginTop = 3,
			ContentMarginRight = 4, ContentMarginBottom = 3,
		};
		panel.AddThemeStyleboxOverride("panel", style);

		var vbox = new VBoxContainer();
		panel.AddChild(vbox);

		var nameLabel = new Label { Text = card.AttributeSet.DisplayName };
		nameLabel.AddThemeFontSizeOverride("font_size", 11);
		nameLabel.AddThemeColorOverride("font_color", new Color(0.9f, 0.9f, 1f));
		vbox.AddChild(nameLabel);

		var factionLabel = new Label
		{
			Text = $"归属: {card.AttributeSet.HeroKey}",
			Modulate = new Color(0.6f, 0.65f, 0.75f),
		};
		factionLabel.AddThemeFontSizeOverride("font_size", 9);
		vbox.AddChild(factionLabel);

		float cd = card.AttributeSet.Cooldown.CurrentValue;
		string cdText = _combatManager.IsInBattle
			? (cd <= 0f ? "就绪" : $"{cd:F1}s")
			: $"{cd:F1}s";
		var cdLabel = new Label
		{
			Text = $"CD: {cdText}",
			Modulate = new Color(0.5f, 0.7f, 1f),
			Name = "CdLabel",
		};
		cdLabel.AddThemeFontSizeOverride("font_size", 9);
		vbox.AddChild(cdLabel);
		_cardCdLabels[card] = cdLabel;

		if (span >= 2 && !card.TagSet.IsEmpty)
		{
			var tagParts = new List<string>();
			foreach (StringName tag in card.TagSet.All)
				tagParts.Add(Tags.GetDisplayName(tag));
			var tagLabel = new Label
			{
				Text = string.Join(" ", tagParts),
				Modulate = new Color(1f, 0.85f, 0.6f),
			};
			tagLabel.AddThemeFontSizeOverride("font_size", 9);
			vbox.AddChild(tagLabel);
		}

		if (span >= 2)
		{
			var abLabels = new List<Label>();
			foreach (AriaAbilityBase ability in card.Abilities)
			{
				string passiveTag = ability is IPassiveAbility ? "[被] " : "";
				string abilityCdText = "";
				if (_combatManager.IsInBattle)
				{
					float abilityCd = _combatManager.GetCooldownRemaining(card, ability);
					abilityCdText = abilityCd <= 0f ? "" : $" {abilityCd:F0}s";
				}
				var abLabel = new Label
				{
					Text = $"• {passiveTag}{ability.DisplayName}{abilityCdText}",
					Name = $"Ab_{ability.Key}",
				};
				abLabel.AddThemeFontSizeOverride("font_size", 9);
				abLabel.AddThemeColorOverride("font_color", new Color(0.75f, 0.85f, 0.75f));
				vbox.AddChild(abLabel);
				abLabels.Add(abLabel);
			}
			_cardAbilityLabels[card] = abLabels;
		}

		panel.GuiInput += (InputEvent @event) =>
		{
			if (@event is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left })
				return;
			OnBoardCardClicked(card, board);
		};

		return panel;
	}

	// 战斗中就地更新 CD label，不重建节点
	private void UpdateBoardCardLabels()
	{
		if (!_combatManager.IsInBattle) return;
		UpdateCardPanels(_playerCardPanels, _boardManager.Battlefield);
		UpdateCardPanels(_enemyCardPanels, _enemyBoardManager.Battlefield);
	}

	private void UpdateCardPanels(Dictionary<CardBase, PanelContainer> panelMap, GameBoard board)
	{
		foreach ((CardBase card, PanelContainer panel) in panelMap)
		{
			// 就地更新 CD label
			if (_cardCdLabels.TryGetValue(card, out Label? cdLabel))
			{
				float cd = card.AttributeSet.Cooldown.CurrentValue;
				cdLabel.Text = $"CD: {(cd <= 0f ? "就绪" : $"{cd:F1}s")}";
			}

			// 就地更新能力 label
			if (_cardAbilityLabels.TryGetValue(card, out List<Label>? abLabels))
			{
				for (int i = 0; i < abLabels.Count && i < card.Abilities.Count; i++)
				{
					AriaAbilityBase ability = card.Abilities[i];
					float abilityCd = _combatManager.GetCooldownRemaining(card, ability);
					string passiveTag = ability is IPassiveAbility ? "[被] " : "";
					string abilityCdText = abilityCd <= 0f ? "" : $" {abilityCd:F0}s";
					abLabels[i].Text = $"• {passiveTag}{ability.DisplayName}{abilityCdText}";
				}
			}
		}
	}

	// ────────────────── 交互逻辑 ──────────────────

	private void OnCellClicked(GameBoard board, int cellIndex)
	{
		BoardEntry? entry = board.GetEntryAt(cellIndex);
		if (entry is not null)
		{
			_selectedCard = entry.Card;
			_isPlacingFromList = false;
			_selectedBoard = board;
			SetStatus($"已选中 {entry.Card.AttributeSet.DisplayName}，点击空格移动");
			_boardDirty = true;
			return;
		}

		if (_selectedCard is null)
		{
			SetStatus("请先点选卡牌列表中的卡牌，或点选棋盘上已放置的卡牌");
			return;
		}

		// 根据目标 board 判断用哪个 BoardManager 和卡牌列表
		BoardManager targetManager = GetBoardManager(board);
		List<CardBase> targetList = GetCardList(board);

		if (_isPlacingFromList)
		{
			// 检查卡牌是否已在某个棋盘上
			BoardManager? currentManager = GetCurrentCardManager(_selectedCard);
			if (currentManager is not null && currentManager != targetManager)
			{
				// 跨棋盘放置：从来源棋盘移除
				GameBoard? srcBoard = currentManager.GetBoardOf(_selectedCard);
				srcBoard?.RemoveCard(_selectedCard);
				// 从来源列表移除，加入目标列表
				List<CardBase> srcList = GetCardList(srcBoard!);
				srcList.Remove(_selectedCard);
			}
			else if (currentManager == targetManager)
			{
				// 同棋盘移动，用移动逻辑
				GoMoveCard(_selectedCard, board, cellIndex);
				return;
			}

			PushPlan plan = targetManager.PreviewMove(_selectedCard, board, cellIndex);
			if (plan.IsFeasible && targetManager.CommitMove(_selectedCard, board, cellIndex))
			{
				if (!targetList.Contains(_selectedCard))
					targetList.Add(_selectedCard);
				AppendLog($"放置 {_selectedCard.AttributeSet.DisplayName} → {BoardName(board)} 格{cellIndex} | {DescribePlan(plan)}");
				_selectedCard = null;
				_isPlacingFromList = false;
				_selectedBoard = null;
			}
			else
			{
				SetStatus($"放置失败 | {DescribePlan(plan)}");
			}
		}
		else if (_selectedBoard is not null)
		{
			// 移动已放置的卡牌（可以在同棋盘内移动）
			BoardManager srcManager = GetBoardManager(_selectedBoard);
			PushPlan plan = srcManager.PreviewMove(_selectedCard, board, cellIndex);
			if (plan.IsFeasible && srcManager.CommitMove(_selectedCard, board, cellIndex))
			{
				AppendLog($"移动 {_selectedCard.AttributeSet.DisplayName} → {BoardName(board)} 格{cellIndex} | {DescribePlan(plan)}");
				_selectedCard = null;
				_selectedBoard = null;
			}
			else
			{
				SetStatus($"移动失败 | {DescribePlan(plan)}");
			}
		}
	}

	private void GoMoveCard(CardBase card, GameBoard board, int cellIndex)
	{
		BoardManager mgr = GetBoardManager(board);
		PushPlan plan = mgr.PreviewMove(card, board, cellIndex);
		if (plan.IsFeasible && mgr.CommitMove(card, board, cellIndex))
		{
			AppendLog($"移动 {card.AttributeSet.DisplayName} → {BoardName(board)} 格{cellIndex} | {DescribePlan(plan)}");
			_selectedCard = null;
			_isPlacingFromList = false;
			_selectedBoard = null;
		}
		else
		{
			SetStatus($"移动失败 | {DescribePlan(plan)}");
		}
	}

	private void OnBoardCardClicked(CardBase card, GameBoard board)
	{
		_selectedCard = card;
		_isPlacingFromList = false;
		_selectedBoard = board;
		SetStatus($"已选中 {card.AttributeSet.DisplayName}，点击空格移动");
		_boardDirty = true;
	}

	private void OnCancelSelection()
	{
		_selectedCard = null;
		_isPlacingFromList = false;
		_selectedBoard = null;
		SetStatus("已取消选择");
		_boardDirty = true;
	}

	private BoardManager GetBoardManager(GameBoard board)
	{
		if (_boardManager.Battlefield == board || _boardManager.Bench == board)
			return _boardManager;
		return _enemyBoardManager;
	}

	private BoardManager? GetCurrentCardManager(CardBase card)
	{
		if (_boardManager.GetBoardOf(card) is not null) return _boardManager;
		if (_enemyBoardManager.GetBoardOf(card) is not null) return _enemyBoardManager;
		return null;
	}

	private List<CardBase> GetCardList(GameBoard board)
	{
		if (board == _boardManager.Battlefield || board == _boardManager.Bench)
			return _playerCards;
		return _enemyCards;
	}

	private static string BoardName(GameBoard board)
	{
		return board == null ? "?" : "棋盘";
	}

	// ────────────────── 战斗控制 ──────────────────

	private void OnStartBattle()
	{
		if (_playerHero is null || _enemyHero is null)
		{
			SetStatus("英雄为空，无法开战");
			return;
		}

		_combatManager.EndBattle();
		RebuildHeroHP();

		var playerCombatants = new List<ICombatant>();
		foreach (CardBase c in _playerCards)
		{
			if (_boardManager.Battlefield.ContainsCard(c))
				playerCombatants.Add(c);
		}

		var enemyCombatants = new List<ICombatant>();
		foreach (CardBase c in _enemyCards)
		{
			if (_enemyBoardManager.Battlefield.ContainsCard(c))
				enemyCombatants.Add(c);
		}

		_combatManager.StartBattle(_playerHero, playerCombatants, _enemyHero, enemyCombatants);
		AppendLog($"战斗开始：我方 {playerCombatants.Count} 张 / 敌方 {enemyCombatants.Count} 张");
	}

	private void RebuildHeroHP()
	{
		_playerHero.AttributeSet.Health.SetCurrentValue(_playerHero.AttributeSet.MaxHealth.CurrentValue);
		_enemyHero.AttributeSet.Health.SetCurrentValue(_enemyHero.AttributeSet.MaxHealth.CurrentValue);
	}

	private void OnEndBattle()
	{
		if (_combatManager.IsInBattle)
			AppendLog("战斗结束（手动）");
		_combatManager.EndBattle();
	}

	private void OnClearBattlefield()
	{
		_combatManager.EndBattle();
		_enemyCards.Clear();
		_playerCards.Clear();
		_selectedCard = null;
		_isPlacingFromList = false;
		_selectedBoard = null;

		foreach (CardBase card in _boardManager.Battlefield.GetCards().ToList())
			_boardManager.Battlefield.RemoveCard(card);
		foreach (CardBase card in _boardManager.Bench.GetCards().ToList())
			_boardManager.Bench.RemoveCard(card);
		foreach (CardBase card in _enemyBoardManager.Battlefield.GetCards().ToList())
			_enemyBoardManager.Battlefield.RemoveCard(card);

		RebuildHeroHP();
		_boardDirty = true;
		AppendLog("战场已清空");
	}

	private void CheckOutcome()
	{
		if (!_combatManager.IsInBattle) return;

		if (_playerHero.AttributeSet.Health.CurrentValue <= 0f)
		{
			_combatManager.EndBattle();
			AppendLog("【失败】我方英雄阵亡");
		}
		else if (_enemyHero.AttributeSet.Health.CurrentValue <= 0f)
		{
			_combatManager.EndBattle();
			AppendLog("【胜利】敌方英雄阵亡");
		}
	}

	// ────────────────── 日志 ──────────────────

	private void AppendLog(string line)
	{
		_logLines.Add(line);
		if (_logLines.Count > MAX_LOG_LINES) _logLines.RemoveAt(0);
		_logDirty = true;
	}

	private void FlushLog()
	{
		if (!_logDirty) return;
		_logDirty = false;
		_log.Text = string.Join("\n", _logLines);
	}

	private void SetStatus(string text)
	{
		_statusLabel.Text = text;
	}

	// ────────────────── 事件总线 ──────────────────

	private void SubscribeBus()
	{
		CombatEventBus.DamageDealt += OnDamageDealt;
		CombatEventBus.AbilityActivated += OnAbilityActivated;
		CombatEventBus.HealthBelowHalf += OnHealthBelowHalf;
		CombatEventBus.NearDeath += OnNearDeath;
	}

	private void UnsubscribeBus()
	{
		CombatEventBus.DamageDealt -= OnDamageDealt;
		CombatEventBus.AbilityActivated -= OnAbilityActivated;
		CombatEventBus.HealthBelowHalf -= OnHealthBelowHalf;
		CombatEventBus.NearDeath -= OnNearDeath;
	}

	private void OnDamageDealt(DamageInfo info)
	{
		string type = info.IsPiercing ? "穿透" : "普通";
		AppendLog($"[{type}伤害] {NameOf(info.Source)} → {NameOf(info.Target)}：{info.Amount:F0}（护甲吸收 {info.AbsorbedByArmor:F0}，扣血 {info.DamageToHealth:F0}）");
	}

	private void OnAbilityActivated(ICombatant? combatant, AriaAbilityBase ability)
	{
		AppendLog($"[能力] {NameOf(combatant)} 发动 {ability.DisplayName}");
	}

	private void OnHealthBelowHalf(ICombatant combatant)
	{
		AppendLog($"[半血] {NameOf(combatant)} 生命跌破一半");
	}

	private void OnNearDeath(ICombatant combatant)
	{
		AppendLog($"[濒死] {NameOf(combatant)} 濒临死亡");
	}

	private string NameOf(ICombatant? combatant)
	{
		if (combatant is null) return "?";
		if (ReferenceEquals(combatant, _enemyHero)) return _enemyHero.AttributeSet.HeroDisplayName;
		if (ReferenceEquals(combatant, _playerHero)) return _playerHero.AttributeSet.HeroDisplayName;
		foreach (CardBase c in _enemyCards)
			if (ReferenceEquals(c, combatant)) return c.AttributeSet.DisplayName;
		foreach (CardBase c in _playerCards)
			if (ReferenceEquals(c, combatant)) return c.AttributeSet.DisplayName;
		return "?";
	}

	private static string DescribePlan(PushPlan plan)
	{
		if (!plan.IsFeasible) return "不可行";
		return $"方向={plan.Direction} 距离={plan.PushDistance} 受影响={plan.AffectedCards.Count} 目标格={plan.TargetCell}";
	}
}
