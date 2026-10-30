using Aetheris.Core.Randomness;

namespace Aetheris.Tests.Infrastructure;

/// <summary>
/// The random source of the test host: it gives the numbers a test scripted, in order, and
/// asks the system source when nothing is scripted, so a roll of the game is repeatable where
/// a test needs it and untouched elsewhere (task 00014, section 11).
/// </summary>
public sealed class ScriptedRandomSource : IRandomSource
{
    private readonly object _lock = new();
    private readonly SystemRandomSource _system = new();
    private Queue<double>? _script;

    /// <summary>
    /// Scripts the next numbers; once they are used up the source goes back to the system one.
    /// </summary>
    /// <param name="numbers">The numbers, each in <c>[0, 1)</c>.</param>
    public void Script(params double[] numbers)
    {
        lock (_lock)
        {
            _script = new Queue<double>(numbers);
        }
    }

    /// <summary>
    /// Drops what is left of the script.
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _script = null;
        }
    }

    /// <inheritdoc />
    public double NextDouble()
    {
        lock (_lock)
        {
            if (_script is { Count: > 0 })
            {
                return _script.Dequeue();
            }
        }

        return _system.NextDouble();
    }
}
