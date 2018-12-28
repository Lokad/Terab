/* © 2018. Lokad SAS */

/* Terab C/C++ API

  This file should be considered as the primary integration point for any app
  that leverages Terab as its UTXO storage backend. This API is intended to be
  maintained in a backward compatible way for the later versions of Terab.
*/

#pragma once

#include <stdint.h>

#ifdef __cplusplus
extern "C" {
#endif

// Explicit packing to facilitate interop.
#pragma pack(1)

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
typedef uint32_t block_handle_t;

/* Persistent identifier of a committed block.
*/
typedef struct block_id { uint8_t value[32]; } block_id_t;

/* Persistent identifier of an uncommitted block.

   Unlike committed blocks, which have a definite 32-byte block id that 
   can be used to identify them, uncomitted blocks need an identifier to
   represent them across connections. 
 */ 
typedef struct block_ucid { uint8_t value[16]; } block_ucid_t;

/* Boolean flags present on a block (within 'block_info_t'). */

  /* The block is frozen. Any attempt to create a child block
     will result in TERAB_ERR_BLOCK_FROZEN. 
  */
  #define TERAB_BLOCK_FROZEN		0x01

  /* The block has been committed. Any attempt to write to this block
     will result in TERAB_ERR_BLOCK_COMMITTED.

     Attempting to create a child block of an uncommitted block will
     result in TERAB_ERR_BLOCK_UNCOMMITTED.
   */
  #define TERAB_BLOCK_COMMITTED		0x02


/* Block metadata, to recursively enumerate the parents. */
typedef struct block_info block_info_t;

/*
  Details about a block.
  'flags' is not represented with an 'enum' in order to facilitate
  interop, which requires fixed-size members.
*/
struct block_info
{
  block_handle_t parent;
  
  uint32_t flags;

  int32_t blockheight;

  /* Public blockchain identifier of a committed block. 
     If uncommitted, will contain only zeroes. 
   */
  block_id_t blockid;
};

/* Uniquely identify a transaction output. */
typedef struct outpoint outpoint_t;

struct outpoint
{
  uint8_t txid[32];
  int32_t index;
};

/* Persisted boolean flags on a coin (within 'coin_t'). */

  /* The block is frozen. Any attempt to create a child block
	 will result in TERAB_ERR_BLOCK_FROZEN.
  */
#define TERAB_COIN_FLAGS_COINBASE		0x01

/* Status of a coin (within 'coin_t'). */

  /* No status. */
#define TERAB_COIN_STATUS_NONE                      0

  /* Get or Set completed successfully over the coin. */
#define TERAB_COIN_STATUS_SUCCESS                   1
  
  /* Get coin failed because outpoint could not be found.*/
#define TERAB_COIN_STATUS_OUTPOINT_NOT_FOUND        2

  /* The block identified by the handle is too old to be used as context. */
#define TERAB_COIN_STATUS_INVALID_CONTEXT           4

  /* The block handle is invalid, and cannot be attached to a known block. */
#define TERAB_COIN_STATUS_INVALID_BLOCK_HANDLE      8

  /* Get coin partially failed because the script could not be written to the storage. */
#define TERAB_COIN_STATUS_STORAGE_TOO_SHORT         16

/* A coin, identified by its outpoint, and asssociated to two blocks which 
  represent the events in its lifecycle, namely its production and consumption.

  A coin is first produced, and thus enters the UTXO dataset. This coin can
  later be spent. The strict UTXO includes only the produced-but-not-spent
  yet coins.
 
  production: identifies the block where the output
              has been produced (zero if not).

  consumption: identifies the block where the the output
              has been consumed (zero if not).

  satoshis: the monetary amount associated to the coin.

  nLockTime: parameter to claim the coins (actually duplicated among all
		coins originating from the same transaction).

  script_offset:
  script_length:
		The script is intended to be externally stored in a buffer provided 
		on the  side. Indices represents the data segment  within this buffer 
		associated to the script of the coin. When the coin is found, the
		'script_length' is returned. However, if 'TERAB_COIN_STATUS_STORAGE_TOO_SHORT'
		then, no corresponding script can be loaded from 'storage'. The script
		length is returned however, to facilitate choosing an adequate size for
		the storage during a later 'get_coins' attempt.

  flags: misc flags intended for persistence within the UTXO set.

  status: return code at the coin-level (nb: populated through side-effect
        when 'terab_utxo_set_coins()' is called).

  The script is the binary data attached to a transaction output. Terab has no
  specific affinity to the Bitcoin scripting system, hence the script is treated
  as a binary payload without any attempt at inspecting this data. The intent is
  to decouple the UTXO API from the cryptographic validation as much as possible.

*/
typedef struct coin coin_t;

struct coin
{
  outpoint_t outpoint;
  block_handle_t production;
  block_handle_t consumption;
  uint64_t satoshis;
  uint32_t nLockTime;
  int32_t script_offset;
  int32_t script_length;
  uint8_t flags;
  uint8_t status;
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

/* Starts the write sequence for a new block.

   conn: opaque connection handle.
   parentid: the 32-byte hash of the requested block.
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
	block_id_t* parentid,
	block_handle_t* block,
	block_ucid_t* block_ucid
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
	 `terab_utxo_get_committed_block` on the same connection.

   - TERAB_ERR_BLOCK_COMMITTED if there exists another block with
	 the provided `blockid`.

   This method is IDEMPOTENT: attempting to commit a block that
   is already committed simply succeeds.
*/
int32_t terab_utxo_commit_block(
	connection_t conn,
	block_handle_t block,
	block_id_t* blockid
);

/* Acquire a handle to an existing, committed block. 

   conn: opaque connection handle.
   blockid: the 32-byte hash of the requested block.
   block: returned as the handle to the block.

   Errors:

     TERAB_ERR_BLOCK_UNKNOWN if 'blockid' does not correspond
     to a known block.     

   This method is PURE.
 */
int32_t terab_utxo_get_committed_block(
  connection_t conn,
  block_id_t* blockid,
  block_handle_t* block
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
	block_ucid_t* block_ucid,
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
   context: identifies the block of reference.
   coin_length: the number of coins in 'coin'.
   coins: contains the outpoints to be queried, overwritten with responses.
   storage_length: the number of bytes in 'storage'.
   storage: used to store coin scripts.

   Only blocks that are direct or indirect parents of 'block' will be taken
   into account to construct the response.

   Upon return, the `coins[].script_offset` offsets point to spans inside the
   `storage` buffer. If the storage is too short then some coins  have a 
   `coins[].status flaggged with `TERAB_COIN_STATUS_STORAGE_TOO_SHORT`.
   However `coins[].script_length` will be set as a hint to the amount of 
   memory required. 

   Errors: 

     TERAB_ERR_BLOCK_UNKNOWN if 'block' does not correspond
     to a known block.     

     TERAB_ERR_BLOCK_CORRUPT if 'block' is an open block that has become 
     corrupt. This is an error because otherwise the transactions included 
     in the block and lost due to corruption would be silently ignored.

   The method is PURE.
*/
int32_t terab_utxo_get_coins(
  connection_t conn,
  block_handle_t context,
  int32_t coin_length,
  coin_t* coins,
  int32_t storage_length,
  uint8_t* storage
);

/* Write new outputs and their scripts to a new block.
  
   conn: opaque connection handle.
   context: identifies the block being written to.
   coin_length: specifies the number of outpoints to be written.
   coins: specifies what should be written to the block.
   storage_length: the number of bytes in 'storage'.
   storage: used to store coin scripts.

   Errors: 

   - TERAB_ERR_BLOCK_COMMITTED if 'block' is already committed.

   - TERAB_ERR_BLOCK_UNKNOWN if 'block' does not correspond to a
     known block.

   - TERAB_ERR_BLOCK_CORRUPTED if the block has become corrupted. 
     This error is non-recoverable: open a new block and start 
     writing there. 

   - TERAB_ERR_INVALID_REQUEST if one or more coin fields contradicts
     data specified by other blocks, or if a coin is malformed.

   - TERAB_ERR_INCONSISTENT_REQUEST if one or more coin fields contradict
     data specified for this block by previous calls to `terab_utxo_set_coins`
     (or the current one, if the same outpoint appears more than once).

   Validation rules: 

   - Submitting a `coin_t` that is identical to the current state of the
     outpoint on the terab instance is always valid (by idempotence).

   - The `coin_t.production` and `coin_t.consumption` represent production or 
     spending events for the transaction output in the chain leading up 
     to `block`. Those events are exclusive, and one of the must be
	 zero at every one.

     When an event can be changed, the corresponding field may only be set to 
     `block` (to indicate that the event occurred in the current block) or to
     zero (to undo a previous write that set the field to `block`, thereby
     "deleting" the event).
     
   - If `coin_t.production` is non zero, then `coin_t.consumption` must be zero, and
     the `coin_t.script.length` must be greater than zero.

   - If `coin_t.consumption` is non zero, then `coin_t.production` must be zero, and
     the `coin_t.script.length` must be zero as well. The values of `coin_t.satoshi`
	 and `coin_t.nLockTime` must be zero.

   - The `coin_t.satoshi`, `coin_t.nLockTime` and the content of `coin_t.script` 
     can never be changed once set (and there is no reasonable situation where they 
     would need to be changed). The will be the same for all chains in terab
     (not just for the chain leading up to `block`). 

   This method is IDEMPOTENT.
*/
int32_t terab_utxo_set_coins(
  connection_t conn,
  block_handle_t context,
  int32_t coin_length,
  coin_t* coins,
  int32_t storage_length,
  uint8_t* storage
);

/* Successful call. */
#define TERAB_SUCCESS                     0 

/* Failed to connect to the Terab service (misc. happens).
This problem might be caused by a network connectivity issue, or because 
the Terab service is down or even non-existent. */
#define TERAB_ERR_CONNECTION_FAILED       1 

/* Connection rejected, too many clients (misc. happens).
This problem is caused by the client app(s) which have spawned too many
concurrent connections. */
#define TERAB_ERR_TOO_MANY_CLIENTS        2 

/* Failed to authenticate with the Terab service (misc. happens).
The client did connect to the Terab service, but the method call was 
rejected at the authentication level. This problem is most likely caused 
by a configuration mismatch between the Terab instance and the configuration 
of the client.*/
#define TERAB_ERR_AUTHENTICATION_FAILED   3 

/* Terab service is not ready yet to accept requests  (misc. happens).
This problem is transient and should addressed on the client side by a 
retry-policy with fixed back-off.*/
#define TERAB_ERR_SERVICE_UNAVAILABLE     4 

/* Too many requests are concurrently made to Terab (broken client).
This is problem is transient and may be addressed on the client side by 
a retry-policy with exponential back-off. Then, it is preferable if 
client implementations can avoid hitting the limit altogether through a 
design that ensures that the cap on concurrent connections is never reached. */
#define TERAB_ERR_TOO_MANY_REQUESTS       5 

/* Something wrong happened (broken service).
This error should not happen, and reflects a defect in the Terab 
implementation itself. It is advised to not even try fixing this problem 
on the client side, but to take contact with the team in charge of the 
Terab implementation. */
#define TERAB_ERR_INTERNAL_ERROR          6 

/* No more storage left for the write operation (misc. happens).
All subsequent operations, both reads and writes, should be expected
to fail as well. Indeed, read operations should be expected to start
failing as well because the Terab instance may not be able to properly
operate while ensuring that internal logs are properly persisted.
Depending on the Terab implementation, the current instance might be
physically upgraded or replaced. The client implementation is not
expected to be able to mitigate this problem in any way. */
#define TERAB_ERR_STORAGE_FULL            7 

/* Non-recoverable data corruption at the service level (misc. happens).
All subsequent operations, both reads and writes, should be expected
to fail. Depending on the Terab implementation, the instance might be
repairable or recoverable; or not. The client implementation is not
expected to be able to mitigate this problem in any way. */
#define TERAB_ERR_STORAGE_CORRUPTED       8 

/* The block being written is corrupted and cannot be recovered (misc. happens).
Due to the weak durability offered by Terab, a transient problem such 
as a power cycle may corrupt a block being written. In this case, all
the data associated to the uncommitted block should be considered as 
lost. If a block is corrupted, the client implementation should open 
a new block and repeat all the writes for this block. The client 
implementation is expected to be capable of recovering from this 
problem. */
#define TERAB_ERR_BLOCK_CORRUPTED         9 

/* This block is too old and does not accept new children blocks (broken client).
The block is too far from the chain tip according to the configuration
of the Terab instance. Indeed, new blocks or block writes can only be 
made against recent parts of the blockchain. As the client is expected 
to know beforehand which blocks remain eligible for blockchain extension, 
the client implementation is expected to avoid this problem altogether.
*/
#define TERAB_ERR_BLOCK_FROZEN           10 

/* This block is committed and does not accept new coin events (broken client).
This problem occurs when a client app attemps to modify a block that
is already committed. This problem should be addressed by the client
app. */
#define TERAB_ERR_BLOCK_COMMITTED        11 

/* A block handle refers to an unknown block (broken client).
This problem is caused by client implementations. */
#define TERAB_ERR_BLOCK_UNKNOWN          12 

/* Broken idempotence. Request contradicts previous one (broken client).
The content of the method call contradicts, at the block level, the 
content previously written by another method call. The stand-alone 
content of the call is valid; it is only deemed incorrect as it is 
inconsistent with the state of Terab. This error code strictly 
reflects a class of problems that can only be obtained through 
multiple method calls which, taken in isolation, would have been 
considered correct. In practice, it is expected that those problems 
emerge as race conditions within the client implementation. */
#define TERAB_ERR_INCONSISTENT_REQUEST   13 

/* Generic invalidity of the arguments of the request (broken client).
The content of the method call is deemed incorrect. This assertion is 
made independently from the state of the blockchain. For example, 
negative buffer lengths are always invalid. The error code captures 
the broad class of problems that could arise when the client 
implementation attempts to push corrupted data to the Terab instance. 
The client implementation is expected to avoid this problem altogether. */
#define TERAB_ERR_INVALID_REQUEST        14 

#pragma pack()

#ifdef __cplusplus
}  // extern "C"
#endif
