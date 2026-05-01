namespace Larchik.Application.Helpers;

/// <summary>
/// Void success payload for <see cref="Result{T}"/> on commands (replaces MediatR.Unit).
/// </summary>
public readonly struct Unit
{
    public static Unit Value => default;
}
