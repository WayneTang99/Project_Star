using Godot;

namespace Project_Star.Application.Common;

/// <summary>Describes why an application operation could not be completed.</summary>
public sealed record Failure(StringName Code, string Message);
