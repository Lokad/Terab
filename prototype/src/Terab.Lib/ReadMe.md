# Terab.Lib

This library contains the server-side logic of Terab.

Namespaces are organized as follow:

* `Chains`: persistence of the chain (tree) or blocks.
* `Coins`: persistence of the UTXO dataset.
* `Messaging`: bulk of the client-server interactions.
* `Networking`: logic underneath the `Messaging`.

A managed wrapper in C# of the C/C++ API is provided in
the namespace `Messaging.NativeClient`.

## Message passing and concurrency

A Terab instance is rigigly associated to a fixed number of shards,
with one `CoinController` instance per shard. Outpoints are hashed
and then distributed across shards through their hash values.

Terab is designed around a simple message-passing infrastructure.
The only multi-threaded part is the `BoundedInbox` which can be
safely concurrently written (but assume a single reader).

The lifecycle of a coin-write request is:

1. ConnectionController, receives request and forwards to ..
2. DispatchController, forwards request to ..
3. CoinController, persists and forwards response to ..
4. DispatchController, forwards response to ..
5. ConnectionController, sends response.

## Terminology

Coin vs UTXO: A coin is first produced, hence enters the UTXO, and
then consumed, hence exits the UTXO. From the Terab perspective, the
UTXO is explicitly mananged through coins and their lifecycles.

Block vs. Sector: The term _block_ is ambiguous here because
it could refer to both the hardware storage block or the Bitcoin
block. In order to avoid this ambiguity, the hardware storage 
blocks are referred to as _sectors_.

## Performance insights

### Zero-allocation C#

Leveraging `Span` and the `ref struct`, Terab does not generate any
GC pressure while reading or updating the UTXO. Neverthless, while
unsafe code is used in Terab, the vast majority of the logic is _safe_
(.NET terminology).

### Memory mapped hash tables

Terab can be seen as a layered hash table implemented directly
over memory mapped files. The first layer includes a probabilistic
filter, which ensures that deeper layers are not needlessly probed
whenever a non-existent outpoint is queried.

### Designing for 4096-byte blocks

At the hardware level, practically everything operates over
blocks of 4KB. Thus, reading/writing up to 4KB incurs little
overhead as the store is I/O bound anyway.

### DOS prevention through SipHash

In order to avoid Terab being adversely targeted, outpoints are
distributed againts their hashes, computed through `SipHash` an
hashing algorithm dedicated to DOS prevention.

### The chain is quasi-linear

While blocks are properly identified by crypto-hashes,
the chain is actually quasi-linear with less than 1% of
orphaned blocks observed in practice. Thus, all blocks,
including orphaned ones, can be indexed over 4 bytes.

