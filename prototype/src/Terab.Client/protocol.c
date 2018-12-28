#include "protocol.h"
#include "connection.h"
#include "ranges.h"

typedef struct {
	uint32_t size;
	uint32_t requestId;
	uint32_t clientId;
	response_kind kind;
} header_response_s;

header_response_s read_response_header(range* source);

open_block_response_s read_open_block(range* source);
commit_block_response_s read_commit_block(range* source);
get_block_handle_response_s read_get_block_handle(range* source);
get_block_info_response_s read_get_block_info(range* source);

// Header - read & write
header_response_s read_response_header(range* source) {
	header_response_s header;
	header.size = read_uint32(source);
	header.requestId = read_uint32(source);
	header.clientId = read_uint32(source); // expected to be zero
	header.kind = read_uint32(source);
	return header;
}

void write_header(range* buffer, int32_t message_kind)
{
	clear_uint32(buffer);				// message length
	clear_uint32(buffer);				// requestId
	clear_uint32(buffer);				// clientId
	write_int32(buffer, message_kind);  // message kind
}

// Open Block
terab_status_enum_t open_block(
	connection_s* conn,
	block_id_t* parent_id,
	block_handle_t* block,
	block_ucid_t* block_ucid
)
{
	range buffer = connection_get_send_buffer(conn);
	write_header(&buffer, open_block_request);
	write_bytes(&buffer, (char*)parent_id, 32);

	if (!connection_send_request(conn, buffer.begin, NULL))
		return TSE_INTERNAL_ERROR;

	if (!connection_wait_response(conn, &buffer))
		return TSE_INTERNAL_ERROR;

	header_response_s header = read_response_header(&buffer);

	if (header.kind != open_block_response)
		return TSE_INTERNAL_ERROR;

	open_block_response_s response = read_open_block(&buffer);
	
	block_ucid_t zero = { 0 };

	switch (response.status)
	{
	case obs_success:
		*block = response.handle;
		*block_ucid = response.identifier;
		return TSE_SUCCESS;

	case obs_parent_not_found:
		*block = 0; // do not expose uninitialized memory
		*block_ucid = zero; 
		return TSE_BLOCK_UNKNOWN;
	}

	return TSE_INTERNAL_ERROR;
}

open_block_response_s read_open_block(range* source)
{
	open_block_response_s resp;
	resp.status = read_uint8(source);
	resp.handle = read_uint32(source);

	for (int i = 0; i < 16; ++i)
		resp.identifier.value[i] = read_uint8(source);

	return resp;
}

// Commit Block
terab_status_enum_t commit_block(connection_s* conn, block_handle_t block, block_id_t* blockid)
{
	range buffer = connection_get_send_buffer(conn);
	write_header(&buffer, commit_block_request);
	write_uint32(&buffer, block);
	write_bytes(&buffer, (char*)blockid, 32);

	if (!connection_send_request(conn, buffer.begin, NULL))
		return TSE_INTERNAL_ERROR;

	if (!connection_wait_response(conn, &buffer))
		return TSE_INTERNAL_ERROR;

	header_response_s header = read_response_header(&buffer);

	if (header.kind != commit_block_response)
		return TSE_INTERNAL_ERROR;

	commit_block_response_s response = read_commit_block(&buffer);

	switch (response.status)
	{
	case cbs_success:
		return TSE_SUCCESS;
	case cbs_block_not_found:
		return TSE_BLOCK_UNKNOWN;
	case cbs_block_id_mismatch:
		return TSE_BLOCK_COMMITTED;
	}

	return TSE_INTERNAL_ERROR;
}

commit_block_response_s read_commit_block(range* source)
{
	commit_block_response_s resp;
	resp.status = read_uint8(source);

	return resp;
}

// Get Committed Block Handle
terab_status_enum_t get_committed_block_handle(connection_s* conn, block_id_t* blockid, block_handle_t* result)
{
	range buffer = connection_get_send_buffer(conn);
	write_header(&buffer, get_block_handle_request);

	write_bytes(&buffer, (char*)blockid, 32);  // Committed Block Id
	clear_bytes(&buffer, 16);                 // Uncommitted Block Id
	write_uint8(&buffer, (uint8_t)1);         // Is Committed?

	if (!connection_send_request(conn, buffer.begin, NULL))
		return TSE_INTERNAL_ERROR;

	if (!connection_wait_response(conn, &buffer))
		return TSE_INTERNAL_ERROR;

	header_response_s header = read_response_header(&buffer);

	if (header.kind != get_block_handle_response)
		return TSE_INTERNAL_ERROR;

	get_block_handle_response_s response = read_get_block_handle(&buffer);

	switch (response.status)
	{
	case gbh_success:
		*result = response.handle;
		return TSE_SUCCESS;
	case gbh_block_not_found:
		*result = 0;
		return TSE_BLOCK_UNKNOWN;
	}

	return TSE_INTERNAL_ERROR;
}

// Get Uncommitted Block Handle
terab_status_enum_t get_uncommitted_block_handle(
	connection_s* conn,
	block_ucid_t* block_ucid,
	block_handle_t* result
)
{
	range buffer = connection_get_send_buffer(conn);
	write_header(&buffer, get_block_handle_request);

	clear_bytes(&buffer, 32);                    // Committed Block Id
	write_bytes(&buffer, (char*)block_ucid, 16);  // Uncommitted Block Id
	write_uint8(&buffer, (uint8_t)0);            // Is Committed?

	if (!connection_send_request(conn, buffer.begin, NULL))
		return TSE_INTERNAL_ERROR;

	if (!connection_wait_response(conn, &buffer))
		return TSE_INTERNAL_ERROR;

	header_response_s header = read_response_header(&buffer);

	if (header.kind != get_block_handle_response)
		return TSE_INTERNAL_ERROR;

	get_block_handle_response_s response = read_get_block_handle(&buffer);

	switch (response.status)
	{
	case gbh_success:
		*result = response.handle;
		return TSE_SUCCESS;
	case gbh_block_not_found:
		*result = 0;
		return TSE_BLOCK_UNKNOWN;
	}

	return TSE_INTERNAL_ERROR;
}

get_block_handle_response_s read_get_block_handle(range* source)
{
	get_block_handle_response_s resp;
	resp.status = read_uint8(source);
	resp.handle = read_uint32(source);

	return resp;
}

// Get Block Info
terab_status_enum_t get_block_info(connection_s* conn, block_handle_t block, block_info_t* info)
{
	range buffer = connection_get_send_buffer(conn);
	write_header(&buffer, get_block_info_request);
	write_uint32(&buffer, block);

	if (!connection_send_request(conn, buffer.begin, NULL))
		return TSE_INTERNAL_ERROR;

	if (!connection_wait_response(conn, &buffer))
		return TSE_INTERNAL_ERROR;

	header_response_s header = read_response_header(&buffer);

	if (header.kind != get_block_info_response)
		return TSE_INTERNAL_ERROR;

	get_block_info_response_s response = read_get_block_info(&buffer);

	info->parent = response.parent;
	info->flags = response.isCommitted == 1 ? TERAB_BLOCK_COMMITTED : 0;
	info->blockheight = response.blockheight;
	info->blockid = response.blockid;

	return TSE_SUCCESS;
}

get_block_info_response_s read_get_block_info(range* source)
{
	get_block_info_response_s resp;
	
	read_bytes(source, (char*)&resp.blockid, 32);
	read_bytes(source, (char*)&resp.block_ucid, 16);
	resp.handle = read_uint32(source);
	resp.parent = read_uint32(source);
	resp.blockheight = read_int32(source);
	resp.isCommitted = read_uint8(source);

	return resp;
}

// Set Coins
terab_status_enum_t set_coins(
	connection_s* conn, 
	block_handle_t context, 
	int32_t coin_length, 
	coin_t* coins, 
	int32_t storage_length,
	uint8_t* storage)
{
	// Send one request per coin
	uint32_t requestId = 0;
	coin_t* end = coins + coin_length;

	connection_batch_begin(conn);
	for (coin_t* coin = coins; coin < end; coin++)
	{
		range buffer = connection_get_send_buffer(conn);

		if (coin->script_offset < 0)
			return TERAB_ERR_INVALID_REQUEST;

		// Coin production request
		// ---------------
		if (coin->production != 0)
		{
			if (coin->script_length <= 0)
				return TERAB_ERR_INVALID_REQUEST;

			write_header(&buffer, produce_coin_request);

			write_bytes(&buffer, (char*)&coin->outpoint, sizeof(outpoint_t));
			write_uint32(&buffer, context);
			write_uint8(&buffer, coin->flags);
			write_uint64(&buffer, coin->satoshis);
			write_uint32(&buffer, coin->nLockTime);
			write_bytes(&buffer, (char*)(storage + coin->script_offset), coin->script_length);
		}
		// Coin consumption request
		// ----------------
		else if (coin->consumption != 0)
		{
			write_header(&buffer, consume_coin_request);

			write_bytes(&buffer, (char*)&coin->outpoint, sizeof(outpoint_t));
			write_uint32(&buffer, context);
		}
		// Coin removal request
		// ----------------
		else if (coin->production == 0 && coin->consumption == 0)
		{
			write_header(&buffer, remove_coin_request);

			write_bytes(&buffer, (char*)&coin->outpoint, sizeof(outpoint_t));
			write_uint32(&buffer, context);
			write_uint8(&buffer, /* Remove Production*/ 1);
			write_uint8(&buffer, /* Remove Consumption*/ 1);
		}
		else
		{
			return TERAB_ERR_INVALID_REQUEST;
		}

		// Get 'requestId' of the first coin
		if (!connection_send_request(conn, buffer.begin, (coin == coins) ? &requestId : NULL))
			return TSE_INTERNAL_ERROR;
	}
	connection_batch_end(conn);

	// Receive one response per request (not in the same order)
	for (int32_t i = 0; i < coin_length; i++)
	{
		range buffer = connection_get_send_buffer(conn);

		if (!connection_wait_response(conn, &buffer))
			return TSE_INTERNAL_ERROR;

		header_response_s header = read_response_header(&buffer);

		change_coin_response_s response;
		response.status = read_uint8(&buffer);

		switch (header.kind)
		{
		case produce_coin_response:
		case consume_coin_response:
		case remove_coin_response:
			break;
		default:
			return TSE_INTERNAL_ERROR;
		}

		coin_t* coin = coins + (header.requestId - requestId);

		switch (response.status)
		{
		case ccs_success:
			coin->status = TERAB_COIN_STATUS_SUCCESS;
			break;
		case ccs_outpoint_not_found:
			coin->status = TERAB_COIN_STATUS_OUTPOINT_NOT_FOUND;
			break;
		case ccs_invalid_context:
			coin->status = TERAB_COIN_STATUS_INVALID_CONTEXT;
			break;
		case ccs_invalid_block_handle:
			coin->status = TERAB_COIN_STATUS_INVALID_BLOCK_HANDLE;
			break;
		default:
			return TSE_INTERNAL_ERROR;
		}
	}

	return TSE_SUCCESS;
}

// Get Coins
terab_status_enum_t get_coins(
	connection_s* conn,
	block_handle_t context,
	int32_t coin_length,
	coin_t* coins,
	range* storage
)
{
	// Send one request per outpoint
	uint32_t requestId = 0;
	coin_t* end = coins + coin_length;

	connection_batch_begin(conn);
	for (coin_t* coin = coins; coin < end; coin++)
	{
		range buffer = connection_get_send_buffer(conn);

		write_header(&buffer, get_coin_request);

		write_bytes(&buffer, (char*)&coin->outpoint, sizeof(outpoint_t));
		write_uint32(&buffer, context);

		// Get 'requestId' of the first outpoint
		if (!connection_send_request(conn, buffer.begin, (coin == coins) ? &requestId : NULL))
			return TSE_INTERNAL_ERROR;
	}
	connection_batch_end(conn);

	// Receive one response per outpoint (not in the same order)
	int32_t script_offset = 0;
	for (int32_t i = 0; i < coin_length; i++)
	{
		range buffer = connection_get_send_buffer(conn);

		if (!connection_wait_response(conn, &buffer))
			return TSE_INTERNAL_ERROR;

		char* response_origin = buffer.begin;

		header_response_s header = read_response_header(&buffer);

		if(header.kind != get_coin_response)
			return TSE_INTERNAL_ERROR;

		get_coin_response_s response;
		response.status = read_uint8(&buffer);
		read_bytes(&buffer, (char*)(&response.outpoint), sizeof(outpoint_t));
		response.flags = read_uint8(&buffer);
		response.context = read_uint32(&buffer);
		response.production = read_uint32(&buffer);
		response.consumption = read_uint32(&buffer);
		response.satoshis = read_uint64(&buffer);
		response.nLockTime = read_uint32(&buffer);

		int32_t script_length = header.size - (uint32_t)(buffer.begin - response_origin);

		coin_t* coin = coins + (header.requestId - requestId);

		coin->outpoint = response.outpoint;
		coin->production = response.production;
		coin->consumption = response.consumption;
		coin->satoshis = response.satoshis;
		coin->nLockTime = response.nLockTime;
		coin->flags = response.flags;
		
		coin->script_offset = script_offset;
		coin->script_length = script_length;	

		switch (response.status)
		{
		case gcs_success:
			coin->status = TERAB_COIN_STATUS_SUCCESS;
			break;
		case gcs_outpoint_not_found:
			coin->status = TERAB_COIN_STATUS_OUTPOINT_NOT_FOUND;
			break;
		default:
			return TSE_INTERNAL_ERROR;
		}

		// Copy the script if storage capacity allows
		if (range_len(*storage) >= script_length)
		{
			copy_range(storage, buffer, script_length);
		}
		else
		{
			coin->status |= TERAB_COIN_STATUS_STORAGE_TOO_SHORT;
		}

		script_offset += script_length;
	}

	return TSE_SUCCESS;
}
