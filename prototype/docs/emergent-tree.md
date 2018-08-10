# Emergent tree, a tiny data structure to query block chain lineages

Status: early draft, incomplete

> By Joannes Vermorel, Lokad, 2018-03-20

**Abstract:** Blocks in Bitcoin Cash can be given immutable unique compact identifiers (32bits in practice). However, as blocks may get orphaned, those identifiers alone do not properly describe the full block chain tree. Here we present the emergent tree, a constant size data structure with can validly answer any relevant lineage query from a Bitcoin mining node.


_The emergent layer is  the very top of the forest and only the tallest trees reach this level. Seeing the block chain as a tree, the emergent layer is the only place where anything happen. In the following, Bitcoin always refers to Bitcoin Cash._

## Introduction

The Bitcoin consensus mechanism ensures that blocks are emitted at an average rate of one block per 10min. The consensus mechanism ensures that only the longest chain is preserved by the network of miners. Thus, the block chain is almost similar to a linked list, but not quite. Indeed, competing blocks may happen at the tip of the block chain, generating tree-extensions, all of them but one, the longest chain, getting orphaned within a limited number of blocks.

Each tip of the block chain has its own unspent transaction output (UTXO) dataset. The management UTXO dataset is the typically piece that allow the economic validation of the incoming transaction by miners. The full detail of the UTXO management goes beyond the scope of this paper. However, in order to manage the UTXO dataset, and depending on the data structures used for this purpose, the miners may need to perform numerous _lineage_ queries. Those queries can allow miners to probe the block chain graph structure, and to prove whether an incoming transaction is valid or not.

In the following, we introduce a data structure named _emergent trees_ which offers a small memory footprint as well as constant time for both queries and update.

Each block is uniquely and securely defined by its hash (32 bytes) and the hash of its parent (32 bytes). From a graph perspective, the block chain is a tree that goes back to the genesis block of Bitcoin. 

A block is said to be _permanently orphaned_ (orphaned in the following) when the two conditions are met: (A) the block does not belong the longest chain (B) the maximal block height of any descendent of the block is less than the block height of the longest chain minus 100. 

The constant value 100 is somewhat arbitrary here, it is chosen as it matches the maturation duration, expressed in block height, of coinbase transactions.

We define as _Bitcoin lineage_ (lineage in the following) as a data structure that be queried with: 

* query: given two blocks `a` and `b` is `b` a descendent of `a`?
* query: given a block `a`, should this block be considered as orphaned?
* update: given a block `a`, add the block `b` as one of its direct descendent.

As lineage queries may have to be performed for the validation of every incoming transaction, it is important to provide a efficient data structure supporting both those two queries and the update. This data structure should be as fast as possible, and takes as little memory as possible.


## 32 bits compact identifiers for blocks

When security is not needed, each block can be uniquely identified by an `int32` identifier. Indeed, even taking into account a large orphan rates, 


