// so unit tests don't run in parallel, because different instantiations
// of the BlockchainPersister tends to step on each other's toes.
[assembly: Xunit.CollectionBehavior(MaxParallelThreads = 1)]