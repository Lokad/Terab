#pragma once

#include <cstdint>

typedef struct range_s {
	char* begin; 
	const char* end;
} range;

size_t range_len(range range);

char* read_int64(range* range, /*out*/ int64_t* value);
char* skip_int64(range* range);
char* write_int64(range* range, int64_t value);
char* clear_int64(range* range);

char* read_uint64(range* range, /*out*/ uint64_t* value);
char* skip_uint64(range* range);
char* write_uint64(range* range, uint64_t value);
char* clear_uint64(range* range);

char* read_uint32(range* range, /*out*/ uint32_t* value);
char* skip_uint32(range* range);
char* write_uint32(range* range, uint32_t value);
char* clear_uint32(range* range);

char* read_int32(range* range, /*out*/ int32_t* value);
char* skip_int32(range* range);
char* write_int32(range* range, int32_t value);
char* clear_int32(range* range);

char* read_uint32(range* range, /*out*/ uint32_t* value);
char* skip_uint32(range* range);
char* write_uint32(range* range, uint32_t value);
char* clear_uint32(range* range);

char* read_int8(range* range, /*out*/ int8_t* value);
char* skip_int8(range* range);
char* write_int8(range* range, int8_t value);
char* clear_int8(range* range);

char* read_uint8(range* range, /*out*/ uint8_t* value);
char* skip_uint8(range* range);
char* write_uint8(range* range, uint8_t value);
char* clear_uint8(range* range);

char* copy_bytes(range* dst, const char* src, size_t n);
char* read_bytes(range* src, char* dst, size_t n);