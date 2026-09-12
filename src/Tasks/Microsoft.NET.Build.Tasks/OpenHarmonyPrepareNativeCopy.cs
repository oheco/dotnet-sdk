// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks;

/// <summary>Removes native outputs that the following MSBuild copy must replace.</summary>
public sealed class OpenHarmonyPrepareNativeCopy : TaskBase
{
    [Required]
    public ITaskItem[] SourceFiles { get; set; } = [];

    [Required]
    public ITaskItem[] DestinationFiles { get; set; } = [];

    public bool OnlyIfSourceNewer { get; set; }

    public bool SkipUnchangedFiles { get; set; }

    protected override void ExecuteCore()
    {
#if NET
        if (SourceFiles.Length != DestinationFiles.Length)
        {
            throw new InvalidOperationException("OpenHarmony native copy source and destination counts differ.");
        }

        for (int i = 0; i < SourceFiles.Length; i++)
        {
            string source = Path.GetFullPath(SourceFiles[i].ItemSpec);
            string destination = Path.GetFullPath(DestinationFiles[i].ItemSpec);
            if (source == destination || !File.Exists(source) || !File.Exists(destination) ||
                (File.GetAttributes(destination) & FileAttributes.ReparsePoint) != 0)
            {
                continue;
            }

            var sourceInfo = new FileInfo(source);
            var destinationInfo = new FileInfo(destination);
            if ((OnlyIfSourceNewer && sourceInfo.LastWriteTimeUtc <= destinationInfo.LastWriteTimeUtc) ||
                (SkipUnchangedFiles && sourceInfo.Length == destinationInfo.Length &&
                 sourceInfo.LastWriteTimeUtc == destinationInfo.LastWriteTimeUtc))
            {
                continue;
            }

            if (OpenHarmonyCodesign.IsLoadableElf(source) && OpenHarmonyCodesign.IsLoadableElf(destination))
            {
                // Executed ELF inodes reject writes with EPERM, even after the
                // process exits. The following copy needs a new inode. Managed
                // assemblies, object files and detached symbols stay untouched.
                File.Delete(destination);
                Log.LogMessage(MessageImportance.Low, "Replacing OpenHarmony native output: {0}", destination);
            }
        }
#else
        Log.Log(new Message(MessageLevel.Error, Strings.OpenHarmonySigningHostUnsupported, code: "OHOSSDK0002"));
#endif
    }
}
