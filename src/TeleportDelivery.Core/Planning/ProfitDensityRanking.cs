using System.Runtime.CompilerServices;
using TeleportDelivery.Core.Domain;

namespace TeleportDelivery.Core.Planning;

/// <summary>
/// Orders parcels by profit per unit of capacity, without comparing any two of them.
/// </summary>
/// <remarks>
/// A positive double's bits order like its value, so the top bits of a score are a bucket
/// index and a counting sort orders the pool in linear time. Arrays are reused per call.
/// </remarks>
public sealed class ProfitDensityRanking
{
    /// <summary>
    /// Sign, exponent and six mantissa bits: scores within 1/64 (about 1.6%) can share a
    /// bucket. Measured: 16 bits lost profit on similar parcels, 20 cost more than they saved.
    /// </summary>
    private const int BucketBits = 18;

    private const int BucketCount = 1 << BucketBits;
    private const int BucketShift = 64 - BucketBits;

    private readonly int[] bucketOfParcel;
    private readonly int[] bucketOffsets = new int[BucketCount + 1];
    private readonly int[] ordered;

    /// <param name="maximumParcels">Largest pool this instance will be asked to order.</param>
    public ProfitDensityRanking(int maximumParcels)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumParcels);

        bucketOfParcel = new int[maximumParcels];
        ordered = new int[maximumParcels];
    }

    /// <summary>
    /// Parcel positions, best first, valid until the next <see cref="Rank"/>.
    /// </summary>
    public ReadOnlySpan<int> Order => ordered.AsSpan(0, rankedCount);

    private int rankedCount;

    /// <summary>
    /// Orders the given parcels under the given prices.
    /// </summary>
    /// <param name="parcels">
    /// The pool to order, no larger than the size given at construction.
    /// </param>
    /// <param name="price">What weight and volume cost.</param>
    public void Rank(Parcel[] parcels, ResourcePrice price)
    {
        ArgumentNullException.ThrowIfNull(parcels);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            parcels.Length, ordered.Length, nameof(parcels));

        int count = parcels.Length;

        Array.Clear(bucketOffsets);

        for (int i = 0; i < count; i++)
        {
            int bucket = BucketOf(price.ScoreOf(parcels[i]));
            bucketOfParcel[i] = bucket;

            // One slot high, so the offsets below can be computed in place.
            bucketOffsets[bucket + 1]++;
        }

        // Top bucket first, so the best parcels come first.
        int running = 0;
        for (int bucket = BucketCount - 1; bucket >= 0; bucket--)
        {
            int size = bucketOffsets[bucket + 1];
            bucketOffsets[bucket + 1] = running;
            running += size;
        }

        for (int i = 0; i < count; i++)
        {
            ordered[bucketOffsets[bucketOfParcel[i] + 1]++] = i;
        }

        rankedCount = count;
    }

    /// <summary>
    /// The bucket of a score, which is always positive and finite.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int BucketOf(double score) =>
        (int)(BitConverter.DoubleToUInt64Bits(score) >> BucketShift);
}
