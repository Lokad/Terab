#pragma once

/* The return type of functions that signal failure by returning 0 */
typedef enum
{
	RS_FAILURE = 0,
	RS_OK,
} return_status_t;

#define OK (RS_OK)
#define UNSPECIFIED (status_detail = SD_UNSPECIFIED, RS_FAILURE)
#define KO(DETAIL) (status_detail = SD_##DETAIL, RS_FAILURE)


/* because restricting failure to 0 value hurts precise diagnostics,
   here are the details:
*/
typedef enum
{
	/* User gives us technically wrong inputs (say, bad connection strings) */
	SD_USER = 1,

	/* Trouble with network: Client code should call terab_disconnect, and
	   create a new connection, possibly with different connection string. */
	SD_CONNECTIVITY = 2,

	/* Runtime behaves badly. Client-code should exit the process */
    SD_RUNTIME = 3,

	/* Library has received very unexpected input from server. Either TCP feeds us
	   garbage, or the server implements a small deviation from the expected protocol,
	   or the server runtime is corrupted.
	   Should this translate to TERAB_ERR_STORAGE_CORRUPTED ?
	SD_SERVER = 4,  */
	/* Library has self-diagnosed a bug: this should translate as a TERAB_ERR_INTERNAL_ERROR
	SD_LIB = 5, */
	SD_UNSPECIFIED = 0x7FFFFFFF, // TODO: [vermorel] we should remove these, one-by-one
} status_detail_t;


/* those details are meant to be stored in the following thread local (ala errno): */
#if defined(_MSC_VER)
__declspec(thread) extern status_detail_t status_detail;
#else
extern __thread status_detail_t status_detail;
#endif
