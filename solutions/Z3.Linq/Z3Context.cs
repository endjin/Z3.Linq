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