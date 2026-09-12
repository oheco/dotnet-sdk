# ASP.NET Core integration

The OpenHarmony SDK 10.0.401-ohos.2 bundles ASP.NET Core 10.0.12, its reference and runtime packs, Web templates, analyzers, static assets and development tools.

The framework is built from [oheco/dotnet-aspnetcore](https://github.com/oheco/dotnet-aspnetcore), branch `ohos/10.0.12`. Its `eng/openharmony/` directory owns the fixed-input build, native acceptance and distribution recipes for this combined SDK release. The runtime and vendored MSBuild inputs retain the SDK ohos.1 provenance.

Web, Razor, StaticWebAssets and WebAssembly task projects initialize their source resource paths after importing `Sdk.props`, because that import initializes `RepoRoot`. This fixes missing SDK entry files when building projects directly outside the VMR.

ASP.NET Core host behavior, supported publish modes and limitations are documented in the ASP.NET Core adaptation repository.
