#/bin/sh

make -C src/Terab.Client debug
make -C extern/src/lmdb
dotnet build -c Debug src/Terab.Linux.sln

