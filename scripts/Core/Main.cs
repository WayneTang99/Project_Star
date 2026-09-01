using Godot;
using Project_Star.Core.Managers;

namespace Project_Star.Core;

// 主场景根节点：_Ready 中 new 生成并挂载各管理器，供全局引用。
public partial class Main : Node
{
	// 主状态机管理器
	public GameManager GameManager { get; private set; } = null!;

	// 局内轮次管理器
	public RoundTurnManager RoundTurnManager { get; private set; } = null!;

	// 英雄管理器
	public HeroManager HeroManager { get; private set; } = null!;

	// 卡牌管理器
	public CardManager CardManager { get; private set; } = null!;

	// 棋盘管理器
	public BoardManager BoardManager { get; private set; } = null!;

	// 事件管理器
	public EventManager EventManager { get; private set; } = null!;

	public override void _Ready()
	{
		base._Ready();
		GameManager = new GameManager();
		AddChild(GameManager);

		HeroManager = new HeroManager();
		AddChild(HeroManager);

		CardManager = new CardManager();
		AddChild(CardManager);

		BoardManager = new BoardManager();
		AddChild(BoardManager);

		RoundTurnManager = new RoundTurnManager();
		AddChild(RoundTurnManager);

		EventManager = new EventManager();
		AddChild(EventManager);
	}
}