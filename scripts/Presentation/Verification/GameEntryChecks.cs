using System.Reflection;
using Godot;
using Project_Star.Domain.Common;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

// 正式入口与初始资源行为检查，不向被测对局注入资源（表现层验证）。
internal static class GameEntryChecks
{
    // 从真实Main场景创建全部英雄，验证正常开局、只读刷新及重开都只发一次首轮收入。
    public static bool FormalResources(Control owner)
    {
        if (ProjectSettings.GetSetting("application/run/main_scene").AsString() != "res://Main.tscn") return false;
        var root = GD.Load<PackedScene>("res://Main.tscn").Instantiate<GameMain>();
        owner.AddChild(root);
        try
        {
            var presenter = (MatchPresenter)typeof(GameMain).GetField("_presenter", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(root)!;
            var registry = DefinitionRegistry.Scan(typeof(GameMain).Assembly);
            foreach (var hero in registry.Heroes.Values)
            {
                presenter.Reset();
                presenter.SelectHero(hero.Attributes.Identity.Key);
                var first = presenter.View.Player!;
                var income = hero.Attributes.Persistent.GetFinalValue(GameAttributeKeys.Income);
                if (first.Wealth != income || first.Round != 1 || first.Turn != 1 || first.Experience != 1
                    || first.Cards.Count != 0 || first.BoardPlacements.Count != 0) return false;
                presenter.RefreshView();
                presenter.SelectHero(hero.Attributes.Identity.Key);
                if (presenter.View.Player!.MatchId != first.MatchId || presenter.View.Player.Wealth != income) return false;
                presenter.Reset();
                presenter.SelectHero(hero.Attributes.Identity.Key);
                if (presenter.View.Player!.MatchId == first.MatchId || presenter.View.Player.Wealth != income
                    || presenter.View.Player.Cards.Count != 0) return false;
            }
            return true;
        }
        finally { owner.RemoveChild(root); root.Free(); }
    }
}
