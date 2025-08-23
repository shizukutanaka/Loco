using Xunit;

// Disable test parallelization for CLI tests to avoid environment and filesystem interference
[assembly: CollectionBehavior(DisableTestParallelization = true)]
