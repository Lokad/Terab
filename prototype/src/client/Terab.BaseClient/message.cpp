#include "message.h"
#include "connection_impl.h"

#include "ranges.h"

int parse_expected_response(range buffer, response_type expected_type, /* out */ int* has_type, /* out */ response_type* actual_type, size_t expected_size, /* out */ void* result);
int parse_block_handle(range read, size_t expected_size, void* result);
int parse_opened_block(range read, size_t expected_size, void* result);
int parse_everything_ok(range read, size_t expected_size, void* result);
int parse_uncommitted_block_info(range read, size_t expected_size, void* result);
int parse_committed_block_info(range read, size_t expected_size, void* result);

int32_t get_block_handle(connection_impl* conn, const uint8_t* blockid, block_handle_t* result) 
{
	range buffer;
	if (!connection_write_message_header(conn, request_get_block_handle, &buffer))
		return TERAB_ERR_INTERNAL_ERROR;
	if (!copy_bytes(&buffer, (const char*)blockid, 32))
		return TERAB_ERR_INTERNAL_ERROR;

	if (!connection_send_request(conn, buffer.begin))
		return TERAB_ERR_INTERNAL_ERROR;
	
	if (!connection_wait_response(conn, &buffer))
		return TERAB_ERR_INTERNAL_ERROR;

	response_block_handle response;
	int has_type = 0; response_type actual_type;
	if (!parse_expected_response(buffer, block_handle, &has_type, &actual_type, sizeof(response), &response))
	{
		return TERAB_ERR_INTERNAL_ERROR;
	}
	*result = response.block_handle;
	return TERAB_SUCCESS;
}

int32_t open_block(
	connection_impl* conn,
	block_handle_t parent,
	block_handle_t* block,
	block_ucid_t* block_ucid
)
{
	range buffer;
	if (!connection_write_message_header(conn, request_open_block, &buffer))
		return TERAB_ERR_INTERNAL_ERROR;
	if (!copy_bytes(&buffer, (char*)&parent, sizeof(parent)))
		return TERAB_ERR_INTERNAL_ERROR;

	if (!connection_send_request(conn, buffer.begin))
		return TERAB_ERR_INTERNAL_ERROR;
	if (!connection_wait_response(conn, &buffer))
		return TERAB_ERR_INTERNAL_ERROR;

	response_opened_block response;
	int has_type = 0; response_type actual_type;
	if (!parse_expected_response(buffer, opened_block, &has_type, &actual_type, sizeof(response), &response))
	{
		return TERAB_ERR_INTERNAL_ERROR;
	}
	*block = response.alias;
	*block_ucid = response.identifier;
	return TERAB_SUCCESS;
}

int32_t get_uncommitted_block(
	connection_impl* conn,
	block_ucid_t block_ucid,
	block_handle_t* result
)
{
	range buffer;
	if (!connection_write_message_header(conn, request_get_uncommitted_block_handle, &buffer))
		return TERAB_ERR_INTERNAL_ERROR;
	if (!copy_bytes(&buffer, (char*)block_ucid.value, sizeof(block_ucid.value)))
		return TERAB_ERR_INTERNAL_ERROR;

	if (!connection_send_request(conn, buffer.begin))
		return TERAB_ERR_INTERNAL_ERROR;
	if (!connection_wait_response(conn, &buffer))
		return TERAB_ERR_INTERNAL_ERROR;

	response_block_handle response;
	int has_type = 0; response_type actual_type;
	if (!parse_expected_response(buffer, block_handle, &has_type, &actual_type, sizeof(response), &response))
	{
		return TERAB_ERR_INTERNAL_ERROR;
	}
	*result = response.block_handle;
	return TERAB_SUCCESS;
}

int32_t commit_block(connection_impl* conn, block_handle_t block, uint8_t* blockid)
{
	range buffer;
	if (!connection_write_message_header(conn, request_commit_block, &buffer))
		return TERAB_ERR_INTERNAL_ERROR;
	if (!write_int32(&buffer, block))
		return TERAB_ERR_INTERNAL_ERROR;
	if (!copy_bytes(&buffer, (char*)blockid, 32))
		return TERAB_ERR_INTERNAL_ERROR;

	if (!connection_send_request(conn, buffer.begin))
		return TERAB_ERR_INTERNAL_ERROR;
	if (!connection_wait_response(conn, &buffer))
		return TERAB_ERR_INTERNAL_ERROR;

	response_everything_ok response;
	int has_type = 0; response_type actual_type;
	if (!parse_expected_response(buffer, everything_ok, &has_type, &actual_type, sizeof(response), &response))
	{
		return TERAB_ERR_INTERNAL_ERROR;
	}
	return TERAB_SUCCESS;
}

int32_t get_blockinfo(connection_impl* conn, block_handle_t block, block_info_t* info)
{
	range buffer;
	if (!connection_write_message_header(conn, request_get_block_info, &buffer))
		return TERAB_ERR_INTERNAL_ERROR;
	if (!write_int32(&buffer, block))
		return TERAB_ERR_INTERNAL_ERROR;
	if (!write_int32(&buffer, block))
		return TERAB_ERR_INTERNAL_ERROR;

	if (!connection_send_request(conn, buffer.begin))
		return TERAB_ERR_INTERNAL_ERROR;
	if (!connection_wait_response(conn, &buffer))
		return TERAB_ERR_INTERNAL_ERROR;

	response_committed_block_information resp_committed;
	int has_type = 0; response_type actual_type;
	if (!parse_expected_response(buffer, committed_block_info, &has_type, &actual_type, sizeof(resp_committed), &resp_committed))
	{
		if (has_type && actual_type == uncommitted_block_info)
		{
			response_uncommitted_block_information resp_uncommitted;
			if (!parse_expected_response(buffer, uncommitted_block_info, &has_type, &actual_type, sizeof(resp_uncommitted), &resp_uncommitted))
			{
				exit(1);
			}
			memset(info->blockid, 0, sizeof(info->blockid));
			info->flags = (block_flags_t)0;
			info->parent = resp_uncommitted.parent;
			info->blockheight= resp_uncommitted.height;
			return TERAB_SUCCESS;
		}
	}
	else 
	{
		memcpy(info->blockid, resp_committed.id, sizeof(info->blockid));
		info->flags = TERAB_BLOCK_COMMITTED;
		info->parent = resp_committed.parent;
		info->blockheight = resp_committed.height;
		return TERAB_SUCCESS;
	}
	return TERAB_ERR_INTERNAL_ERROR;
}

int parse_expected_response(range buffer, response_type expected_type, /* out */ int* has_type,  /* out */ response_type* actual_type, size_t expected_size, /* out */ void* result)
{
	range read = buffer;

	uint32_t size;
	uint32_t requestId;
	uint32_t clientId;
	uint8_t sharded;
	response_type type;

	if (!read_uint32(&read, &size)) return 0; // message len
	if (range_len(buffer) != size) return 0; 

	if (!read_uint32(&read, &requestId)) return 0; // request id;
	if (!read_uint32(&read, &clientId)) return 0; // request id;
	if (!read_uint8(&read, &sharded)) return 0; // is sharded
	if (!read_int32(&read, (int32_t*)&type)) return 0; // response type

	// report real type to caller:
	*has_type = 1;
	*actual_type = type;

	if (expected_type != type) return 0;
	
	switch (type)
	{
	case block_handle:
		parse_block_handle(read, expected_size, result);
		break;
	case opened_block:
		parse_opened_block(read, expected_size, result);
		break;
	case everything_ok:
		parse_everything_ok(read, expected_size, result);
		break;
	case committed_block_info:
		parse_committed_block_info(read, expected_size, result);
		break;
	case uncommitted_block_info:
		parse_uncommitted_block_info(read, expected_size, result);
		break;
	default:
		return 0; // unknown response type
	}
}

int parse_block_handle(range read, size_t expected_size, void* result)
{
	if (expected_size != sizeof(response_block_handle))
		return 0;

	response_block_handle* resp  = (response_block_handle*)result;
	if (!read_int32(&read, &(resp->block_handle)))
		return 0; // should be an assert

	return 1;
}

int parse_opened_block(range read, size_t expected_size, void* result)
{
	if (expected_size != sizeof(response_opened_block))
		return 0;

	response_opened_block* resp = (response_opened_block*)result;
	if (!read_int32(&read, &(resp->alias)))
		return 0; // should be an assert

	for (int i = 0; i < 16; ++i)
	{
		if (!read_int8(&read, &(resp->identifier.value[i])))
			return 0;
	}
	return 1;
}

// this one is a bit silly but we should be able to macroify all this serialization code
int parse_everything_ok(range read, size_t expected_size, void* result)
{
	if (expected_size != sizeof(response_everything_ok))
		return 0;

	response_everything_ok* resp = (response_everything_ok*)result;

	return 1;
}

int parse_committed_block_info(range read, size_t expected_size, void* result)
{
	if (expected_size != sizeof(response_committed_block_information))
		return 0;

	response_committed_block_information* resp = (response_committed_block_information*)result;
	if (!read_bytes(&read, (char*)&(resp->id[0]), sizeof(resp->id)))
		return 0;
	if (!read_int32(&read, &(resp->alias)))
		return 0;
	if (!read_int32(&read, &(resp->parent)))
		return 0;
	if (!read_int32(&read, &(resp->height)))
		return 0;

	return 1;
}

int parse_uncommitted_block_info(range read, size_t expected_size, void* result)
{
	if (expected_size != sizeof(response_uncommitted_block_information))
		return 0;

	response_uncommitted_block_information* resp = (response_uncommitted_block_information*)result;
	if (!read_bytes(&read, (char*)&(resp->id[0]), sizeof(resp->id)))
		return 0;
	if (!read_int32(&read, &(resp->alias)))
		return 0;
	if (!read_int32(&read, &(resp->parent)))
		return 0;
	if (!read_int32(&read, &(resp->height)))
		return 0;

	return 1;
}


int32_t write_txs(connection_impl* conn, block_handle_t block, int32_t txo_length, txo_t* txos)
{
	int serverStatus = TERAB_SUCCESS;
	int nbPendingReplies = 0;
	int can_read = 0;
	// it is not expected that we can read at this moment (all replies ought to have been dequeued before)
	if (!connection_can_read(conn, &can_read) || can_read)
	{
		return TERAB_ERR_INTERNAL_ERROR; // we're not sure if the connection_failed, or
	}                                    // the server is spitting garbage at us

    txo_t* end = txos + txo_length;
	for (txo_t* txo = txos; txo < end; txo++)
	{
		range buffer;
		while (can_read)
		{
			connection_wait_response(conn, &buffer);
			int has_type; response_type actual_type;
			response_everything_ok reply_ok = {};
			if (!parse_expected_response(buffer, everything_ok, &has_type, &actual_type, sizeof(everything_ok), &reply_ok) && !has_type)
			{
				return TERAB_ERR_INTERNAL_ERROR;
			}
			if (has_type && actual_type != everything_ok)
			{
				return TERAB_ERR_INTERNAL_ERROR; // stupid client does not accomodate server failures
			}
			if (!connection_can_read(conn, &can_read))
			{
				return TERAB_ERR_INTERNAL_ERROR;
			}
			nbPendingReplies--;
		}

		if (!connection_write_message_header(conn, request_write_raw_txo, &buffer))
			return TERAB_ERR_INTERNAL_ERROR;
		if (!copy_bytes(&buffer, (char*)txo->outpoint.txid, sizeof(txo->outpoint.txid)))
			return TERAB_ERR_INTERNAL_ERROR;
		if (!write_int32(&buffer, txo->outpoint.index))
			return TERAB_ERR_INTERNAL_ERROR;
		if (!write_int32(&buffer, txo->produced))
			return TERAB_ERR_INTERNAL_ERROR;
		if (!write_int32(&buffer, txo->spent))
			return TERAB_ERR_INTERNAL_ERROR;
		if (!write_int64(&buffer, txo->satoshi))
			return TERAB_ERR_INTERNAL_ERROR;
		if (!write_int32(&buffer, txo->payload.length))
			return TERAB_ERR_INTERNAL_ERROR;
		if (!copy_bytes(&buffer, (char*)txo->payload.data, txo ->payload.length))
			return TERAB_ERR_INTERNAL_ERROR;
		if (!connection_send_request(conn, buffer.begin))
		{
			return TERAB_ERR_INTERNAL_ERROR;
		}
		nbPendingReplies ++;
	}

	while (nbPendingReplies>0)
	{
		range buffer;
		connection_wait_response(conn, &buffer);
		int has_type; response_type actual_type;
		response_everything_ok reply_ok = {};
		if (!parse_expected_response(buffer, everything_ok, &has_type, &actual_type, sizeof(everything_ok), &reply_ok) && !has_type)
		{
			return TERAB_ERR_INTERNAL_ERROR;
		}
		if (has_type && actual_type != everything_ok)
		{
			return TERAB_ERR_INTERNAL_ERROR; // stupid client does not accomodate server failures
		}
		nbPendingReplies--;

	}
}

