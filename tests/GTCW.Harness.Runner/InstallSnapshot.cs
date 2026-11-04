using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace GTCW.Harness.Runner
{
    /// <summary>
    /// Size and timestamp of every file the game could plausibly write in the
    /// install, taken before and after the headless runs: the proof that a run
    /// read the install and wrote nothing to it.
    /// </summary>
    internal sealed class InstallSnapshot
    {
        private static readonly string[] Recursive = { "Config", "Campaigns", "Battlefields", "Avatar", "Modding", "BepInEx" };

        private readonly Dictionary<string, (long Length, long Ticks)> _files;

        private InstallSnapshot(Dictionary<string, (long, long)> files) => _files = files;

        public static InstallSnapshot Take(Settings settings)
        {
            var files = new Dictionary<string, (long, long)>(StringComparer.OrdinalIgnoreCase);
            void Add(string path)
            {
                var info = new FileInfo(path);
                files[Path.GetRelativePath(settings.HarnessGameDir, path)] = (info.Length, info.LastWriteTimeUtc.Ticks);
            }

            foreach (string file in Directory.GetFiles(settings.HarnessGameDir))
            {
                Add(file);
            }

            foreach (string file in Directory.GetFiles(Path.Combine(settings.HarnessGameDir, settings.DataFolderName)))
            {
                Add(file);
            }

            foreach (string folder in Recursive)
            {
                string full = Path.Combine(settings.HarnessGameDir, folder);
                if (Directory.Exists(full))
                {
                    foreach (string file in Directory.EnumerateFiles(full, "*", SearchOption.AllDirectories))
                    {
                        Add(file);
                    }
                }
            }

            return new InstallSnapshot(files);
        }

        public List<string> Differences(InstallSnapshot after)
        {
            var changes = new List<string>();
            changes.AddRange(after._files.Keys.Except(_files.Keys, StringComparer.OrdinalIgnoreCase).Select(f => "added " + f));
            changes.AddRange(_files.Keys.Except(after._files.Keys, StringComparer.OrdinalIgnoreCase).Select(f => "removed " + f));
            changes.AddRange(_files.Where(p => after._files.TryGetValue(p.Key, out var a) && a != p.Value).Select(p => "changed " + p.Key));
            return changes;
        }
    }
}
