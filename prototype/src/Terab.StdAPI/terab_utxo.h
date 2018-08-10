/* © 2018. Lokad SAS */
#pragma once

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

/* Opaque handle to the underlying persistent connection. 

   For various purposes, e.g. testing or failover, there might be 
   multiple Terab instances available. Also, since each connection
   may only be accessed from a single thread, multiple connections
   may be used from a single process to support multi-threaded access.
   
   The connection handle offers the possibility to interact with 
   multiple instances and/or from multiple threads, without implicitly 
   enforcing a "shared" configuration at the process level.
 */
typedef void* connection_t;

/* Opaque handle to a block present on a Terab server.
   
   A shorter way to reference a block instead of using its 32-byte
   block id. Block handles are specific to a connection, they 
   cannot be reused between connections and must instead be 
   re-generated from the block id. 

   An open (uncommitted) block may become corrupt or lost due to 
   events on the Terab server outside the control of the client. When 
   this happens, requests on the block result in TERAB_ERR_BLOCK_CORRUPT,
   and the block must be opened again and all its contents rewritten.

   An older block may become unknown if another block is committed and
   the common ancestor of the two blocks is so old that Terab chooses to 
   prune the branch.
 */
typedef int32_t block_handle_t;

/* Persistent identifier of an uncommitted block.

   Unlike committed blocks, which have a definite 32-byte block id that 
   can be used to identify them, uncomitted blocks need an identifier to
   represent them across connections. 
 */ 
typedef struct block_ucid { int8_t value[16]; } block_ucid_t;

/* Boolean flags present on a block. */
typedef enum block_flags 
{
  /* The block is frozen. Any attempt to create a child block
     will result in TERAB_ERR_BLOCK_FROZEN. 
  */
  TERAB_BLOCK_FROZEN = 0x01,

  /* The block has been committed. Any attempt to write to this block
     will result in TERAB_ERR_BLOCK_COMMITTED.

     Attempting to create a child block of an uncommitted block will
     result in TERAB_ERR_BLOCK_UNCOMMITTED.
   */
  TERAB_BLOCK_COMMITTED = 0x02,

} block_flags_t;

/* Block metadata, to recursively enumerate the parents. */
typedef struct block_info block_info_t;

struct block_info
{
  block_handle_t parent;
  
  block_flags_t flags;

  int32_t blockheight;

  /* Public blockchain identifier of a committed block. 
     If uncommitted, will contain only zeroes. 
   */
  uint8_t blockid[32]; 
};

/* Uniquely identify a transaction output. */
typedef struct tx_outpoint tx_outpoint_t;

struct tx_outpoint
{
  uint8_t txid[32];
  int32_t index;
};

/* The payload is the binary data attached to a 
   transaction output.

   The payload can be used to encapsulate not only
   the script, but also the locktime if relevant. This
   design is intended to decouple UTXO API from the
   cryptographic validation as much as possible.

   When providing a payload to Terab, `data` should point to 
   a buffer of `length` bytes, containing the payload binary
   data. 

   When receiving a payload from Terab, `length` will always be
   set, but `data` may be null if no buffer was provided to the
   function, or if it was too small. If set, `data` will always
   point to a buffer of `length` bytes.
 */
typedef struct tx_payload tx_payload_t;

struct tx_payload
{
  int32_t length;
  uint8_t* data;
};

/* A self-sufficient transaction output, spent or not,
  intended for the validation of input transactions.

  An outpoint is first created (unspent state) and
  then later spent. The UTXO dataset includes all the
  created-but-not-yet-spent outpoints.

  A given branch of the Bitcoin spend-tree cannot
  exhibit more than two events associated to a given
  transaction output: its production, and its consumption.
  
  produced: identifies the block where the output
            has been produced (always defined).

  spent: identifies the block where the the output
         has been consumed (zero if not).
*/
typedef struct txo txo_t;

struct txo
{
  tx_outpoint_t outpoint;
  block_handle_t produced;
  block_handle_t spent;
  int64_t satoshi;
  tx_payload_t payload;
};

/* Perform initializations needed for good working order of the Terab client,
   along with environment check.

   Please call this before any other function of this API is called, and
   ensure TERAB_SUCCESS has been returned
*/
int32_t terab_initialize();

/* Clean up and releases all resources used by the Terab client.
*/
int32_t terab_shutdown();

/* Get a connection handle intended for all Terab-related operations.

   connection_string: details to connect to the Terab instance.
   conn: returned as an opaque connection handle.

   Errors: 

   - TERAB_ERR_CONNECTION_FAILED if instance is unreachable, did not respond,
     or failed to provide an understandable response.

   - TERAB_ERR_TOO_MANY_CLIENTS if instance refused connection because the 
	 maximum number of client connections has been reached.

   - TERAB_ERR_AUTHENTICATION_FAILED if instance rejected the credentials 
     included in the connection string.

   - TERAB_ERR_SERVER_UNAVAILABLE if the instance has politely requested to try 
	 again later.    
*/
int32_t terab_connect(
  const char* connection_string,
  connection_t* conn
);

int32_t terab_disconnect(connection_t conn, const char* reason);


/* Acquire a handle to an existing, committed block. 

   conn: opaque connection handle.
   blockid: the 32-byte hash of the requested block.
   block: returned as the handle to the block.

   Errors:

     TERAB_ERR_BLOCK_UNKNOWN if 'blockid' does not correspond
     to a known block.     

   This method is PURE.
 */
int32_t terab_utxo_get_block(
  connection_t conn,
  uint8_t* blockid,
  block_handle_t* block
);

/* Get the metadata associated to a block.

   conn: opaque connection handle.
   block: identifies the targeted block.
   info: contains the response, if any.

   Errors: 

     TERAB_ERR_BLOCK_UNKNOWN if `block` does not reference a known 
     block.

     TERAB_ERR_BLOCK_CORRUPTED if `block` references a block that
     has become corrupted. 

   This method is PURE.
*/
int32_t terab_utxo_get_blockinfo(
  connection_t conn,
  block_handle_t block,
  block_info_t* info
);

/* Get the metadata associated to outpoints.
  
   conn: opaque connection handle.
   block: identifies the block of reference.
   outpoints_length: the number of outpoints in 'outpoints'.
   outpoints: identifies the outpoints to be queried.
   txos: contains the response, if any.
   storage_length: the number of bytes in 'storage'.
   storage: used to store txo payloads.

   Only blocks that are direct or indirect parents of 'block' will be taken
   into account to construct the response.

   If `storage` is provided, the `txos[].payload.data` pointers will point 
   to spans inside it. If not enough memory is provided, some payloads will 
   have a null `txos[].payload.data` pointer, but the `txos[].payload.length`
   will be set as a hint to the amount of memory required. 

   Errors: 

     TERAB_ERR_BLOCK_UNKNOWN if 'block' does not correspond
     to a known block.     

     TERAB_ERR_BLOCK_CORRUPT if 'block' is an open block that has become 
     corrupt. This is an error because otherwise the transactions included 
     in the block and lost due to corruption would be silently ignored.

   The method is PURE.
*/
int32_t terab_utxo_get(
  connection_t conn,
  block_handle_t block,
  int32_t outpoints_length,
  tx_outpoint_t* outpoints,
  txo_t* txos,
  size_t storage_length,
  uint8_t* storage
);

/* Starts the write sequence for a new block.
   
   conn: opaque connection handle.
   parent: identifies the parent of the new block.   
   block: returned as the handle to the new block.
   block_ucid: optional, if provided will be updated to contain
               the persistent uncommitted block id of the new block.

   Errors: 

   - TERAB_ERR_BLOCK_FROZEN in UTXO configuration if 'parent' is
     more than 100 blocks away from the head of the longest chain stored
     in Terab.

   - TERAB_ERR_BLOCK_UNKNOWN if 'parent' does not correspond to 
     a known block.

   - TERAB_ERR_BLOCK_UNCOMMITTED if 'parent' exists but is not 
     committed yet.

   - TERAB_ERR_BLOCK_COMMITTED if 'blockid' exists and is already
     committed.

   This method is IDEMPOTENT so long as the block does not 
   become corrupt. Once the block becomes corrupt, calling this function
   will clear the contents of the block and generate a new handle. 
*/
int32_t terab_utxo_open_block(
  connection_t conn,
  block_handle_t parent,
  block_handle_t* block,
  block_ucid_t* block_ucid
);

/* Write new outputs and their payloads to a new block.
  
   conn: opaque connection handle.
   block: identifies the block being written to.
   txo_length: specifies the number of outpoints to be written.
   txos: specifies what should be written to the block.

   Errors: 

   - TERAB_ERR_BLOCK_COMMITTED if 'block' is already committed.

   - TERAB_ERR_BLOCK_UNKNOWN if 'block' does not correspond to a
     known block.

   - TERAB_ERR_BLOCK_CORRUPTED if the block has become corrupted. 
     This error is non-recoverable: open a new block and start 
     writing there. 

   - TERAB_ERR_INVALID_REQUEST if one or more txo fields contradicts
     data specified by other blocks, or if a txo is malformed.

   - TERAB_ERR_INCONSISTENT_REQUEST if one or more txo fields contradict
     data specified for this block by previous calls to `terab_utxo_write_txs`
     (or the current one, if the same outpoint appears more than once).

   Validation rules: 

   - Submitting a `txo_t` that is identical to the current state of the
     outpoint on the terab instance is always valid (by idempotence).

   - The `txo_t.produced` and `txo_t.spent` represent production or 
     spending events for the transaction output in the chain leading up 
     to `block`. An event can only be changed if it has not yet happened
     (a value of zero) or if it has happened in `block`, events that happened
     in other blocks can no longer be changed.
     
     When an event can be changed, the corresponding field may only be set to 
     `block` (to indicate that the event occurred in the current block) or to
     zero (to undo a previous write that set the field to `block`, thereby
     "deleting" the event).
     
   - If `txo_t.produced` is zero, then `txo_t.spent` must also be zero
     (must produce before spending).

   - The `txo_t.satoshi` and `txo_t.payload` values can never be changed 
     once they are set (and there is no reasonable situation where they 
     would need to be changed). The will be the same for all chains in terab
     (not just for the chain leading up to `block`). 
     
     However, if they are already set, then they may be omitted (all bits 
     set to zero), in which case the existing values will be kept.

   This method is IDEMPOTENT.
*/
int32_t terab_utxo_write_txs(
  connection_t conn,
  block_handle_t block,
  int32_t txo_length,
  txo_t* txos
);

/* Acquire a handle to an existing, uncommitted block. 

   conn: opaque connection handle.
   block_ucid: the persistent identifier retrieved from the call
               to `terab_utxo_open_block`
   block: returned as the handle to the block.

   Errors: 

   - TERAB_ERR_BLOCK_UNKNOWN if 'block_ucid' does not correspond to a 
     known block. 

   - TERAB_ERR_BLOCK_CORRUPTED if a corresponding block exists but is 
     corrupted. 

   An uncomitted block may still be retrieved by its block_ucid for a short
   duration after it has been committed (until the association is purged
   from the terab instance).

   This function is PURE.
 */
int32_t terab_utxo_get_uncommitted_block(
  connection_t conn,
  block_ucid_t block_ucid,
  block_handle_t* block
);

/* Closes the write sequence for a new block.
   
   conn: opaque connection handle.
   block: identifies the block to be committed.
   blockid: the 32-byte hash of the block.

   Errors: 

   - TERAB_ERR_BLOCK_CORRUPTED if the block has become corrupted. 
     This error is non-recoverable: open a new block and start 
     writing there. 

   - TERAB_ERR_BLOCK_UNKNOWN if the block is unknown. Make sure
     that it was obtained from `terab_utxo_open_block` or 
     `terab_utxo_get_block` on the same connection. 

   - TERAB_ERR_BLOCK_COMMITTED if there exists another block with 
     the provided `blockid`.

   This method is IDEMPOTENT: attempting to commit a block that 
   is already committed simply succeeds.
*/
int32_t terab_utxo_commit_block(
  connection_t conn,
  block_handle_t block,
  uint8_t* blockid
);

#define TERAB_SUCCESS                     0 /* Successful call. */
/* Failed to connect to the Terab service. */
#define TERAB_ERR_CONNECTION_FAILED       1 
/* Connection rejected, too many clients. */
#define TERAB_ERR_TOO_MANY_CLIENTS        2 
/* Failed to authenticate with the Terab service. */
#define TERAB_ERR_AUTHENTICATION_FAILED   3 
/* Terab service is not ready yet to accept requests. */
#define TERAB_ERR_SERVICE_UNAVAILABLE     4 
/* Too many requests are concurrently made to Terab. */
#define TERAB_ERR_TOO_MANY_REQUESTS       5 
/* Something wrong happened. Contact the Terab team. */
#define TERAB_ERR_INTERNAL_ERROR          6 
/* No more storage left for the write operation. */
#define TERAB_ERR_STORAGE_FULL            7 
/* Non-recoverable data corruption at the service level. */
#define TERAB_ERR_STORAGE_CORRUPTED       8 
/* The block being written is corrupted and cannot be recovered. */
#define TERAB_ERR_BLOCK_CORRUPTED         9 
/* This block is too old and does not accept new children blocks. */
#define TERAB_ERR_BLOCK_FROZEN           10 
/* This block is committed and does not accept new txs. */
#define TERAB_ERR_BLOCK_COMMITTED        11 
/* This block is not committed and does not accept children blocks. */
#define TERAB_ERR_BLOCK_UNCOMMITTED      12 
/* A block handle refers to an unknown block. */
#define TERAB_ERR_BLOCK_UNKNOWN          13 
/* Broken idempotence. Request contradicts previous one.*/
#define TERAB_ERR_INCONSISTENT_REQUEST   14 
/* Generic invalidity of the arguments of the request. */
#define TERAB_ERR_INVALID_REQUEST        15 


#ifdef __cplusplus
}  // extern "C"
#endif
