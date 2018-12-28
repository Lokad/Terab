# Coins

The purpose of this namespace is to ensure access and persistence of the
coins and their lifecycle events, that is, a superset of the strict UTXO 
dataset. 

## Sozu table

The UTXO is stored in a _Sozu table_ which can be seen as a layered hash
table. The first layers are rigidly pre-allocated with a given number of
sectors. The last layer is backed by a regular key-value store.

## Coin packs

The primary object stored on disk is the `CoinPack` which can be seen as
a collection of `Coin`s. The `CoinPack` is intended to match the sector
size, i.e. 4096 bytes on the first layer.

## Coin events

There are only two events that can happen to a coin: its production and,
later, its consumption. Each `Payload` contains a list of `CoinEvent`s.
Each `CoinEvent` contains the following information:

- whether the event is a production or a consumption
- the block when the event has been recorded

On the bit-level, the structure of a `CoinEvent` is the same as the one of a (see)
`BlockAlias` where the one bit reserved for the `CoinEvent` structure has been
filled in according to whether a receiving or spending event is encoded.
