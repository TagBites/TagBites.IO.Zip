﻿namespace TagBites.IO.Zip
{
    /// <summary>
    /// Exposes static method for creating Zip file system.
    /// </summary>
    public static class ZipFileSystem
    {
        /// <summary>
        /// Creates a Zip file system.
        /// </summary>
        /// <param name="fullName">The full name of the .zip file.</param>
        /// <returns>A Zip file system contains the procedures that are used to perform file and directory operations.</returns>
        public static FileSystem Create(string fullName) => new FileSystem(new ZipFileSystemOperations(fullName));
    }
}
