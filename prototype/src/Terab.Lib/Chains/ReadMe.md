# Blockchain namespace

The intent of the `Terab.Chains` namespace is to handle the "chain of blocks" 
which is akin to a linked list of block identifiers, except for the recent 
additions, which behave like a tree of block identifiers.

This namespace is only dedicated to the management of the thin datastructure
comprised only of block identifiers. The actual content of the Bitcoin blocks
is outside the scope of this namespace.


## Identification of blocks

In order to identify the blocks, we are distinguishing:

* `BlockId`: 256bit hash that canonically identifies Bitcoin blocks.
* `BlockAlias`: compact 32bit identifiers internally used in Terab.
* `BlockHandle`: opaque 32bit identifiers intended for the Terab clients.

The internal representation of `BlockAlias` prevents Terab from representing
more than 63 blocks at the same block height.


## Performance strategy

The approach adopted by Terab when it comes to managing the blockchain - understood 
as the thin datastructure solely comprising block identifiers - is fast query and
slow update. Indeed, as the blockchain is only updated once every 10min on average,
it is acceptable to have a "relatively" slow update if - in exchange - the blockchain
can be queried faster.

The two main blockchain queries are:

* validating the _ancestry_ of a coin: verifying that a coin produced in block X can 
be consumed in block Y because Y is verified as being a descendent of X.

* validating the age of a coin: obtaining the blockheight of the block where a given 
coin entry has been produced.

This approach is reflected by `IChainStore` which is intended to contains all the 
blocks, but without any particular indexing strategy. Then, this store can produce
an `ILineage` which is intended for high-performance lineage queries. The lineage
is intended to be refreshed every time a new block is opened or committed.


## Committed and uncommitted blocks

The blockchain implementation distinguishes between `CommittedBlock` and 
`UncommittedBlocks`. Uncommitted blocks are those which have been 'opened' but 
where modifications  can still be made; conversely committed blocks are now
immutable.

Most importantly, this affects the hash of the block: As long as a block can be 
modified, its final hash is not known. Therefore, when 'committing' a block, the 
blockchain hash is communicated to Terab and from that moment on will be the 
identifier of that block.

Before a final hash is known, Terab will assign a temporary identifier to the 
block, which is communicated to the client when opening the block, and which 
will serve for communication about that block before it is committed.

