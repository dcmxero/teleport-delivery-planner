using TeleportDelivery.Core.Domain;

namespace TeleportDelivery.Core.Planning;

/// <summary>
/// Decides which van a parcel goes into.
/// </summary>
public interface IVanSelection
{
    /// <summary>
    /// Name of the loading rule.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Picks a van for this parcel, or returns -1 when it fits in none of them.
    /// </summary>
    int SelectVan(VanLoad[] vans, in Parcel parcel);
}
