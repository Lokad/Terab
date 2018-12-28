#pragma once

#include <stdint.h>
#include <stddef.h>

/* The 'range' struct offers simple stream-like behaviors.
  This structure is intended to facilitate parsing and writing
  binary messages.
*/

typedef struct range_s {
	char* begin; 
	const char* end;
} range;

range range_init(char* begin, size_t len);
size_t range_len(range r);
int range_is_null_or_empty(range r);

int64_t read_int64(range* r);
void skip_int64(range* r);
void write_int64(range* r, int64_t value);
void clear_int64(range* r);

uint64_t read_uint64(range* r);
void skip_uint64(range* r);
void write_uint64(range* r, uint64_t value);
void clear_uint64(range* r);

uint32_t read_uint32(range* r);
void skip_uint32(range* r);
void write_uint32(range* r, uint32_t value);
void clear_uint32(range* r);

int32_t read_int32(range* r);
void skip_int32(range* r);
void write_int32(range* r, int32_t value);
void clear_int32(range* r);

int8_t read_int8(range* r);
void skip_int8(range* r);
void write_int8(range* r, int8_t value);
void clear_int8(range* r);

uint8_t read_uint8(range* r);
void skip_uint8(range* r);
void write_uint8(range* r, uint8_t value);
void clear_uint8(range* r);

void copy_bytes(range* to, const char* src, size_t n);
void copy_range(range* to, range from, size_t n);
void write_bytes(range* dst, const char* src, size_t n);
void clear_bytes(range* to, size_t n);
void read_bytes(range* from, char* dst, size_t n);
void skip_bytes(range* from, size_t n);
