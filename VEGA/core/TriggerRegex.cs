using System.Text.RegularExpressions;

namespace Core;

/// <summary>
/// Centralizes regex policy for user-supplied trigger patterns:
/// timeout cap, whitelist of allowed RegexOptions flags, and a one-shot
/// validation used at creation time.
/// </summary>
public static class TriggerRegex
{
    /// <summary>
    /// Hard cap on a single regex evaluation. Tight enough to neutralize
    /// catastrophic backtracking on the gateway hot path.
    /// </summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromMilliseconds(100);

    /// <summary>
    /// Bits the user is allowed to set on a trigger's RegexOptions.
    /// Excludes Compiled (leaks dynamic assemblies per pattern) and
    /// any non-flag value the runtime may add later.
    /// </summary>
    private const int AllowedMask = (int)(
        RegexOptions.IgnoreCase
        | RegexOptions.Multiline
        | RegexOptions.Singleline
        | RegexOptions.IgnorePatternWhitespace
        | RegexOptions.ECMAScript
        | RegexOptions.CultureInvariant
        | RegexOptions.RightToLeft
    );

    public static RegexOptions Sanitize(int rawOptions) =>
        (RegexOptions)(rawOptions & AllowedMask);

    /// <summary>
    /// Throws if the pattern fails to compile or trips the timeout on a benign input.
    /// Caller is expected to translate the exception into a user-facing error.
    /// </summary>
    public static void Validate(string pattern, int rawOptions)
    {
        var options = Sanitize(rawOptions);
        var regex = new Regex(pattern, options, Timeout);
        // Runs once on a short benign input to surface obvious ReDoS at creation
        // rather than letting it fire on every guild message.
        regex.IsMatch("vega-trigger-validation");
    }
}
