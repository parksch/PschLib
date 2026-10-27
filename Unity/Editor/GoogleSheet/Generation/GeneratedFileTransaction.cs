using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PschLib.GoogleSheets
{
    // Used synchronously on the editor thread, before AssetDatabase.Refresh.
    internal sealed class GeneratedFileTransaction
    {
        private readonly Dictionary<string, byte[]> originals = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> touchedFiles = new List<string>();
        private readonly List<string> createdDirectories = new List<string>();

        public void CreateDirectory(string path)
        {
            var missing = new Stack<string>();
            var current = Path.GetFullPath(path);
            while (!Directory.Exists(current))
            {
                missing.Push(current);
                current = Path.GetDirectoryName(current);
            }

            while (missing.Count > 0)
            {
                var directory = missing.Pop();
                Directory.CreateDirectory(directory);
                createdDirectories.Add(directory);
            }
        }

        public bool WriteIfChanged(string path, string contents, Encoding encoding)
        {
            if (File.Exists(path) && string.Equals(File.ReadAllText(path), contents, StringComparison.Ordinal))
            {
                return false;
            }

            Capture(path);
            File.WriteAllText(path, contents, encoding);
            return true;
        }

        public bool Delete(string path)
        {
            if (!File.Exists(path))
            {
                return false;
            }

            Capture(path);
            File.Delete(path);
            return true;
        }

        private void Capture(string path)
        {
            path = Path.GetFullPath(path);
            if (!originals.ContainsKey(path))
            {
                originals.Add(path, File.Exists(path) ? File.ReadAllBytes(path) : null);
                touchedFiles.Add(path);
            }
        }

        public void Rollback(Exception originalFailure)
        {
            var failures = new List<Exception> { originalFailure };
            for (int i = touchedFiles.Count - 1; i >= 0; i--)
            {
                var path = touchedFiles[i];
                try
                {
                    var original = originals[path];
                    if (original == null)
                    {
                        if (File.Exists(path))
                        {
                            File.Delete(path);
                        }
                    }
                    else if (!File.Exists(path) || !BytesEqual(original, File.ReadAllBytes(path)))
                    {
                        File.WriteAllBytes(path, original);
                    }
                }
                catch (Exception failure)
                {
                    failures.Add(new IOException($"Could not restore generated file '{path}'.", failure));
                }
            }

            for (int i = createdDirectories.Count - 1; i >= 0; i--)
            {
                var path = createdDirectories[i];
                try
                {
                    if (Directory.Exists(path) && Directory.GetFileSystemEntries(path).Length == 0)
                    {
                        Directory.Delete(path);
                    }
                }
                catch (Exception failure)
                {
                    failures.Add(new IOException($"Could not remove generated directory '{path}'.", failure));
                }
            }

            if (failures.Count > 1)
            {
                throw new AggregateException("Code generation failed and some files could not be restored.", failures);
            }
        }

        private static bool BytesEqual(byte[] first, byte[] second)
        {
            if (first.Length != second.Length)
            {
                return false;
            }
            for (int i = 0; i < first.Length; i++)
            {
                if (first[i] != second[i])
                {
                    return false;
                }
            }
            return true;
        }
    }
}
