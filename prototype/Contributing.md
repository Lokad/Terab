# Terab - Contributing

The Terab project is functional, yet several areas could still be greatly 
improved. This document lists the areas of prime interest for further
contributions.

## Loads of tests

Terab already includes its suite of unit tests, however, the quantity (and
quality) of our testing suite is not as high as we would like it to be. The
project would quite a few more regression tests in order to vetted as production
ready.

## Authentication and encryption

Currently, the client-server connection is merely a naked TCP socket with
no authentification and not transport encryption. Those features would be
desirable to add to Terab. It is suggested to adopt cryptographic primitives
that are already used in Bitcoin to minimize the technological mass of
the solution.

## xxHash to ensure integrity

The `CoinPack` would benefit from a non-cryptographic hash intended to detect
storage corruptions. The goal is not to prevent storage corruption from
happening, but merely to detect them when they happen, and terminate the
instance accordingly with a non-recoverable error status.

We suggest to use xxHash, which has a C#/.NET implementation at:
https://github.com/uranium62/xxHash

## Explicit pruning

The `ChainStore` keeps a value named `OldestOrphanHeight` which represents the
oldest orphaned block still present in the coin storage. In order to keep the
`Lineage` queries very fast, it's important to keep the `OldestOrphanHeight`
reasonably close to the tip of the chain - ideally within the last 20,000 blocks
or so.

While Terab performs a lazy pruning over its coin, there is no explict pruning
that provides the guarantee that could be used to raise `OldestOrphanHeight`.

As this pruning does not need to happen more frequently than once in a couple of
month, it could be first implemented with an offline logic (forcing the instance
to shutdown) which is simpler than the online logic.

## Benchmarking against the real blockchain

The benchmark of Terab is synthetic. While it should be reasonably close from
the real thing, performing a real benchmark is of prime interest.

## Layer's tuning

Terab hasn't been pushed to its single machine limits. In particular, we have not
tried and tested:

- Spread the first layer over two Intel Optane devices - balancing the shards over
the two storage devices.
- Take advantage of the second storage layer (already implemented) to increase the 
amount of storage for a fixed hardware cost.

## Utilities

- Storage usage: there is no tool to answer the question "how full" is the Terab 
instance. Such a tool could be implemented by a simple utility taking samples from
one of the shard.

- Re-layout: there is no tool to re-organize to expand and/or migrate the storage
from one file layout to another.

## Burst-write support

Terab does not yet take advantage of the weak durability semantic on coin writes
(durable only when "commit block" is called). This could be used to significantly
improve the burst-writes throughput (although this would not improve the sustained
throughput).

The design would be as follow.

For each shard:
- keep coin updates in memory (no immediate writes).
- when coin updates reaches 4k, persist the diff in a backlog file.
- async process to apply coin updates to the storage.

Upon "commit block", ensure that all coin updates have been flushed into their 
respective journal before returning.

The implementation of `SozuTable` becomes more complex, as it has to properly
take into account the coin updates that are in-memory but not yet part of the
storage.

If the backlog file becomes too large, the Sozu table should revert back to
direct writes.

## Reverse indexing for wallets

In order to serve wallets, a reverse indexing of the UTXO must be implemented,
mapping the script hashes back to the coins. 

The design of Terab can be adapted for this purpose. In particular, the reverse
indexing can be made much more efficient, as both outpoints and script hashes
can be re-hashed through SipHash into 64bits digest. Accidental collisions get
resolved on the UTXO side (but remains rare, hence keeping the overhead 
negligible).

## Multi-machine support

The event-driven design of Terab should rather gracefully evolve into a multi-
machine setup, however this represents a significant undertaking.
