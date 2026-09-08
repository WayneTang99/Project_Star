using Godot;
using Project_Star.Board.Manager;
using Project_Star.Combat.Managers;
using Project_Star.Match;
using Project_Star.Systems;
using Project_Star.UI;

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

	// 战斗管理器
	public CombatManager CombatManager { get; private set; } = null!;

	// 对局流程协调器
	public MatchFlowCoordinator MatchFlow { get; private set; } = null!;

	// 极简可玩 UI
	public MinimalGameUI GameUI { get; private set; } = null!;

	public override void _Ready()
	{
		base._Ready();
		// 管理器由 new 生成时节点名为空（自动命名 @Node@N），必须显式命名，
		// 否则各管理器 _Ready 中的 GetNode("../XxxManager") 找不到兄弟节点。
		GameManager = new GameManager { Name = "GameManager" };
		AddChild(GameManager);

		HeroManager = new HeroManager { Name = "HeroManager" };
		AddChild(HeroManager);

		CardManager = new CardManager { Name = "CardManager" };
		AddChild(CardManager);

		BoardManager = new BoardManager { Name = "BoardManager" };
		AddChild(BoardManager);

		RoundTurnManager = new RoundTurnManager { Name = "RoundTurnManager" };
		AddChild(RoundTurnManager);

		EventManager = new EventManager { Name = "EventManager" };
		AddChild(EventManager);

		CombatManager = new CombatManager { Name = "CombatManager" };
		AddChild(CombatManager);

		MatchFlow = new MatchFlowCoordinator { Name = "MatchFlow" };
		AddChild(MatchFlow);

		GameUI = new MinimalGameUI { Name = "GameUI" };
		AddChild(GameUI);
	}
}