#pragma once

#include <stdint.h>

#include "ranges.h"
#include "status.h"

// you'll read in the (C#) server code that messages longer than 16k are too long to be considered
#define MESSAGE_MAX_LEN (16*1024)
#define DEFAULT_PORT_NUMBER 8338

typedef struct connection_struct connection_s;

connection_s* connection_new(const char* connection_string);
void connection_free(connection_s* connection);

return_status_t connection_open(connection_s* connection);
return_status_t connection_close(connection_s* connection);

return_status_t connection_batch_begin(connection_s* conn);
range connection_get_send_buffer(connection_s* conn);
return_status_t connection_send_request(connection_s* conn, const char* bufEnd, /* out, optional */ uint32_t* requestId);
return_status_t connection_batch_end(connection_s* conn);

return_status_t connection_wait_response(connection_s* conn, /* out */ range* reply);

