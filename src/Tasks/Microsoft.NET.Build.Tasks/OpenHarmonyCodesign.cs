// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Build.Framework;
#if NET
using System.Buffers.Binary;
#endif

namespace Microsoft.NET.Build.Tasks;

/// <summary>Signs native build outputs using the OpenHarmony SDK's signing tool.</summary>
public sealed class OpenHarmonyCodesign : TaskBase
{
    [Required]
    public string[] Directories { get; set; } = [];

    [Required]
    public string CacheDirectory { get; set; } = "";

    public string SigningTool { get; set; } = "binary-sign-tool";

    public string[] ExcludedFiles { get; set; } = [];

    protected override void ExecuteCore()
    {
#if NET
        if (OperatingSystem.IsWindows())
        {
            ReportError(Strings.OpenHarmonySigningHostUnsupported);
            return;
        }

        Directory.CreateDirectory(CacheDirectory);
        var excludedFiles = new HashSet<string>(ExcludedFiles.Select(Path.GetFullPath), StringComparer.Ordinal);
        var enumerationOptions = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = false
        };
        foreach (string directory in Directories.Where(Directory.Exists).Distinct(StringComparer.Ordinal))
        {
            foreach (string file in Directory.EnumerateFiles(directory, "*", enumerationOptions).ToArray())
            {
                if (excludedFiles.Contains(Path.GetFullPath(file)) ||
                    (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0 || !IsLoadableElf(file))
                {
                    continue;
                }

                string cacheKey = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(SigningTool + "\0" + Path.GetFullPath(file))));
                string cacheFile = Path.Combine(CacheDirectory, cacheKey);
                string hash = HashFile(file);
                if (File.Exists(cacheFile) && File.ReadAllText(cacheFile) == hash)
                {
                    continue;
                }

                // Signed files may be sealed against writes. Sign into a new inode in the
                // same directory, then atomically replace the old output after success.
                string signedFile = file + "." + Guid.NewGuid().ToString("N") + ".signed";
                try
                {
                    var startInfo = new ProcessStartInfo(SigningTool)
                    {
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };
                    foreach (string argument in new[] { "sign", "-inFile", file, "-outFile", signedFile, "-selfSign", "1" })
                    {
                        startInfo.ArgumentList.Add(argument);
                    }

                    using Process process = Process.Start(startInfo)!;
                    var stdout = process.StandardOutput.ReadToEndAsync();
                    var stderr = process.StandardError.ReadToEndAsync();
                    process.WaitForExit();
                    string output = stdout.GetAwaiter().GetResult() + stderr.GetAwaiter().GetResult();
                    if (process.ExitCode != 0 || !File.Exists(signedFile))
                    {
                        ReportError(string.Format(Strings.OpenHarmonySigningFailed, file, process.ExitCode, output));
                        return;
                    }

                    File.SetUnixFileMode(signedFile, File.GetUnixFileMode(file));
                    File.Move(signedFile, file, overwrite: true);
                    File.WriteAllText(cacheFile, HashFile(file));
                    Log.LogMessage(MessageImportance.Low, "Signed OpenHarmony ELF: {0}", file);
                }
                catch (System.ComponentModel.Win32Exception exception)
                {
                    ReportError(string.Format(Strings.OpenHarmonySigningToolFailed, SigningTool, exception.Message));
                    return;
                }
                finally
                {
                    File.Delete(signedFile);
                }
            }
        }
#else
        ReportError(Strings.OpenHarmonySigningHostUnsupported);
#endif
    }

    private void ReportError(string message) =>
        Log.Log(new Message(MessageLevel.Error, message, code: "OHOSSDK0001"));

#if NET
    internal static bool IsLoadableElf(string file)
    {
        using var stream = File.OpenRead(file);
        using var reader = new BinaryReader(stream);
        byte[] header = reader.ReadBytes(64);
        if (header.Length < 64 || header[0] != 0x7f || header[1] != 'E' ||
            header[2] != 'L' || header[3] != 'F' || header[4] != 2 || header[5] != 1)
        {
            return false;
        }

        // This SDK targets ELF64 little endian. Relocatable .o inputs are not
        // loaded by the OS and must remain untouched for the native linker.
        ushort type = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(16));
        if (type is not (2 or 3))
        {
            return false;
        }

        ulong tableOffset = BinaryPrimitives.ReadUInt64LittleEndian(header.AsSpan(32));
        ushort entrySize = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(54));
        ushort entryCount = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(56));
        if (entrySize < 56 || tableOffset > (ulong)stream.Length ||
            (ulong)entrySize * entryCount > (ulong)stream.Length - tableOffset)
        {
            return false;
        }

        // objcopy --only-keep-debug preserves the ELF type and program headers,
        // but removes executable bytes and the dynamic table. Signing that file
        // changes its CRC and invalidates the executable's .gnu_debuglink.
        for (int i = 0; i < entryCount; i++)
        {
            stream.Position = (long)tableOffset + (long)i * entrySize;
            byte[] segment = reader.ReadBytes(56);
            uint segmentType = BinaryPrimitives.ReadUInt32LittleEndian(segment);
            uint flags = BinaryPrimitives.ReadUInt32LittleEndian(segment.AsSpan(4));
            ulong fileSize = BinaryPrimitives.ReadUInt64LittleEndian(segment.AsSpan(32));
            if (fileSize != 0 && (segmentType == 2 || (segmentType == 1 && (flags & 1) != 0)))
            {
                return true;
            }
        }

        return false;
    }

    private static string HashFile(string file)
    {
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(SHA256.HashData(stream));
    }
#endif
}
