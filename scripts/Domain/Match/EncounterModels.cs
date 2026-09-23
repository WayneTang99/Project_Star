using Godot;
using Project_Star.Domain.Definitions;

namespace Project_Star.Domain.Match;

public sealed record EncounterChoice(StringName Key, string DisplayName, EncounterKind Kind);

public interface IMatchEvent;

public sealed record EncounterChoicesGeneratedEvent(int Round, int Turn, int Count) : IMatchEvent;

public sealed record EncounterSelectedEvent(StringName EncounterKey, EncounterKind Kind) : IMatchEvent;

public sealed record TurnAdvancedEvent(int Round, int Turn) : IMatchEvent;

public sealed record IncomeGrantedEvent(int Round, int Amount, int CurrentWealth) : IMatchEvent;

public sealed record EncounterSelectionResult(EncounterChoice Choice, bool RequiresBattle);
