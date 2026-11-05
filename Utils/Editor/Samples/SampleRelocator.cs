using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Utils.EditorTools
{
    // Package Manager always copies a sample into Assets/Samples/<package>/<version>/<sample>. Samples listed in
    // Targets are moved to their own folder right after the import. When that folder already exists, the import is an
    // update: file contents are replaced, the old .meta (and so every GUID and reference) stays.
    internal class SampleRelocator : AssetPostprocessor
    {
        private const string SamplesRoot = "Assets/Samples/Utils";

        // Sample display name (package.json) -> folder it lives in.
        private static readonly Dictionary<string, string> Targets = new()
        {
            { "ProjectUI", "Assets/ProjectUI" },
        };

        private static bool scheduled;

        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (scheduled) return;
            foreach (var path in imported)
            {
                if (!path.StartsWith(SamplesRoot + "/")) continue;
                // Assets/Samples/Utils/<version>/<sample>/...
                var parts = path.Substring(SamplesRoot.Length + 1).Split('/');
                if (parts.Length < 2 || !Targets.ContainsKey(parts[1])) continue;

                // Moving assets inside the import callback is not allowed: wait for the import to finish.
                scheduled = true;
                EditorApplication.delayCall += RelocateAll;
                return;
            }
        }

        private static void RelocateAll()
        {
            scheduled = false;
            if (!AssetDatabase.IsValidFolder(SamplesRoot)) return;

            foreach (var versionFolder in AssetDatabase.GetSubFolders(SamplesRoot))
            {
                foreach (var target in Targets)
                {
                    var source = $"{versionFolder}/{target.Key}";
                    if (AssetDatabase.IsValidFolder(source)) Relocate(source, target.Value, target.Key);
                }
                DeleteIfEmpty(versionFolder);
            }
            DeleteIfEmpty(SamplesRoot);
            DeleteIfEmpty("Assets/Samples");
        }

        private static void Relocate(string source, string target, string sampleName)
        {
            if (!AssetDatabase.IsValidFolder(target))
            {
                var error = AssetDatabase.MoveAsset(source, target);
                if (string.IsNullOrEmpty(error)) Debug.Log($"[Utils] {sampleName} установлен в {target}.");
                else Debug.LogError($"[Utils] {sampleName} не перенесён из {source} в {target}: {error}");
                return;
            }

            var update = EditorUtility.DisplayDialog(
                $"{sampleName} уже есть",
                $"Папка {target} уже есть. Обновить её файлами из пакета?\n\n" +
                "Файлы с тем же именем перезапишутся (свои правки в них пропадут), новые добавятся, GUID и ссылки сохранятся. " +
                "Файлы, которых нет в пакете, останутся.",
                "Обновить", "Отмена");

            if (update) UpdateFolder(source, target, sampleName);
            AssetDatabase.DeleteAsset(source);
            AssetDatabase.Refresh();
        }

        private static void UpdateFolder(string source, string target, string sampleName)
        {
            var sourceFull = Path.GetFullPath(source);
            var targetFull = Path.GetFullPath(target);
            int replaced = 0, added = 0;

            foreach (var file in Directory.GetFiles(sourceFull, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".meta")) continue;
                var relative = Path.GetRelativePath(sourceFull, file);
                var destination = Path.Combine(targetFull, relative);

                if (File.Exists(destination))
                {
                    // The copy in Samples got a new GUID from Unity (conflict with the installed one): keep the old .meta.
                    File.Copy(file, destination, true);
                    replaced++;
                    continue;
                }

                CopyNewFolders(sourceFull, targetFull, Path.GetDirectoryName(relative));
                File.Copy(file, destination);
                if (File.Exists(file + ".meta")) File.Copy(file + ".meta", destination + ".meta", true);
                added++;
            }

            Debug.Log($"[Utils] {sampleName} обновлён в {target}: заменено {replaced}, добавлено {added}.");
        }

        // Creates folders missing in the target together with their .meta, outermost first.
        private static void CopyNewFolders(string sourceFull, string targetFull, string relativeFolder)
        {
            if (string.IsNullOrEmpty(relativeFolder)) return;
            var destination = Path.Combine(targetFull, relativeFolder);
            if (Directory.Exists(destination)) return;

            CopyNewFolders(sourceFull, targetFull, Path.GetDirectoryName(relativeFolder));
            Directory.CreateDirectory(destination);
            var meta = Path.Combine(sourceFull, relativeFolder) + ".meta";
            if (File.Exists(meta)) File.Copy(meta, destination + ".meta", true);
        }

        private static void DeleteIfEmpty(string folder)
        {
            if (!AssetDatabase.IsValidFolder(folder)) return;
            if (Directory.EnumerateFileSystemEntries(Path.GetFullPath(folder)).GetEnumerator().MoveNext()) return;
            AssetDatabase.DeleteAsset(folder);
        }
    }
}
