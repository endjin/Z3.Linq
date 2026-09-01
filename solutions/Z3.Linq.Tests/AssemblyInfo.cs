using Microsoft.VisualStudio.TestTools.UnitTesting;

// Configure test parallelization. Required by MSTest 4.0 (MSTEST0001).
[assembly: Parallelize(Workers = 0, Scope = ExecutionScope.MethodLevel)]
