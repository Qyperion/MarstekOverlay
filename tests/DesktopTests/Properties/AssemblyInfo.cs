using Xunit.Sdk;
using Xunit.v3;

// The language of the app is one for the whole process, so tests that word something cannot run side by side.
[assembly: Parallelization(Mode = ParallelMode.None)]
