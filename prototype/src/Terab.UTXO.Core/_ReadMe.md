# UTXO API

## Thread-safety

Even a "safe" language like Rust would not protect the
UTXO store against concurrency issues emerging from race
conditions over the data storage devices themselves.

Instead, the store is rigidly set up for k CPUs. The vast 
majority of concurrency issues are trivially solved by a 
design that partitions all operations (read and write) 
according to their transaction hash.

## Terminology

* Block vs. Sector: The term _block_ is ambiguous here because
it could refer to both the hardware storage block or the Bitcoin
block. In order to avoid this ambiguity, the hardware storage blocks
are referred to as _sectors_.

## Performance insights

### The chain is quasi-linear

While blocks are properly identified by crypto-hashes,
the chain is actually quasi-linear with less than 1% of
orphaned blocks observed in practice. Thus, all blocks,
including orphaned ones can be indexed over 4-bytes.

### Designing for 4096-byte blocks

At the hardware level, practically everything operates over
blocks of 4KB. Thus, reading/writing up to 4KB incur little
overhead as the store is I/O bound anyway.

## Annex

### Hashes with 128 bits should be sufficient

By introducing a private salt, all the crypto-identifiers
can be safely be compacted to 128 bits hashes as those
hashes (and the salt) will never be exposed to any 3rd party
not even the client app that controls the store.

## Glossary

BlockId: The hash of the blockchain block in question. This is a
hash of 32 bytes which identifies a block uniquely and which does not 
change over time. It is emitted by miners, Terab takes it as given data.

BlockAlias: A number which identifies a block in the blockchain within
the Terab application. This number is shorter than the hash which 
facilitates communicaton with the Terab application. This number persists
from session to session with one instance of the Terab application, however
it cannot be transferred between different applications. The numbers will not
persist potential purging of unused blocks on the application.

ParentAlias: Whenever ancestry is concerned or other questions involving 
the parent of a block, the application asks for the BlockAlias of the parent, 
again for the sake of optimized communication.

QuasiOrphans: Blocks that are not on the main chain.

## Implementation

This project is the core of Terab in the sense that here the information is
stored that Terab is all about. This is the UTXO set and a light version of the 
blockchain itself.

### Blockchain

The blockchain implementation distinguishes between _CommittedBlocks_ and 
_UncommittedBlocks_.
Uncommitted blocks are those which have been 'opened' but where modifications 
can still be made.
Most importantly, this affects the hash of the block: As long as a block can be 
modified, its final hash is not known. Therefore, when 'committing' a block, the 
blockchain hash is communicated to Terab and from that moment on will be the 
identifier of that block.
Before a final hash is known, Terab will assign a temporary identifier to the 
block (Hash128), which is communicated to the client when opening the block, and 
which will serve for communication about that block before
it is committed.

The blockchain is not accessible for anyone apart from the blockchain itself. 
The only way to get to know the contents of the blockchain is to enumerate it. 
A wide variety of enumerators is available to enumerate the committed, 
uncommitted or all blocks from the front (starting at block 0) or the back 
(starting at the most recent block). Starting enumeration at the most recent 
block can be of interest as the first (several thousand) blocks do not change 
and most of the money contained in them is already spent, in contrast to the 
more recent blocks.

As most requests on the chain are probably going to be of the same nature and 
the entire information about the blockchain is not required to reply to those 
requests, an _OptimizedLineage_ exists which is optimized for those very 
specific requests that are expected to be recurrent. They will be presented in 
the following.

+ _IsAncestor_: This provides the answer to the question if a given block is an 
ancestor of another given block. This question is interesting because the 
blockchain can diverge into different branches. The question is essentially
whether the ancestor block is in the same branch as the supposed child block.
+ _IsPruneable_: As the blockchain can branch and a client can open as many 
blocks as he wants, possibly without needing to persist all or any of those 
opened blocks, the application and even the client have an interest in keeping 
only the relevant information.
IsPruneable provides the answer to the question whether a given block counts as 
relevant information. A block is not relevant anymore if it is in a branch that 
is not the main chain and is older than a given limit.

The responses to both those questions can be given by only knowing the branches 
that are not the main chain. As the main chain contains the bulk of the blocks 
(especially if the blockchain is pruned from time to time), this reduces
the amount of information to know considerably.

An _OptimizedLineage_ containing only this reduced information can be obtained 
from a full blockchain.

### Sozu table

For detailed information about the Sozu table concept and its implementation, 
refer to `/docs/sozu-table.md`.

# Terab contracts and message exchange with the client

Communication with the Terab application is done via a bitstream 
that is exchanged between the application and a client  with help of a [Server].
Whenever a client wants to connect to the server, it is the task of the 
_Listener_ to discover that new connection.
Every client is attributed a dedicated _ClientConnection_ which allows the 
application to explicitly keep track of all active connections.

The application internally organises and distributes the different client 
requests between the available application threads as follows:
All client connections communicate the incoming requests to a central 
_Dispatcher_, which is mono-threaded to avoid collisions. It is the dispatcher 
that - after some basic message format verifications - 
decides which application thread will be attributed the task at hand.
Once the task has been executed, the return path of the message follows the 
same intermediate stages.

The messages that are exchanged have to follow a strict format as they are sent 
in binary format. The format has been fixed for the requests and responses that 
can be found in `/Messaging`.

Messages are only fully deseralized when they reach the final application thread 
which is tasked with responding to the request. This helps keep the job of the
dispatcher to a minimum. As mono-threaded by design, it is a bottleneck and 
should only do the most necessary work.