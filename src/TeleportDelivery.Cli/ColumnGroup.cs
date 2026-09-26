using System.Globalization;

namespace TeleportDelivery.Cli;

/// <summary>
/// Several figures reported side by side in one cell, under one heading.
/// </summary>
/// <remarks>
/// The group lays out both its heading and its rows, so their widths always match.
/// </remarks>
/// <param name="title">The heading over the group.</param>
/// <param name="width">How wide each figure in it is.</param>
/// <param name="labels">What each figure is called, one per column.</param>
internal sealed class ColumnGroup(string title, int width, params string[] labels)
{
    /// <summary>
    /// The heading: the group's name, and its labels laid out over the figures they name.
    /// </summary>
    public string Header { get; } = $"{title}\n{Lay(width, labels)}";

    /// <summary>
    /// One cell, with the figures in the order the labels were given.
    /// </summary>
    /// <exception cref="ArgumentException">The count does not match the labels.</exception>
    public string Of(params object[] values)
    {
        ArgumentNullException.ThrowIfNull(values);

        if (values.Length != labels.Length)
        {
            throw new ArgumentException(
                $"'{title}' has {labels.Length} columns and was given {values.Length}.",
                nameof(values));
        }

        return Lay(width, values);
    }

    private static string Lay<T>(int width, IReadOnlyList<T> cells) => string.Join(
        ' ',
        cells.Select(cell => string.Format(
            CultureInfo.InvariantCulture,
            "{0," + width.ToString(CultureInfo.InvariantCulture) + "}",
            cell)));
}
