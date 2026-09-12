# .NET SDK 10.0.401 for HarmonyOS PC ARM64

This branch, `ohos/10.0.401`, adapts upstream `v10.0.401` for the
`openharmony-arm64` runtime identifier. It is paired with
[oheco/dotnet-runtime](https://github.com/oheco/dotnet-runtime), branch
`ohos/10.0.12`.

The SDK builds C# applications and supports CoreCLR JIT, ReadyToRun and
NativeAOT publishing. `binary-sign-tool` must be on the HarmonyOS host PATH;
NativeAOT additionally needs the host LLVM compiler, linker and binary tools.
Build and publish outputs are signed automatically. Native output copying
accounts for the host's protection of previously executed ELF inodes.

Shared input manifests, runtime-pack preparation, SDK build, signing,
packaging and native acceptance helpers live in the runtime fork's
[`eng/openharmony`](https://github.com/oheco/dotnet-runtime/tree/ohos/10.0.12/eng/openharmony)
directory. `build-sdk-target.sh` takes this SDK source directory and a prepared
local feed containing the source-built HarmonyOS runtime and compiler packs.

GUI development, workloads, mobile packaging and ASP.NET Core are outside
this delivery. Native SDK acceptance and formal release installation are
still in progress; compilation alone is not release acceptance.
