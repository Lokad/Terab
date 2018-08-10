#include <cstring>
#include <cstdlib>
#ifdef  _MSC_VER
#include <Ws2tcpip.h>
#endif

#include "connection_impl.h"

#ifdef  _MSC_VER
#define strdup _strdup
#else
#define closesocket close
#endif

// you'll read in the (C#) server code that messages longer than 1000 bytes are too long to be considered
#define MESSAGE_MAX_LEN 1000
#define DEFAULT_PORT_NUMBER 15000

int parse_connection_string(const char* connection_string, connection_impl* result);

connection_impl* connection_new(const char* connection_string)
{
	if (!connection_string) return NULL;
	
	connection_impl draft = { 0 };
	if (parse_connection_string(connection_string, &draft))
	{
		draft.sendbuf = (char*)calloc(MESSAGE_MAX_LEN, sizeof(char));
		draft.recvbuf = (char*)calloc(MESSAGE_MAX_LEN, sizeof(char));
		draft.conn_string = strdup(connection_string);
	}
	else
	{
		free(draft.addr_str);
		free(draft.tcp_port_str);
	}

	connection_impl* result = (connection_impl*)calloc(1, sizeof(connection_impl));
	*result = draft;
	return result;
}

int connection_open(connection_impl* conn)
{
	if (conn->ipVersion == 4)
	{
		sockaddr_in dest; memset(&dest, 0, sizeof(dest));

		dest.sin_family = AF_INET;
		dest.sin_addr.s_addr = conn->addr.v4.s_addr;
		dest.sin_port = htons(conn->tcp_port);

		int client = socket(AF_INET, SOCK_STREAM, IPPROTO_TCP);
		int error = connect(client, (struct sockaddr*)&dest, sizeof(dest));
		if (!error)
		{
			conn->is_connected = 1;
			conn->socket = client;

			FD_ZERO(&conn->try_read); FD_SET(conn->socket, &conn->try_read);
			FD_ZERO(&conn->try_write); FD_SET(conn->socket, &conn->try_write);
			FD_ZERO(&conn->check_error); FD_SET(conn->socket, &conn->check_error);

			return 1;
		}
		else
		{
			closesocket(client);
			return 0;
		}
	}
	else if(conn->ipVersion == 6)
	{
		sockaddr_in6 dest; memset(&dest, 0, sizeof(dest));

		dest.sin6_family = AF_INET6;
		dest.sin6_addr = conn->addr.v6;
		dest.sin6_port = htons(conn->tcp_port);

		int client = socket(AF_INET6, SOCK_STREAM, IPPROTO_TCP);
		int error = connect(client, (struct sockaddr*)&dest, sizeof(dest));
		if (!error)
		{
			conn->is_connected = 1;
			conn->socket = client;
			return 1;
		}
		else
		{
			closesocket(client);
			return 0;
		}
	}
}

// result is 1 if connection can be read from without blocking:
int connection_can_read(connection_impl* conn,int* result)
{
	*result = 0;
	if (!conn->is_connected)
		return 0;

	int socket = conn->socket;

	FD_SET(socket, &conn->try_read); FD_SET(socket, &conn->try_write); FD_SET(socket, &conn->check_error);
	int status = select(1, &conn->try_read, &conn->try_write, &conn->check_error, NULL /* infinite timeout */);
	if (status == SOCKET_ERROR || FD_ISSET(socket, &conn->check_error ))
	{
		return 0;
	}
	if (FD_ISSET(socket, &conn->try_read))
		*result = 1;
	return 1;
}


int connection_write_message_header(connection_impl* conn, int32_t message_type, /* out */ range* buffer)
{
	range result = { conn->sendbuf, conn->sendbuf + MESSAGE_MAX_LEN };

	uint8_t* advanced;
	if (!clear_uint32(&result) ) return 0; // messageLen
	if (!clear_uint32(&result) ) return 0; // requestId
	if (!clear_uint32(&result)) return 0; // clientId
	if (!clear_uint8(&result)) return 0;  // is sharded
	if (!write_int32(&result, message_type)) return 0;  // message type

	*buffer = result;
	return 1;
}

int connection_send_request(connection_impl* conn, const char* msgEnd)
{
	if (!conn->is_connected)
		return 0;

	// patch the beginning of sendbuf with the length of what needs sending:
	range sendrange = { conn->sendbuf, msgEnd };
	char* tosend = sendrange.begin;
	size_t to_send_size = msgEnd - tosend;
	if (!write_uint32(&sendrange, to_send_size))
		exit(1); // TODO an assertion system. There is no way on earth this could fail

	while(tosend < msgEnd)
	{
		int sent = send(conn->socket, tosend, msgEnd - tosend, 0);
		if (sent > 0)
		{
			if (sent > to_send_size)
			{
				// TODO improve on this (assertions, logging). This should not happen, though, 
				// so after all, exiting everything is possibly the best thing to do
				exit(1);
			}
			tosend += sent;
		}
		else
		{
			return 0; // connection failed, or something. client had better tear down everything now.
			//
			// we really could do better here, but here, Winsock and BSD sockets diverge quite a bit;
			// we should consider leveraging the sockets compatibility hacks of the Satoshi client
			// (src/compat.h)
		}
	};

	return 1;
}

int connection_wait_response(connection_impl* conn, /* out */ range* reply)
{
	if (!conn->is_connected)
		return 0;
	
	int n = 0;
	range ready = { conn->recvbuf, conn->recvbuf }; // "ready": what's just been received, and is amenable to be read
	range recvbuf = { conn->recvbuf, conn->recvbuf + MESSAGE_MAX_LEN };

	while (range_len(ready) < 4) // 4 bytes, to read the message size
	{
		n = recv(conn->socket, recvbuf.begin, range_len(recvbuf), 0);
		if (n > 0)
		{
			recvbuf.begin += n;
			ready.end += n;
		}
		else
		{
			return 0;
		}
	}

	uint32_t msgsize;
	if (!read_uint32(&ready, &msgsize))
		exit(1);

	if (msgsize > MESSAGE_MAX_LEN)
		exit(1);

	int rest = msgsize - 4;
	while (range_len(ready) < rest)
	{
		n = recv(conn->socket, recvbuf.begin, range_len(recvbuf), 0);
		if (n > 0)
		{
			recvbuf.begin += n;
			ready.end += n;
		}
		else
		{
			return 0;
		}
	}
	if (range_len(ready) > rest)
	{
		// server is not supposed to reply with more data, and is not supposed
		// to send out more data until the next message
		// TODO: raise TERAB_INTERNAL_ERROR
		return 0;
	}

	// at this point, we've got a full message:
	reply->begin = conn->recvbuf;
	reply->end = conn->recvbuf + msgsize;

	return 1;
}

int connection_close(connection_impl* conn)
{
	if (!conn->is_connected)
	{
		return 0;
	}
	int failed = closesocket(conn->socket);
	if (!failed)
	{
		conn->is_connected = 0;
		conn->socket = 0;
		return 1;
	}
	else
	{
		return 0;
	}
}

void connection_free(connection_impl* connection)
{
	free(connection->addr_str);
	free(connection->tcp_port_str);
	free(connection->conn_string);
	free(connection->sendbuf);
	free(connection->recvbuf);
	free(connection);
}

int tokenize_connection_string(const char* connection_string, char** ip_str, char** tcp_port_str);
int parse_connection_string(const char* connection_string, connection_impl* result)
{
	if (!tokenize_connection_string(connection_string, &(result->addr_str), &(result->tcp_port_str)))
	{
		return 0;
	}

	char *ip_str = result->addr_str, *tcp_port_str = result->tcp_port_str;
	if (tcp_port_str != NULL && *tcp_port_str != 0)
	{
		result->tcp_port = atoi(tcp_port_str);
		if (result->tcp_port == 0)
		{
			return 0;
		}
	}
	else
	{
		result->tcp_port = DEFAULT_PORT_NUMBER;
	}
	if (!ip_str || *ip_str == 0)
	{
		return 0;
	}
	if (inet_pton(AF_INET, ip_str, &(result->addr.v4)))
	{
		result->ipVersion = 4;
		return 1;
	}
	if (inet_pton(AF_INET6, ip_str, &(result->addr.v6)))
	{
		result->ipVersion = 6;
		return 1;
	}
	return 0;
}

int tokenize_connection_string(const char* connection_string, char** ip_str, char** portnum_str)
{
	const char *ip_begin, *port_begin = NULL;
	size_t ip_len, port_len;

	const char* first_column;
	if (connection_string[0] == '[') // [address]:port
	{
		ip_begin = connection_string + 1;
		const char* ip_end = strchr(connection_string, ']');
		if (ip_end == NULL) // no matching closing bracket
			return 0;
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
			return 0;
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
			return 0;
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
		*ip_str = (char*)malloc(ip_len + 1);
		strncpy(*ip_str, ip_begin, ip_len);
		(*ip_str)[ip_len] = (char)0;
	}
	else
	{
		*ip_str = NULL;
	}
	if (port_len > 0)
	{
		*portnum_str = (char*)malloc(port_len + 1);
		strncpy(*portnum_str, port_begin, port_len);
		(*portnum_str)[port_len] = (char)0;
	}
	else
	{
		*portnum_str = NULL;
	}
}