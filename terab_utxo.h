/* © 2018. Lokad SAS */

#include <stdint.h>

typedef uint8_t BYTE;

/* Uniquely identify a block, typically the hash of the block. */
typedef struct block_key block_key;

struct block_key
{
	int32_t blockid_length;
	BYTE* blockid;
};

/* Minimal block metadata, to recursively enumerate the parents. */
typedef struct block_info block_info;

struct block_info
{
	block_key parent;
	int32_t blockheight;
};

/* Uniquely identify a transaction output. */
typedef struct tx_outpoint tx_outpoint;

struct tx_outpoint
{
	int32_t txid_length;
	BYTE* txid;
	int32_t output_index;
};

/* The payload is the binary data attached to a 
   transaction output.

   The 'payload' can be used to encapsulate not only
   the script, but also the locktime if relevant. This
   design is intended to decouple UTXO API from the
   cryptographic validation as much as possible.

   The 'capacity' is introduce to offer the possibility
   to pool payloads and avoid re-allocations.
 */
typedef struct tx_payload tx_payload;

struct tx_payload
{
	int32_t capacity;
	int32_t length;
	BYTE* data;
};

/* A self-sufficient unspent transaction output,
  intended for the validation of input transactions.
*/
typedef struct utxo utxo;

struct utxo
{
	int32_t type;
	tx_outpoint outpoint;
	int32_t blockheight;
	int64_t satoshi;
	tx_payload payload;
};

/* Get the metadata associated to a block.

   block: identifies the targeted block.
   info: contains the response, if any.

   This method is PURE.
*/
int32_t terab_uxto_get_blockinfo(
	block_key block,
	block_info* info
);

/* Get the metadata associated to outpoints.
  
   block: identifies the block of reference.
   output_length: indicates how many outputs are to be queried.
   outputs: identifies the outpoints to be queried.
   utxos: contains the response, if any.

   Any call to a block that is more than 100 blocks
   away from the longest chain stored in Terab will
   be rejected.

   The method is PURE.
*/
int32_t terab_utxo_get(
	block_key block,
	int32_t outpoint_length,
	tx_outpoint* outpoints,
	utxo* utxos
);

/* Starts the write sequence for a new block.
   
   parent: identifies the parent of the new block.
   block: identifies the new block itself.

   Any call to a parent block that is more than 
   100 blocks away from the longest chain stored 
   in Terab will be rejected.

   This method is IDEMPOTENT.
*/
int32_t terab_utxo_open_block(
	block_key parent,
	block_key block
);

/* Write new outputs and their payloads to a new block.
  
  block: identifies the block being written to.
  utxo_length: specifies the number of outpoints to be written.
  utxos: specifies what should be written to the block 

  Any call to a block that has either not been opened yet
  or that has already been committed will be rejected.

  Any call to a block that is more than 100 blocks away
  from the longest chain stored in Terab will be rejected.

  This method is IDEMPOTENT.
*/
int32_t terab_utxo_write_txs(
	block_key block,
	int32_t utxo_length,
	utxo* utxos
);

/* Closes the write sequence for a new block.
   
   block: identifies the block written to.

   Any call to a block a block that is more than 100 blocks
   away from the longest chain stored in Terab will be 
   rejected.

   This method is IDEMPOTENT.
*/
int32_t terab_utxo_commit_block(
	block_key block
);

