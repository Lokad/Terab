#include <assert.h>
#include "ranges.h"

range range_init(char* begin, size_t len)
{
	range result;
	result.begin = begin;
	result.end = begin != NULL ? begin + len : NULL;
	return result;
}

size_t range_len(range r) {
	return r.begin != NULL ? r.end - r.begin : 0;
}

int range_is_null_or_empty(range r)
{
	return range_len(r) == 0;
}

uint64_t read_uint64(range* r)
{
	uint64_t value;
	read_bytes(r, (char*)&value, sizeof(value));
	return value;
}

void write_uint64(range* r, uint64_t value)
{
	write_bytes(r, (char*)&value, sizeof(value));
}

int32_t read_int32(range* r)
{
	int32_t value;
	read_bytes(r, (char*)&value, sizeof(value));
	return value;
}

void write_int32(range* r, int32_t value)
{
	write_bytes(r, (char*)&value, sizeof(value));
}

uint32_t read_uint32(range* r)
{
	uint32_t value;
	read_bytes(r, (char*)&value, sizeof(value));
	return value;
}

void write_uint32(range* r, uint32_t value)
{
	write_bytes(r, (char*)&value, sizeof(value));
}

void clear_uint32(range* r)
{
	clear_bytes(r, sizeof(uint32_t));
}

uint8_t read_uint8(range* r)
{
	uint8_t value;
	read_bytes(r, (char*)&value, sizeof(value));
	return value;
}

void skip_uint8(range* r)
{
	skip_bytes(r, sizeof(uint8_t));
}

void write_uint8(range* r, uint8_t value)
{
	write_bytes(r, (char*)&value, sizeof(value));
}

int has_room(range r, size_t n)
{
	return r.end - r.begin >= n;
}

void copy_range(range* to, range from, size_t n)
{
	copy_bytes(to, from.begin, n);
}

void write_bytes(range* to, const char* src, size_t n)
{
	assert((to->end - to->begin) >= n);

	char* dst = to->begin;
	for (int i = n; i > 0; --i)
	{
		*dst++ = *src++;
	}
	to->begin = dst;
}

void copy_bytes(range* to, const char* src, size_t n)
{
	assert((to->end - to->begin) >= n);

	char* dst = to->begin;
	for (int i = n; i > 0; --i)
	{
		*dst++ = *src++;
	}
	to->begin = dst;
}

void clear_bytes(range* to, size_t n)
{
	assert((to->end - to->begin) >= n);

	// HACK: use 'memzero' 
	char* dst = to->begin;
	for (int i = n; i > 0; --i)
	{
		*dst++ = 0;
	}
	to->begin = dst;
}

void read_bytes(range* from, char* dst, size_t n)
{
	assert((from->end - from->begin) >= n);

	char* src = from->begin;
	for (int i = n; i > 0; --i)
	{
		*dst++ = *src++;
	}
	from->begin = src;
}

void skip_bytes(range* from, size_t n)
{
	assert((from->end - from->begin) >= n);

	from->begin += n;
}
