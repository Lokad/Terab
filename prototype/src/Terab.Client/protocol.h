/* Terab Client-Server protocol

  This file implements the client-server protocol of Terab which operates
  over a simple TCP socket. Unlike the primary C/C++ API of Terab, backward 
  compatibility is not intended to be maintained for the protocol itself.
*/

#pragma once

#include "terab.h"
#include "connection.h"
#include "status.h"

/* Typed version of return codes in the Terab API.

   As enum types are not implicitely convertible to another one, this design avoids 
   to mistakenly convert a status type into another type.
   */
typedef enum
{
	TSE_SUCCESS = TERAB_SUCCESS,
#define TSE_(x) TSE_ ## x = TERAB_ERR_ ## x
	TSE_(CONNECTION_FAILED),
	/* the above expands to
		  TSE_CONNECTION_FAILED = TERAB_ERR_CONNECTION_FAILED
	   which in turn expands to:
		  TSE_CONNECTION_FAILED = 1
	*/
	TSE_(TOO_MANY_CLIENTS),
	TSE_(AUTHENTICATION_FAILED),
	TSE_(SERVICE_UNAVAILABLE),
	TSE_(TOO_MANY_REQUESTS),
	TSE_(INTERNAL_ERROR),
	TSE_(STORAGE_FULL),
	TSE_(STORAGE_CORRUPTED),
	TSE_(BLOCK_CORRUPTED),
	TSE_(BLOCK_FROZEN),
	TSE_(BLOCK_COMMITTED),
	TSE_(BLOCK_UNKNOWN),
	TSE_(INCONSISTENT_REQUEST),
	TSE_(INVALID_REQUEST),
#undef TSE_
} terab_status_enum_t;


terab_status_enum_t open_block(connection_s* conn, 
	block_id_t* parent_id, block_handle_t* block, block_ucid_t* block_ucid);

terab_status_enum_t commit_block(connection_s* conn, 
	block_handle_t block, block_id_t* blockid);

terab_status_enum_t get_committed_block_handle(connection_s* conn, 
	block_id_t* blockid, block_handle_t* result);

terab_status_enum_t get_uncommitted_block_handle(connection_s* conn, 
	block_ucid_t* block_ucid, block_handle_t* result);

terab_status_enum_t get_block_info(connection_s* conn, 
	block_handle_t block, block_info_t* info);

terab_status_enum_t set_coins(
	connection_s* conn,
	block_handle_t context,
	int32_t coin_length,
	coin_t* coins,
	int32_t storage_length,
	uint8_t* storage);

terab_status_enum_t get_coins(
	connection_s* conn, 
	block_handle_t context, 
	int32_t coin_length, 
	coin_t* coins, 
	range* storage);

typedef enum {
	/* Connection controller */
	authenticate_request = 2,
	close_connection_request = 4,

	/* Chain controller */
	open_block_request = 16,
	commit_block_request = 18,
	get_block_handle_request = 20,
	get_block_info_request = 22,

	/* Coin controller */
	get_coin_request = 64,
	produce_coin_request = 66,
	consume_coin_request = 68,
	remove_coin_request = 70,
} request_kind;


typedef enum {
	/* Chain controller */
	open_block_response = 17,
	commit_block_response = 19,
	get_block_handle_response = 21,
	get_block_info_response = 23,

	/* Coin controller */
	get_coin_response = 65,
	produce_coin_response = 67,
	consume_coin_response = 69,
	remove_coin_response = 71,

} response_kind;

// Open Block
typedef enum {
	obs_success = 0,
	obs_parent_not_found = 1,
} open_block_status;

typedef struct
{
	open_block_status status;
	uint32_t handle;
	block_ucid_t identifier;
} open_block_response_s;

// Commit Block
typedef enum {
	cbs_success = 0,
	cbs_block_not_found = 1,
	cbs_block_id_mismatch = 2,
} commit_block_status;

typedef struct
{
	commit_block_status status;
} commit_block_response_s;

// Get Block Handle
typedef enum {
	gbh_success = 0,
	gbh_block_not_found = 1,
} get_block_handle_status;

typedef struct
{
	get_block_handle_status status;
	uint32_t handle;
} get_block_handle_response_s;

typedef unsigned char boolean;

// Get Block Information
typedef struct
{
	block_id_t blockid;
	block_ucid_t block_ucid;
	uint32_t handle;
	uint32_t parent;
	int32_t blockheight;
	uint8_t isCommitted;
} get_block_info_response_s;

// Set Coin
typedef enum {
	ccs_success = 0,
	ccs_outpoint_not_found = 1,
	ccs_invalid_context = 2,
	ccs_invalid_block_handle = 3,
} change_coin_status;

typedef struct
{
	change_coin_status status;
} change_coin_response_s;

// Get Coin
typedef enum {
	gcs_success = 0,
	gcs_outpoint_not_found = 1,
} get_coin_status;

typedef struct
{
	get_coin_status status;
	outpoint_t outpoint;
	uint8_t flags;
	block_handle_t context;
	block_handle_t production;
	block_handle_t consumption;
	uint64_t satoshis;
	uint32_t nLockTime;
} get_coin_response_s;

