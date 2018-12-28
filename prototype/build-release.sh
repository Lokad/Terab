#/bin/sh

make -C src/Terab.Client release
make -C extern/src/lmdb
dotnet build -c Release src/Terab.Linux.sln

