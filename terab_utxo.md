# Terab UTXO API

> By Joannes Vermorel, Lokad, 2017-01-15

See also an [introduction to Terab](https://terab.lokad.com/overview.html)

**Abstract:** Terab intends to provides a minimal standardized API dedicated to the management of the UTXO database for Bitcoin Cash. The document provides a high-level overview of the principles guiding our design proposal.

## Overview of the API

The UTXO API of Terab is specified as a C/C++ API. While, we don't intent to introduce hard-dependencies to C/C++, all major programming languages interface well with C/C++. More over, this approach allows us to preserve the network angles as an implementation detail of the specification.

Terab will ultimately support multiple APIs, intended for distinct purposes. The UTXO API focuses on the  temporal graph of transactions of Bitcoin, where each node is identified by its hash, and where each vertex is decorated by a monetary transfer measured in Satoshis.

## Technical description

The API includes a list of methods and structures defined in [terab_utxo.h](https://github.com/Lokad/Terab/blob/master/terab_utxo.h).

## Full UTXO plus partial TXO storage

The blockchain is a hybrid between a tree of blocks and a linked list of blocks. Due to the very nature of Bitcoin mining only the freshest part of the blockchain - the most recent blocks - can actually behave as a tree-like structures. Yet, the longest-chain mining rule ensures that tree-like properties of the blockchain does not, and instead always resolve to a linked list of blocks.

The Terab API is explicitly taking advantage of this blockchain by making sure that the API itself does not stand in the way of highly optimized implementation.

The "UTXO" storage of Terab (UTXO standing for _unspent transaction outputs_) would actually be better qualified as an hybrid storage between:

* a UTXO storage - which only keep unspent outputs.
* a TXO storage - which would keep all outputs.

For all the blocks that are no further than 100 blocks away from the longest chain ever stored in Terab, the entire UTXO set is available for query; but also all the TXO consumptions that did happen through those blocks. This property of Terab ensures that a miner can correctly assess if an output is truly unspent or not.

The 100 blocks cut-off rule of Terab is aligned with the transaction validation rule that prevents coinbase transactions to be spent for 100 blocks.

Restricting the read queries to more recent blocks also ensure that old transactions outputs that have been spent can be fully pruned from the physical data storage that supports Terab. To further clarify, while reading "recent" block, the API can well return "old" outputs, well beyond the last 100 blocks.

Also, as the Terab API exposes some methods that are guaranteed to be _pure_, returning immutable results, the Terab API does prevent any TXO entry to be pruned for 200 blocks.

### Moving away from undo+reorg 

The Terab API moves away from the historic Bitcoin implementation which relied on undo+reorg block patterns. It would still be possible to implement the Terab UTXO API through an undo+reorg approach, but this is not a requirement; and we are inclined to believe that there are superior approaches which are easier to distribute.

### Full TXO mode

Terab is also intended to support - through configuration - a _full TXO_ mode where the spent outpoints are never pruned. The Terab API is identical for the two configuration _full TXO_ vs _UTXO_; as it is not possible to alternate between the two modes.

## Durability at the block level

Coping with the I/O throughput is one of the major challenged faced by an implementation of the Terab UTXO API. Terab addresses this challenge upfront by adopting a rather specific approach to [durability](https://en.wikipedia.org/wiki/Durability_(database_systems)).

Writes made to the API are only guaranteed to be durable once a block is _committed_ through the API. This leads to an API design were blocks are first _opened_ and finally _committed_. In case of a power failure or other transient failure bringing down the whole Terab system, only committed blocks are guaranteed to be retrievable. 

This design offers the possibility to Terab implementations to largely mitigate the I/O challenge by keeping the most recent entries in non-durable memory.

In practice, Terab does not imply that committing a
block will be treated a single monolithic operation. Implementations are expected to try flushing incoming data to durable storage as soon as possible, but not offering durability guarantees before the block commit.

## Idempotence and purity

Designing software for distributed computing is difficult. There are many assumptions that cannot be made. See the [fallacies of distributed computing](https://en.wikipedia.org/wiki/Fallacies_of_distributed_computing). The API is designed to precisely take those aspects into account by making all methods either _pure_ or _idempotent_.

A _pure_ method is a method that, when it succeeds, always returns the same results. Unlike an _idempotent_ method, a _pure_ method has no observable side-effect, not even the first call. By construction, pure methods are safe to be called multiple times. 

The intent associated to _pure_ methods is to support _read_ methods that always return the exact same data.

An _idempotent_ (*) method is a method that can be safely called multiple times with the _same_ arguments. All responses are identical, and the state of the system is not be modified by any call except the first one.

The intent associated to _idempotent_ methods is to support _write_ methods, which can be safely retried through retry-policies, which are, in practice, required when designing distributed systems.

(*) Our terminology is slightly adjusted to the specific requirements of Terab. In the literature, you may find slightly different interpretations of those terms.

## Strictly append-only

The API of Terab is strictly append-only. It does not offer any mechanism to re-write previously written data. This design is intentional.

* It prevents entire classes of mistakes from being made with the other Bitcoin micro-services which populate and consume the present API.
* It enables a wide range of both software and hardware optimizations which would otherwise be much more difficult if the data was mutable.
* It vastly reduces the surface-attack area of the micro-service itself. An attacker could still [brick](https://en.wikipedia.org/wiki/Brick_(electronics)) a Terab appliance, but not rewrite the past, not through the API itself (*). 

(*) With sufficient system privilege, all hacks remain possible; however, a defense in-depth design not only complicate the hack, but also make it vastly slower.

## No server-side instantiation

The results returned by the API are always injected into pre-allocated structures passed by the client. If the allocation is insufficient, the method call will fail.

This design removes entirely classes of mistakes where memory management could be considered as ambiguous. As the API does not return anything _new_, it's the sole responsibility of the client to manage its memory.

## Error codes

TODO: error codes must be specified

All methods returns a `int32_t` which should be treated as the error code. When an error is encountered, the behavior of the API is fully unspecified. The client may not make _any_ assumption on the data that might be obtained through a failing method call.

Terab ensures that all failing method calls have no observable side-effect on the state of the system.

## Capacity limits of methods

TODO: capacity limits must be specified

Most methods offered by Terab offer the possibility to perform many read/write at once, typically by passing one or more arrays as part of the request. This design is intentional as chatty APIs do not scale well due to latency problems.

Yet, Terab cannot offer predicable performance over arbitrary large requests. Thus, each method specify its maximal capacity.

## Annexes

### `int32_t` for block identifiers

One century of blocks only represents 5.5M valid blocks, and still represent less than 10M blocks even considering a dramatic increase of the block orphaning rate. Thus,  blocks can be identified through 32-bits integers. Numbering the blocks is done on the client-side of Terab. 

While it is not a strict requirement, blocks are expected to be numbered starting from zero, and going onward through +1 increments. 

### Asynchronous API

While offering of an asynchronous API would be desirable in a context like Terab, it introduces a non-trivial complexity burden for both the Terab and the client implementations. Furthermore, as C does not offer native coroutines, it would displace the coroutine specification burden to Terab as well.
