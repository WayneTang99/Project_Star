using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Online;
using Project_Star.Domain.Definitions;

namespace Project_Star.Presentation.Game;

// 联网操作面板：只消费云端快照，通过应用用例提交意图（表现层）。
public sealed partial class OnlineAdventureView : Control
{
    public event Action? Closed;
    private OnlineJourney _journey = null!;
    private IDefinitionCatalog _definitions = null!;
    private readonly CancellationTokenSource _lifetime = new();
    private readonly VBoxContainer _operations = new(), _cards = new();
    private readonly Label _status = new() { CustomMinimumSize = new Vector2(0, 44) }, _summary = new() { CustomMinimumSize = new Vector2(0, 70) };
    private readonly LineEdit _username = new() { Name = "Username", MaxLength = 32 }, _password = new() { Name = "Password", Secret = true, MaxLength = 128 },
        _invitation = new() { Name = "Invitation", MaxLength = 128 };
    private readonly OptionButton _hero = new() { Name = "Hero" }, _card = new() { Name = "Card" }, _zone = new() { Name = "Zone" };
    private readonly SpinBox _start = new() { Name = "Start", MinValue = 1, MaxValue = 10, Value = 1, Step = 1 };
    private readonly ConfirmationDialog _sale = new() { Title = "出售卡牌", MinSize = new Vector2I(380, 180) };
    private Button _login = null!, _register = null!, _logout = null!, _create = null!, _restore = null!, _retry = null!, _move = null!, _sell = null!;
    private bool _busy;
    private double _pollElapsed;

    // 在加入场景树之前注入云端用例及只读身份目录。
    public void Configure(OnlineJourney journey, IDefinitionCatalog definitions) { _journey = journey; _definitions = definitions; }
    public override void _Ready()
    {
        Name = "OnlineAdventure"; ZIndex = 100; MouseFilter = MouseFilterEnum.Stop;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var shade = new ColorRect { Color = new Color("211a16ed"), MouseFilter = MouseFilterEnum.Stop };
        AddChild(shade); shade.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        var margin = new MarginContainer(); AddChild(margin); margin.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        foreach (var side in new[] { "left", "right", "top", "bottom" }) margin.AddThemeConstantOverride("margin_" + side, 24);
        var panel = Panel(margin, 24); var column = Column(panel, 16);
        var header = new HBoxContainer(); column.AddChild(header);
        var title = Text(header, "联网冒险", 24, MatchTheme.Gold); title.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var close = Button(header, "×", () => { Hide(); Closed?.Invoke(); }, "Close"); close.TooltipText = "返回大厅";
        MatchTheme.Text(_status, 16, MatchTheme.Muted); column.AddChild(_status);
        var body = new HBoxContainer { SizeFlagsVertical = SizeFlags.ExpandFill }; body.AddThemeConstantOverride("separation", 24); column.AddChild(body);
        var accountPanel = Panel(body, 20); accountPanel.CustomMinimumSize = new Vector2(280, 0);
        var accountScroll = Scroll(accountPanel); var account = Column(accountScroll, 12); account.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Input(account, "账号", _username); Input(account, "密码", _password); Input(account, "邀请码", _invitation);
        var signIn = new HBoxContainer(); signIn.AddThemeConstantOverride("separation", 12); account.AddChild(signIn);
        _login = Button(signIn, "登录", () => Authenticate(false), "Login"); HtmlReliefStyle.Button(_login, true);
        foreach (var state in new[] { "font_color", "font_hover_color", "font_focus_color", "font_pressed_color", "font_hover_pressed_color" })
            _login.AddThemeColorOverride(state, new Color("172823"));
        _register = Button(signIn, "注册", () => Authenticate(true), "Register");
        _logout = Button(account, "退出账号", () => Run(() => _journey.SignOutAsync(_lifetime.Token)), "Logout");
        Text(account, "本次运行保持登录，关闭游戏后重新登录可恢复云端旅程。", 12, MatchTheme.Muted).CustomMinimumSize = new Vector2(230, 0);
        Text(account, "本次同行", 16, MatchTheme.Muted); account.AddChild(_hero); Style(_hero);
        foreach (var hero in _definitions.Heroes.Values.OrderBy(x => x.Attributes.Identity.Key.ToString()))
        { _hero.AddItem(hero.Attributes.Identity.DisplayName); _hero.SetItemMetadata(_hero.ItemCount - 1, hero.Attributes.Identity.Key.ToString()); }
        _create = Button(account, "开始新旅程", () => Run(() => _journey.SubmitAsync(new OnlineCommand(Guid.NewGuid(), "create",
            HeroKey: new StringName(_hero.GetSelectedMetadata().AsString())), _lifetime.Token)), "Create");
        _restore = Button(account, "恢复旅程", () => Run(() => _journey.LoadAsync(_lifetime.Token)), "Restore");
        _retry = Button(account, "重试原操作", () => Run(() => _journey.RetryAsync(_lifetime.Token)), "Retry");
        var matchPanel = Panel(body, 20); matchPanel.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        var matchScroll = Scroll(matchPanel); var match = Column(matchScroll, 12); match.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        MatchTheme.Text(_summary, 18, MatchTheme.Gold); _summary.AutowrapMode = TextServer.AutowrapMode.WordSmart; match.AddChild(_summary);
        match.AddChild(_operations); _operations.AddThemeConstantOverride("separation", 10);
        Text(match, "己方阵容", 18, MatchTheme.Gold); match.AddChild(_cards);
        var build = new HBoxContainer(); build.AddThemeConstantOverride("separation", 8); match.AddChild(build);
        _card.SizeFlagsHorizontal = SizeFlags.ExpandFill; _card.CustomMinimumSize = new Vector2(140, 42); build.AddChild(_card); Style(_card);
        _zone.AddItem("战场"); _zone.AddItem("备战"); build.AddChild(_zone); Style(_zone);
        build.AddChild(_start); _start.CustomMinimumSize = new Vector2(70, 42); _start.TooltipText = "起始格（1～10）";
        _move = Button(build, "移动", () => Move(), "Move");
        _sell = Button(build, "出售", ConfirmSale, "Sell"); AddChild(_sale);
        _username.TextChanged += _ => UpdateEnabled(); _password.TextChanged += _ => UpdateEnabled();
        Render(); _status.Text = "请登录或使用邀请码注册";
    }

    // 返回联网面板继续本次内存会话；不创建第二个游戏根场景。
    public void Open() { Show(); _username.GrabFocus(); }
    public override void _Process(double delta)
    {
        if (!IsVisibleInTree() || _busy || !_journey.IsAuthenticated || _journey.Current?.State.Phase != "PvpPending") return;
        _pollElapsed += delta;
        if (_pollElapsed < 1.5) return;
        _pollElapsed = 0;
        Run(() => _journey.PollAsync(_lifetime.Token));
    }
    public override void _ExitTree() { _lifetime.Cancel(); _lifetime.Dispose(); }
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (IsVisibleInTree() && @event.IsActionPressed("ui_cancel") && !_sale.Visible)
        { Hide(); Closed?.Invoke(); GetViewport().SetInputAsHandled(); }
    }
    private void Authenticate(bool registration)
    {
        var username = _username.Text; var password = _password.Text; var invitation = registration ? _invitation.Text : null;
        _password.Text = _invitation.Text = "";
        Run(() => _journey.SignInAsync(username, password, invitation, _lifetime.Token));
    }
    private async void Run(Func<Task<OnlineReply>> action)
    {
        if (_busy) return;
        _busy = true; _status.Text = "正在连接…"; UpdateEnabled();
        try
        {
            var reply = await action();
            if (GodotObject.IsInstanceValid(this) && IsInsideTree()) _status.Text = Message(reply);
        }
        catch (OperationCanceledException) { }
        catch (Exception) { if (GodotObject.IsInstanceValid(this) && IsInsideTree()) _status.Text = "暂时无法完成操作，请恢复旅程后重试。"; }
        finally { _busy = false; if (GodotObject.IsInstanceValid(this) && IsInsideTree()) Render(); }
    }
    private void Render()
    {
        Clear(_operations); Clear(_cards);
        var current = _journey.Current;
        var boundary = current?.State.Pvp ?? current?.State.Monster;
        _summary.Text = current is null ? "尚未开始旅程" : $"第{boundary?.Round ?? current.State.Round}轮 · 第{boundary?.Turn ?? current.State.Turn}回合　金币 {current.State.Wealth}\n声望 {current.State.Reputation}　胜场 {current.State.PvpWins} / 10　经验 {current.State.Experience}";
        var selectedCard = _card.Selected >= 0 ? _card.GetSelectedMetadata().AsString() : "";
        _card.Clear();
        if (current is not null)
        {
            var state = current.State;
            if (state.Status != "InProgress")
                Text(_operations, state.Status == "Won" ? "旅程胜利，可以开始新旅程。" : "旅程结束，可以开始新旅程。", 18, MatchTheme.Gold);
            if (state.Phase == "EncounterChoice")
            {
                Text(_operations, "选择遭遇", 18, MatchTheme.Gold);
                foreach (var choice in state.Choices)
                {
                    var type = choice.IsCardShop ? "select_shop" : choice.IsChoiceEvent ? "select_event" : choice.Kind == "Monster" ? "select_monster" : choice.Kind == "Pvp" ? "start_pvp" : "";
                    Row(choice.Name, type.Length == 0 ? "尚未接入" : "前往", () => Send(current, type, encounter: choice.Key), type.Length == 0);
                }
            }
            if (state.Shop is { } shop)
            {
                foreach (var offer in shop.Offers)
                    Row($"{offer.Name} · {offer.Level}级 · {offer.Price}金币", offer.Sold ? "已售罄" : "购买",
                        () => Send(current, "buy", offer: offer.Index), offer.Sold || offer.Price > state.Wealth);
                CommandButton(current, $"刷新 · {shop.RefreshCost}金币", "refresh_shop", disabled: !shop.CanRefresh);
                CommandButton(current, "离开商店", "leave_shop");
            }
            if (state.Visit is { } visit && state.Phase == "Event")
            {
                if (!visit.Resolved)
                    foreach (var option in visit.Options) Row(option.Name, "选择", () => Send(current, "resolve_event", visit: visit.Id, option: option.Key));
                else CommandButton(current, "继续旅程", "leave_event", visit: visit.Id);
            }
            if (state.Monster is { } monster && state.Phase is var phase && (phase == "MonsterPreparation" || phase == "MonsterResult"))
            {
                BattleText(monster);
                if (phase == "MonsterPreparation") CommandButton(current, "开始战斗", "resolve_monster", monster.Id);
                else if (state.Status == "InProgress") CommandButton(current, "进入下一回合", "finish_monster", monster.Id, disabled: state.Rewards.Count > 0);
            }
            if (state.Pvp is { } pvp)
            {
                BattleText(pvp);
                if (state.Phase == "PvpPending")
                {
                    Text(_operations, _journey.BattleStatus == "failed" ? "战斗暂未完成，可重试同一场。" : "正在等待服务器结算…", 16, MatchTheme.Muted);
                    if (_journey.BattleStatus == "failed") CommandButton(current, "重试本场战斗", "retry_pvp", pvp.Id);
                }
                else if (state.Status == "InProgress") CommandButton(current, "进入下一轮", "finish_pvp", pvp.Id, disabled: state.Rewards.Count > 0);
            }
            if (state.Status == "InProgress" && state.Phase != "PvpPending")
                foreach (var reward in state.Rewards)
                    Row($"{reward.Name} · {reward.Level}级", "领取", () => Send(current, "claim_reward",
                        battle: state.Phase == "PvpResult" ? state.Pvp?.Id : state.Monster?.Id, reward: reward.Index));
            foreach (var card in state.Cards)
            {
                Text(_cards, $"{CardName(card)} · {card.Level}级 · {(card.Zone == "Bench" ? "备战" : "战场")}第{card.Start + 1}格", 16, MatchTheme.Muted);
                _card.AddItem(CardName(card)); _card.SetItemMetadata(_card.ItemCount - 1, card.Id.ToString());
                if (card.Id.ToString() == selectedCard) _card.Select(_card.ItemCount - 1);
            }
        }
        if (_card.ItemCount == 0) { _card.AddItem("选择卡牌"); _card.SetItemMetadata(0, ""); Text(_cards, "暂无卡牌", 16, MatchTheme.Muted); }
        UpdateEnabled();
    }
    private void BattleText(OnlineBattle battle)
    {
        Text(_operations, $"第{battle.Round}轮 · 对手 {battle.OpponentName}" + (battle.IsTestOpponent ? "（测试构筑）" : ""), 18, MatchTheme.Gold);
        foreach (var card in battle.OpponentCards.Where(x => x.Start is not null)) Text(_operations, $"{CardName(card)} · {card.Level}级 · 第{card.Start + 1}格", 16, MatchTheme.Muted);
        if (battle.Result is { } result)
        {
            var outcome = result.Outcome == "PlayerVictory" ? "胜利" : result.Outcome == "OpponentVictory" ? "失败" : "平局";
            Text(_operations, $"{outcome} · 己方生命 {result.PlayerHealth} · 对手生命 {result.OpponentHealth}", 18, MatchTheme.Gold);
        }
    }
    private void CommandButton(OnlineMatch current, string text, StringName type, Guid? battle = null, Guid? visit = null, bool disabled = false) =>
        Button(_operations, text, () => Send(current, type, battle: battle, visit: visit)).Disabled = disabled || Locked;
    private void Row(string text, string action, Action command, bool disabled = false)
    {
        var row = new HBoxContainer(); row.AddThemeConstantOverride("separation", 16); _operations.AddChild(row);
        var label = Text(row, text, 16); label.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        Button(row, action, command).Disabled = disabled || Locked;
    }
    private void Send(OnlineMatch current, StringName type, StringName? encounter = null, int? offer = null, Guid? battle = null,
        int? reward = null, Guid? visit = null, StringName? option = null, Guid? card = null, StringName? zone = null, int? start = null) =>
        Run(() => _journey.SubmitAsync(new OnlineCommand(Guid.NewGuid(), type, current.MatchId, current.Version,
            EncounterKey: encounter, OfferIndex: offer, BattleId: battle, RewardIndex: reward, VisitId: visit, OptionKey: option,
            CardId: card, TargetZone: zone, TargetStart: start), _lifetime.Token));
    private void Move()
    {
        if (_journey.Current is { } current && Guid.TryParse(_card.GetSelectedMetadata().AsString(), out var card))
            Send(current, "move_card", card: card, zone: _zone.Selected == 1 ? new StringName("Bench") : new StringName("Battlefield"), start: (int)_start.Value - 1);
    }
    private void ConfirmSale()
    {
        if (_journey.Current is not { } current || !Guid.TryParse(_card.GetSelectedMetadata().AsString(), out var id)) return;
        var card = current.State.Cards.Single(x => x.Id == id);
        _sale.DialogText = $"出售{CardName(card)}，获得{card.Value}金币？";
        void Confirm() { _sale.Confirmed -= Confirm; _sale.Canceled -= Cancel; Send(current, "sell_card", card: id); }
        void Cancel() { _sale.Confirmed -= Confirm; _sale.Canceled -= Cancel; }
        _sale.Confirmed += Confirm; _sale.Canceled += Cancel; _sale.PopupCentered();
    }
    private bool Locked => _busy || !_journey.IsAuthenticated || _journey.Pending is not null;
    private void UpdateEnabled()
    {
        if (_login is null) return;
        var signed = _journey.IsAuthenticated;
        _username.Editable = _password.Editable = _invitation.Editable = !signed && !_busy;
        _login.Disabled = _register.Disabled = signed || _busy || _username.Text.Length < 3 || _password.Text.Length < 10;
        _logout.Disabled = _restore.Disabled = !signed || _busy;
        _create.Disabled = Locked || _journey.Current?.State.Status == "InProgress";
        _hero.Disabled = _create.Disabled;
        _retry.Disabled = !signed || _busy || _journey.Pending is null;
        var editable = !Locked && _journey.Current is { State.Status: var status, State.Phase: var phase }
            && status == "InProgress" && phase != "PvpPending" && _journey.Current.State.Cards.Count > 0;
        _move.Disabled = _sell.Disabled = _card.Disabled = _zone.Disabled = !editable; _start.Editable = editable;
        if (_busy) foreach (var child in _operations.FindChildren("*", "Button", true, false).OfType<Button>()) child.Disabled = true;
    }
    private string CardName(OnlineCard card) => card.Name.Length > 0 ? card.Name : _definitions.Cards.TryGetValue(card.Key, out var definition) ? definition.Attributes.Identity.DisplayName : "未知卡牌";
    private string Message(OnlineReply reply) => reply.IsSuccess ? "已同步服务器状态" : reply.Error?.ToString() switch
    {
        "authentication_failed" => "账号或密码不正确。", "registration_rejected" => "注册未完成，请核对账号、密码和邀请码。",
        "authentication_required" or "client.session_lost" => "登录已失效，请重新登录恢复旅程。",
        "client.logout_uncertain" => "已退出本机，暂未确认云端会话撤销。",
        "version_incompatible" => "此旅程使用其他规则版本，云端存档已保留。",
        "client.stale_view" or "version_conflict" => "状态已更新，请按最新状态重新操作。",
        "client.operation_pending" => "请先重试原操作，确认上次操作的结果。",
        "match_not_found" => "暂无进行中的旅程，可开始新旅程。",
        _ => reply.CanRetry ? !_journey.IsAuthenticated ? "暂时无法登录；若注册响应丢失，请用原账号密码登录。"
            : _journey.Pending is not null ? "暂时无法确认结果，请重试原操作；不会重新抽取对手。"
            : "暂时无法同步，请稍后恢复旅程。" : "操作未完成，请恢复旅程并核对当前阶段。",
    };
    private static PanelContainer Panel(Node parent, int padding)
    {
        var panel = new PanelContainer(); panel.AddThemeStyleboxOverride("panel", HtmlReliefStyle.Surface(margin: padding)); parent.AddChild(panel); return panel;
    }
    private static VBoxContainer Column(Node parent, int gap) { var box = new VBoxContainer(); box.AddThemeConstantOverride("separation", gap); parent.AddChild(box); return box; }
    private static ScrollContainer Scroll(Node parent) { var scroll = new ScrollContainer { SizeFlagsVertical = SizeFlags.ExpandFill, HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled }; parent.AddChild(scroll); return scroll; }
    private static Label Text(Node parent, string text, int size, Color? color = null)
    {
        var label = new Label { Text = text, AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = MouseFilterEnum.Ignore };
        MatchTheme.Text(label, size, color); parent.AddChild(label); return label;
    }
    private static Button Button(Node parent, string text, Action action, string name = "")
    {
        var button = new Button { Text = text, CustomMinimumSize = new Vector2(0, 42) }; if (name.Length > 0) button.Name = name;
        parent.AddChild(button); button.AddThemeFontOverride("font", MatchTheme.Font()); button.AddThemeFontSizeOverride("font_size", 16);
        HtmlReliefStyle.Button(button); button.Pressed += action; return button;
    }
    private static void Input(Node parent, string label, LineEdit field) { Text(parent, label, 16, MatchTheme.Muted); parent.AddChild(field); Style(field); }
    private static void Style(Control control)
    {
        control.CustomMinimumSize = new Vector2(0, 42); control.AddThemeFontOverride("font", MatchTheme.Font()); control.AddThemeFontSizeOverride("font_size", 16);
        if (control is OptionButton button) HtmlReliefStyle.Button(button);
        else
        {
            foreach (var state in new[] { "normal", "read_only" }) control.AddThemeStyleboxOverride(state, HtmlReliefStyle.Surface("pressed"));
            control.AddThemeStyleboxOverride("focus", MatchTheme.Outline(new Color("cbb7a1")));
        }
    }
    private static void Clear(Node parent) { foreach (var child in parent.GetChildren()) { parent.RemoveChild(child); child.QueueFree(); } }
}
