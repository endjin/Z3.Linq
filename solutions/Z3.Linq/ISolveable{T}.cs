namespace Z3.Linq
{
    /// <summary>
    /// Enables optimization constraints as expressed by OrderBy to be deferred, just like
    /// everything else.
    /// </summary>
    /// <typeparam name="T">Result type.</typeparam>
    public interface ISolveable<T>
    {
        T? Solve();
    }
}