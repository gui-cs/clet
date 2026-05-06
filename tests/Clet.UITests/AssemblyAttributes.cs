using Xunit;

// TG's `IApplication` has process-global state (Application.AppModel,
// ConfigurationManager, scheme manager) that's incompatible with two harness
// instances running concurrently. Disable parallelization at the assembly
// level so future-added test classes can't accidentally run a second harness
// alongside the first.
[assembly: CollectionBehavior (DisableTestParallelization = true)]
