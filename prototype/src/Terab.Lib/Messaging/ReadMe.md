# Messaging namespace

This namespace defines the fine-print of the binary protocol between the
Terab server and the Terab client. Unlike, the 'official' C/C++ API of
Terab, there is little or no intent to preserve backward compatibility for
this API.

The client-server protocol operates over a single TCP socket, following an
asynchronous pattern where requests (resp. responses) are continuously
received (resp. sent).
