#include "ranges.h"

//#define DEF_WRITE_T(integer_type)                                     \
//uint8_t* write_##integer_type(range* range, integer_type value)        \
//{                                                                     \
//	return copy_bytes(range, (uint8_t*)&value, sizeof(integer_type)); \
//}
//
//DEF_WRITE_T(
//
//)

// some private function declarations - implementation is at the end of this file

char* clear_bytes(range* to, size_t n);
char* read_bytes(range* from, char* dst, size_t n);
char* skip_bytes(range* from, size_t n);


size_t range_len(range range) {
	return range.end - range.begin;
}

char* read_int64(range* range, /*out*/ int64_t* value)
{
	return read_bytes(range, (char*)value, sizeof(*value));
}

char* skip_int64(range* range)
{
	return skip_bytes(range, sizeof(int64_t));
}

char* write_int64(range* range, int64_t value)
{
	return copy_bytes(range, (char*)&value, sizeof(value));
}

char* clear_int64(range* range) {
	return clear_bytes(range, sizeof(int64_t));
}

char* read_uint64(range* range, /*out*/ uint64_t* value)
{
	return read_bytes(range, (char*)value, sizeof(*value));
}

char* skip_uint64(range* range)
{
	return skip_bytes(range, sizeof(uint64_t));
}

char* write_uint64(range* range, uint64_t value)
{
	return copy_bytes(range, (char*)&value, sizeof(value));
}

char* clear_uint64(range* range)
{
	return clear_bytes(range, sizeof(uint64_t));
}

char* read_int32(range* range, /*out*/ int32_t* value)
{
	return read_bytes(range, (char*)value, sizeof(*value));
}

char* skip_int32(range* range)
{
	return skip_bytes(range, sizeof(int32_t));
}

char* write_int32(range* range, int32_t value)
{
	return copy_bytes(range, (char*)&value, sizeof(value));
}

char* clear_int32(range* range) {
	return clear_bytes(range, sizeof(int32_t));
}

char* read_uint32(range* range, /*out*/ uint32_t* value)
{
	return read_bytes(range, (char*)value, sizeof(*value));
}

char* skip_uint32(range* range)
{
	return skip_bytes(range, sizeof(uint32_t));
}

char* write_uint32(range* range, uint32_t value)
{
	return copy_bytes(range, (char*)&value, sizeof(value));
}

char* clear_uint32(range* range)
{
	return clear_bytes(range, sizeof(uint32_t));
}

char* read_int8(range* range, /*out*/ int8_t* value)
{
	return read_bytes(range, (char*)value, sizeof(*value));
}

char* skip_int8(range* range)
{
	return skip_bytes(range, sizeof(int8_t));
}

char* write_int8(range* range, int8_t value)
{
	return copy_bytes(range, (char*)&value, sizeof(value));
}

char* clear_int8(range* range)
{
	return clear_bytes(range, sizeof(int8_t));
}

char* read_uint8(range* range, /*out*/ uint8_t* value)
{
	return read_bytes(range, (char*)value, sizeof(*value));
}

char* skip_uint8(range* range)
{
	return skip_bytes(range, sizeof(uint8_t));
}

char* write_uint8(range* range, uint8_t value)
{
	return copy_bytes(range, (char*)&value, sizeof(value));
}

char* clear_uint8(range* range)
{
	return clear_bytes(range, sizeof(uint8_t));
}

int has_room(range range, size_t n)
{
	return range.end - range.begin >= n;
}

char* copy_bytes(range* to, const char* src, size_t n)
{
	if ((to->end - to->begin) < n)
		return NULL;

	char* dst = to->begin;
	for (int i = n; i > 0; --i)
	{
		*dst++ = *src++;
	}
	return to->begin = dst;
}

char* clear_bytes(range* to, size_t n)
{
	if ((to->end - to->begin) < n)
		return NULL;

	char* dst = to->begin;
	for (int i = n; i > 0; --i)
	{
		*dst++ = 0;
	}
	return to->begin = dst;
}

char* read_bytes(range* from, char* dst, size_t n)
{
	if ((from->end - from->begin) < n)
		return NULL;

	char* src = from->begin;
	for (int i = n; i > 0; --i)
	{
		*dst++ = *src++;
	}
	return from->begin = src;
}

char* skip_bytes(range* from, size_t n)
{
	if ((from->end - from->begin) < n)
		return NULL;
	return from->begin += n;
}