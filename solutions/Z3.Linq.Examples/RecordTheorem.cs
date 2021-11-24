namespace Z3.Linq.Examples;

// This doesn't work because the code that constructs an output representing the solution
// does not have the necessary special handling for the standard constructor-based initialization
// idiom that records use. (Anonymous types and ordinary properties work only because there
// is special handling for them. Special handling could be added for this too but we don't
// currently have it
public record RecordTheorem<T1, T2>
{
    public T1 X { get; init; } = default!;

    public T2 Y { get; init; } = default!;
}