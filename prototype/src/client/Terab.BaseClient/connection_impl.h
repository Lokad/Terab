#pragma once

#include <cstdint>

#ifdef  _MSC_VER
#include <Ws2tcpip.h>
#endif

#include "ranges.h"

typedef struct {
	int msg_seq;
	int is_connected;
	int socket;
	char* sendbuf;
	char* recvbuf;
	int ipVersion;
	union {
		struct in_addr v4;
		struct in_addr6 v6;
	} addr;
	int tcp_port;
	char* conn_string;
	char* addr_str;
	char* tcp_port_str;

	fd_set try_read, try_write, check_error;
} connection_impl;


connection_impl* connection_new(const char* connection_string);
void connection_free(connection_impl* connection);

int connection_open(connection_impl* connection);
int connection_can_read(connection_impl* conn, int* result);
int connection_close(connection_impl* connection);

int connection_write_message_header(connection_impl* conn, int32_t message_type, /* out */ range* buffer);
int connection_send_request(connection_impl* conn, const char* bufEnd);
int connection_wait_response(connection_impl* conn, /* out */ range* reply);