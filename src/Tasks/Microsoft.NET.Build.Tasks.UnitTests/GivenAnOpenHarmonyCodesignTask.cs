// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Utilities;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests;

public class GivenAnOpenHarmonyCodesignTask
{
    [PlatformSpecificFact(TestPlatforms.Linux | TestPlatforms.OSX)]
    public void ItPreparesOnlyNativeFilesThatTheFollowingCopyMustReplace()
    {
        string root = Path.Combine(Path.GetTempPath(), "ohos copy " + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string source = Path.Combine(root, "source");
            string destination = Path.Combine(root, "destination");
            string newer = Path.Combine(root, "newer");
            string managed = Path.Combine(root, "managed.dll");
            string symbols = Path.Combine(root, "symbols.dbg");
            string objectFile = Path.Combine(root, "input.o");
            string link = Path.Combine(root, "linked output");
            File.WriteAllBytes(source, CreateElf(2));
            File.WriteAllBytes(destination, CreateElf(1));
            File.WriteAllBytes(newer, CreateElf(3));
            File.WriteAllText(managed, "MZ managed assembly");
            File.WriteAllBytes(symbols, CreateElf(4, fileSize: 0));
            File.WriteAllBytes(objectFile, CreateElf(5, type: 1));
            File.CreateSymbolicLink(link, newer);
            DateTime timestamp = DateTime.UtcNow;
            File.SetLastWriteTimeUtc(source, timestamp);
            File.SetLastWriteTimeUtc(destination, timestamp.AddMinutes(-2));
            File.SetLastWriteTimeUtc(newer, timestamp.AddMinutes(2));
            var task = new OpenHarmonyPrepareNativeCopy
            {
                BuildEngine = new MockBuildEngine(),
                SourceFiles = Enumerable.Repeat(new TaskItem(source), 6).ToArray(),
                DestinationFiles = new[] { destination, newer, managed, symbols, objectFile, link }
                    .Select(path => new TaskItem(path)).ToArray(),
                OnlyIfSourceNewer = true
            };
            task.Execute().Should().BeTrue();
            File.Exists(destination).Should().BeFalse();
            File.ReadAllBytes(newer).Should().Equal(CreateElf(3));
            File.ReadAllText(managed).Should().Be("MZ managed assembly");
            File.ReadAllBytes(symbols).Should().Equal(CreateElf(4, fileSize: 0));
            File.ReadAllBytes(objectFile).Should().Equal(CreateElf(5, type: 1));
            File.GetAttributes(link).HasFlag(FileAttributes.ReparsePoint).Should().BeTrue();

            File.Copy(source, destination);
            File.SetLastWriteTimeUtc(destination, File.GetLastWriteTimeUtc(source));
            task.SourceFiles = [new TaskItem(source)];
            task.DestinationFiles = [new TaskItem(destination)];
            task.OnlyIfSourceNewer = false;
            task.SkipUnchangedFiles = true;
            task.Execute().Should().BeTrue();
            File.Exists(destination).Should().BeTrue();
            task.SkipUnchangedFiles = false;
            task.Execute().Should().BeTrue();
            File.Exists(destination).Should().BeFalse();

            task.SourceFiles = [new TaskItem(Path.Combine(root, "missing"))];
            task.DestinationFiles = [new TaskItem(newer)];
            task.Execute().Should().BeTrue();
            File.Exists(newer).Should().BeTrue();
            task.SourceFiles = [new TaskItem(newer)];
            task.Execute().Should().BeTrue();
            File.Exists(newer).Should().BeTrue();
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [PlatformSpecificFact(TestPlatforms.Linux | TestPlatforms.OSX)]
    public void ItSignsOnlyElfFilesAndPreservesIncrementalOutputs()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string root = Path.Combine(Path.GetTempPath(), "ohos codesign " + Guid.NewGuid().ToString("N"));
        try
        {
            string outputs = Directory.CreateDirectory(Path.Combine(root, "output files")).FullName;
            string elf = Path.Combine(outputs, "test app");
            string managed = Path.Combine(outputs, "test.dll");
            string symbols = Path.Combine(outputs, "test.custom-symbols");
            File.WriteAllBytes(symbols, CreateElf(5));
            File.WriteAllBytes(elf, CreateElf(1));
            File.SetUnixFileMode(elf, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.WriteAllText(managed, "MZ managed assembly");
            string objectFile = Path.Combine(outputs, "linker input.o");
            string debugFile = Path.Combine(outputs, "detached symbols");
            byte[] objectBytes = CreateElf(7, type: 1);
            byte[] debugBytes = CreateElf(8, fileSize: 0);
            File.WriteAllBytes(objectFile, objectBytes);
            File.WriteAllBytes(debugFile, debugBytes);
            string external = Directory.CreateDirectory(Path.Combine(root, "external")).FullName;
            string externalElf = Path.Combine(external, "library.so");
            File.WriteAllBytes(externalElf, CreateElf(4));
            Directory.CreateSymbolicLink(Path.Combine(outputs, "linked directory"), external);
            string signer = Path.Combine(root, "signing tool");
            File.WriteAllText(signer, "#!/bin/sh\nset -eu\ncat \"$3\" > \"$5\"\nprintf signed >> \"$5\"\n");
            File.SetUnixFileMode(signer, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);

            var task = new OpenHarmonyCodesign
            {
                BuildEngine = new MockBuildEngine(),
                Directories = [outputs],
                ExcludedFiles = [symbols],
                CacheDirectory = Path.Combine(root, "cache"),
                SigningTool = signer
            };

            task.Execute().Should().BeTrue();
            byte[] signed = File.ReadAllBytes(elf);
            signed.Length.Should().Be(127);
            File.GetUnixFileMode(elf).Should().Be(UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
            File.ReadAllText(managed).Should().Be("MZ managed assembly");
            new FileInfo(externalElf).Length.Should().Be(121);
            new FileInfo(symbols).Length.Should().Be(121);

            task.Execute().Should().BeTrue();
            File.ReadAllBytes(elf).Should().Equal(signed);
            File.ReadAllBytes(objectFile).Should().Equal(objectBytes);
            File.ReadAllBytes(debugFile).Should().Equal(debugBytes);

            File.WriteAllBytes(elf, CreateElf(2));
            task.Execute().Should().BeTrue();
            File.ReadAllBytes(elf)[120].Should().Be(2);
            new FileInfo(elf).Length.Should().Be(127);

            File.WriteAllBytes(elf, CreateElf(3));
            File.WriteAllText(signer, "#!/bin/sh\nprintf '{failed}' >&2\nexit 23\n");
            task.Execute().Should().BeFalse();
            File.ReadAllBytes(elf).Should().Equal(CreateElf(3));
            Directory.GetFiles(outputs).Length.Should().Be(5);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static byte[] CreateElf(byte value, ushort type = 3, ulong fileSize = 1)
    {
        // ELF64 with one executable PT_LOAD segment and a payload byte.
        byte[] bytes = new byte[121];
        using var writer = new BinaryWriter(new MemoryStream(bytes));
        writer.Write(new byte[] { 0x7f, (byte)'E', (byte)'L', (byte)'F', 2, 1, 1 });
        writer.BaseStream.Position = 16;
        writer.Write(type);
        writer.Write((ushort)183);
        writer.Write(1u);
        writer.BaseStream.Position = 32;
        writer.Write(64ul);
        writer.BaseStream.Position = 52;
        writer.Write((ushort)64);
        writer.Write((ushort)56);
        writer.Write((ushort)1);
        writer.BaseStream.Position = 64;
        writer.Write(1u);
        writer.Write(5u);
        writer.Write(120ul);
        writer.BaseStream.Position = 96;
        writer.Write(fileSize);
        writer.Write(1ul);
        writer.Write(1ul);
        writer.Write(value);
        return bytes;
    }
}
