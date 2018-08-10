// Terab.NativeConsole.cpp : Defines the entry point for the console application.
//

#include <cstdio>
#include <cstdlib>

#include "../../../Terab.StdAPI/terab_utxo.h"

uint8_t genesis_block_id[32] = {
	0x00, 0x00, 0x00, 0x00, 0x00, 0x19, 0xd6, 0x68, 0x9c, 0x08, 0x5a,
	0xe1, 0x65, 0x83, 0x1e, 0x93, 0x4f, 0xf7, 0x63, 0xae, 0x46, 0xa2,
	0xa6, 0xc1, 0x72, 0xb3, 0xf1, 0xb6, 0x0a, 0x8c, 0xe2, 0x6f
};

int main()
{
	connection_t conn;
	int32_t status;

	status = terab_initialize();
	if (status)
	{
		fprintf(stderr, "failed to init terab");
	}

	status = terab_connect("[127.0.0.1]:15000", &conn);
	if (status)
		return status;

	block_handle_t child = { 0 };
	block_ucid_t child_ucid = { 0 };
	for (int i=0; i <5; ++i)
	{
		status = terab_utxo_open_block(conn, 0, &child, &child_ucid);
		if (status != TERAB_SUCCESS)
		{
			return status;
		}

		printf("block ucid: %02hhx,%02hhx,%02hhx,%02hhx,%02hhx,%02hhx,%02hhx,%02hhx,%02hhx,%02hhx,%02hhx,%02hhx,%02hhx,%02hhx,%02hhx,%02hhx\n",
			child_ucid.value[0], child_ucid.value[1], child_ucid.value[2], child_ucid.value[3],
			child_ucid.value[4], child_ucid.value[5], child_ucid.value[6], child_ucid.value[7],
			child_ucid.value[8], child_ucid.value[9], child_ucid.value[10], child_ucid.value[11],
			child_ucid.value[12], child_ucid.value[13], child_ucid.value[14], child_ucid.value[15]);
	}
	status = terab_disconnect(conn, "disconnecting");
	if (status)
		return status;

	printf("say bye bye\n");
	char bye[256];
	scanf("%s", bye);

    return 0;
}