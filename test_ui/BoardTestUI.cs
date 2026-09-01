using System.Collections.Generic;
using Godot;
using Project_Star.Core.Bases;
using Project_Star.Core.Board;
using Project_Star.Core.Managers;
using Project_Star.Core.Types;

namespace Project_Star.TestUI;

public partial class BoardTestUI : Control
{
	private const string EMPTY_CELL = "·";

	private BoardManager _boardManager = null!;

	private CardSize _selectedSize = CardSize.Small;

	private CardBase? _selectedCard;

	private bool _newCardMode = true;

	private Label _statusLabel = null!;

	private readonly List<Button> _battlefieldCells = new();

	private readonly List<Button> _benchCells = new();

	public override void _Ready()
	{
		base._Ready();
		_boardManager = new BoardManager();
		AddChild(_boardManager);
		BuildUI();
		Refresh();
		SetStatus("选尺寸→点目标格放新卡；点已放卡牌→再点目标格移动");
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

		root.AddChild(new Label { Text = "战场区" });
		BuildBoardRow(root, _boardManager.Battlefield, _battlefieldCells);

		root.AddChild(new Label { Text = "备战区" });
		BuildBoardRow(root, _boardManager.Bench, _benchCells);

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
		SetStatus($"准备放置 {size} 卡牌，点击目标格");
	}

	private void OnCellPressed(GameBoard board, int cell)
	{
		if (_newCardMode)
		{
			CardBase card = new BoardTestCard(_selectedSize);
			PushPlan plan = _boardManager.PreviewMove(card, board, cell);
			if (plan.IsFeasible && _boardManager.CommitMove(card, board, cell))
			{
				_selectedCard = card;
				_newCardMode = false;
				SetStatus($"已放置，可点其他格移动 | {DescribePlan(plan)}");
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
			}
			else
			{
				SetStatus($"移动失败 | {DescribePlan(plan)}");
			}
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

	private void Refresh()
	{
		UpdateRow(_battlefieldCells, _boardManager.Battlefield);
		UpdateRow(_benchCells, _boardManager.Bench);
	}

	private static void UpdateRow(List<Button> cells, GameBoard board)
	{
		for (int i = 0; i < cells.Count; i++)
		{
			BoardEntry? entry = board.GetEntryAt(i);
			cells[i].Text = entry is null ? EMPTY_CELL : GetCardLabel(entry);
		}
	}

	private static string GetCardLabel(BoardEntry entry)
	{
		string size = entry.Card.AttributeSet.Size switch
		{
			CardSize.Small => "S",
			CardSize.Medium => "M",
			CardSize.Large => "L",
			_ => "?",
		};
		return entry.CanPush ? size : $"{size}*";
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