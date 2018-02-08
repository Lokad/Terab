# Terab UTXO API

> By Joannes Vermorel, Lokad, 2017-02-08

See also an [introduction to Terab](https://terab.lokad.com/overview/)

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

## Capacity limits of methods

Most methods offered by Terab offer the possibility to perform many read/write at once, typically by passing one or more arrays as part of the request. This design is intentional as chatty APIs do not scale well due to latency problems.

Yet, Terab cannot offer predicable performance over arbitrary large requests. Thus, a Terab instance should specify through its nominal configuration the maximal number of TXOs which can be read or written in a single method call.

## Error codes

All methods return a `int32_t` which should be treated as the error code. When an error is encountered, the behavior of the API is fully unspecified. The client should not make _any_ assumption on the data that might be obtained through a failing method call, beyond the error code itself.

There are three broad classes of problems that can be encountered:

* **Broken client**: The client implementation needs a fix.
* **Broken service**: The Terab implementation needs a fix.
* **Misc. happens**: A hardware problem or an IT problem is causing a malfunction. Worst case, the Terab instance needs replacement.

Terab ensures that all failing method calls have no observable side-effect on the state of the system.

Below, we list the error codes that can be returned by Terab.

### `TERAB_SUCCESS`

The method call succeeded.

### `TERAB_CONNECTION_FAILED` (misc. happens)

The client did not connect to the Terab service. This problem might be caused by a network connectivity issue, or because the Terab service is down or even non-existent.

### `TERAB_AUTHENTICATION_FAILED` (misc. happens)

The client did connect to the Terab service, but the method call was rejected at the authentication level. This problem is most likely caused by a configuration mismatch between the Terab instance and the configuration of the client.

### `TERAB_SERVICE_UNAVAILABLE` (misc. happens)

The Terab instance is not ready yet to accept incoming calls. This problem is transient and should addressed on the client side by a retry-policy with fixed back-off.

### `TERAB_TOO_MANY_REQUESTS` (broken client)

The hard-limit on concurrent connections to the Terab service has been reached. This limit is expected to be part of the explicit configuration of the Terab instance.

This is problem is transient and may be addressed on the client side by a retry-policy with exponential back-off. Then, it is preferable if client implementations can avoid hitting the limit altogether through a design that ensures that the cap on concurrent connections is never reached.

### `TERAB_INTERNAL_ERROR` (broken service)

An unexpected and non-recoverable problem did happen within the Terab instance. This error should not happen, and reflects a defect in the Terab implementation itself.

It is advised to not even try fixing this problem on the client side, but to take contact with the team in charge of the Terab implementation.

### `TERAB_STORAGE_FULL` (misc. happens)

The Terab instance has reached its data storage limit. All subsequent operations, both reads and writes, should be expected to fail as well. Indeed, read operations should be expected to start failing as well because the Terab instance may not be able to properly operate while ensuring that internal logs are properly persisted.

Depending on the Terab implementation, the current instance might be physically upgraded or replaced. The client implementation is not expected to be able to mitigate this problem in any way.

### `TERAB_STORAGE_CORRUPTED` (misc. happens)

The Terab instance is suffering from a non-recoverable data corruption problem. All subsequent operations, both reads and writes, should be expected to fail.

Depending on the Terab implementation, the instance might be repairable or recoverable; or not. The client implementation is not expected to be able to mitigate this problem in any way.

### `TERAB_BLOCK_CORRUPTED` (misc. happens)

The non-committed block has suffered a non-recoverable problem within the Terab instance. Due to the weak durability offered by Terab, a transient problem such as a power cycle may corrupt a block being written. In this case, all the data associated to the uncommitted block should be considered as lost.

If a block is corrupted, the client implementation should open a new block and repeat all the writes for this block. The client implementation is expected to be capable of recovering from this problem.

### `TERAB_BLOCK_FROZEN` (broken client)

The block is too far from the longest chain according to the configuration of the Terab instance. Indeed, unless Terab is configured to keep the full TXO database, new blocks or block writes can only be made against recent parts of the blockchain.

As the client is expected to know beforehand which blocks remain eligible for blockchain extension, the client implementation is expected to avoid this problem altogether.

### `TERAB_BLOCK_UNKNOWN` (broken client)

The arguments refer to a block identifier that is unknown to the Terab instance.

As the client is expected to properly keep track of the block identifiers returned by Terab, the client implementation is expected to avoid this problem altogether.

### `TERAB_TOO_MANY_TXOS` (broken client)

The arguments include too many TXO passed as once, either for a read or write operation.

As the client is expected to know the limits of Terab, the client implementation is expected to avoid this problem altogether.

### `TERAB_BUFFER_TOO_SMALL` (broken client)

One or several buffers are too small to contain the data that should be returned by the Terab instance. The existence of this error code is the consequence of the zero server-side allocation policy of Terab.

As the client is expected to know the memory footing of the payloads attached to transactions, the client implementation is expected to avoid this problem altogether. In particular, it is strongly discouraged to use this error message to "probe" the right buffer size.

### `TERAB_INCONSISTENT_REQUEST` (broken client)

The content of the method call contradicts, at the block level, the content previously written by another method call. The stand-alone content of the call is valid; it is only deemed incorrect as it is inconsistent with the state of Terab.

This error code strictly reflects a class of problems that can only be obtained through multiple method calls which, taken in isolation, would have been considered correct. In practice, it is expected that those problems emerge as race conditions within the client implementation.

### `TERAB_INVALID_REQUEST` (broken client)

The content of the method call is deemed incorrect. This assertion is made independently from the state of the blockchain. For example, negative buffer lengths are always invalid.

The error code captures the broad class of problems that could arise when the client implementation attempts to push corrupted data to the Terab instance. The client implementation is expected to avoid this problem altogether.

## Annexes

### `int32_t` for block identifiers

One century of blocks only represents 5.5M valid blocks, and still represent less than 10M blocks even considering a dramatic increase of the block orphaning rate. Thus, blocks can be identified through 32-bits integers. Assigning the block identifiers is done on the server side of Terab.

While it is not a strict requirement, block identifiers are expected to be numbered starting from zero, and going onward through +1 increments. 

### Asynchronous API

While offering of an asynchronous API would be desirable in a context like Terab, it introduces a non-trivial complexity burden for both the Terab and the client implementations. Furthermore, as C does not offer native coroutines, it would displace the coroutine specification burden to Terab as well.
