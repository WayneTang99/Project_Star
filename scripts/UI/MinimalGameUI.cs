using System.Text;
using Aria;
using Godot;
using Project_Star.Combat.Events;
using Project_Star.Combat.Managers;
using Project_Star.Core.AttributeSets;
using Project_Star.Core.Bases;
using Project_Star.Core.Interfaces;
using Project_Star.Board.Data;
using Project_Star.Board.Manager;
using Project_Star.Core.Types;
using Project_Star.Entities.Events;
using Project_Star.Match;
using Project_Star.Systems;

namespace Project_Star.UI;

// 极简可玩 UI：纯代码构造（不依赖 .tscn / 美术资源），覆盖 选英雄 → 事件 → 商店 → 战斗 → 结算 的完整对局闭环，
// 供人类玩家点击游玩一局完整对局。只读管理器状态并订阅事件，不直接修改底层逻辑。
public partial class MinimalGameUI : CanvasLayer
{
	// 主状态机管理器
	private GameManager _gameManager = null!;
	// 英雄管理器
	private HeroManager _heroManager = null!;
	// 卡牌管理器
	private CardManager _cardManager = null!;
	// 棋盘管理器
	private BoardManager _boardManager = null!;
	// 轮次管理器
	private RoundTurnManager _roundTurnManager = null!;
	// 事件管理器
	private EventManager _eventManager = null!;
	// 战斗管理器
	private CombatManager _combatManager = null!;
	// 对局流程协调器
	private MatchFlowCoordinator _matchFlow = null!;

	// 选英雄面板
	private VBoxContainer _heroSelectPanel = null!;
	// 顶部 HUD
	private HBoxContainer _hud = null!;
	// HUD 轮次/回合标签
	private Label _hudTurnLabel = null!;
	// HUD 财富标签
	private Label _hudWealthLabel = null!;
	// HUD 经验标签
	private Label _hudExpLabel = null!;
	// HUD 声望标签
	private Label _hudRepLabel = null!;
	// HUD 等级标签
	private Label _hudLevelLabel = null!;
	// 事件面板（容器）
	private VBoxContainer _eventPanel = null!;
	// 商店面板
	private VBoxContainer _shopPanel = null!;
	// 商店卡牌列表容器
	private VBoxContainer _shopList = null!;
	// 手牌与棋盘面板
	private VBoxContainer _boardPanel = null!;
	// 手牌列表容器（持有未上盘卡牌）
	private VBoxContainer _handBox = null!;
	// 战场区列表容器
	private VBoxContainer _battlefieldBox = null!;
	// 备战区列表容器
	private VBoxContainer _benchBox = null!;
	// 当前打开的商店事件（离开商店时结算用）
	private ShopEvent? _currentShopEvent;
	// 战斗面板
	private VBoxContainer _combatPanel = null!;
	// 我方英雄 HP 标签
	private Label _friendlyHpLabel = null!;
	// 敌方英雄 HP 标签
	private Label _enemyHpLabel = null!;
	// 我方卡牌冷却标签
	private Label _friendlyCdLabel = null!;
	// 敌方卡牌冷却标签
	private Label _enemyCdLabel = null!;
	// 结算面板
	private VBoxContainer _resultPanel = null!;
	// 结算结果标签
	private Label _resultLabel = null!;
	// 日志富文本
	private RichTextLabel _logLabel = null!;
	// 上一帧是否处于战斗（用于战斗结束后恢复事件面板）
	private bool _battleWasRunning;

	public override void _Ready()
	{
		base._Ready();

		// 兄弟节点引用（与各管理器相同的 GetNode("../Xxx") 模式）；引用缺失时防御性置空
		_gameManager = GetNodeOrNull<GameManager>("../GameManager")!;
		_heroManager = GetNodeOrNull<HeroManager>("../HeroManager")!;
		_cardManager = GetNodeOrNull<CardManager>("../CardManager")!;
		_roundTurnManager = GetNodeOrNull<RoundTurnManager>("../RoundTurnManager")!;
		_eventManager = GetNodeOrNull<EventManager>("../EventManager")!;
		_boardManager = GetNodeOrNull<BoardManager>("../BoardManager")!;
		_combatManager = GetNodeOrNull<CombatManager>("../CombatManager")!;
		_matchFlow = GetNodeOrNull<MatchFlowCoordinator>("../MatchFlow")!;

		BuildLayout();

		// 订阅事件：实例事件与静态 CombatEventBus 均在此登记
		if (_gameManager is not null)
		{
			_gameManager.StateChangedEvent += OnStateChanged;
		}

		if (_heroManager is not null)
		{
			_heroManager.HeroSelectedEvent += OnHeroSelected;
		}

		if (_eventManager is not null)
		{
			_eventManager.EventsGeneratedEvent += OnEventsGenerated;
		}

		if (_roundTurnManager is not null)
		{
			_roundTurnManager.TurnStartedEvent += OnTurnStarted;
		}

		if (_matchFlow is not null)
		{
			_matchFlow.FlowLog += OnFlowLog;
		}

		if (_cardManager is not null)
		{
			_cardManager.CardAddedEvent += OnCardAdded;
		}

		if (_boardManager is not null)
		{
			_boardManager.Battlefield.LayoutChangedEvent += RefreshBoardPanel;
			_boardManager.Bench.LayoutChangedEvent += RefreshBoardPanel;
		}

		CombatEventBus.DamageDealt += OnDamageDealt;
		CombatEventBus.AbilityActivated += OnAbilityActivated;
		CombatEventBus.EffectApplied += OnEffectApplied;

		// 初始按当前状态驱动一次面板可见性
		if (_gameManager is not null)
		{
			ApplyStateVisibility(_gameManager.CurrentState);
		}
	}

	public override void _ExitTree()
	{
		base._ExitTree();

		// 注销实例事件
		if (_gameManager is not null)
		{
			_gameManager.StateChangedEvent -= OnStateChanged;
		}

		if (_heroManager is not null)
		{
			_heroManager.HeroSelectedEvent -= OnHeroSelected;
		}

		if (_eventManager is not null)
		{
			_eventManager.EventsGeneratedEvent -= OnEventsGenerated;
		}

		if (_roundTurnManager is not null)
		{
			_roundTurnManager.TurnStartedEvent -= OnTurnStarted;
		}

		if (_matchFlow is not null)
		{
			_matchFlow.FlowLog -= OnFlowLog;
		}

		if (_cardManager is not null)
		{
			_cardManager.CardAddedEvent -= OnCardAdded;
		}

		if (_boardManager is not null)
		{
			_boardManager.Battlefield.LayoutChangedEvent -= RefreshBoardPanel;
			_boardManager.Bench.LayoutChangedEvent -= RefreshBoardPanel;
		}

		// 注销静态总线（关键：避免跨对局/测试泄漏）
		CombatEventBus.DamageDealt -= OnDamageDealt;
		CombatEventBus.AbilityActivated -= OnAbilityActivated;
		CombatEventBus.EffectApplied -= OnEffectApplied;
	}

	// 每帧轮询：战斗时刷新 HP 与冷却；战斗结束恢复事件面板
	public override void _Process(double delta)
	{
		base._Process(delta);

		if (_combatManager is null)
		{
			return;
		}

		if (_combatManager.IsInBattle)
		{
			_combatPanel.Visible = true;
			_eventPanel.Visible = false;
			_shopPanel.Visible = false;
			_boardPanel.Visible = false;
		}
		else if (_battleWasRunning)
		{
			// 战斗刚结束：恢复事件面板（若仍处于局内）
			_combatPanel.Visible = false;
			if (_gameManager.CurrentState == GameState.InMatch)
			{
				_eventPanel.Visible = true;
				_boardPanel.Visible = true;
			}
		}

		_battleWasRunning = _combatManager.IsInBattle;
		UpdateCombatPanel();
	}

	// 构建全部界面（全屏 Control + 各面板）
	private void BuildLayout()
	{
		var root = new Control { Name = "Root" };
		root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		AddChild(root);

		var margin = new MarginContainer();
		margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		margin.AddThemeConstantOverride("margin_left", 16);
		margin.AddThemeConstantOverride("margin_top", 16);
		margin.AddThemeConstantOverride("margin_right", 16);
		margin.AddThemeConstantOverride("margin_bottom", 16);
		root.AddChild(margin);

		var mainBox = new VBoxContainer();
		mainBox.AddThemeConstantOverride("separation", 12);
		margin.AddChild(mainBox);

		BuildHud(mainBox);
		BuildHeroSelectPanel(mainBox);
		BuildEventPanel(mainBox);
		BuildShopPanel(mainBox);
		BuildBoardPanel(mainBox);
		BuildCombatPanel(mainBox);
		BuildResultPanel(mainBox);
		BuildLogPanel(mainBox);
	}

	// 构建顶部 HUD
	private void BuildHud(Container parent)
	{
		_hud = new HBoxContainer { Visible = false };
		_hud.AddThemeConstantOverride("separation", 20);
		_hudTurnLabel = new Label();
		_hudWealthLabel = new Label();
		_hudExpLabel = new Label();
		_hudRepLabel = new Label();
		_hudLevelLabel = new Label();
		_hud.AddChild(_hudTurnLabel);
		_hud.AddChild(_hudWealthLabel);
		_hud.AddChild(_hudExpLabel);
		_hud.AddChild(_hudRepLabel);
		_hud.AddChild(_hudLevelLabel);
		parent.AddChild(_hud);
	}

	// 构建选英雄面板
	private void BuildHeroSelectPanel(Container parent)
	{
		_heroSelectPanel = new VBoxContainer { Visible = false };
		_heroSelectPanel.AddThemeConstantOverride("separation", 8);

		var title = new Label { Text = "选择英雄" };
		title.AddThemeFontSizeOverride("font_size", 28);
		_heroSelectPanel.AddChild(title);

		if (_heroManager is not null)
		{
			foreach (HeroBase hero in _heroManager.AvailableHeroes)
			{
				var button = new Button
				{
					Text = $"{hero.AttributeSet.HeroDisplayName}（{hero.AttributeSet.HeroKey}）",
					CustomMinimumSize = new Vector2(260, 0),
				};
				HeroBase captured = hero;
				button.Pressed += () => _heroManager.SelectHero(captured);
				_heroSelectPanel.AddChild(button);
			}
		}

		parent.AddChild(_heroSelectPanel);
	}

	// 构建事件面板
	private void BuildEventPanel(Container parent)
	{
		_eventPanel = new VBoxContainer { Visible = false };
		_eventPanel.AddThemeConstantOverride("separation", 8);

		var title = new Label { Text = "当前事件" };
		title.AddThemeFontSizeOverride("font_size", 24);
		_eventPanel.AddChild(title);

		parent.AddChild(_eventPanel);
	}

	// 构建商店面板
	private void BuildShopPanel(Container parent)
	{
		_shopPanel = new VBoxContainer { Visible = false };
		_shopPanel.AddThemeConstantOverride("separation", 8);

		var title = new Label { Text = "商店" };
		title.AddThemeFontSizeOverride("font_size", 24);
		_shopPanel.AddChild(title);

		_shopList = new VBoxContainer();
		_shopList.AddThemeConstantOverride("separation", 4);
		_shopPanel.AddChild(_shopList);

		var leaveButton = new Button { Text = "离开商店" };
		leaveButton.Pressed += OnLeaveShopPressed;
		_shopPanel.AddChild(leaveButton);

		parent.AddChild(_shopPanel);
	}

	// 构建手牌与棋盘面板
	private void BuildBoardPanel(Container parent)
	{
		_boardPanel = new VBoxContainer { Visible = false };
		_boardPanel.AddThemeConstantOverride("separation", 8);

		var title = new Label { Text = "我的手牌与棋盘" };
		title.AddThemeFontSizeOverride("font_size", 24);
		_boardPanel.AddChild(title);

		_handBox = new VBoxContainer();
		_handBox.AddThemeConstantOverride("separation", 4);
		_boardPanel.AddChild(_handBox);

		_battlefieldBox = new VBoxContainer();
		_battlefieldBox.AddThemeConstantOverride("separation", 4);
		_boardPanel.AddChild(_battlefieldBox);

		_benchBox = new VBoxContainer();
		_benchBox.AddThemeConstantOverride("separation", 4);
		_boardPanel.AddChild(_benchBox);

		parent.AddChild(_boardPanel);
	}

	// 构建战斗面板
	private void BuildCombatPanel(Container parent)
	{
		_combatPanel = new VBoxContainer { Visible = false };
		_combatPanel.AddThemeConstantOverride("separation", 8);

		var title = new Label { Text = "战斗中" };
		title.AddThemeFontSizeOverride("font_size", 24);
		_combatPanel.AddChild(title);

		_friendlyHpLabel = new Label();
		_enemyHpLabel = new Label();
		_friendlyCdLabel = new Label();
		_enemyCdLabel = new Label();
		_combatPanel.AddChild(_friendlyHpLabel);
		_combatPanel.AddChild(_enemyHpLabel);
		_combatPanel.AddChild(_friendlyCdLabel);
		_combatPanel.AddChild(_enemyCdLabel);

		parent.AddChild(_combatPanel);
	}

	// 构建结算面板
	private void BuildResultPanel(Container parent)
	{
		_resultPanel = new VBoxContainer { Visible = false };
		_resultPanel.AddThemeConstantOverride("separation", 12);

		_resultLabel = new Label();
		_resultLabel.AddThemeFontSizeOverride("font_size", 28);
		_resultPanel.AddChild(_resultLabel);

		if (_gameManager is not null)
		{
			var button = new Button { Text = "返回主菜单" };
			button.Pressed += () => _gameManager.ReturnToMainMenu();
			_resultPanel.AddChild(button);
		}

		parent.AddChild(_resultPanel);
	}

	// 构建底部日志面板
	private void BuildLogPanel(Container parent)
	{
		var logBox = new VBoxContainer();
		logBox.AddThemeConstantOverride("separation", 4);
		var logTitle = new Label { Text = "对局日志" };
		logBox.AddChild(logTitle);

		var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 140), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
		var logMargin = new MarginContainer();
		logMargin.AddThemeConstantOverride("margin_right", 12);
		_logLabel = new RichTextLabel { FitContent = true, ScrollFollowing = true };
		scroll.AddChild(logMargin);
		logMargin.AddChild(_logLabel);
		logBox.AddChild(scroll);
		parent.AddChild(logBox);
	}

	// 状态切换：驱动各面板可见性与刷新
	private void OnStateChanged(GameState newState)
	{
		ApplyStateVisibility(newState);
		AppendLog($"状态切换：{newState}");

		if (newState == GameState.Result)
		{
			string reason = _gameManager.LastMatchEndReason?.ToString() ?? "Unknown";
			bool won = _gameManager.LastMatchEndReason == MatchEndReason.Victory;
			_resultLabel.Text = $"对局结束: {reason}";
			_resultLabel.AddThemeColorOverride("font_color", won ? new Color(0.2f, 0.9f, 0.3f) : new Color(0.9f, 0.3f, 0.3f));
		}
		else if (newState == GameState.HeroSelect)
		{
			RefreshHeroButtons();
		}
		else if (newState == GameState.InMatch)
		{
			RefreshHud();
			RefreshBoardPanel();
			AppendLog($"对局开始");
		}
	}

	// 按状态驱动面板可见性
	private void ApplyStateVisibility(GameState state)
	{
		bool inBattle = _combatManager is not null && _combatManager.IsInBattle;

		_heroSelectPanel.Visible = state is GameState.MainMenu or GameState.HeroSelect;
		_hud.Visible = state == GameState.InMatch;
		_eventPanel.Visible = state == GameState.InMatch && !inBattle;
		_boardPanel.Visible = state == GameState.InMatch && !inBattle;
		_combatPanel.Visible = inBattle;
		_shopPanel.Visible = false;
		_resultPanel.Visible = state == GameState.Result;
	}

	// 英雄选中：进入局内并记录日志
	private void OnHeroSelected(HeroBase hero)
	{
		AppendLog($"选择英雄：{hero.AttributeSet.HeroDisplayName}");
	}

	// 事件组生成：重建事件按钮
	private void OnEventsGenerated(int round, int turn, Godot.Collections.Array<EventBase> events)
	{
		AppendLog($"第 {round} 轮第 {turn} 回合，生成 {events.Count} 个事件");
		ClearEventButtons();

		foreach (EventBase evt in events)
		{
			string suffix = evt switch
			{
				MonsterEvent => "【战斗】",
				PvPEvent => "【战斗】",
				ShopEvent => "【商店】",
				_ => "",
			};
			var button = new Button { Text = $"{evt.AttributeSet.EventDisplayName} {suffix}", CustomMinimumSize = new Vector2(260, 0) };
			EventBase captured = evt;
			button.Pressed += () => OnEventButtonPressed(captured);
			_eventPanel.AddChild(button);
		}
	}

	// 事件按钮点击分发
	private void OnEventButtonPressed(EventBase evt)
	{
		if (_combatManager is not null && _combatManager.IsInBattle)
		{
			return;
		}

		switch (evt)
		{
			case MonsterEvent:
			case PvPEvent:
				AppendLog($"进入战斗：{evt.AttributeSet.EventDisplayName}");
				_matchFlow?.StartBattleForEvent(evt);
				break;
			case ShopEvent shop:
				OpenShop(shop);
				break;
			default:
				AppendLog($"结算事件：{evt.AttributeSet.EventDisplayName}");
				_matchFlow?.TryResolveEvent(evt);
				break;
		}
	}

	// 打开商店：按英雄阵营过滤可购买卡牌
	private void OpenShop(ShopEvent shop)
	{
		_currentShopEvent = shop;
		_shopPanel.Visible = true;
		_eventPanel.Visible = false;

		ClearChildren(_shopList);

		HeroBase? hero = _heroManager?.CurrentHero;
		if (hero is null)
		{
			var label = new Label { Text = "未选择英雄" };
			_shopList.AddChild(label);
			return;
		}

		Godot.Collections.Array<CardBase> offers = _cardManager!.GetCardTemplatesByFaction(hero.AttributeSet.HeroKey);
		if (offers.Count == 0)
		{
			var label = new Label { Text = "该阵营暂无可用卡牌" };
			_shopList.AddChild(label);
			return;
		}

		foreach (CardBase card in offers)
		{
			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);
			float price = card.AttributeSet.Value.CurrentValue;
			var nameLabel = new Label { Text = $"{card.AttributeSet.DisplayName}  ${price:F0}" };
			var buyButton = new Button { Text = "购买" };
			CardBase captured = card;
			buyButton.Pressed += () => BuyCard(captured);
			row.AddChild(nameLabel);
			row.AddChild(buyButton);
			_shopList.AddChild(row);
		}
	}

	// 购买卡牌并扣财富
	private void BuyCard(CardBase card)
	{
		HeroBase? hero = _heroManager?.CurrentHero;
		if (hero is null)
		{
			return;
		}

		bool ok = _cardManager!.BuyCard(card, hero.AttributeSet.Wealth);
		if (ok)
		{
			AppendLog($"购买卡牌：{card.AttributeSet.DisplayName}");
			RefreshHud();
		}
		else
		{
			AppendLog($"购买失败：财富不足");
		}
	}

	// 离开商店：结算商店事件并推进回合
	private void OnLeaveShopPressed()
	{
		_shopPanel.Visible = false;
		_eventPanel.Visible = true;

		if (_currentShopEvent is not null)
		{
			ShopEvent shop = _currentShopEvent;
			_currentShopEvent = null;
			AppendLog($"离开商店，结算事件：{shop.AttributeSet.EventDisplayName}");
			_matchFlow?.TryResolveEvent(shop);
		}
	}

	// 回合开始：刷新 HUD
	private void OnTurnStarted(int round, int turn)
	{
		RefreshHud();
	}

	// 卡牌加入玩家：刷新手牌与棋盘面板
	private void OnCardAdded(CardBase card)
	{
		RefreshBoardPanel();
	}

	// 手牌中放置卡牌：自动落到战场区，其次备战区
	private void OnPlaceCardPressed(CardBase card)
	{
		if (_boardManager is null)
		{
			return;
		}

		(GameBoard Board, int Cell)? result = _boardManager.AutoPlaceCard(card);
		if (result is { } placed)
		{
			string area = placed.Board == _boardManager.Battlefield ? "战场" : "备战";
			AppendLog($"放置卡牌：{card.AttributeSet.DisplayName} 到 {area} 格{placed.Cell}");
		}
		else
		{
			AppendLog("棋盘已满，无法放置");
		}

		RefreshBoardPanel();
	}

	// 从棋盘收回卡牌回手牌
	private void OnRemoveFromBoardPressed(CardBase card)
	{
		if (_boardManager.GetBoardOf(card) is { } board)
		{
			board.RemoveCard(card);
			AppendLog($"收回卡牌：{card.AttributeSet.DisplayName}");
			RefreshBoardPanel();
		}
	}

	// 刷新手牌与棋盘面板：重建手牌 / 战场区 / 备战区三个列表
	private void RefreshBoardPanel()
	{
		if (_boardPanel is null || _boardManager is null || _cardManager is null)
		{
			return;
		}

		ClearChildren(_handBox);
		ClearChildren(_battlefieldBox);
		ClearChildren(_benchBox);

		// 手牌：玩家拥有且未上盘的卡牌
		var handLabel = new Label { Text = $"手牌（{CountHandCards()}）" };
		handLabel.AddThemeFontSizeOverride("font_size", 18);
		_handBox.AddChild(handLabel);

		foreach (CardBase card in _cardManager.PlayerCards)
		{
			if (_boardManager.GetBoardOf(card) is not null)
			{
				continue;
			}

			var row = new HBoxContainer();
			row.AddThemeConstantOverride("separation", 8);
			var nameLabel = new Label { Text = $"{card.AttributeSet.DisplayName}（{card.AttributeSet.Size}）" };
			var placeButton = new Button { Text = "放置" };
			CardBase captured = card;
			placeButton.Pressed += () => OnPlaceCardPressed(captured);
			row.AddChild(nameLabel);
			row.AddChild(placeButton);
			_handBox.AddChild(row);
		}

		if (_handBox.GetChildCount() == 1)
		{
			_handBox.AddChild(new Label { Text = "（无可用卡牌）" });
		}

		// 战场区
		var battlefieldHeader = new Label { Text = "战场区 (10 格)" };
		battlefieldHeader.AddThemeFontSizeOverride("font_size", 18);
		_battlefieldBox.AddChild(battlefieldHeader);

		foreach ((CardBase card, int startCell) in _boardManager.Battlefield.GetLayout())
		{
			AddBoardRow(_battlefieldBox, card, startCell);
		}

		// 备战区
		var benchHeader = new Label { Text = "备战区 (10 格)" };
		benchHeader.AddThemeFontSizeOverride("font_size", 18);
		_benchBox.AddChild(benchHeader);

		foreach ((CardBase card, int startCell) in _boardManager.Bench.GetLayout())
		{
			AddBoardRow(_benchBox, card, startCell);
		}
	}

	// 手牌数量：玩家拥有且未上盘的卡牌数
	private int CountHandCards()
	{
		int count = 0;
		foreach (CardBase card in _cardManager.PlayerCards)
		{
			if (_boardManager.GetBoardOf(card) is null)
			{
				count++;
			}
		}

		return count;
	}

	// 为棋盘列表追加一行：卡牌名 + 起始格 + 收回按钮
	private void AddBoardRow(VBoxContainer box, CardBase card, int startCell)
	{
		var row = new HBoxContainer();
		row.AddThemeConstantOverride("separation", 8);
		var nameLabel = new Label { Text = $"{card.AttributeSet.DisplayName} @格{startCell}" };
		var removeButton = new Button { Text = "收回" };
		CardBase captured = card;
		removeButton.Pressed += () => OnRemoveFromBoardPressed(captured);
		row.AddChild(nameLabel);
		row.AddChild(removeButton);
		box.AddChild(row);
	}

	// 刷新 HUD 文本
	private void RefreshHud()
	{
		HeroBase? hero = _heroManager?.CurrentHero;
		if (hero is null)
		{
			return;
		}

		_hudTurnLabel.Text = $"轮次/回合：{_roundTurnManager.CurrentRound} / {_roundTurnManager.CurrentTurn}";
		_hudWealthLabel.Text = $"财富：{hero.AttributeSet.Wealth.CurrentValue:F0}";
		_hudExpLabel.Text = $"经验：{hero.AttributeSet.Experience.CurrentValue:F0}";
		_hudRepLabel.Text = $"声望：{hero.AttributeSet.Reputation.CurrentValue:F0}";
		_hudLevelLabel.Text = $"等级：{hero.AttributeSet.Level.CurrentValue:F0}";
	}

	// 战斗轮询刷新：HP 与冷却
	private void UpdateCombatPanel()
	{
		bool inBattle = _combatManager.IsInBattle;
		_combatPanel.Visible = inBattle;
		if (!inBattle)
		{
			return;
		}

		ICombatant? friendly = _combatManager.FriendlyHero;
		ICombatant? enemy = _combatManager.EnemyHero;
		_friendlyHpLabel.Text = $"我方英雄 HP：{GetHpText(friendly)}";
		_enemyHpLabel.Text = $"敌方英雄 HP：{GetHpText(enemy)}";
		_friendlyCdLabel.Text = "我方卡牌冷却：" + BuildCooldownText(_combatManager, _combatManager.FriendlyCards);
		_enemyCdLabel.Text = "敌方卡牌冷却：" + BuildCooldownText(_combatManager, _combatManager.EnemyCards);
	}

	// 取实体 HP 文本（当前/最大）
	private static string GetHpText(ICombatant? combatant)
	{
		if (combatant?.AttributeSet.GetAttribute(HeroAttributeSet.HEALTH) is not { } health)
		{
			return "-";
		}

		float max = combatant.AttributeSet.GetAttribute(HeroAttributeSet.MAX_HEALTH)?.CurrentValue ?? health.CurrentValue;
		return $"{health.CurrentValue:F0} / {max:F0}";
	}

	// 构建卡牌冷却摘要文本
	private static string BuildCooldownText(CombatManager cm, System.Collections.Generic.List<ICombatant> cards)
	{
		if (cards.Count == 0)
		{
			return "无";
		}

		var sb = new StringBuilder();
		foreach (ICombatant card in cards)
		{
			float cd = 0f;
			if (card.Abilities.Count > 0)
			{
				cd = cm.GetCooldownRemaining(card, card.Abilities[0]);
			}

			string name = card is CardBase c ? c.AttributeSet.DisplayName : "卡牌";
			sb.Append($"{name}:{cd:F1}s ");
		}

		return sb.ToString();
	}

	// 刷新英雄按钮（回到选英雄状态时重建）
	private void RefreshHeroButtons()
	{
		ClearChildren(_heroSelectPanel);
		var title = new Label { Text = "选择英雄" };
		title.AddThemeFontSizeOverride("font_size", 28);
		_heroSelectPanel.AddChild(title);

		if (_heroManager is not null)
		{
			foreach (HeroBase hero in _heroManager.AvailableHeroes)
			{
				var button = new Button
				{
					Text = $"{hero.AttributeSet.HeroDisplayName}（{hero.AttributeSet.HeroKey}）",
					CustomMinimumSize = new Vector2(260, 0),
				};
				HeroBase captured = hero;
				button.Pressed += () => _heroManager.SelectHero(captured);
				_heroSelectPanel.AddChild(button);
			}
		}
	}

	// 清空事件按钮（保留标题）
	private void ClearEventButtons()
	{
		ClearChildrenKeepFirst(_eventPanel);
	}

	// 追加日志行并滚动到底部
	private void AppendLog(string line)
	{
		if (_logLabel is null)
		{
			return;
		}

		_logLabel.AppendText(line + "\n");
	}

	// 伤害事件日志
	private void OnDamageDealt(DamageInfo info)
	{
		string target = info.Target is CardBase tc ? tc.AttributeSet.DisplayName : "英雄";
		AppendLog($"伤害：{target} 受到 {info.DamageToHealth:F1} 点伤害");
	}

	// 能力发动日志
	private void OnAbilityActivated(ICombatant? combatant, AriaAbilityBase ability)
	{
		string owner = combatant switch
		{
			CardBase c => c.AttributeSet.DisplayName,
			HeroBase h => h.AttributeSet.HeroDisplayName,
			_ => "未知",
		};
		AppendLog($"发动能力：{owner} → {ability.DisplayName}");
	}

	// 效果结算日志
	private void OnEffectApplied(ICombatant? target, AriaEffectBase effect)
	{
		string name = target is CardBase tc ? tc.AttributeSet.DisplayName : "英雄";
		AppendLog($"效果结算：{name} ← {effect.GetType().Name}");
	}

	// 对局流程日志
	private void OnFlowLog(string msg)
	{
		AppendLog(msg);
	}

	// 清空容器全部子节点
	private static void ClearChildren(Container container)
	{
		foreach (Node child in container.GetChildren())
		{
			child.QueueFree();
		}
	}

	// 清空容器子节点但保留第一个（标题）
	private static void ClearChildrenKeepFirst(Container container)
	{
		for (int i = container.GetChildCount() - 1; i >= 1; i--)
		{
			container.GetChild(i).QueueFree();
		}
	}
}