#include <stdlib.h>
#include <stddef.h>
#include <string.h>

#include "compat.h"

#include "connection.h"

typedef struct connection_struct {
 uint32_t msg_seq;
 int is_connected;
 SOCKET socket;
 char* sendbuf;
 char* sendptr;
 int in_batch;
 char* recvbuf;
 int ipVersion;
 union {
	 struct in_addr v4;
	 struct in6_addr v6;
 } addr;
 int tcp_port;
 char* conn_string;
 range addr_str;
 range tcp_port_str;

 fd_set try_read, check_error;
} connection_s;

return_status_t parse_connection_string(const char* connection_string, connection_s* result);

connection_s* connection_new(const char* connection_string)
{
	if (!connection_string) return NULL;

	connection_s draft = { 0 };
	if (!parse_connection_string(connection_string, &draft))
	{
		return NULL;
	}

	// the only allocations in the client lib :
	// (soon to be grouped in one allocator-callback to the miner))
	connection_s* result = (connection_s*)calloc(1, sizeof(connection_s));
	draft.sendbuf = calloc(2*MESSAGE_MAX_LEN, sizeof(char));
	draft.sendptr = draft.sendbuf;
	draft.in_batch = 0;
	draft.recvbuf = calloc(MESSAGE_MAX_LEN, sizeof(char));
	size_t conn_str_len = strlen(connection_string);
	draft.conn_string = calloc(conn_str_len + 1, sizeof(char));

	strncpy(draft.conn_string, connection_string, conn_str_len);
	draft.conn_string[conn_str_len] = '\0'; // reputs a string terminator for good measure

	// rebase the "addr_str" range (which is relative to the original, uncopied, connection_string)
	// on the saved copy, "conn_string":
	if (!range_is_null_or_empty(draft.addr_str))
	{
		ptrdiff_t offset = draft.addr_str.begin - connection_string;
		size_t len = range_len(draft.addr_str);
		draft.addr_str = range_init(draft.conn_string + offset, len);
	}

	// ditto, rebase the "tcp_port_str" range:
	if (!range_is_null_or_empty(draft.tcp_port_str))
	{
		ptrdiff_t offset = draft.tcp_port_str.begin - connection_string;
		size_t len = range_len(draft.tcp_port_str);
		draft.tcp_port_str = range_init(draft.conn_string + offset, len);
	}

	*result = draft;
	return result;
}

return_status_t connection_open(connection_s* conn)
{
	ADDRESS_FAMILY addr_family;
	if (conn->ipVersion == 4)
	{
		addr_family = AF_INET;
	}
	else if (conn->ipVersion == 6)
	{
		addr_family = AF_INET6;
	}
	else
	{
		return UNSPECIFIED;
	}

	SOCKET client = socket(addr_family, SOCK_STREAM, IPPROTO_TCP);
	if (client == INVALID_SOCKET)
		return UNSPECIFIED;

	int error;
	int tcp_nodelay_enabled = 1;
	error = setsockopt(client, IPPROTO_TCP, TCP_NODELAY, &tcp_nodelay_enabled, sizeof(tcp_nodelay_enabled));
	if (error)
	{
		closesocket(client);
		return UNSPECIFIED;
	}

	if (addr_family == AF_INET)
	{
		struct sockaddr_in dest = { 0 };
		dest.sin_family = AF_INET;
		dest.sin_addr.s_addr = conn->addr.v4.s_addr;
		dest.sin_port = htons(conn->tcp_port);

		error = connect(client, (struct sockaddr*)&dest, sizeof(dest));
	}
	else if (addr_family == AF_INET6)
	{
		struct sockaddr_in6 dest = { 0 };
		dest.sin6_family = AF_INET6;
		dest.sin6_addr = conn->addr.v6;
		dest.sin6_port = htons(conn->tcp_port);

		error = connect(client, (struct sockaddr*)&dest, sizeof(dest));
	}
	else
	{
		exit(1);
	}

	if (error)
	{
		closesocket(client);
		return UNSPECIFIED;
	}

	conn->is_connected = 1;
	conn->socket = client;

	FD_ZERO(&conn->try_read); FD_SET(conn->socket, &conn->try_read);
	FD_ZERO(&conn->check_error); FD_SET(conn->socket, &conn->check_error);

	return OK;

}

return_status_t connection_batch_begin(connection_s* conn)
{
	conn->in_batch = 1;
	return OK;
}

static return_status_t accept_message(connection_s* conn, const char* msgEnd, uint32_t* outRequestId)
{
	range msg_range = range_init(conn->sendptr, msgEnd - conn->sendptr);
	size_t to_send_size = range_len(msg_range);

	if (to_send_size > MESSAGE_MAX_LEN)
	{
		if (outRequestId) *outRequestId = 0;
		return UNSPECIFIED;
	}

	// patch the beginning of sendbuf with the length of what needs sending:
	range edit_range = msg_range;
	write_int32(&edit_range, to_send_size);
	uint32_t requestId = conn->msg_seq;
	write_uint32(&edit_range, requestId);

	if (outRequestId) *outRequestId = requestId;
	conn->sendptr = msgEnd;
	conn->msg_seq = requestId + 1;
	return OK;
}

static return_status_t socket_send(SOCKET socket, const char* to_send, size_t len)
{
	if (len > INT_MAX)
	{
		return UNSPECIFIED;
	}
	int remaining = (int)len;

	while (remaining > 0)
	{
		int sent = send(socket, to_send, remaining, 0);
		if (sent > 0)
		{
			if (sent > remaining)
			{
				// TODO: improve on this (assertions, logging). This should not happen, though, 
				// so after all, exiting everything is possibly the best thing to do
				exit(1);
			}
			to_send += sent;
			remaining -= sent;
		}
		else
		{
			return UNSPECIFIED; // connection failed, or something. client had better tear down everything now.
			//
			// we really could do better here, but here, Winsock and BSD sockets diverge quite a bit;
			// we should consider leveraging the sockets compatibility hacks of the Satoshi client
			// (src/compat.h)
		}
	};
	return OK;
}

return_status_t flush_send_buffer(connection_s* conn)
{
	size_t len = conn->sendptr - conn->sendbuf;

	// actual sending, right now:
	if (!socket_send(conn->socket, conn->sendbuf, len))
	{
		return RS_FAILURE;
	}
	else
	{
		// reset send buffer:
		conn->sendptr = conn->sendbuf;
		return OK;
	}
}

return_status_t connection_send_request(connection_s* conn, const char* msgEnd, /* out, optional */ uint32_t* outRequestId)
{
	if (!conn->is_connected)
	{
		if (outRequestId) { *outRequestId = 0; }
		return KO(UNSPECIFIED);
	}

	if (!accept_message(conn, msgEnd, outRequestId))
	{
		return RS_FAILURE;
	}

	size_t len = conn->sendptr - conn->sendbuf;
	if ( !conn->in_batch || len >= MESSAGE_MAX_LEN)
	{
		return flush_send_buffer(conn);
	}

	// defer sending:
	return OK;
}

return_status_t connection_batch_end(connection_s* conn)
{
	conn->in_batch = 0;
	if (conn->sendbuf != conn->sendptr)
	{
		flush_send_buffer(conn);
	}
	return OK;
}

return_status_t connection_wait_response(connection_s* conn, /* out */ range* reply)
{
	if (!conn->is_connected)
		return UNSPECIFIED;
	
	int n = 0;

	char* dest = conn->recvbuf;
	// first, we need 4 bytes to get the message size.
	int needed = 4;
	do
	{
		n = recv(conn->socket, dest, needed, 0);
		if (n > needed || n <= 0)
		{
			return UNSPECIFIED;
		}
		dest += n;
		needed -= n;

	} while (needed > 0);

	range ready = range_init(conn->recvbuf, 4);
	int32_t msgsize = read_int32(&ready);

	if (msgsize > MESSAGE_MAX_LEN || msgsize < 16) // not really a message, then
		exit(1);

	needed = msgsize - 4;
	while (needed > 0)
	{
		n = recv(conn->socket, dest, needed, 0);
		if (n > needed || n <= 0)
		{
			return UNSPECIFIED;
		}
		dest += n;
		needed -= n;
	}

	// at this point, we've got a full message:
	reply->begin = conn->recvbuf;
	reply->end = conn->recvbuf + msgsize;

	return OK;
}

return_status_t connection_close(connection_s* conn)
{
	if (!conn->is_connected)
	{
		return UNSPECIFIED;
	}
	int failed = closesocket(conn->socket);
	if (!failed)
	{
		conn->is_connected = 0;
		conn->socket = 0;
		return OK;
	}
	else
	{
		return UNSPECIFIED;
	}
}

void connection_free(connection_s* connection)
{
	free(connection->conn_string);
	free(connection->sendbuf);
	free(connection->recvbuf);
	free(connection);
}

return_status_t tokenize_connection_string(const char* connection_string, range* ip_str, range* tcp_port_str);

return_status_t parse_connection_string(const char* connection_string, connection_s* result)
{
	range tcp_port_as_range = { 0 }, address_as_range = { 0 };

	if (!tokenize_connection_string(connection_string, &address_as_range, &tcp_port_as_range))
	{
		return UNSPECIFIED;
	}

	if (range_is_null_or_empty(address_as_range))
	{
		return UNSPECIFIED;
	}
	size_t addr_len = range_len(address_as_range);
	if (addr_len >= 100) // 100 char are enough to write an IP address
	{
		return UNSPECIFIED;
	}

	char buffer[100];
	memcpy(buffer, address_as_range.begin, addr_len);
	buffer[addr_len] = '\0';

	if (inet_pton(AF_INET, buffer, &(result->addr.v4)))
	{
		result->ipVersion = 4;
	}
	else if (inet_pton(AF_INET6, buffer, &(result->addr.v6)))
	{
		result->ipVersion = 6;
	}
	else
	{
		return UNSPECIFIED;
	}

	if (range_is_null_or_empty(tcp_port_as_range))
	{
		result->tcp_port = DEFAULT_PORT_NUMBER;
	}
	else
	{
		size_t port_len = range_len(tcp_port_as_range);
		if (port_len >= 100)
		{
			return UNSPECIFIED;
		}
		memcpy(buffer, tcp_port_as_range.begin, port_len);
		buffer[port_len] = '\0';

		result->tcp_port = atoi(buffer);
		if (result->tcp_port == 0)
		{
			return UNSPECIFIED;
		}
	}
	result->tcp_port_str = tcp_port_as_range;
	result->addr_str = address_as_range;
	return OK;
}

return_status_t tokenize_connection_string(const char* connection_string, range* ip_str, range* portnum_str)
{
	const char *ip_begin, *port_begin = NULL;
	size_t ip_len, port_len;

	const char* first_column;
	if (connection_string[0] == '[') // [address]:port
	{
		ip_begin = connection_string + 1;
		const char* ip_end = strchr(connection_string, ']');
		if (ip_end == NULL) // no matching closing bracket
			return UNSPECIFIED;
		ip_len = ip_end - ip_begin;

		port_begin = ip_end + 1;
		port_len = strlen(port_begin);
        if (port_len > 0 && *port_begin == ':') // we may have a port number:
		{
			port_begin++;
			port_len--;
		}
		else if (port_len > 0) // we have something else: not an [IPv6]:port format
		{
			return UNSPECIFIED;
		}
	}
	else if ((first_column = strchr(connection_string, ':')) && !strchr(first_column + 1, ':'))  // address:port  - only one column, so ok 
	{                                                                                        // but only for ipV4
		ip_begin = connection_string;
		ip_len = first_column - ip_begin;
		port_begin = first_column + 1;
		port_len = strlen(port_begin);
		if (port_len == 0) // dangling ':', no port number: invalid
		{
			return UNSPECIFIED;
		}
	}
	else
	{
		ip_begin = connection_string;
		ip_len = strlen(connection_string);
		port_len = 0;
	}

	if (ip_len > 0)
	{
		*ip_str = range_init(ip_begin, ip_len);
	}

	if (port_len > 0)
	{
		*portnum_str = range_init(port_begin, port_len);
	}
	return OK;
}

range connection_get_send_buffer(connection_s* conn)
{
	return range_init(conn->sendptr, MESSAGE_MAX_LEN);
}