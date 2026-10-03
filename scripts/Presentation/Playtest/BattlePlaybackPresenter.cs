using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Project_Star.Application.Combat;
using Project_Star.Application.Match;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;

namespace Project_Star.Presentation.Playtest;

// 回放的只读刷新数据，不包含可变对局或结算服务。
public sealed record BattlePlaybackViewModel(long Tick, bool Paused, int Speed, bool Completed,
    BattleStateSnapshot State, IReadOnlyList<string> Feedback, IReadOnlyDictionary<EntityId, long> Activations);

// 仅推进冻结记录的游标；暂停、倍速和跳过均不执行玩法计算。
public sealed class BattlePlaybackPresenter
{
    private readonly BattleResolution _source;
    private readonly List<string> _feedback = new();
    private readonly Dictionary<EntityId, long> _activations = new();
    private int _frameIndex = 1;
    private int _eventIndex;
    private double _seconds;
    private BattleStateSnapshot _state;
    public bool Paused { get; private set; }
    public int Speed { get; private set; } = 1;
    public bool Completed => _frameIndex == _source.Result.States.Count;
    public long Tick => Math.Min(_source.Result.EndedAt.Value, (long)Math.Floor(_seconds * 10 + 1e-7));
    public BattleResolution Source => _source;

    // 初始画面使用战前身份和首份战斗状态，包括下场战斗生命加成。
    public BattlePlaybackPresenter(BattleResolution source)
    {
        _source = source;
        if (source.Result.States.Count == 0) throw new ArgumentException("Battle playback requires captured states.", nameof(source));
        _state = source.Result.States[0];
    }

    // 返回游标和集合的冻结副本，后续播放不改变之前的展示数据。
    public BattlePlaybackViewModel Capture() => new(Tick, Paused, Speed, Completed, _state,
        Array.AsReadOnly(_feedback.ToArray()), new ReadOnlyDictionary<EntityId, long>(new Dictionary<EntityId, long>(_activations)));

    // 暂停仅改变播放状态。
    public void TogglePause() { if (!Completed) Paused = !Paused; }

    // 首版只提供1×和2×速度。
    public void ToggleSpeed() => Speed = Speed == 1 ? 2 : 1;

    // 按原始快照顺序推进，并在每份快照前消费对应的日志前缀。
    public bool Advance(double seconds)
    {
        if (Paused || Completed || !double.IsFinite(seconds) || seconds < 0) return false;
        var before = Tick;
        _seconds = Math.Min((double)_source.Result.EndedAt.ToSeconds(), _seconds + seconds * Speed);
        var changed = Consume(Tick);
        return changed || Tick != before;
    }

    // 跳过同样消费全部记录，只跳过等待时间。
    public void Skip()
    {
        _seconds = (double)_source.Result.EndedAt.ToSeconds();
        Consume(_source.Result.EndedAt.Value);
    }

    private bool Consume(long tick)
    {
        var changed = false;
        while (_frameIndex < _source.Result.States.Count && _source.Result.States[_frameIndex].Tick.Value <= tick)
        {
            var next = _source.Result.States[_frameIndex++];
            while (_eventIndex < next.EventCount)
            {
                var item = _source.Result.Events[_eventIndex++];
                switch (item)
                {
                    case AbilityActivatedEvent ability:
                        _activations[ability.SourceCardId] = ability.Tick.Value;
                        Add($"{Name(ability.SourceCardId)}{(ability.IsEcho ? "回响" : "发动")}"); break;
                    case DamageDealtEvent damage:
                        Add($"{Side(damage.TargetSide)}受伤 {damage.HealthDamage} · 护甲抵消 {damage.ArmorAbsorbed}"
                            + (damage.SourceKind == DamageSourceKind.Eclipse ? " · 日蚀" : "")); break;
                    case CardDestroyedEvent destroyed: Add($"{Name(destroyed.CardId)}已摧毁"); break;
                    case CardTransformedEvent transformed:
                        Add($"{Name(transformed.TargetCardId)}转变为{next.Cards.First(card => card.Id == transformed.TargetCardId).TransformedIdentity!.DisplayName}"); break;
                    case CardChargedEvent charged: Add($"{Name(charged.TargetCardId)}充能 {charged.AmountTicks / 10m:0.0}秒"); break;
                }
            }
            Recovery(_state.Player, next.Player, "我方"); Recovery(_state.Opponent, next.Opponent, "敌方");
            _state = next; changed = true;
        }
        return changed;
    }

    private void Recovery(HeroBattleSnapshot before, HeroBattleSnapshot after, string side)
    {
        if (after.Health > before.Health) Add($"{side}治疗 +{after.Health - before.Health}");
        if (after.Armor > before.Armor) Add($"{side}护甲 +{after.Armor - before.Armor}");
    }
    private void Add(string text) { _feedback.Add(text); if (_feedback.Count > 4) _feedback.RemoveAt(0); }
    private string Name(EntityId id) => _state.Cards.FirstOrDefault(card => card.Id == id)?.TransformedIdentity?.DisplayName
        ?? _source.PlayerBefore.Cards.Concat(_source.OpponentBefore.Cards)
        .FirstOrDefault(card => card.Id == id)?.DisplayName ?? "技能/套装";
    private static string Side(SideId side) => side == SideId.Player ? "我方" : "敌方";

    // 只把战斗记录中的卡牌最终属性投影进战前展示快照。
    public MatchSnapshot Project(SideId side)
    {
        var before = side == SideId.Player ? _source.PlayerBefore : _source.OpponentBefore;
        return before with { Cards = Array.AsReadOnly(before.Cards.Select(card =>
        {
            var state = _state.Cards.FirstOrDefault(item => item.Id == card.Id && item.Side == side);
            if (state?.TransformedIdentity is { } identity)
                return card with
                {
                    Key = identity.Key, DisplayName = identity.DisplayName, Level = state.Level,
                    Size = identity.Size, FactionKey = identity.FactionKey, ElementKeys = identity.ElementKeys,
                    SetKey = identity.SetKey, Illustration = identity.Illustration,
                    DescriptionEntries = identity.DescriptionEntries, Tags = state.TransformedTags,
                    BaseValues = state.Values, CurrentValues = state.Values, Abilities = state.TransformedAbilities,
                    IsFlying = state.IsFlying, IsBerserk = state.IsBerserk, CooldownMultiplier = 1m,
                    Quests = Array.Empty<QuestProgressSnapshot>(),
                    GemSockets = Array.AsReadOnly(new GemSnapshot?[identity.GemSocketCount]),
                };
            return state is null ? card : card with
                { CurrentValues = state.Values, IsFlying = state.IsFlying, IsBerserk = state.IsBerserk };
        }).ToArray()) };
    }
}
