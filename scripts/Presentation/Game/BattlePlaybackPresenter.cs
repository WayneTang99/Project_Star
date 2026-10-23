using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Project_Star.Application.Combat;
using Project_Star.Application.Match;
using Project_Star.Domain.Combat;
using Project_Star.Domain.Common;
using Project_Star.Domain.Match;

namespace Project_Star.Presentation.Game;

// 回放的只读刷新数据，不包含可变对局或结算服务。
public sealed record BattlePlaybackViewModel(long Tick, bool Paused, int Speed, bool Completed,
    BattleStateSnapshot State, IReadOnlyList<string> Feedback, IReadOnlyDictionary<EntityId, long> Activations)
{
    public IReadOnlyList<BattleEvent> VisualEvents { get; init; } = Array.Empty<BattleEvent>();
}

// 仅推进冻结记录的游标；暂停、倍速和跳过均不执行玩法计算。
public sealed class BattlePlaybackPresenter
{
    private readonly BattleResolution _source;
    private readonly List<string> _feedback = new();
    private readonly Dictionary<EntityId, long> _activations = new();
    private readonly List<BattleEvent> _visualEvents = new();
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
        Array.AsReadOnly(_feedback.ToArray()), new ReadOnlyDictionary<EntityId, long>(new Dictionary<EntityId, long>(_activations)))
        { VisualEvents = Array.AsReadOnly(_visualEvents.Where(item => item.Tick.Value >= Tick - 8).ToArray()) };

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
                if (item is CardChargedEvent or DamageDealtEvent) _visualEvents.Add(item);
                switch (item)
                {
                    case AbilityActivatedEvent ability:
                        _activations[ability.SourceCardId] = ability.Tick.Value;
                        Add($"{Name(ability.SourceCardId)}{(ability.IsEcho ? "回响" : "发动")}"); break;
                    case DamageDealtEvent damage:
                        Add($"{Side(damage.TargetSide)}受伤 {damage.HealthDamage} · 护甲抵消 {damage.ArmorAbsorbed}"
                            + (damage.SourceKind == DamageSourceKind.Eclipse ? " · 日蚀" : "")); break;
                    case AttackBlockedEvent blocked: Add($"{Side(blocked.TargetSide)}隐身避开攻击"); break;
                    case HeroStealthChangedEvent stealth:
                        Add(stealth.RemainingTicks > 0 ? $"{Side(stealth.Side)}隐身 {stealth.RemainingTicks / 10m:0.0}秒"
                            : $"{Side(stealth.Side)}隐身结束"); break;
                    case CardDestroyedEvent destroyed: Add($"{Name(destroyed.CardId)}已摧毁"); break;
                    case CardRepairedEvent repaired: Add($"{Name(repaired.CardId)}已修复"); break;
                    case CardUpgradedEvent upgraded: Add($"{Name(upgraded.CardId)}升至{upgraded.Level}级"); break;
                    case CardSummonedEvent summoned:
                        Add($"{Name(summoned.SourceCardId)}召唤{next.Cards.First(card => card.Id == summoned.SummonedCardId).SummonedCard!.Identity.DisplayName}"); break;
                    case CardTransformedEvent transformed:
                        Add($"{Name(transformed.TargetCardId)}转变为{next.Cards.First(card => card.Id == transformed.TargetCardId).TransformedIdentity!.DisplayName}"); break;
                    case CardChargedEvent charged: Add($"{Name(charged.TargetCardId)}充能 {charged.AmountTicks / 10m:0.0}秒"); break;
                }
            }
            Recovery(_state.Player, next.Player, "我方"); Recovery(_state.Opponent, next.Opponent, "敌方");
            _state = next; changed = true;
        }
        _visualEvents.RemoveAll(item => item.Tick.Value < tick - 8);
        return changed;
    }

    private void Recovery(HeroBattleSnapshot before, HeroBattleSnapshot after, string side)
    {
        if (after.Health > before.Health) Add($"{side}治疗 +{after.Health - before.Health}");
        if (after.Armor > before.Armor) Add($"{side}护甲 +{after.Armor - before.Armor}");
    }
    private void Add(string text) { _feedback.Add(text); if (_feedback.Count > 4) _feedback.RemoveAt(0); }
    private string Name(EntityId id) => _state.Cards.FirstOrDefault(card => card.Id == id)?.TransformedIdentity?.DisplayName
        ?? _state.Cards.FirstOrDefault(card => card.Id == id)?.SummonedCard?.Identity.DisplayName
        ?? _source.PlayerBefore.Cards.Concat(_source.OpponentBefore.Cards)
        .FirstOrDefault(card => card.Id == id)?.DisplayName ?? "技能/套装";
    private static string Side(SideId side) => side == SideId.Player ? "我方" : "敌方";

    // 只把战斗记录中的卡牌最终属性投影进战前展示快照。
    public MatchSnapshot Project(SideId side)
    {
        var before = side == SideId.Player ? _source.PlayerBefore : _source.OpponentBefore;
        var summonedStates = _state.Cards.Where(card => card.Side == side && card.SummonedCard is not null).ToArray();
        var summonedCards = summonedStates.Select(state =>
        {
            var summoned = state.SummonedCard!;
            var identity = summoned.Identity;
            return new CardSnapshot(state.Id, identity.Key, identity.DisplayName, state.Level, 0,
                identity.Size, identity.FactionKey, identity.ElementKeys, Array.Empty<QuestProgressSnapshot>())
            {
                SetKey = identity.SetKey, Illustration = identity.Illustration, DescriptionEntries = identity.DescriptionEntries,
                Tags = summoned.Tags, BaseValues = summoned.BaseValues, CurrentValues = state.Values, Abilities = summoned.Abilities,
                QuestDefinitions = state.QuestDefinitions,
                IsFlying = state.IsFlying, IsBerserk = state.IsBerserk,
                GemSockets = Array.AsReadOnly(new GemSnapshot?[identity.GemSocketCount]),
            };
        });
        var placements = before.BoardPlacements.ToList();
        foreach (var item in _source.Result.Events.Take(_state.EventCount))
        {
            BoardPlacementSnapshot? restored = null;
            if (item is CardTransformedEvent transform)
            {
                var transformed = _state.Cards.FirstOrDefault(card => card.Id == transform.TargetCardId && card.Side == side);
                var placementIndex = placements.FindIndex(placement => placement.CardId == transform.TargetCardId);
                if (placementIndex >= 0 && transformed?.TransformedIdentity is { } identity)
                {
                    var placement = placements[placementIndex];
                    placements[placementIndex] = placement with { EndExclusive = placement.Start + identity.OccupiedSlots };
                }
            }
            if (item is CardSummonedEvent summon && summon.Side == side)
            {
                var state = summonedStates.Single(card => card.Id == summon.SummonedCardId);
                restored = new BoardPlacementSnapshot(state.Id, BoardZone.Battlefield, summon.BoardStart,
                    summon.BoardStart + state.SummonedCard!.Identity.OccupiedSlots);
            }
            if (item is CardRepairedEvent repair && repair.Side == side)
                restored = new BoardPlacementSnapshot(repair.CardId, BoardZone.Battlefield,
                    repair.BoardStart, repair.BoardStart + repair.OccupiedSlots);
            if (restored is null) continue;
            placements.RemoveAll(placement => placement.Zone == BoardZone.Battlefield && placement.Start < restored.EndExclusive
                && restored.Start < placement.EndExclusive);
            placements.Add(restored);
        }
        return before with { BoardPlacements = placements.AsReadOnly(), BattlefieldCount = placements.Count(item => item.Zone == BoardZone.Battlefield),
            Cards = Array.AsReadOnly(before.Cards.Concat(summonedCards).Select(card =>
        {
            var state = _state.Cards.FirstOrDefault(item => item.Id == card.Id && item.Side == side);
            if (state?.TransformedIdentity is { } identity)
                card = card with
                {
                    Key = identity.Key, DisplayName = identity.DisplayName, Level = state.Level,
                    Size = identity.Size, FactionKey = identity.FactionKey, ElementKeys = identity.ElementKeys,
                    SetKey = identity.SetKey, Illustration = identity.Illustration,
                    DescriptionEntries = identity.DescriptionEntries, Tags = state.TransformedTags,
                    BaseValues = state.Values, CurrentValues = state.Values, Abilities = state.TransformedAbilities,
                    IsFlying = state.IsFlying, IsBerserk = state.IsBerserk, CooldownMultiplier = 1m,
                    Quests = Array.Empty<QuestProgressSnapshot>(),
                    QuestDefinitions = state.QuestDefinitions,
                    GemSockets = Array.AsReadOnly(new GemSnapshot?[identity.GemSocketCount]),
                };
            if (state is null) return card;
            if (state.UpgradedAbilities.Count > 0)
                card = card with
                {
                    Level = state.Level, Abilities = state.UpgradedAbilities, CooldownMultiplier = state.CooldownMultiplier,
                    BaseValues = state.TransformedIdentity is not null ? card.BaseValues
                        : new ReadOnlyDictionary<Godot.StringName, int>(card.BaseValues.Keys.Concat(state.UpgradeBaseChanges.Keys).Distinct()
                            .ToDictionary(key => key, key => checked((card.BaseValues.TryGetValue(key, out var value) ? value : 0)
                                + (state.UpgradeBaseChanges.TryGetValue(key, out var change) ? change : 0)))),
                };
            var projected = card with { CurrentValues = state.Values, EffectAmounts = state.EffectAmounts,
                CurrentCooldownSeconds = state.CooldownDurationUnits.Count > 0 ? state.CooldownDurationUnits.Max() / 20m : null,
                IsFlying = state.IsFlying, IsBerserk = state.IsBerserk, Tags = state.QuestTags,
                ElementKeys = state.QuestElementKeys.Count > 0 ? state.QuestElementKeys : card.ElementKeys };
            return state.Quests.Count == 0 ? projected : projected with
            {
                ElementKeys = state.QuestElementKeys, Tags = state.QuestTags, Abilities = state.QuestAbilities,
                Quests = Array.AsReadOnly(state.Quests.Select(quest => new QuestProgressSnapshot(
                    quest.Key, quest.Progress, quest.RequiredCount, quest.Unlocked)).ToArray()),
                QuestDefinitions = state.QuestDefinitions.Count > 0 ? state.QuestDefinitions : projected.QuestDefinitions,
            };
        }).ToArray()) };
    }
}
