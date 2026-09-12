# .NET SDK 10.0.401 for HarmonyOS PC ARM64

This branch, `ohos/10.0.401`, adapts upstream `v10.0.401` for the
`openharmony-arm64` runtime identifier. It is paired with
[oheco/dotnet-runtime](https://github.com/oheco/dotnet-runtime), branch
`ohos/10.0.12`.

The SDK builds C# applications and supports CoreCLR JIT, ReadyToRun and
NativeAOT publishing. `binary-sign-tool` must be on the HarmonyOS host PATH;
NativeAOT additionally needs the host LLVM compiler, linker and binary tools.
Build and publish outputs are signed automatically. Native output copying
accounts for the host's protection of previously executed ELF inodes. NativeAOT
outputs include the ICU/OpenSSL dependencies beside the executable. On the
native host, debug symbols remain uncompressed because the supplied LLVM
linker lacks zlib compression; `CompressSymbols=true` explicitly overrides this
for a linker with compression support.

ASP.NET Core 10.0.12 is integrated for the SDK `10.0.401-ohos.2` distribution.
See [ASP.NET integration](eng/openharmony/ASP.NET.md) and the
[combined build/acceptance recipe](https://github.com/oheco/dotnet-aspnetcore/tree/ohos/10.0.12/eng/openharmony).
Web, MVC/Razor, SignalR and managed gRPC run on CoreCLR; supported Minimal API
applications also support NativeAOT. HTTP/3 requires a compatible MsQuic native
dependency, which is not included.

The original Runtime/MSBuild dependency build helpers live in the runtime fork's
[`eng/openharmony`](https://github.com/oheco/dotnet-runtime/tree/ohos/10.0.12/eng/openharmony)
directory. `build-sdk-target.sh` takes this SDK source directory and a prepared
local feed containing the source-built HarmonyOS runtime and compiler packs.

The vendored `tpr/msbuild` source fixes worker/task-host Unix socket placement
on HarmonyOS. Build this dependency before composing the SDK feed:

```sh
# runtime_kit names the runtime fork's eng/openharmony directory.
python3 "$runtime_kit/nuget-inputs.py" stage /path/to/msbuild-input-cache \
  eng/openharmony/nuget-inputs-msbuild.json --destination /path/to/msbuild-feed
export DOTNET_INSTALL_DIR=/path/to/fixed-linux-sdk-10.0.300
export NUGET_PACKAGES=/path/to/new-msbuild-cache
export DOTNET_CLI_HOME=/path/to/new-msbuild-cli-home
export DOTNET_OHOS_MSBUILD_FEED=/path/to/msbuild-feed
bash eng/openharmony/build-msbuild.sh tpr/msbuild /path/to/new-msbuild.log
python3 eng/openharmony/merge-msbuild-feed.py /path/to/prepared-sdk-feed \
  tpr/msbuild/artifacts/packages/Release /path/to/new-sdk-feed \
  --dependency-feed /path/to/msbuild-feed \
  --dependency-manifest eng/openharmony/nuget-inputs-msbuild.json
```

Use the merged feed and a separate empty package cache for the SDK build with
its fixed Linux SDK 10.0.302. The six patched MSBuild packages retain the
upstream 18.9.4 identity; their contents and source provenance are recorded in
the merged feed manifest. An archived source checkout must set
`DOTNET_OHOS_SOURCE_COMMIT` to the SDK commit containing its vendored source.
The dependency passed a fresh offline build, bootstrap sample and relevant
communication tests. The released SDK passed full native acceptance.

GUI development, workloads and mobile packaging remain outside this delivery.
The initial SDK 10.0.401-ohos.1 and Runtime 10.0.12-ohos.1 release is preserved;
the following validation record describes that earlier Runtime/SDK delivery.
Native oo 0.5.0 verified installation through the official v3 index, normal and
versioned commands, JIT/R2R/AOT builds and execution, and removal of both
packages from a directory with spaces. See [the initial-release validation record](eng/openharmony/VALIDATION.json).

The runtime/compiler binaries use runtime commit `033589b2981`; the generic
NativeAOT integration package separately includes the Unix library-path
quoting fix from `8b7bf48c679`. The Release build-kit explains the fixed-input
reproduction sequence. SDK archives use GNU long-link records, verified with
native tar. NativeAOT succeeds when the SDK installation path contains spaces.
