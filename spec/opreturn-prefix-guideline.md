# Terab 4-byte prefix guideline for OP_RETURN on Bitcoin Cash
> By Joannes Vermorel (Lokad), Amaury Séchet (Bitcoin ABC), Shammah Chancellor (Bitcoin ABC), May 23rd, 2018

**Abstract:** As an _optional guideline_, we recommend that all present and future protocols to be implemented on Bitcoin Cash implement a 4-byte prefix scheme, referred to as protocol identifiers, whenever they use OP_RETURN. This scheme will simplify interoperability between protocols, facilitate selective pruning of the blockchain and get built-in support from low-level infrastructure components. The proposal also provides a basic early stage process to let the community claim prefixes for their own protocols.

_In the following, Bitcoin always refers to Bitcoin Cash._

## Overview

Anticipating the May 15th upgrade of Bitcoin, the ecosystem has gained renewed interest for overlay protocols built on top of the blockchain, leveraging the new capacity at 223 bytes of the OP_RETURN opcode of Bitcoin. However, it appears that protocols that are presently emerging and that are gaining market traction are not systematically prefixing their messages carried through OP_RETURN, which represents the simplest option to sort messages according to their respective protocols. 

As a result, unless the Bitcoin community rapidly agrees to a unifying scheme, as the usage of OP_RETURN grows, the collisions between protocols will multiply. While none of those collisions endanger Bitcoin itself, they will significantly and needlessly complicate the design of the software intended to operate those overlay protocols.

## Recommended guideline: systematic 4-byte prefix

The authors jointly recommend that all OP_RETURN protocols implemented on Bitcoin should start by specifying a _unique_ 4-byte prefix - referred to as the protocol identifier (or protocol ID) - which will be prepended to all messages related to their own protocol.

The OP_RETURN opcode works with a sequence of OP_PUSHDATA:

    OP_RETURN
    OP_PUSHDATA [data]
    ...
    OP_PUSHDATA [data]

In Bitcoin, multiple OP_PUSHDATA are considered as _standard_ transactions.

The present guideline recommends inserting `0x04 [protocol ID]` as the very first element to specify your protocol identifier. That is:

    OP_RETURN
    0x04 [protocol ID]
    OP_PUSHDATA [data]
    ...
    OP_PUSHDATA [data]

The protocol ID comes first in order to improve the performance of any filter being implemented to operate on the blockchain: the protocol ID is picked first in order to skip the data as soon as possible.

Also the protocol ID values, little-endian, **must be higher than 0x00 00 00 0F and lower than 0x10 00 00 00**. The lower range is reserved because identifiers would collide with special push ops, and would provide a favorable treatment to a short list of protocols. The upper range is reserved for backward compatible potential future adjustments to the present guideline.

The inclusive range 0x0A BC DE 00 to 0x0A BC DE FF is reserved for testing purposes. Software toolkits and educational materials should use this range to demonstrate how protocols are built on Bitcoin. This range is similar in spirit to the domain name `example.com`.

As a courtesy to the community, we recommend to either submit a ticket or a pull request to the Git repository at https://github.com/Lokad/Terab/ to claim your prefix with:

* A display name for the protocol
* An author (or list of authors)
* A URL pointing to the specification of the protocol
* A CashAddr address (to avoid ambiguity and to later modify names / authors / URL).

This last step is _not_ a requirement. However, if you don’t try to make your prefix known to the community at large, be aware that it leaves you open to collisions with a useful protocol which just happens to have a lot more traction than yours.

See also _Annex: file format of /etc/protocols.csv_

## Why 4-byte

The value of **4** bytes has been chosen as a tradeoff between the blockchain data overhead and the number of distinct protocols supported by the prefixing scheme. 

* With 4-byte prefixes, taking into account the 4 restricted bits, Bitcoin supports over 260+ million distinct protocols. Realistically, Bitcoin will never run out of identifiers for protocols.
* The footprint overhead is low (2 to 5 bytes out of 223), so this guideline has minimal impact on the overall usability of OP_RETURN.

Furthermore, by sticking to this guideline, you can expect a degree of support from the teams working on fundamental infrastructure pieces of Bitcoin such as Terab.

**Why not 2-byte**: opting for short protocol identifiers would create an artificial scarcity of protocol identifiers with no clear upside for the community. Short identifiers will predictably result in giving a first-mover advantage - mostly through status - to a selection of participants, which is undesirable. Furthermore, as participants will also anticipate this scarcity, land grabbing and identifier squatting will also predictably ensue, which are also undesirable.

## The problem

Any OP_RETURN protocol should be resilient to adversarial behaviors where participants can be expected to push garbage to the blockchain. However, there is a big difference between:

* regular adversaries pushing garbage in your direction.
* having the next large social network generating collisions in your direction. 

Adversaries have to pay transaction fees to garbage your protocol which is not an economically efficient form of attack. However, the next large social network might _profitably_ collide with your protocol while doing it _at scale_.

## Intent of support
The blockchain will grow very large and it will become pruneable in the future. Hence, the data carried by OP_RETURN should _not_ be expected to be preserved by default by every Bitcoin participant in the future.

Yet, scalable blockchain components<sup>1</sup> will offer some degree of built-in support to selectively persist messages according to specified 4-byte prefixes (and more, although the fine print is fuzzy for now). By adopting a 4-byte prefix for your protocol now, you will benefit from some degree of support from the infrastructure pieces that are presently being developed to make Bitcoin scale.

If you do not abide to this guideline - _we don’t force you_ - be aware that you should not expect _de facto_ big data infrastructure of Bitcoin to support your protocol. You will have to build those software pieces yourself.

## Annex: file format of /etc/protocols.csv
In order to make known protocols easily available to the community at large, a simple file format is proposed to gather the protocol prefixes, following a pattern essentially similar to `/etc/services` which exists in Linux distributions. 

This file `protocols.csv` should be seen as an early stage effort to help various protocols gain traction within the Bitcoin community. If the number of active protocols becomes greater than a few hundred, we expect that the file `protocols.csv` will be superseded with an approach more scalable than having a flat text file holding all known protocols.

The URL for the file is expected to be:

https://github.com/Lokad/Terab/blob/master/etc/protocols.csv 

The file is encoded in CSV as per [RFC 4180](https://tools.ietf.org/html/rfc4180) with the following options:

* UTF-8 encoding
* Unix line ending (\n)
* Comma delimiter
* Optional quote escaping for strings
* Quote escaped strings cannot contain newline (\n), returns (\r) or quotes (“)
* First line is the column headers

Then the columns themselves:

* `Prefix`: hexadecimal encoded (aka 0x01234567, little-endian)
* `DisplayName`: string
* `Authors`: string
* `BitcoinAddress`: a valid CashAddr (starting with `bitcoincash:`)
* `SpecificationUrl`: string

Each line should not be longer than 1KB (1024 bytes) in total.

The lines should be sorted in increasing order against their prefix.

## Footnotes

1. For example, Terab (open source project by Lokad) is a specialized persistence backend intended to support the blockchain ultimately growing up to blocks of 1 terabyte. Such software components will be required in the future to cope with the increased usage of Bitcoin.
