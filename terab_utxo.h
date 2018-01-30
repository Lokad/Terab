/* © 2018. Lokad SAS */

#include <stdint.h>

/* Block metadata, to recursively enumerate the parents. */
typedef struct block_info block_info;

struct block_info
{
	int32_t parent;
	int32_t blockheight;
	uint8_t blockid[32];
};

/* Uniquely identify a transaction output. */
typedef struct tx_outpoint tx_outpoint;

struct tx_outpoint
{
	uint8_t txid[32];
	int32_t index;
};

/* The payload is the binary data attached to a 
   transaction output.

   The 'payload' can be used to encapsulate not only
   the script, but also the locktime if relevant. This
   design is intended to decouple UTXO API from the
   cryptographic validation as much as possible.

   The 'capacity' is introduce to offer the possibility
   to pool payloads and avoid re-allocations.

   The implementation of Terab will not modify the
   'capacity' value, but will modify the 'length' value
   to indicate the length of the actual payload. The
   content of 'data' beyond 'length' is left unspecified.
 */
typedef struct tx_payload tx_payload;

struct tx_payload
{
	int32_t capacity;
	int32_t length;
	uint8_t* data;
};

/* A self-sufficient transaction output spent or not,
  intended for the validation of input transactions.

  Event is a enumeration with the values:
    event = 1: Created
    event = 2: Spent

  An outpoint is first created (unspent state) and
  then later spent. The UTXO dataset includes all the
  create-but-not-yet-spent outpoints.

  If 'txo' is a 'Spent' event, then the two events
  associated to the outpoints becomes eligible for
  collection.
*/
typedef struct txo txo;

struct txo
{
	int32_t event;
	tx_outpoint outpoint;
	int32_t blockheight;
	int64_t satoshi;
	tx_payload payload;
};

/* Get a connection handle intended for all Terab-related operations.

   connection_string: details to connect to the Terab instance.
   conn: returned as an opaque connection handle.

   For various purposes, e.g. testing or failover, there might be
   multiple Terab instances available. The connection handle offers
   the possibility to interact with multiple instances without
   implicitly enforcing a "shared" configuration at the process
   level.
*/
int32_t terab_connect(
	char* connection_string,
	void* conn
);

/* Get the metadata associated to a block.

   conn: opaque connection handle.
   block: identifies the targeted block.
   info: contains the response, if any.

   In UTXO configuration, any call to a block that is more than 
   100 blocks away from the longest chain stored in Terab will
   be rejected.

   This method is PURE.
*/
int32_t terab_uxto_get_blockinfo(
	void* conn,
	int32_t block,
	block_info* info
);

/* Get the metadata associated to outpoints.
  
   conn: opaque connection handle.
   block: identifies the block of reference.
   output_length: indicates how many outputs are to be queried.
   outputs: identifies the outpoints to be queried.
   txos: contains the response, if any.

   The method is PURE.
*/
int32_t terab_utxo_get(
	void* conn,
	int32_t block,
	int32_t outpoint_length,
	tx_outpoint* outpoints,
	txo* txos
);

/* Starts the write sequence for a new block.
   
   conn: opaque connection handle.
   parent: identifies the parent of the new block.
   blockid: the 32-byte hash of the new block.
   block: returned as the compact identifier of the new block.

   In UTXO configuration, any call to a parent block that is 
   more than 100 blocks away from the longest chain stored
   in Terab will be rejected.

   This method is IDEMPOTENT.
*/
int32_t terab_utxo_open_block(
	void* conn,
	int32_t parent,
	uint8_t* blockid,
	int32_t* block
);

/* Write new outputs and their payloads to a new block.
  
  conn: opaque connection handle.
  block: identifies the block being written to.
  txo_length: specifies the number of outpoints to be written.
  txos: specifies what should be written to the block.

  In UTXO configuration, any call to a block that is more than 
  100 blocks away from the longest chain stored in Terab will 
  be rejected.

  This method is IDEMPOTENT.
*/
int32_t terab_utxo_write_txs(
	void* conn,
	int32_t block,
	int32_t txo_length,
	txo* txos
);

/* Closes the write sequence for a new block.
   
   conn: opaque connection handle.
   block: identifies the block written to.

   In UTXO configuration, any call to a block a block that 
   is more than 100 blocks away from the longest chain stored 
   in Terab will be rejected.

   This method is IDEMPOTENT.
*/
int32_t terab_utxo_commit_block(
	void* conn,
	int32_t block
);
