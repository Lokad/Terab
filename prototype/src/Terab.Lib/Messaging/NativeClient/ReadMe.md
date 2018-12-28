# NativeClient namespace

This namespace contains a C# PInvoke wrapper around the C/C++ API
of Terab. This namespace does not contribute to the server-side
logic of Terab. 

This namespace is primarily intended for testing purposes, however
it could also be used by a client implementation if this implementation
happens to be in .NET.

This namespace is the sole point of depdencence from `Terab.Lib` to
`Terab.Client`. The rest of the server logic is NOT dependent upon the
client library. As PInvoke calls are late-bound, it is possible to run
the Terab server without `Terab.Client`.
