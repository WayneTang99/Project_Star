using System;
using System.Collections.Generic;
using Godot;
using Project_Star.Board.Algo;
using Project_Star.Board.Data;
using Project_Star.Board.Manager;
using Project_Star.Core.Bases;
using Project_Star.Core.Types;

namespace Project_Star.TestUI;

// 棋盘推挤独立测试 UI：点击式演示放置 / 移动 / 推挤 / 不可推挤。
public partial class BoardTestUI : Control
{
	private const string EMPTY_CELL = "·";

	private const int EXTERNAL_POOL_SIZE = 6;

	private readonly Random _random = new();

	private readonly Dictionary<CardBase, Color> _cardColors = new();

	private readonly List<CardBase> _externalPool = new();

	private BoardManager _boardManager = null!;

	private CardSize _selectedSize = CardSize.Small;

	private CardBase? _selectedCard;

	private bool _newCardMode = true;

	private Label _statusLabel = null!;

	private HBoxContainer _externalRow = null!;

	private readonly List<Button> _battlefieldCells = new();

	private readonly List<Button> _benchCells = new();

	public override void _Ready()
	{
		base._Ready();
		_boardManager = new BoardManager();
		AddChild(_boardManager);
		BuildUI();
		RefreshExternal();
		Refresh();
		SetStatus("选尺寸→点空格放新卡；点已放卡牌→再点空格移动；点外部卡自动加入");
	}

	private void BuildUI()
	{
		var root = new VBoxContainer();
		root.SetAnchorsPreset(LayoutPreset.FullRect);
		AddChild(root);

		var controlRow = new HBoxContainer();
		root.AddChild(controlRow);

		foreach (CardSize size in new[] { CardSize.Small, CardSize.Medium, CardSize.Large })
		{
			CardSize captured = size;
			var button = new Button { Text = size.ToString() };
			button.Pressed += () => OnSizeSelected(captured);
			controlRow.AddChild(button);
		}

		var unpushableButton = new Button { Text = "切换不可推挤" };
		unpushableButton.Pressed += OnToggleUnpushable;
		controlRow.AddChild(unpushableButton);

		var refreshButton = new Button { Text = "刷新外部区" };
		refreshButton.Pressed += OnRefreshExternal;
		controlRow.AddChild(refreshButton);

		root.AddChild(new Label { Text = "战场区" });
		BuildBoardRow(root, _boardManager.Battlefield, _battlefieldCells);

		root.AddChild(new Label { Text = "备战区" });
		BuildBoardRow(root, _boardManager.Bench, _benchCells);

		root.AddChild(new Label { Text = "外部区（点击卡牌自动加入战场区，放不下则入备战区）" });
		_externalRow = new HBoxContainer();
		root.AddChild(_externalRow);

		_statusLabel = new Label();
		root.AddChild(_statusLabel);
	}

	private void BuildBoardRow(VBoxContainer root, GameBoard board, List<Button> cells)
	{
		var row = new HBoxContainer();
		root.AddChild(row);

		for (int i = 0; i < BoardManager.BOARD_CAPACITY; i++)
		{
			int cell = i;
			var button = new Button
			{
				Text = EMPTY_CELL,
				CustomMinimumSize = new Vector2(48, 48),
			};
			button.Pressed += () => OnCellPressed(board, cell);
			row.AddChild(button);
			cells.Add(button);
		}
	}

	private void OnSizeSelected(CardSize size)
	{
		_selectedSize = size;
		_newCardMode = true;
		_selectedCard = null;
		SetStatus($"准备放置 {size} 卡牌，点击空目标格");
	}

	private void OnCellPressed(GameBoard board, int cell)
	{
		BoardEntry? entry = board.GetEntryAt(cell);

		if (entry is not null)
		{
			_selectedCard = entry.Card;
			_newCardMode = false;
			SetStatus($"已选中 {GetCardLabel(entry)}，点击空格移动");
		}
		else if (_newCardMode)
		{
			CardBase card = new BoardTestCard(_selectedSize);
			PushPlan plan = _boardManager.PreviewMove(card, board, cell);
			if (plan.IsFeasible && _boardManager.CommitMove(card, board, cell))
			{
				SetStatus($"已放置 | {DescribePlan(plan)}");
			}
			else
			{
				SetStatus($"放置失败 | {DescribePlan(plan)}");
			}
		}
		else if (_selectedCard is not null)
		{
			PushPlan plan = _boardManager.PreviewMove(_selectedCard, board, cell);
			if (plan.IsFeasible && _boardManager.CommitMove(_selectedCard, board, cell))
			{
				SetStatus($"移动成功 | {DescribePlan(plan)}");
				_selectedCard = null;
			}
			else
			{
				SetStatus($"移动失败 | {DescribePlan(plan)}");
			}
		}
		else
		{
			SetStatus("选择尺寸放新卡，或点击已放卡牌移动");
		}

		Refresh();
	}

	private void OnToggleUnpushable()
	{
		if (_selectedCard is null)
		{
			SetStatus("请先点选一张已放置卡牌");
			return;
		}

		GameBoard? board = _boardManager.GetBoardOf(_selectedCard);
		BoardEntry? entry = board?.GetEntry(_selectedCard);
		if (entry is null)
		{
			SetStatus("卡牌不在棋盘上");
			return;
		}

		entry.CanPush = !entry.CanPush;
		SetStatus($"{_selectedCard.AttributeSet.DisplayName} CanPush={entry.CanPush}");
		Refresh();
	}

	private void OnRefreshExternal()
	{
		_externalPool.Clear();
		for (int i = 0; i < EXTERNAL_POOL_SIZE; i++)
		{
			_externalPool.Add(new BoardTestCard((CardSize)_random.Next(3)));
		}

		RefreshExternal();
		SetStatus($"外部区已刷新 {EXTERNAL_POOL_SIZE} 张卡牌");
		Refresh();
	}

	private void RefreshExternal()
	{
		foreach (Node child in _externalRow.GetChildren())
		{
			_externalRow.RemoveChild(child);
			child.QueueFree();
		}

		for (int i = 0; i < _externalPool.Count; i++)
		{
			int index = i;
			CardBase card = _externalPool[i];
			var button = new Button
			{
				Text = GetSizeLabel(card.AttributeSet.Size),
				SelfModulate = GetCardColor(card),
				CustomMinimumSize = new Vector2(48, 48),
			};
			button.Pressed += () => OnExternalCardPressed(index);
			_externalRow.AddChild(button);
		}
	}

	private void OnExternalCardPressed(int index)
	{
		if (index < 0 || index >= _externalPool.Count)
		{
			return;
		}

		CardBase card = _externalPool[index];
		(GameBoard Board, int Cell)? target = _boardManager.AutoPlaceCard(card);
		if (target is null)
		{
			SetStatus("战场区与备战区均无法放置该卡牌");
			return;
		}

		_externalPool.RemoveAt(index);
		RefreshExternal();
		string boardName = target.Value.Board == _boardManager.Battlefield ? "战场区" : "备战区";
		SetStatus($"已自动加入{boardName} 格{target.Value.Cell}");
		Refresh();
	}

	private Color GetCardColor(CardBase card)
	{
		if (!_cardColors.TryGetValue(card, out Color color))
		{
			color = Color.FromHsv((float)_random.NextDouble(), 0.75f, 0.95f);
			_cardColors[card] = color;
		}

		return color;
	}

	private void Refresh()
	{
		UpdateRow(_battlefieldCells, _boardManager.Battlefield);
		UpdateRow(_benchCells, _boardManager.Bench);
	}

	private void UpdateRow(List<Button> cells, GameBoard board)
	{
		for (int i = 0; i < cells.Count; i++)
		{
			BoardEntry? entry = board.GetEntryAt(i);
			if (entry is null)
			{
				cells[i].Text = EMPTY_CELL;
				cells[i].SelfModulate = Colors.White;
			}
			else
			{
				cells[i].Text = GetCardLabel(entry);
				cells[i].SelfModulate = entry.Card == _selectedCard
					? GetCardColor(entry.Card).Lightened(0.3f)
					: GetCardColor(entry.Card);
			}
		}
	}

	private static string GetCardLabel(BoardEntry entry)
	{
		string size = GetSizeLabel(entry.Card.AttributeSet.Size);
		return entry.CanPush ? size : $"{size}*";
	}

	private static string GetSizeLabel(CardSize size)
	{
		return size switch
		{
			CardSize.Small => "S",
			CardSize.Medium => "M",
			CardSize.Large => "L",
			_ => "?",
		};
	}

	private static string DescribePlan(PushPlan plan)
	{
		if (!plan.IsFeasible)
		{
			return "不可行";
		}

		return $"方向={plan.Direction} 距离={plan.PushDistance} 受影响={plan.AffectedCards.Count} 目标格={plan.TargetCell}";
	}

	private void SetStatus(string text)
	{
		_statusLabel.Text = text;
	}
}