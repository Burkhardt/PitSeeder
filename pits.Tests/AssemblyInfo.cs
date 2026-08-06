using Xunit;

// CR003 §2/§8: the ordinary suite remains non-parallel. Tests run the real pits CLI
// against shared directories and process-global state (MasterFlagFile.TicketDuration);
// product concurrency is proven by explicit tests, never by runner scheduling.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
