/* This file is only here to define the storage for global variable "status_detail".
*/

#include "status.h"


#if defined(_MSC_VER)
__declspec(thread) status_detail_t status_detail = SD_UNSPECIFIED;
#else
__thread status_detail_t status_detail = SD_UNSPECIFIED;
#endif
