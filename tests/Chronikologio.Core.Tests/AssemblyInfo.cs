using Xunit;

// The interface language is a global: the tests that change it must not run beside the ones that read it.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
