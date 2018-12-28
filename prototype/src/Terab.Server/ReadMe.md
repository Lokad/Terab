# Terab Server

Host of the Terab server.

## Usage

The Terab instance must first be initialized once with the `init` command.

    dotnet Terab.Server.dll init --config /path/to/config --layer1 256 --layer2 1024

The `--layer1` argument is expressed in gigabytes and represents the size of
first storage layer of Terab (allocated at initialization).

The `--layer2` argument is optional, and also expressed in gigabytes. It represents
the size of the second storage layer of Terab (allocated at initialization).

It is recommended that the sum of the layer 1 and 2 should be at least twice the
expected size of the UTXO dataset. Once both layer 1 and layer 2 are full, Terab
will start using its layer 3 until storage runs out.

Running the Terab instance is done with the `run` command:

	dotnet Terab.Server.dll run --config /path/to/config

## The Terab layers

Terab organizes its data in a way that is akin to a layered hash table.

**Layer 1.** Intended to contain the bulk of the "active" part of the UTXO dataset.
This layer is a collection of pre-allocated sectors of 4KB. This layer also contains 
a probabilistic filter used to avoid needlessly probing the deeper layers.

**Layer 2.** Optional. When present, the old UTXO entries that do not fit into the
layer 1 are gradually overflowing into the layer 2. This layer is also a collection of
pre-allocated sectors. The number of sectors of the layer 2 matches the number of
sector of the layer 1. The size of the sectors is a multiple of 4KB.

**Layer 3.** Final storage fallback for UTXO entries that do not fit in either layer 1
or layer 2 (when present). Unlike layer 1 or layer 2, the layer 3 is backed by a regular
key-value store (LMDB presently), which gradually requests further file storage.

We recommend to dedicate an Intel Optane card to the layer 1, and to dedicated a larger
SSD drive to the layer 2. For example:

- Layer 1 at 225GB over an Intel Optane SSD 900P Series (280GB, AIC PCIe x4, 3D XPoint)
- Layer 2 at 900GB over an Samsung 970 PRO Series - 1TB PCIe NVMe - M.2 Internal SSD
- Layer 3 defaulting to Layer 1.

## XML configuration file

Terab uses an XML file for its configuration (POX, plain old XML). The class reflecting 
this configuration file is `TerabConfig`.

    <?xml version="1.0" encoding="utf-8" ?>
    <TerabConfig>
        <ipAddress>127.0.0.1</ipAddress>
        <port>8338</port>
		<layer1Path>/path/to/dir</layer1Path>
		<layer2Path>/optional/path/to/dir</layer2Path>
		<layer3Path>/optional/path/to/dir</layer3Path>
    </TerabConfig>

When the `layer2Path` is not specified, the layer 2 is omitted entirely. When the `layer3Path` 
is not specified, the `layer1Path` is used to persist the content of the layer 3.
