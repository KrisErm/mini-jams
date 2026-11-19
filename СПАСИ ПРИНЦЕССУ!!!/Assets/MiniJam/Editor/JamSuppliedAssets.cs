using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace MiniJam.Editor
{
    public static class JamSuppliedAssets
    {
        const string Source = "Assets/MiniJam/Editor/SourceAssets/";
        const string Destination = "Assets/MiniJam/Imported/";
        // Existing assets are reused by GUID, even if the user moved their folders.
        public static void Install(JamConfig config)
        {
            ExtractMissing(Source + "Characters.zip", Destination + "Characters");
            ExtractMissing(Source + "Dungeon.zip", Destination + "Dungeon");
            Directory.CreateDirectory("Assets/StreamingAssets/MiniJam");
            File.Copy(Source + "Twist.mp4.bytes", "Assets/StreamingAssets/MiniJam/twist.mp4", true);
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            config.guard = RequiredModel("c564c8c0d0cdc45429417f315e9f3e41", "MaleCharacterPolyart.prefab");
            config.princess = RequiredModel("257c304c754d3b247a51ef5bea7a90d0", "FemaleCharacterPolyart.prefab");
            config.floorModule = RequiredModel("9501d4b29a1b421468e968648a88aa47", "NGF_Env_Floor_01.fbx");
            config.wallModule = RequiredModel(null, "NGF_Env_Wall_01.fbx");
            config.doorwayModule = RequiredModel("c98e75160b2f42142825552c9b9b7930", "NGF_Env_Stone_doorway_01.fbx");
            config.idle = Clip("Idle_Normal_SwordAndShield.fbx");
            config.walk = Clip("MoveFWD_Normal_InPlace_SwordAndShield.fbx");
            config.attack = Clip("Attack01_SwordAndShiled.fbx");
            config.guardYaw = config.princessYaw = 0;
            config.suppliedArt = true;
            EditorUtility.SetDirty(config); AssetDatabase.SaveAssets();
        }
        static string FindPath(string guid, string filename)
        {
            if (!string.IsNullOrEmpty(guid))
            {
                string byGuid = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(byGuid)) return byGuid;
            }
            string imported = AssetDatabase.GetAllAssetPaths().FirstOrDefault(p =>
                p.StartsWith(Destination, StringComparison.Ordinal) && Path.GetFileName(p) == filename);
            if (imported != null) return imported;
            return AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(filename), new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath).OrderBy(p => p, StringComparer.Ordinal)
                .FirstOrDefault(p => Path.GetFileName(p) == filename);
        }
        static GameObject RequiredModel(string guid, string filename)
        {
            string path = FindPath(guid, filename);
            var model = path == null ? null : AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null) throw new InvalidOperationException("Не удалось импортировать " + filename + ". См. Console.");
            return model;
        }
        static AnimationClip Clip(string filename)
        {
            string path = FindPath(null, filename);
            if (path == null) throw new InvalidOperationException("Не найдена анимация " + filename);
            var clip = AssetDatabase.LoadAllAssetsAtPath(path).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal));
            if (clip == null) throw new InvalidOperationException("В FBX нет анимационного клипа: " + filename);
            return clip;
        }
        static void ExtractMissing(string archivePath, string targetRoot)
        {
            if (!File.Exists(archivePath)) throw new FileNotFoundException("Скопируй всю папку MiniJam из обновления.", archivePath);
            // Folder metas are intentionally not copied: folders can exist at another path.
            // Each asset's own meta is preserved together with its bytes.
            using (var zip = ZipFile.OpenRead(archivePath))
            {
                var entries = zip.Entries.ToDictionary(e => e.FullName, StringComparer.Ordinal);
                var pending = new List<Tuple<ZipArchiveEntry, ZipArchiveEntry, string>>();
                string fullRoot = Path.GetFullPath(targetRoot) + Path.DirectorySeparatorChar;
                foreach (var entry in zip.Entries)
                {
                    if (string.IsNullOrEmpty(entry.Name) || entry.FullName.EndsWith(".meta", StringComparison.Ordinal)) continue;
                    if (entry.FullName.Contains("/Scene/") || entry.FullName.EndsWith(".cs", StringComparison.Ordinal)) continue;
                    entries.TryGetValue(entry.FullName + ".meta", out ZipArchiveEntry meta);
                    string guid = null;
                    if (meta != null)
                    {
                        using (var reader = new StreamReader(meta.Open()))
                        {
                            var match = Regex.Match(reader.ReadToEnd(), @"(?m)^guid: ([0-9a-f]{32})");
                            if (match.Success) guid = match.Groups[1].Value;
                        }
                    }
                    string existing = guid == null ? FindPath(null, entry.Name) : AssetDatabase.GUIDToAssetPath(guid);
                    if (!string.IsNullOrEmpty(existing)) continue;
                    string target = Path.GetFullPath(Path.Combine(targetRoot, entry.FullName));
                    if (!target.StartsWith(fullRoot, StringComparison.OrdinalIgnoreCase)) throw new IOException("Недопустимый путь в архиве.");
                    if (File.Exists(target))
                    {
                        string existingGuid = AssetDatabase.AssetPathToGUID(targetRoot + "/" + entry.FullName);
                        if (guid != null && guid != existingGuid)
                            throw new IOException("Файл с другим GUID уже существует: " + target + ". Перенеси его перед повторным импортом.");
                        continue;
                    }
                    pending.Add(Tuple.Create(entry, meta, target));
                }
                AssetDatabase.StartAssetEditing();
                try
                {
                    foreach (var item in pending)
                    {
                        Directory.CreateDirectory(Path.GetDirectoryName(item.Item3));
                        using (var input = item.Item1.Open()) using (var output = File.Create(item.Item3)) input.CopyTo(output);
                        if (item.Item2 != null)
                            using (var input = item.Item2.Open()) using (var output = File.Create(item.Item3 + ".meta")) input.CopyTo(output);
                    }
                }
                finally { AssetDatabase.StopAssetEditing(); }
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            }
        }
    }
}
