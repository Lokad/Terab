# Terab UTXO API

> By Joannes Vermorel, Lokad, 2017-01-15

See also an [introduction to Terab](https://terab.lokad.com/overview.html)

**Abstract:** Terab intends to provides a minimal standardized API dedicated to the management of the UTXO database for Bitcoin Cash. The document provides a high-level overview of the principles guiding our design proposal.

## Overview of the API

The UTXO API of Terab is specified as a C/C++ API. While, we don't intent to introduce hard-dependencies to C/C++, all major programming languages interface well with C/C++. More over, this approach allows us to preserve the network angles as an implementation detail of the specification.

Terab will ultimately support multiple APIs, intended for distinct purposes. The UTXO API focuses on the  temporal graph of transactions of Bitcoin, where each node is identified by its hash, and where each vertex is decorated by a monetary transfer measured in Satoshis.

## Technical description

The API includes a list of methods and structures defined in [terab_utxo.h](https://github.com/Lokad/Terab/blob/master/terab_utxo.h).

## Idempotence, monotony and purity

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
