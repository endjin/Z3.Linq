namespace Z3.Linq;

using Microsoft.Z3;

using System;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// Context object for Z3 theorem proving through LINQ. Manages the configuration
/// of the theorem prover and provides centralized infrastructure for logging.
/// </summary>
public sealed class Z3Context : IDisposable
{
    /// <summary>
    /// Z3 configuration object.
    /// </summary>
    private readonly Dictionary<string, string> config;

    /// <summary>
    /// Creates a new Z3 context for theorem proving.
    /// </summary>
    public Z3Context()
    {
        this.config = new Dictionary<string, string>
        {
            { "MODEL", "true" }
        };
    }

    /// <summary>
    /// Gets/sets the logger used for diagnostic output.
    /// </summary>
    public TextWriter? Log { get; set; }

    /// <summary>
    /// Gets or sets how long a single solve or optimisation may run before Z3 gives up, or
    /// <see langword="null"/> for no limit.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Some theorems cannot be decided - nonlinear integer arithmetic is undecidable in general -
    /// and without a limit Z3 searches until the process is killed. When the limit is reached the
    /// solve throws <see cref="TheoremUndecidedException"/>: the theorem has been proved neither
    /// satisfiable nor unsatisfiable. A theorem Z3 can decide within the limit is unaffected.
    /// See #85.
    /// </para>
    /// <para>
    /// Wall-clock time, so the same theorem may or may not hit the limit on a different machine.
    /// <see cref="ResourceLimit"/> is the deterministic alternative.
    /// </para>
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">
    /// The value is not positive, or exceeds what Z3 can represent in milliseconds.
    /// </exception>
    public TimeSpan? Timeout
    {
        get => this.timeout;
        set
        {
            if (value is { } t && (t <= TimeSpan.Zero || t.TotalMilliseconds > uint.MaxValue))
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The timeout must be positive and no more than uint.MaxValue milliseconds.");
            }

            this.timeout = value;
        }
    }

    /// <summary>
    /// Gets or sets how much work Z3 may do on a single solve or optimisation before giving up,
    /// in Z3's own resource units, or <see langword="null"/> for no limit.
    /// </summary>
    /// <remarks>
    /// The deterministic sibling of <see cref="Timeout"/>: the same theorem does the same amount
    /// of work everywhere, so a limit that is reached on one machine is reached on all of them.
    /// The unit is Z3's <c>rlimit</c>, which has no fixed relationship to time. When the limit is
    /// reached the solve throws <see cref="TheoremUndecidedException"/>. See #85.
    /// </remarks>
    /// <exception cref="ArgumentOutOfRangeException">The value is zero.</exception>
    public uint? ResourceLimit
    {
        get => this.resourceLimit;
        set
        {
            if (value is 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "The resource limit must be positive.");
            }

            this.resourceLimit = value;
        }
    }

    private TimeSpan? timeout;

    private uint? resourceLimit;

    /// <summary>
    /// Closes the native resources held by the Z3 theorem prover.
    /// </summary>
    public void Dispose()
    {
    }

    /// <summary>
    /// Creates a new theorem based on the given type to establish the environment with
    /// the variables constrained by the theorem.
    /// </summary>
    /// <typeparam name="T">Theorem environment type.</typeparam>
    /// <returns>New theorem object based on the given environment.</returns>
    public Theorem<T> NewTheorem<T>()
    {
        return new Theorem<T>(this);
    }

    /// <summary>
    /// Creates a new theorem from a template instance, which supplies the environment type and
    /// the length of any collection symbols in it.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The type is inferred from the instance, which is what lets an anonymous type be used as
    /// an environment "on the fly". The instance is also the template for the solution: a
    /// collection symbol takes its length from the corresponding collection on the template,
    /// because a solution never changes a collection's length and has to get it from somewhere.
    /// That is what lets a value tuple or an anonymous type - neither of which has anywhere to
    /// put an initialiser - hold a collection at all. Where the type has an initialiser of its
    /// own the template still wins. See #78.
    /// </para>
    /// <para>
    /// Nothing else about the template is read. Its values do not constrain the theorem and do
    /// not reach the solution, and it is never written to.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// ctx.NewTheorem(new { x = default(int), y = default(int) }).Where(t => t.x > t.y)
    /// ctx.NewTheorem((Values: new int[3], Total: 0)).Where(t => t.Values[0] + t.Values[1] + t.Values[2] == t.Total)
    /// </code>
    /// </example>
    /// <typeparam name="T">Theorem environment type (typically inferred).</typeparam>
    /// <param name="template">
    /// An instance of the environment type. Its collections give the solution's their length;
    /// nothing else about it is used.
    /// </param>
    /// <returns>New theorem object based on the given environment.</returns>
    public Theorem<T> NewTheorem<T>(T template)
    {
        return new Theorem<T>(this, template);
    }

    /// <summary>
    /// Factory method for Z3 contexts based on the given configuration.
    /// </summary>
    /// <returns>New Z3 context.</returns>
    internal Context CreateContext()
    {
        return new Context(config);
    }

    /// <summary>
    /// The solver parameters carrying <see cref="Timeout"/> and <see cref="ResourceLimit"/>, or
    /// <see langword="null"/> when neither is set.
    /// </summary>
    /// <param name="context">The native context the parameters are created in.</param>
    /// <remarks>
    /// Applied per solver rather than through the context configuration, so that a limit is a
    /// property of the <see cref="Z3Context"/> that can be changed between solves.
    /// </remarks>
    internal Params? CreateLimits(Context context)
    {
        if (this.timeout is null && this.resourceLimit is null)
        {
            return null;
        }

        Params limits = context.MkParams();

        if (this.timeout is { } t)
        {
            limits.Add("timeout", (uint)t.TotalMilliseconds);
        }

        if (this.resourceLimit is { } r)
        {
            limits.Add("rlimit", r);
        }

        return limits;
    }

    /// <summary>
    /// Helpers to write diagnostic log output to the registered logger, if any.
    /// </summary>
    /// <param name="s">Log output string.</param>
    internal void LogWriteLine(string s)
    {
        if (Log != null)
        {
            Log.WriteLine(s);
        }
    }
}