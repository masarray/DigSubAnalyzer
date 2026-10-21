using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ProcessBus.Core.Models;

namespace ProcessBus.Core.Presentation;

/// <summary>
/// Protocol identity of a GOOSE control block on a particular captured LAN.
/// Sequence numbers, reception time, ConfRev and DataSet values are NOT identity.
/// GoCB references are case-sensitive IEC 61850 object references.
/// </summary>
public readonly record struct GoosePublisherKey(string SourceMac, string AppId, string GoCbRef, string VlanId)
{
    public static GoosePublisherKey From(GooseMessageItem message) => new(
        NormalizeMac(message.SourceMac),
        (message.AppId ?? string.Empty).Trim().ToUpperInvariant(),
        string.Equals(message.GoCbRef, "N/A", StringComparison.OrdinalIgnoreCase)
            ? $"fallback:{message.GoId}|{message.DataSet}"
            : (message.GoCbRef ?? string.Empty).Trim(),
        (message.VlanId ?? string.Empty).Trim());

    private static string NormalizeMac(string? mac) =>
        (mac ?? string.Empty).Trim().Replace('-', ':').ToUpperInvariant();
}

/// <summary>
/// Stable WPF item container identity. The receive snapshot is replaced atomically,
/// while the row instance is preserved; only changed visible fields are notified.
/// </summary>
public sealed class GoosePublisherRow : INotifyPropertyChanged
{
    public GoosePublisherRow(GoosePublisherKey key, GooseMessageItem current)
    {
        Key = key;
        Current = current;
    }

    public GoosePublisherKey Key { get; }
    public GooseMessageItem Current { get; private set; }
    public bool IsStale { get; private set; }
    public string GoId => Current.GoId;
    public string GoCbRef => Current.GoCbRef;
    public string AppId => Current.AppId;
    public bool IsTest => Current.IsTest;
    public uint StNum => Current.StNum;
    public uint SqNum => Current.SqNum;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool Update(GooseMessageItem message)
    {
        if (Key != GoosePublisherKey.From(message))
            throw new ArgumentException("Publisher identity mismatch.", nameof(message));

        var before = Current;
        var changed = !ReferenceEquals(before, message);
        Current = message;
        if (IsStale)
        {
            IsStale = false;
            OnPropertyChanged(nameof(IsStale));
        }
        if (!changed)
            return false;

        if (before.GoId != message.GoId) OnPropertyChanged(nameof(GoId));
        if (before.GoCbRef != message.GoCbRef) OnPropertyChanged(nameof(GoCbRef));
        if (before.AppId != message.AppId) OnPropertyChanged(nameof(AppId));
        if (before.IsTest != message.IsTest) OnPropertyChanged(nameof(IsTest));
        if (before.StNum != message.StNum) OnPropertyChanged(nameof(StNum));
        if (before.SqNum != message.SqNum) OnPropertyChanged(nameof(SqNum));
        return true;
    }

    internal void MarkStale()
    {
        if (IsStale) return;
        IsStale = true;
        OnPropertyChanged(nameof(IsStale));
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// Bounded keyed reconciliation on the UI dispatcher, never on the capture hot path.
/// An absent selected publisher retains its last coherent frame as stale; unrelated
/// evicted publishers are removed without resetting the selected item container.
/// </summary>
public sealed class GoosePublisherCatalog
{
    private const int MaxPublisherRows = 256;
    private readonly Dictionary<GoosePublisherKey, GoosePublisherRow> _byKey = new();
    public ObservableCollection<GoosePublisherRow> Rows { get; } = new();

    public GoosePublisherRow? Find(GoosePublisherKey? key) =>
        key.HasValue && _byKey.TryGetValue(key.Value, out var row) ? row : null;

    public bool Reconcile(IReadOnlyList<GooseMessageItem> messages, GoosePublisherKey? selected)
    {
        var seen = new HashSet<GoosePublisherKey>();
        var changed = false;
        foreach (var message in messages)
        {
            var key = GoosePublisherKey.From(message);
            seen.Add(key);
            if (_byKey.TryGetValue(key, out var row))
            {
                changed |= row.Update(message);
            }
            else if (Rows.Count < MaxPublisherRows)
            {
                row = new GoosePublisherRow(key, message);
                _byKey.Add(key, row);
                Rows.Add(row);
                changed = true;
            }
        }

        // Only remove truly absent non-selected rows. Do not rebuild the whole list.
        for (var i = Rows.Count - 1; i >= 0; i--)
        {
            var row = Rows[i];
            if (seen.Contains(row.Key)) continue;
            if (selected.HasValue && selected.Value == row.Key)
            {
                row.MarkStale();
                continue;
            }
            _byKey.Remove(row.Key);
            Rows.RemoveAt(i);
            changed = true;
        }
        return changed;
    }

    /// <summary>Same deterministic filter for publisher exploration; filtering
    /// never changes the operator's selected publisher key.</summary>
    public static bool MatchesFilter(GooseMessageItem message, string? gooseId, string? search)
    {
        if (!string.IsNullOrWhiteSpace(gooseId) &&
            !string.Equals(gooseId, "All", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(message.GoId, gooseId, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(message.GoCbRef, gooseId, StringComparison.OrdinalIgnoreCase))
            return false;

        if (string.IsNullOrWhiteSpace(search))
            return true;

        var text = search.Trim();
        return Contains(message.GoId, text) ||
               Contains(message.GoCbRef, text) ||
               Contains(message.AppId, text) ||
               Contains(message.DataSet, text) ||
               Contains(message.SourceMac, text) ||
               Contains(message.DestinationMac, text);

        static bool Contains(string? field, string text) =>
            !string.IsNullOrWhiteSpace(field) &&
            field.Contains(text, StringComparison.OrdinalIgnoreCase);
    }

    public void Clear()
    {
        _byKey.Clear();
        Rows.Clear();
    }
}
