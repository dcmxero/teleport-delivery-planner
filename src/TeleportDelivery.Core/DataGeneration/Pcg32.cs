namespace TeleportDelivery.Core.DataGeneration;

/// <summary>
/// A small PCG random generator for test data.
/// </summary>
/// <remarks>
/// Not <see cref="Random"/>, whose algorithm is not guaranteed across .NET releases; the
/// same seed must give the same parcels as the published figures.
/// </remarks>
/// <param name="seed">Chooses where in the stream to start.</param>
/// <param name="sequence">
/// Which independent stream to draw from; the same seed on another stream is unrelated.
/// </param>
public sealed class Pcg32(ulong seed, ulong sequence = 1)
{
    private const ulong Multiplier = 6364136223846793005UL;

    /// <summary>
    /// The stream increment, always odd so the full period is visited.
    /// </summary>
    private readonly ulong increment = (sequence << 1) | 1UL;

    private ulong state = InitialState(seed, (sequence << 1) | 1UL);

    private double? spareGaussian;

    /// <summary>
    /// The standard PCG seeding: advance, add the seed, advance again.
    /// </summary>
    /// <remarks>
    /// Written out from a zero state to match the reference implementation step by step.
    /// </remarks>
    private static ulong InitialState(ulong seed, ulong increment) => unchecked(
        (((0UL * Multiplier) + increment) + seed) * Multiplier + increment);

    /// <summary>
    /// Draws the next 32 bits.
    /// </summary>
    public uint NextUInt32()
    {
        ulong previous = state;
        state = unchecked((state * Multiplier) + increment);

        // PCG's XSH-RR output function.
        uint xorshifted = (uint)(((previous >> 18) ^ previous) >> 27);
        int rotation = (int)(previous >> 59);
        return (xorshifted >> rotation) | (xorshifted << ((-rotation) & 31));
    }

    /// <summary>
    /// Draws uniformly from [0, 1) with 53 bits of precision.
    /// </summary>
    public double NextDouble()
    {
        ulong high = (ulong)(NextUInt32() >> 5);
        ulong low = NextUInt32() >> 6;
        return ((high << 26) | low) * (1.0 / 9007199254740992.0);
    }

    /// <summary>
    /// Draws from the standard normal distribution using the Marsaglia polar method.
    /// </summary>
    /// <remarks>
    /// The method yields two values; the second is kept for the next call.
    /// </remarks>
    public double NextGaussian()
    {
        if (spareGaussian is { } spare)
        {
            spareGaussian = null;
            return spare;
        }

        double u, v, squaredRadius;
        do
        {
            u = (2.0 * NextDouble()) - 1.0;
            v = (2.0 * NextDouble()) - 1.0;
            squaredRadius = (u * u) + (v * v);
        }
        while (squaredRadius is <= 0.0 or >= 1.0);

        double scale = Math.Sqrt(-2.0 * Math.Log(squaredRadius) / squaredRadius);
        spareGaussian = v * scale;
        return u * scale;
    }
}
