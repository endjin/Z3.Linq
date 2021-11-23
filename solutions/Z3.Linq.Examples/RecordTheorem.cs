namespace Z3.Linq.Examples;

// This doesn't work because they have special handling for constructing ordinary (albeit private) properties,
// and also for anonymous types, but they have no support for the standard constructor-based init idiom
// that records use.
//    public record RTheorem<T1, T2>(T1 X, T2 Y);
// But if the record type offers a default ctor, it's happy.
public record RecordTheorem<T1, T2>
{
    public T1 X { get; init; } = default!;

    public T2 Y { get; init; } = default!;
}