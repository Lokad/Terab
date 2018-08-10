// Terab.BaseClientVs.cpp : Defines the exported functions for the DLL application.
//

#include <cstdio>
#include <cstdlib>
#include "../../Terab.StdAPI/terab_utxo.h"

#include "connection_impl.h"
#include "message.h"


int32_t terab_initialize()
{
	#ifdef  _MSC_VER
	WSADATA wsaData;
	int wsInitFailed = WSAStartup(MAKEWORD(2, 2), &wsaData);
	if (wsInitFailed)
	{
		return TERAB_ERR_INTERNAL_ERROR;
	}
	#endif
}

int32_t terab_shutdown()
{
	#ifdef _MSC_VER
	WSACleanup();
	#endif
	return TERAB_SUCCESS;
}


int32_t terab_connect( const char* connection_string, connection_t* conn )
{

	printf("connecting to %s\n", connection_string);
	connection_impl* result = connection_new(connection_string);

	if (result == NULL)
	{
		return TERAB_ERR_CONNECTION_FAILED;
	}

	if (!connection_open(result))
	{
		connection_free(result);
		return TERAB_ERR_CONNECTION_FAILED;
	}

	*conn = result;
	return TERAB_SUCCESS;
}


int32_t terab_disconnect(connection_t connection, const char* reason)
{
	connection_impl* cnx = (connection_impl*) connection;
	printf("disconnecting from %s\n", cnx->conn_string);

	if (!connection_close(cnx))
	{
		return TERAB_ERR_INTERNAL_ERROR;
	}

	connection_free(cnx);
	return TERAB_SUCCESS;
}

int32_t terab_utxo_get_block(
	connection_t connection,
	uint8_t* blockid,
	block_handle_t* block
)
{
	connection_impl* cnx = (connection_impl*)connection;
	return get_block_handle(cnx, blockid, block);
}

int32_t terab_utxo_get_blockinfo(
	connection_t connection,
	block_handle_t block,
	block_info_t* info
)
{
	connection_impl* cnx = (connection_impl*)connection;
	return get_blockinfo(cnx, block, info);
}

int32_t terab_utxo_get(
	connection_t conn,
	block_handle_t block,
	int32_t outpoints_length,
	tx_outpoint_t* outpoints,
	txo_t* txos,
	size_t storage_length,
	uint8_t* storage
)
{
	return TERAB_ERR_INTERNAL_ERROR;
}



int32_t terab_utxo_open_block(
	connection_t conn,
	block_handle_t parent,
	block_handle_t* block,
	block_ucid_t* block_ucid
)
{
	connection_impl* cnx = (connection_impl*)conn;
	return open_block(cnx, parent, block, block_ucid);
}


int32_t terab_utxo_write_txs(
	connection_t conn,
	block_handle_t block,
	int32_t txo_length,
	txo_t* txos
)
{
	connection_impl* cnx = (connection_impl*)conn;
	return write_txs( cnx, block, txo_length, txos);
}


int32_t terab_utxo_get_uncommitted_block(
	connection_t conn,
	block_ucid_t block_ucid,
	block_handle_t* block
)
{
	connection_impl* cnx = (connection_impl*)conn;
	return get_uncommitted_block(cnx, block_ucid, block);
}


int32_t terab_utxo_commit_block(
	connection_t conn,
	block_handle_t block,
	uint8_t* blockid
)
{
	connection_impl* cnx = (connection_impl*)conn;
	return commit_block(cnx, block, blockid);
}
