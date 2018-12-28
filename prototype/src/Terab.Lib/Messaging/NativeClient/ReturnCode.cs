// Copyright Lokad 2018 under MIT BCH.
namespace Terab.Lib.Messaging.NativeClient
{
    public enum ReturnCode : int
    {
        SUCCESS = 0, /* Successful call. */
        ERR_CONNECTION_FAILED = 1, /* Failed to connect to the Terab service. */
        ERR_TOO_MANY_CLIENTS = 2, /* Connection rejected, too many clients. */
        ERR_AUTHENTICATION_FAILED = 3, /* Failed to authenticate with the Terab service. */
        ERR_SERVICE_UNAVAILABLE = 4, /* Terab service is not ready yet to accept requests. */
        ERR_TOO_MANY_REQUESTS = 5, /* Too many requests are concurrently made to Terab. */
        ERR_INTERNAL_ERROR = 6, /* Something wrong happened. Contact the Terab team. */
        ERR_STORAGE_FULL = 7, /* No more storage left for the write operation. */
        ERR_STORAGE_CORRUPTED = 8, /* Non-recoverable data corruption at the service level. */
        ERR_BLOCK_CORRUPTED = 9, /* The block being written is corrupted and cannot be recovered. */
        ERR_BLOCK_FROZEN = 10, /* This block is too old and does not accept new children blocks. */
        ERR_BLOCK_COMMITTED = 11, /* This block is committed and does not accept new txs. */
        ERR_BLOCK_UNKNOWN = 12, /* A block handle refers to an unknown block. */
        ERR_INCONSISTENT_REQUEST = 13, /* Broken idempotence. Request contradicts previous one.*/
        ERR_INVALID_REQUEST = 14, /* Generic invalidity of the arguments of the request. */
    }
}