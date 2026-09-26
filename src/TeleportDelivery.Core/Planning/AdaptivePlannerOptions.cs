namespace TeleportDelivery.Core.Planning;

/// <summary>
/// Settings for <see cref="AdaptivePlanner"/>.
/// </summary>
public sealed record AdaptivePlannerOptions
{
    /// <summary>
    /// The settings used when none are given.
    /// </summary>
    public static AdaptivePlannerOptions Default { get; } = new();

    /// <summary>
    /// How many price splits to try, spread evenly from all-volume to all-weight.
    /// </summary>
    public int PriceCandidates { get; init; } = 13;

    /// <summary>
    /// At most how many estimates get a full plan built, beside the fixed-price plan.
    /// </summary>
    public int Finalists { get; init; } = 5;

    /// <summary>
    /// How long the search may start new work; a plan already being built is finished.
    /// </summary>
    public TimeSpan TimeBudget { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>
    /// Throws when a count is below one.
    /// </summary>
    public AdaptivePlannerOptions Validated()
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(PriceCandidates, 1, nameof(PriceCandidates));
        ArgumentOutOfRangeException.ThrowIfLessThan(Finalists, 1, nameof(Finalists));
        return this;
    }
}
