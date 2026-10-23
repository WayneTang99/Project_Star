using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Godot;
using Project_Star.Application.Online;
using Project_Star.Infrastructure.Definitions;
using Project_Star.Infrastructure.Online;
using Project_Star.Presentation.Game;

namespace Project_Star.Presentation.Verification;

// 网络故障、会话轮换、单调视图及真实云端流程检查（表现层验证）。
internal static class OnlineClientChecks
{
    private static int _checks;
    private static readonly Guid Player = Guid.NewGuid(), Match = Guid.NewGuid();
    private static readonly string Access = new('a', 43), Refresh = new('r', 43), NextAccess = new('b', 43), NextRefresh = new('s', 43);
    private static readonly CancellationToken Ct = CancellationToken.None;

    // 用真实HTTP适配和受控消息处理器复现丢响应、旧回执及续期竞争。
    internal static async Task Run(Control owner)
    {
        _checks = 0;
        var rejected = false;
        try { using var invalid = new OnlineGateway(new Uri("http://example.com/")); }
        catch (ArgumentException) { rejected = true; }
        Check("remote_plain_http_rejected", rejected);
        var refreshes = 0;
        using (var handler = new Handler(async request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.EndsWith("login")) { Check("login_has_no_bearer", request.Headers.Authorization is null); return Response(200, Session()); }
            if (path.EndsWith("refresh"))
            {
                refreshes++;
                Check("refresh_uses_original_credential", request.Headers.Authorization is null
                    && (await request.Content!.ReadAsStringAsync()).Contains(Refresh));
                return Response(200, Session(true));
            }
            return request.Headers.Authorization?.Parameter == Access ? Response(401, new { error = "authentication_required" }) : Response(200, Envelope(3));
        }))
        using (var gateway = new OnlineGateway(new Uri("http://127.0.0.1/"), handler))
        {
            Check("fake_sign_in", (await gateway.SignInAsync("tester", "password-for-test", null, Ct)).IsSuccess);
            var replies = await Task.WhenAll(gateway.CurrentAsync(Ct), gateway.CurrentAsync(Ct));
            Check("parallel_reads_one_serial_refresh", refreshes == 1 && replies.All(x => x.IsSuccess && x.Match?.Version == 3));
        }
        var bodies = new List<string>(); var committed = 0; var reads = 0; var failOnce = true;
        using (var handler = new Handler(async request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("login")) return Response(200, Session());
            if (request.Method == HttpMethod.Get) return Response(200, Envelope(++reads == 1 ? 3 : 5));
            bodies.Add(await request.Content!.ReadAsStringAsync());
            if (failOnce) { failOnce = false; committed++; throw new HttpRequestException("Simulated lost response."); }
            return Response(200, Envelope(4));
        }))
        using (var gateway = new OnlineGateway(new Uri("http://127.0.0.1/"), handler))
        {
            var journey = new OnlineJourney(gateway);
            await journey.SignInAsync("tester", "password-for-test", null, Ct);
            var command = new OnlineCommand(Guid.NewGuid(), "leave_shop", Match, 3);
            var failed = await journey.SubmitAsync(command, Ct);
            Check("lost_response_keeps_original_command", failed.CanRetry && journey.Pending == command && journey.Current?.Version == 3);
            Check("new_operation_blocked_while_uncertain", (await journey.SubmitAsync(command with { RequestId = Guid.NewGuid() }, Ct)).Status == 409 && bodies.Count == 1);
            await journey.LoadAsync(Ct);
            Check("read_can_observe_newer_state_while_pending", journey.Current?.Version == 5 && journey.Pending == command);
            await journey.RetryAsync(Ct);
            Check("retry_reuses_exact_payload_and_cannot_rewind", committed == 1 && bodies.Count == 2 && bodies[0] == bodies[1]
                && journey.Pending is null && journey.Current?.Version == 5);
            Check("stale_ui_does_not_submit", (await journey.SubmitAsync(command, Ct)).Error == "client.stale_view" && bodies.Count == 2);
        }
        using (var handler = new Handler(request => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("login")
            ? Response(200, Session()) : request.Method == HttpMethod.Post ? Response(409, new { error = "version_conflict" }) : Response(200, Envelope(6)))))
        using (var gateway = new OnlineGateway(new Uri("http://127.0.0.1/"), handler))
        {
            var journey = new OnlineJourney(gateway); await journey.SignInAsync("tester", "password-for-test", null, Ct);
            var conflict = await journey.SubmitAsync(new OnlineCommand(Guid.NewGuid(), "leave_shop", Match, 6), Ct);
            Check("conflict_refreshes_state_without_automatic_resubmit", conflict.Status == 409 && journey.Pending is null && journey.Current?.Version == 6);
        }
        using (var handler = new Handler(request => request.RequestUri!.AbsolutePath.EndsWith("login") ? Task.FromResult(Response(200, Session()))
            : request.RequestUri.AbsolutePath.EndsWith("refresh") ? Task.FromException<HttpResponseMessage>(new HttpRequestException("Lost rotated credentials."))
            : Task.FromResult(Response(401, new { error = "authentication_required" }))))
        using (var gateway = new OnlineGateway(new Uri("http://127.0.0.1/"), handler))
        {
            await gateway.SignInAsync("tester", "password-for-test", null, Ct);
            var lost = await gateway.CurrentAsync(Ct);
            Check("uncertain_refresh_requires_new_login", lost.Error == "client.session_lost" && !gateway.IsAuthenticated);
        }
        using (var handler = new Handler(request => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("login") ? Response(200, Session())
            : new HttpResponseMessage(HttpStatusCode.Redirect) { Headers = { Location = new Uri("https://example.com/") } })))
        using (var gateway = new OnlineGateway(new Uri("http://127.0.0.1/"), handler))
        {
            await gateway.SignInAsync("tester", "password-for-test", null, Ct);
            Check("redirect_not_followed", (await gateway.CurrentAsync(Ct)).Error == "client.redirect_rejected");
            var logout = await gateway.SignOutAsync(Ct);
            Check("logout_failure_clears_local_session_without_claiming_revocation", !gateway.IsAuthenticated && logout.Error == "client.logout_uncertain");
        }
        using (var handler = new Handler(request => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("login") ? Response(200, Session())
            : request.Method == HttpMethod.Get ? Response(200, Envelope(3)) : Response(200, new { }))))
        using (var gateway = new OnlineGateway(new Uri("http://127.0.0.1/"), handler))
        {
            var journey = new OnlineJourney(gateway); await journey.SignInAsync("tester", "password-for-test", null, Ct);
            var reply = await journey.SubmitAsync(new OnlineCommand(Guid.NewGuid(), "buy", Match, 3, OfferIndex: 0), Ct);
            Check("unknown_success_response_keeps_request_for_retry", reply.Error == "client.invalid_response" && journey.Pending is not null);
        }
        var pvpId = Guid.NewGuid(); var currentReads = 0;
        using (var handler = new Handler(request => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("login") ? Response(200, Session())
            : request.RequestUri.AbsolutePath.StartsWith("/v1/battles/") ? Response(200, new { battleId = pvpId, status = "completed", result = Envelope(8, "PvpResult", pvpId) })
            : request.Method == HttpMethod.Post ? Response(202, Envelope(7, "PvpPending", pvpId))
            : ++currentReads == 1 ? Response(200, Envelope(6, "EncounterChoice")) : Response(200, Envelope(10, "PvpResult", pvpId)))))
        using (var gateway = new OnlineGateway(new Uri("http://127.0.0.1/"), handler))
        {
            var journey = new OnlineJourney(gateway); await journey.SignInAsync("tester", "password-for-test", null, Ct);
            var accepted = await journey.SubmitAsync(new OnlineCommand(Guid.NewGuid(), "start_pvp", Match, 6, EncounterKey: new Godot.StringName("encounter.pvp")), Ct);
            Check("accepted_pvp_exposes_fixed_task", accepted.Status == 202 && journey.Current?.State.Pvp?.Id == pvpId && journey.Current.State.Pvp.Turn == 8);
            await journey.PollAsync(Ct);
            Check("pvp_poll_applies_completed_result", journey.Current?.Version == 8 && journey.Current.State.Pvp?.Result?.Outcome == "PlayerVictory");
            await journey.LoadAsync(Ct); await journey.PollAsync(Ct);
            Check("historical_battle_result_cannot_rewind_reward_state", journey.Current?.Version == 10);
            var battleView = new OnlineAdventureView(); battleView.Configure(journey, DefinitionRegistry.Scan(typeof(GameMain).Assembly)); owner.AddChild(battleView);
            try
            {
                var summary = (Label)typeof(OnlineAdventureView).GetField("_summary", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(battleView)!;
                Check("battle_title_uses_frozen_boundary", summary.Text.StartsWith("第1轮 · 第8回合", StringComparison.Ordinal));
            }
            finally { owner.RemoveChild(battleView); battleView.Free(); }
        }
        var oldCreatedMatch = Guid.NewGuid(); var newerMatch = Guid.NewGuid(); var createReads = 0; var createAttempts = 0;
        using (var handler = new Handler(request =>
        {
            if (request.RequestUri!.AbsolutePath.EndsWith("login")) return Task.FromResult(Response(200, Session()));
            if (request.Method == HttpMethod.Get) return Task.FromResult(++createReads == 1 ? Response(404, new { error = "match_not_found" }) : Response(200, WithId(newerMatch, 9)));
            return ++createAttempts == 1 ? Task.FromException<HttpResponseMessage>(new HttpRequestException("Lost creation response.")) : Task.FromResult(Response(201, WithId(oldCreatedMatch, 1)));
        }))
        using (var gateway = new OnlineGateway(new Uri("http://127.0.0.1/"), handler))
        {
            var journey = new OnlineJourney(gateway); await journey.SignInAsync("tester", "password-for-test", null, Ct);
            await journey.SubmitAsync(new OnlineCommand(Guid.NewGuid(), "create", HeroKey: new StringName("hero.paladin")), Ct);
            await journey.LoadAsync(Ct); await journey.RetryAsync(Ct);
            Check("old_create_receipt_cannot_replace_new_active_match", journey.Current?.MatchId == newerMatch && journey.Current.Version == 9 && journey.Pending is null);
        }
        using (var handler = new Handler(request => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("login") ? Response(200, Session())
            : request.RequestUri.AbsolutePath.StartsWith("/v1/battles/") ? Response(200, new { battleId = pvpId, status = "failed", result = (object?)null })
            : Response(request.Method == HttpMethod.Post ? 202 : 200, Envelope(7, "PvpPending", pvpId)))))
        using (var gateway = new OnlineGateway(new Uri("http://127.0.0.1/"), handler))
        {
            var journey = new OnlineJourney(gateway); await journey.SignInAsync("tester", "password-for-test", null, Ct); await journey.PollAsync(Ct);
            Check("technical_failure_is_queryable", journey.BattleStatus == "failed");
            var retry = await journey.SubmitAsync(new OnlineCommand(Guid.NewGuid(), "retry_pvp", Match, 7, BattleId: pvpId), Ct);
            Check("accepted_retry_clears_stale_failure_without_new_battle", retry.Status == 202 && journey.BattleStatus.IsEmpty && journey.Current?.State.Pvp?.Id == pvpId);
        }
        var root = GD.Load<PackedScene>("res://Main.tscn").Instantiate<GameMain>(); owner.AddChild(root);
        try
        {
            var shell = root.GetNode<MatchShell>("MatchShell"); shell.EnableLobby();
            shell.GetNode<Button>("GameLobby/SceneStage/Online").EmitSignal(Button.SignalName.Pressed);
            var view = root.GetNode<OnlineAdventureView>("OnlineAdventure");
            Check("main_lobby_opens_online_overlay", view.Visible && !shell.Visible && shell.ProcessMode == Node.ProcessModeEnum.Disabled);
            var close = view.FindChildren("*", "Button", true, false).OfType<Button>().Single(x => x.Name == "Close");
            close.EmitSignal(Button.SignalName.Pressed);
            Check("closing_overlay_restores_same_main", !view.Visible && shell.Visible && root.GetNode<MatchShell>("MatchShell") == shell);
        }
        finally { owner.RemoveChild(root); root.Free(); }
        GD.Print($"ONLINE_CLIENT_CHECKS_PASSED={_checks}");
    }

    // 使用新建qa账号及现有云端规则，真实跑通购买、事件、怪物和PvP继续；不注入数值或存档。
    internal static async Task Cloud()
    {
        _checks = 0;
        var invitation = OS.GetEnvironment("PROJECT_STAR_TEST_INVITATION");
        if (invitation.Length == 0) throw new InvalidOperationException("Missing test invitation.");
        var username = "qa_client_" + Guid.NewGuid().ToString("N")[..9];
        var password = Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        using var gateway = new OnlineGateway(new Uri("http://127.0.0.1:15080/"));
        var journey = new OnlineJourney(gateway);
        Check("cloud_register_and_restore_empty_account", (await journey.SignInAsync(username, password, invitation, Ct)).IsSuccess && journey.Current is null);
        await Send("create", hero: new StringName("hero.paladin"));
        Check("cloud_formal_start_has_five_gold", journey.Current!.State.Wealth == 5 && journey.Current.State.Round == 1 && journey.Current.State.Turn == 1);
        var matchId = journey.Current.MatchId; var bought = false; var eventSeen = false; var monsterSeen = false; var pvpSeen = false;
        for (var step = 0; step < 70; step++)
        {
            var current = journey.Current!; var state = current.State;
            if (state.Round == 2 && state.Phase == "EncounterChoice") break;
            switch (state.Phase.ToString())
            {
                case "EncounterChoice":
                    var choice = (!eventSeen ? state.Choices.FirstOrDefault(x => x.IsChoiceEvent && (x.Key == "encounter.landfill" || x.Key == "encounter.training_ground")) : null)
                        ?? state.Choices.FirstOrDefault(x => x.IsCardShop) ?? state.Choices.First(x => x.Kind == "Monster" || x.Kind == "Pvp");
                    await Send(choice.IsCardShop ? "select_shop" : choice.IsChoiceEvent ? "select_event" : choice.Kind == "Monster" ? "select_monster" : "start_pvp", encounter: choice.Key);
                    break;
                case "Shop":
                    var offer = state.Shop!.Offers.FirstOrDefault(x => !x.Sold && x.Price <= state.Wealth);
                    if (!bought && offer is not null)
                    {
                        var command = Command("buy", offer: offer.Index);
                        var reply = await journey.SubmitAsync(command, Ct); Check("cloud_purchase_commits", reply.IsSuccess);
                        var duplicate = await gateway.SendAsync(command, Ct);
                        Check("cloud_duplicate_purchase_receipt_same_version", duplicate.Match?.Version == journey.Current!.Version);
                        await journey.LoadAsync(Ct);
                        Check("cloud_purchase_restore_keeps_card_and_debit", journey.Current!.State.Cards.Count > 0 && journey.Current.State.Wealth == state.Wealth - offer.Price);
                        bought = true;
                    }
                    await Send("leave_shop"); break;
                case "Event":
                    if (!state.Visit!.Resolved) { await Send("resolve_event", visit: state.Visit.Id, option: state.Visit.Options[0].Key); eventSeen = true; }
                    else await Send("leave_event", visit: state.Visit.Id);
                    break;
                case "MonsterPreparation": await Send("resolve_monster", battle: state.Monster!.Id); monsterSeen = true; break;
                case "MonsterResult":
                    if (state.Rewards.Count > 0) await Send("claim_reward", battle: state.Monster!.Id, reward: state.Rewards[0].Index);
                    else await Send("finish_monster", battle: state.Monster!.Id);
                    break;
                case "PvpPending":
                    Check("cloud_pvp_job_accepted", state.Pvp is not null);
                    for (var attempt = 0; attempt < 120 && journey.Current!.State.Phase == "PvpPending"; attempt++)
                    { var reply = await journey.PollAsync(Ct); if (!reply.IsSuccess || journey.BattleStatus == "failed") throw new InvalidOperationException("Cloud PvP did not complete."); await Task.Delay(250); }
            Check("cloud_fixed_job_completes_without_client_result_submission", journey.Current!.State.Phase == "PvpResult" && journey.Current.State.Pvp!.Id == state.Pvp!.Id);
                    pvpSeen = true; break;
                case "PvpResult":
                    if (state.Rewards.Count > 0) await Send("claim_reward", battle: state.Pvp!.Id, reward: state.Rewards[0].Index);
                    else await Send("finish_pvp", battle: state.Pvp!.Id);
                    break;
                default: throw new InvalidOperationException("Unexpected cloud phase.");
            }
        }
        Check("cloud_normal_path_enters_round_two", bought && monsterSeen && pvpSeen && journey.Current!.State.Round == 2 && journey.Current.State.Turn == 1);
        Check("cloud_same_match_preserved", journey.Current.MatchId == matchId);
        if (journey.Current.State.Cards.FirstOrDefault() is { } owned)
        {
            var before = journey.Current;
            var moved = await journey.SubmitAsync(new OnlineCommand(Guid.NewGuid(), "move_card", before.MatchId, before.Version,
                CardId: owned.Id, TargetZone: new StringName("Bench"), TargetStart: 0), Ct);
            Check("cloud_move_uses_authoritative_board", moved.IsSuccess && journey.Current!.State.Cards.Single(x => x.Id == owned.Id).Zone == "Bench");
            before = journey.Current!;
            var returned = await journey.SubmitAsync(new OnlineCommand(Guid.NewGuid(), "move_card", before.MatchId, before.Version,
                CardId: owned.Id, TargetZone: new StringName("Battlefield"), TargetStart: owned.Start ?? 0), Ct);
            Check("cloud_return_card_to_battlefield", returned.IsSuccess && journey.Current!.State.Cards.Single(x => x.Id == owned.Id).Zone == "Battlefield");
            before = journey.Current!;
            var sold = await journey.SubmitAsync(new OnlineCommand(Guid.NewGuid(), "sell_card", before.MatchId, before.Version, CardId: owned.Id), Ct);
            Check("cloud_sale_removes_server_owned_instance", sold.IsSuccess && journey.Current!.State.Cards.All(x => x.Id != owned.Id));
        }
        var refreshField = typeof(OnlineGateway).GetField("_refresh", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var originalRefresh = (string)refreshField.GetValue(gateway)!;
        typeof(OnlineGateway).GetField("_access", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(gateway, new string('x', 43));
        var restored = await Task.WhenAll(gateway.CurrentAsync(Ct), gateway.CurrentAsync(Ct));
        Check("cloud_parallel_read_rotates_real_session", restored.All(x => x.IsSuccess && x.Match?.MatchId == matchId) && (string)refreshField.GetValue(gateway)! != originalRefresh);
        var version = journey.Current.Version;
        Check("cloud_logout", (await journey.SignOutAsync(Ct)).IsSuccess && !journey.IsAuthenticated);
        Check("cloud_relogin_restores_same_match", (await journey.SignInAsync(username, password, null, Ct)).IsSuccess
            && journey.Current?.MatchId == matchId && journey.Current.Version == version);
        await journey.SignOutAsync(Ct);
        GD.Print($"ONLINE_CLOUD_CHECKS_PASSED={_checks}; account={username}; match={matchId}; eventSeen={eventSeen}");

        OnlineCommand Command(StringName type, StringName? hero = null, StringName? encounter = null, int? offer = null,
            Guid? battle = null, int? reward = null, Guid? visit = null, StringName? option = null) =>
            new(Guid.NewGuid(), type, journey.Current?.MatchId, journey.Current?.Version, hero, encounter, offer, battle, reward, visit, option);
        async Task Send(StringName type, StringName? hero = null, StringName? encounter = null, Guid? battle = null,
            int? reward = null, Guid? visit = null, StringName? option = null)
        { var reply = await journey.SubmitAsync(Command(type, hero, encounter, battle: battle, reward: reward, visit: visit, option: option), Ct); Check("cloud_" + type, reply.IsSuccess); }
    }

    // 原生UI按HTML检查空账号与商品视图，只使用受控展示夹具。
    internal static async Task Capture(Control owner)
    {
        using var handler = new Handler(request => Task.FromResult(request.RequestUri!.AbsolutePath.EndsWith("login") ? Response(200, Session()) : Response(200, Envelope(3))));
        using var gateway = new OnlineGateway(new Uri("http://127.0.0.1/"), handler);
        var journey = new OnlineJourney(gateway);
        var view = new OnlineAdventureView(); view.Configure(journey, DefinitionRegistry.Scan(typeof(GameMain).Assembly)); owner.AddChild(view);
        try
        {
            foreach (var size in new[] { new Vector2I(1280, 720), new Vector2I(1600, 900) })
            {
                owner.GetWindow().Size = size; owner.GetTree().Root.ContentScaleSize = size;
                await Frame(); await Frame(); await Save("login");
                Check("online_view_inside_viewport_" + size.X, owner.GetViewportRect().Encloses(view.GetGlobalRect()));
            }
            var fields = view.FindChildren("*", "LineEdit", true, false).OfType<LineEdit>().ToArray();
            await Type(fields.Single(x => x.Name == "Username"), "tester");
            await Type(fields.Single(x => x.Name == "Password"), "password-for-test");
            var login = view.FindChildren("*", "Button", true, false).OfType<Button>().Single(x => x.Name == "Login");
            await Frame(); await Save("ready");
            var point = login.GetGlobalRect().GetCenter();
            owner.GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = true }, true);
            owner.GetViewport().PushInput(new InputEventMouseButton { Position = point, GlobalPosition = point, ButtonIndex = MouseButton.Left, Pressed = false }, true);
            await Frame(); await Frame(); await Save("shop");
            Check("native_login_click_and_password_clear", journey.IsAuthenticated && fields.Single(x => x.Name == "Password").Text.Length == 0);
            var create = view.FindChildren("*", "Button", true, false).OfType<Button>().Single(x => x.Name == "Create");
            Check("active_match_blocks_second_creation", create.Disabled);
            var purchase = view.FindChildren("*", "Button", true, false).OfType<Button>().Single(x => x.Text == "购买");
            var rect = purchase.GetGlobalRect(); purchase.GrabFocus(); await Frame();
            Check("online_button_focus_keeps_geometry", purchase.GetGlobalRect() == rect);
            await Save("focus");
        }
        finally { owner.RemoveChild(view); view.Free(); }
        async Task Frame() => await owner.ToSignal(owner.GetTree(), SceneTree.SignalName.ProcessFrame);
        async Task Type(LineEdit field, string value)
        {
            field.GrabFocus(); await Frame();
            foreach (var character in value)
                owner.GetViewport().PushInput(new InputEventKey { Unicode = character, Pressed = true }, true);
            await Frame(); Check("native_type_" + field.Name, field.Text == value);
        }
        async Task Save(string name)
        {
            await owner.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            using var image = owner.GetViewport().GetTexture().GetImage();
            Check("save_online_" + name, image.SavePng($"res://output/online-client/native-{name}-{image.GetWidth()}.png") == Error.Ok);
        }
    }
    private static object Session(bool next = false) => new { playerId = Player, accessToken = next ? NextAccess : Access, refreshToken = next ? NextRefresh : Refresh };
    private static object WithId(Guid id, int version)
    {
        var node = JsonSerializer.SerializeToNode(Envelope(version))!;
        node["matchId"] = id.ToString();
        return node;
    }
    private static object Envelope(int version, string phase = "Shop", Guid? battleId = null) => new
    {
        matchId = Match, version, rulesVersion = new string('a', 64),
        state = new { phase, status = "InProgress", round = battleId.HasValue ? 2 : 1, turn = battleId.HasValue ? 1 : 2, wealth = 5, experience = 1, reputation = 10, pvpWins = 0,
            cards = Array.Empty<object>(), choices = Array.Empty<object>(), pendingRewards = Array.Empty<object>(),
            pvp = battleId is null ? null : new { id = battleId, round = 1, isTestOpponent = false,
                opponent = new { name = "真实快照对手", cards = Array.Empty<object>() },
                result = phase == "PvpResult" ? new { outcome = "PlayerVictory", playerRemainingHealth = 100, opponentRemainingHealth = 0 } : null },
            shop = new { canRefresh = true, refreshCost = 2, offers = new[] { new { index = 0, key = "card.assassin_dagger", name = "刺客匕首", level = 1, price = 3, sold = false } } } }
    };
    private static HttpResponseMessage Response(int status, object body) => new((HttpStatusCode)status)
        { Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json") };
    private static void Check(string name, bool success) { if (!success) throw new InvalidOperationException(name); _checks++; GD.Print("PASS " + name); }
    // 在真实HttpClient边界注入故障，不复制服务器模拟或结算规则（验证模块）。
    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) => send(request);
    }
}
