using System;
using Terab.UTXO.Core.Sozu;

namespace Terab.UTXO.Core.SectorStore
{
    /// <summary>
    /// Intended to read or write sectors to the physical storage.
    /// <remarks>Index of sector inside a store is zero based.</remarks>
    /// </summary>
    public interface ISectorStore : IDisposable
    {
        /// <summary>
        /// Initialize must be called only once on freshly created
        /// instance of ISectorStore and.
        /// </summary>
        void Initialize();

        /// <summary>
        /// Indicates whether the store has already some sectors.
        /// </summary>
        bool HasSectors();

        /// <summary>
        /// Allocate additional capacity for new sectors.
        /// </summary>
        /// <param name="count">
        /// number of neighboring sectors to allocate at once
        /// </param>
        /// <returns>index of first of neighboring allocated sectors</returns>
        SectorIndex AllocateSectors(int count);

        /// <summary>Read a sector of given index and return it. </summary>
        /// <remarks>
        /// The returned 'Sector' is invalidated at the next call.
        /// </remarks>
        /// <param name="index">index of sector to be read. </param>
        Sector Read(SectorIndex index);

        /// <summary>
        /// Try read a sector of given index and return it via out parameter.
        /// </summary>
        /// <remarks>
        /// The returned 'Sector' is invalidated at the next call.
        /// </remarks>
        /// <param name="index">index of sector to be read. </param>
        /// <param name="sector">
        /// out parameter for returning sector read.
        /// </param>
        /// <returns>True if read sector succeeds, false otherwise. </returns>
        bool TryRead(SectorIndex index, out Sector sector);

        /// <summary>
        /// write a sector to store on the provided index.
        /// </summary>
        /// <param name="index">index given to the sector</param>
        /// <param name="source">sector to be written</param>
        void Write(SectorIndex index, Sector source);

        /// <summary>
        /// Write operations are not guaranteed to be durable until this method
        /// is called.
        /// </summary>
        void EnsurePersistence();
    }
}