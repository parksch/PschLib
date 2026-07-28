using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PschLib
{
    internal static class GoogleSheetPathUtility
    {
        public static string NormalizeAssetFolder(string assetPath, string label)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
            {
                throw new InvalidOperationException($"The {label} path is empty.");
            }

            var normalized = assetPath.Replace('\\', '/').Trim().TrimEnd('/');

            if (!normalized.StartsWith("Assets/", StringComparison.Ordinal) || normalized.Contains("/../") || normalized.EndsWith("/..", StringComparison.Ordinal))
            {
                throw new InvalidOperationException($"The {label} path must be inside Assets: '{assetPath}'.");
            }

            return normalized;
        }

        public static string GetAbsolutePath(string assetPath)
        {
            var projectPath = Directory.GetParent(Application.dataPath)?.FullName;

            if (string.IsNullOrWhiteSpace(projectPath))
            {
                throw new InvalidOperationException("The Unity project path could not be resolved.");
            }

            return Path.Combine(projectPath, assetPath.Replace('/', Path.DirectorySeparatorChar));
        }

        public static void EnsureAssetFolder(string assetFolder)
        {
            var normalized = NormalizeAssetFolder(assetFolder, "asset output");
            var sections = normalized.Split('/');
            var current = sections[0];

            for (var index = 1; index < sections.Length; index++)
            {
                var next = $"{current}/{sections[index]}";

                if (!AssetDatabase.IsValidFolder(next))
                {
                    AssetDatabase.CreateFolder(current, sections[index]);
                }

                current = next;
            }
        }
    }
}
