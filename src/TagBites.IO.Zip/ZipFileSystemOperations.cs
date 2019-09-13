using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ICSharpCode.SharpZipLib.Zip;
using TagBites.IO.Operations;

namespace TagBites.IO.Zip
{
    internal class ZipFileSystemOperations : IFileSystemOperations, IDisposable
    {
        internal ZipFile File { get; }

        public ZipFileSystemOperations(string fullName)
        {
            File = System.IO.File.Exists(fullName)
                ? new ZipFile(fullName)
                : ZipFile.Create(fullName);
        }


        public IFileSystemStructureLinkInfo GetLinkInfo(string fullName) => GetInfo(fullName);
        public string CorrectPath(string path) => path;

        public Stream ReadFile(FileLink file)
        {
            return ReadFile(file.FullName);
        }
        private Stream ReadFile(string fullName)
        {
            lock (File)
            {
                var entry = File.GetEntry(fullName);
                return entry != null ? File.GetInputStream(entry) : null;
            }
        }
        public IFileLinkInfo WriteFile(FileLink file, Stream stream)
        {
            if (stream == null)
                throw new NullReferenceException("stream");

            lock (File)
            {
                File.BeginUpdate();
                File.Add(new ZipStaticDataSource(stream), file.FullName, CompressionMethod.Stored);
                File.CommitUpdate();
            }
            
            return GetFileInfo(file.FullName);
        }
        public IFileLinkInfo MoveFile(FileLink source, FileLink destination)
        {
            lock (File)
            {
                var stream = ReadFile(source);
                WriteFile(destination, stream);
                DeleteFile(source);
            }

            return GetFileInfo(destination.FullName);
        }
        public void DeleteFile(FileLink file)
        {
            lock (File)
            {
                File.BeginUpdate();
                File.Delete(file.FullName);
                File.CommitUpdate();
            }
        }

        public IFileSystemStructureLinkInfo CreateDirectory(DirectoryLink directory)
        {
            lock (File)
            {
                if (File.GetEntry(GetDirectoryCorrectName(directory.FullName)) == null)
                {
                    File.BeginUpdate();
                    File.AddDirectory(directory.FullName);
                    File.CommitUpdate();
                }
            }

            return GetDirectoryInfo(directory.FullName);
        }
        public IFileSystemStructureLinkInfo MoveDirectory(DirectoryLink source, DirectoryLink destination)
        {
            lock (File)
            {
               
            }

            return GetFileInfo(destination.FullName);
        }
        public void DeleteDirectory(DirectoryLink directory, bool recursive)
        {
            lock (File)
            {
                File.BeginUpdate();
                var entry = File.GetEntry(GetDirectoryCorrectName(directory.FullName));
                File.Delete(entry);
                File.CommitUpdate();
            }
        }
        public IList<IFileSystemStructureLinkInfo> GetLinks(DirectoryLink directory, FileSystem.ListingOptions options)
        {
            var directoryName = GetDirectoryCorrectName(directory.FullName);

            lock (File)
            {
                return File.Cast<ZipEntry>()
                    .Where(x => x.Name != directoryName && x.Name.StartsWith(directoryName))
                    .Select(x => GetInfo(x.Name, x))
                    .Cast<IFileSystemStructureLinkInfo>()
                    .ToList();
            }
        }

        public IFileSystemStructureLinkInfo UpdateMetadata(FileSystemStructureLink link, IFileSystemLinkMetadata metadata)
        {
            lock (File)
            {
             
            }
            return GetInfo(link.FullName);
        }

        private ZipLinkInfo GetFileInfo(string fullName)
        {
            var info = GetInfo(fullName);
            return info != null && !info.IsDirectory ? info : null;
        }
        private ZipLinkInfo GetDirectoryInfo(string fullName)
        {
            var info = GetInfo(GetDirectoryCorrectName(fullName));
            return info != null && info.IsDirectory ? info : null;
        }
        private ZipLinkInfo GetInfo(string fullName)
        {
            //lock (Client)
            {
                var item = File.GetEntry(fullName);
                return item != null ? GetInfo(fullName, item) : null;
            }
        }
        private ZipLinkInfo GetInfo(string fullPath, ZipEntry line)
        {
            var item = new ZipLinkInfo(this, fullPath)
            {
                IsDirectory = line.IsDirectory,
                CreationTime = line.DateTime,
                LastWriteTime = line.DateTime,
                Length = line.Size
            };

            return item;
        }

        public void Dispose() => ((IDisposable)File).Dispose();

        private string GetDirectoryCorrectName(string fullName)
        {
            return fullName[fullName.Length - 1] == Path.AltDirectorySeparatorChar
                ? fullName
                : fullName + Path.AltDirectorySeparatorChar;
        }

        private class ZipLinkInfo : IFileLinkInfo
        {
            private FileHash _hash;
            private ZipFileSystemOperations Owner { get; }

            public string FullName { get; }
            public bool Exists => true;
            public bool IsDirectory { get; set; }

            public DateTime? CreationTime { get; set; }
            public DateTime? LastWriteTime { get; set; }
            public bool IsHidden => false;
            public bool IsReadOnly => false;

            public string ContentPath => FullName;
            public long Length { get; set; }
            public FileHash Hash
            {
                get
                {
                    if (!Exists)
                        return FileHash.Empty;

                    if (_hash.IsEmpty)
                    {
                        using var stream = Owner.ReadFile(FullName);
                        _hash = new FileHash(FileHashAlgorithm.Md5, HashHelper.Md5(stream));
                    }

                    return _hash;
                }
            }

            public ZipLinkInfo(ZipFileSystemOperations owner, string fullName)
            {
                Owner = owner;
                FullName = fullName;
            }

        }
        static class HashHelper
        {
            internal static string Md5(Stream stream)
            {
                using var algorithm = MD5.Create();
                var hash = algorithm.ComputeHash(stream);
                return HashBytesToString(hash);
            }

            private static string HashBytesToString(byte[] hash)
            {
                var sb = new StringBuilder(hash.Length);

                for (var i = 0; i < hash.Length; i++)
                    sb.Append(i.ToString("X2"));

                return sb.ToString().ToLower();
            }
        }
        private class ZipStaticDataSource : IStaticDataSource
        {
            private readonly Stream _stream;

            public ZipStaticDataSource(Stream stream)
            {
                _stream = stream;
            }

            public Stream GetSource() => _stream;
        }
    }
}
