# Fixed source dependencies

`msbuild/` contains the complete upstream MSBuild 18.9.4 source snapshot used
by this SDK's original Microsoft.Build packages. `msbuild-source.json` records
the upstream and .NET VMR commits, archive URL, checksum, license and changes.
The original MIT license and third-party notices remain in the source tree.

HarmonyOS cannot use `/tmp` for Unix socket endpoints. MSBuild's two internal
`NamedPipeUtil` implementations use `Path.GetTempPath()` only when the running
runtime identifier starts with `openharmony-`. The installed SDK launcher
provides an application-private `TMPDIR`. Other platforms retain their
upstream behavior. This covers task hosts, build nodes and resolver nodes;
task isolation remains enabled.

The patched managed MSBuild packages are SDK build inputs. Building the
dependency on Linux does not create target-native ELF files for distribution;
the SDK layout creates its host executables with the OpenHarmony apphost pack.
Native task-host and multi-project acceptance are required after composition.

The MSBuild source build and its additional fixed-input inventory are currently
being validated. Do not substitute this source tree for a completed release
validation report.
