#pragma once

#include "../../Terab.StdAPI/terab_utxo.h"
#include "connection_impl.h"

int32_t get_block_handle(connection_impl* conn, const uint8_t* blockid, block_handle_t* result);
int32_t open_block(connection_impl* conn, block_handle_t parent, block_handle_t* block, block_ucid_t* block_ucid);
int32_t get_uncommitted_block(connection_impl* conn, block_ucid_t block_ucid, block_handle_t* result);
int32_t commit_block(connection_impl* conn, block_handle_t block, uint8_t* blockid);
int32_t get_blockinfo(connection_impl* conn, block_handle_t block, block_info_t* info);

int32_t write_txs(connection_impl* conn, block_handle_t block, int32_t txo_length, txo_t* txos);

typedef enum {
	request_authenticate = 4,
	request_open_block = 1,
	request_get_block_handle = 17,
	request_get_uncommitted_block_handle = 20,
	request_commit_block = 3,
	request_get_block_info = 21,
	request_write_raw_txo = 32, // does not exists on the server side yet
} request_type;


typedef enum : int32_t {
	server_busy = 1,
	too_many_clients = 2,
	authenticated = 4,

	// faults:
	request_too_long = 8,
	request_too_short = 9,
	client_id_field_not_empty = 10,
	out_buffer_full = 11,

	everything_ok = 16,
	block_handle = 17,
	ancestor_response = 18,
	pruneable_response = 19,
	uncommitted_block_info = 20,
	committed_block_info = 21,
	opened_block = 22,

	// this one does not exists (yet) on the server:

} response_type;

struct response_everything_ok
{
};

struct response_block_handle
{
	int32_t block_handle;
};

struct response_opened_block
{
	int32_t alias;
	block_ucid_t identifier;
};

struct response_committed_block_information
{
	uint8_t id[32];
	int32_t alias;
	int32_t parent;
	int32_t height;
};

struct response_uncommitted_block_information
{
	uint8_t id[16];
	int32_t alias;
	int32_t parent;
	int32_t height;
};

