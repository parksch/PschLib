using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace PschLib
{
    internal static class GoogleSheetDocumentation
    {
        private const string GuideFileName = "GoogleSheetGuide.html";

        public static void Open(string anchor = null)
        {
            if (!TryFindGuide(out var guidePath))
            {
                EditorUtility.DisplayDialog("Google Sheet Guide", "GoogleSheetGuide.html could not be found.", "OK");
                return;
            }

            var url = new Uri(guidePath).AbsoluteUri;

            if (!string.IsNullOrWhiteSpace(anchor))
            {
                url += anchor.StartsWith("#", StringComparison.Ordinal) ? anchor : $"#{anchor}";
            }

            Application.OpenURL(url);
        }

        private static bool TryFindGuide(out string guidePath)
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssembly(typeof(GoogleSheetDocumentation).Assembly);

            if (packageInfo != null && TryFindInRoot(packageInfo.resolvedPath, out guidePath))
            {
                return true;
            }

            foreach (var guid in AssetDatabase.FindAssets("PschLib.Editor t:AssemblyDefinitionAsset"))
            {
                var assemblyPath = AssetDatabase.GUIDToAssetPath(guid);

                if (!assemblyPath.StartsWith("Assets/", StringComparison.Ordinal))
                {
                    continue;
                }

                var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;

                if (string.IsNullOrWhiteSpace(projectRoot))
                {
                    continue;
                }

                var absoluteAssemblyPath = Path.GetFullPath(Path.Combine(projectRoot, assemblyPath));
                var editorDirectory = Directory.GetParent(absoluteAssemblyPath)?.FullName;
                var packageRoot = string.IsNullOrWhiteSpace(editorDirectory) ? null : Directory.GetParent(editorDirectory)?.FullName;

                if (TryFindInRoot(packageRoot, out guidePath))
                {
                    return true;
                }
            }

            guidePath = null;
            return false;
        }

        private static bool TryFindInRoot(string rootPath, out string guidePath)
        {
            if (!string.IsNullOrWhiteSpace(rootPath))
            {
                var documentationPath = Path.Combine(rootPath, "Documentation", GuideFileName);

                if (File.Exists(documentationPath))
                {
                    guidePath = documentationPath;
                    return true;
                }

                var hiddenDocumentationPath = Path.Combine(rootPath, "Documentation~", GuideFileName);

                if (File.Exists(hiddenDocumentationPath))
                {
                    guidePath = hiddenDocumentationPath;
                    return true;
                }
            }

            guidePath = null;
            return false;
        }
    }
}
