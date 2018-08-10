# The Terab project

STATUS: **incomplete, non-functional**

Terab is intended as the scalable database back-end of a full Bitcoin 
implementation. Terab provides a blockchain centric key-value store
specifically tailored for the needs of Bitcoin Cash.

In particular, Terab seeks to max-out the capabilites of the underlying
hardware, especially from the I/O perspective, which is one of the
challenges involved with on-chain scaling.

The data structure leveraged by Terab is referred as the Sozu Table.
More on this storage system can be found in `/docs/sozu-table.md`.
