﻿# List of components

To enable full offline source-building of the VMR, we have no other choice than to synchronize all the necessary code into the VMR. This also includes any code referenced via git submodules. More details on why and how this is done can be found here:
- [Strategy for managing external source dependencies](src/arcade/Documentation/UnifiedBuild/VMR-Strategy-For-External-Source.md)
- [Source Synchronization Process](src/arcade/Documentation/UnifiedBuild/VMR-Design-And-Operation.md#source-synchronization-process)

## Detailed list

<!-- component list beginning -->
- `src/arcade`  
*[dotnet/arcade@f209a92](https://github.com/dotnet/arcade/tree/f209a925b15bc66ecb9a8825bd9595937bbe3aa1)*
- `src/aspire`  
*[dotnet/aspire@137e8dc](https://github.com/dotnet/aspire/tree/137e8dcae0a7b22c05f48c4e7a5d36fe3f00a8d7)*
- `src/aspnetcore`  
*[dotnet/aspnetcore@ea91964](https://github.com/dotnet/aspnetcore/tree/ea9196442528eb76098bcde2c5de32f74ab7404b)*
    - `src/aspnetcore/src/submodules/googletest`  
    *[google/googletest@a1e255a](https://github.com/google/googletest/tree/a1e255a582377e1006bb88a408ac3f933ba7c916)*
    - `src/aspnetcore/src/submodules/MessagePack-CSharp`  
    *[aspnet/MessagePack-CSharp@9511905](https://github.com/aspnet/MessagePack-CSharp/tree/95119056ee8f4da1714b055a4f16893afaa73af7)*
- `src/cecil`  
*[dotnet/cecil@0cb8de0](https://github.com/dotnet/cecil/tree/0cb8de0eb8dfe676a449d080ed8c66b01faa6604)*
- `src/command-line-api`  
*[dotnet/command-line-api@803d859](https://github.com/dotnet/command-line-api/tree/803d8598f98fb4efd94604b32627ee9407f246db)*
- `src/deployment-tools`  
*[dotnet/deployment-tools@57d7bae](https://github.com/dotnet/deployment-tools/tree/57d7baec5f331a145174d0e8f00d7bbfdf2b77d4)*
- `src/diagnostics`  
*[dotnet/diagnostics@513150c](https://github.com/dotnet/diagnostics/tree/513150c2f25077b1fcb194407e53c433c975f39b)*
- `src/efcore`  
*[dotnet/efcore@fdb1568](https://github.com/dotnet/efcore/tree/fdb1568132b2109850db4045d4c3381b9c29d42d)*
- `src/emsdk`  
*[dotnet/emsdk@4ea46ba](https://github.com/dotnet/emsdk/tree/4ea46baeaf74d5a99cb93593362b6d8263b10550)*
- `src/fsharp`  
*[dotnet/fsharp@19610c0](https://github.com/dotnet/fsharp/tree/19610c0b654766eec49d044cb97ca6eaa2a63d16)*
- `src/msbuild`  
*[dotnet/msbuild@c4d51a1](https://github.com/dotnet/msbuild/tree/c4d51a11b84b4aabd9e5da1e3099f2c7c85024fe)*
- `src/nuget-client`  
*[nuget/nuget.client@1975634](https://github.com/nuget/nuget.client/tree/19756345139c45de23bd196e9b4be01d48e84fdd)*
    - `src/nuget-client/submodules/NuGet.Build.Localization`  
    *[NuGet/NuGet.Build.Localization@f15db7b](https://github.com/NuGet/NuGet.Build.Localization/tree/f15db7b7c6f5affbea268632ef8333d2687c8031)*
- `src/razor`  
*[dotnet/razor@a7648d0](https://github.com/dotnet/razor/tree/a7648d0ddc50f60c651bc2a25f4f36bdc2b496d7)*
- `src/roslyn`  
*[dotnet/roslyn@6a9d2b0](https://github.com/dotnet/roslyn/tree/6a9d2b077973e31b213517579f674b461053ac1c)*
- `src/roslyn-analyzers`  
*[dotnet/roslyn-analyzers@12b427e](https://github.com/dotnet/roslyn-analyzers/tree/12b427eb30b98288319463a6c9ce30567822c1ee)*
- `src/runtime`  
*[dotnet/runtime@43295bb](https://github.com/dotnet/runtime/tree/43295bb5378453d2ec4d9272cb44c6f50b4faa1f)*
- `src/scenario-tests`  
*[dotnet/scenario-tests@b7ce675](https://github.com/dotnet/scenario-tests/tree/b7ce67571aff209313584ed0ee34f714270099dd)*
- `src/sdk`  
*[dotnet/sdk@b26f3e6](https://github.com/dotnet/sdk/tree/b26f3e6f8a806e240163817aa0441f63a9566a5f)*
- `src/source-build-externals`  
*[dotnet/source-build-externals@13b0815](https://github.com/dotnet/source-build-externals/tree/13b08152f434be328df1228f29e79c8e70806ca3)*
    - `src/source-build-externals/src/abstractions-xunit`  
    *[xunit/abstractions.xunit@b75d54d](https://github.com/xunit/abstractions.xunit/tree/b75d54d73b141709f805c2001b16f3dd4d71539d)*
    - `src/source-build-externals/src/application-insights`  
    *[microsoft/ApplicationInsights-dotnet@43825e0](https://github.com/microsoft/ApplicationInsights-dotnet/tree/43825e06a22cdfb702fc199a7ba99a7d541d48c6)*
    - `src/source-build-externals/src/azure-activedirectory-identitymodel-extensions-for-dotnet`  
    *[AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet@e67b25b](https://github.com/AzureAD/azure-activedirectory-identitymodel-extensions-for-dotnet/tree/e67b25be77532af9ba405670b34b4d263d505fde)*
    - `src/source-build-externals/src/cssparser`  
    *[dotnet/cssparser@0d59611](https://github.com/dotnet/cssparser/tree/0d59611784841735a7778a67aa6e9d8d000c861f)*
    - `src/source-build-externals/src/docker-creds-provider-2.2.0`  
    *[mthalman/docker-creds-provider@5701f66](https://github.com/mthalman/docker-creds-provider/tree/5701f6667c1fbd805684857baaa860383bbdfed7)*
    - `src/source-build-externals/src/docker-creds-provider-2.2.1`  
    *[mthalman/docker-creds-provider@6c73fa4](https://github.com/mthalman/docker-creds-provider/tree/6c73fa4784795ae07f49305a057abf5c473d2adb)*
    - `src/source-build-externals/src/humanizer`  
    *[Humanizr/Humanizer@3ebc38d](https://github.com/Humanizr/Humanizer/tree/3ebc38de585fc641a04b0e78ed69468453b0f8a1)*
    - `src/source-build-externals/src/MSBuildLocator`  
    *[microsoft/MSBuildLocator@e0281df](https://github.com/microsoft/MSBuildLocator/tree/e0281df33274ac3c3e22acc9b07dcb4b31d57dc0)*
    - `src/source-build-externals/src/newtonsoft-json`  
    *[JamesNK/Newtonsoft.Json@0a2e291](https://github.com/JamesNK/Newtonsoft.Json/tree/0a2e291c0d9c0c7675d445703e51750363a549ef)*
    - `src/source-build-externals/src/spectre-console`  
    *[spectreconsole/spectre.console@7397169](https://github.com/spectreconsole/spectre.console/tree/7397169a2757dc3657598bdea4ac222c0f283425)*
    - `src/source-build-externals/src/xunit`  
    *[xunit/xunit@f110e5b](https://github.com/xunit/xunit/tree/f110e5bee5dfd4c08339587c9c3df9292fcb597c)*
    - `src/source-build-externals/src/xunit/src/xunit.assert/Asserts`  
    *[xunit/assert.xunit@5c8c10e](https://github.com/xunit/assert.xunit/tree/5c8c10e085eb42f39f2fe0b40c94bf56649eb0a4)*
    - `src/source-build-externals/src/xunit/tools/build`  
    *[xunit/build-tools@8e186b0](https://github.com/xunit/build-tools/tree/8e186b0f8e398796e75453f3f18952b06d29fdfd)*
    - `src/source-build-externals/src/xunit/tools/media`  
    *[xunit/media@5738b6e](https://github.com/xunit/media/tree/5738b6e86f08e0389c4392b939c20e3eca2d9822)*
- `src/source-build-reference-packages`  
*[dotnet/source-build-reference-packages@fd609e3](https://github.com/dotnet/source-build-reference-packages/tree/fd609e3b427601180d23633e2f1a4cdac6c42c20)*
- `src/sourcelink`  
*[dotnet/sourcelink@49b08e9](https://github.com/dotnet/sourcelink/tree/49b08e968e7e5edf282f068b4bce56b9909433b0)*
- `src/symreader`  
*[dotnet/symreader@0710a78](https://github.com/dotnet/symreader/tree/0710a7892d89999956e8808c28e9dd0512bd53f3)*
- `src/templating`  
*[dotnet/templating@bf40ec0](https://github.com/dotnet/templating/tree/bf40ec00f3761436f9e503691191ed722575f1bb)*
- `src/test-templates`  
*[dotnet/test-templates@9f1f944](https://github.com/dotnet/test-templates/tree/9f1f944a1aa00a016e69dc911aa12b24945fba79)*
- `src/vstest`  
*[microsoft/vstest@c8f1628](https://github.com/microsoft/vstest/tree/c8f1628bcfce589e3165e171ecf8a8ce5b3c688f)*
- `src/windowsdesktop`  
*[dotnet/windowsdesktop@b47b7ab](https://github.com/dotnet/windowsdesktop/tree/b47b7abac6a8237fee25b41ec7fd0f561efc6ad0)*
- `src/winforms`  
*[dotnet/winforms@ff838ac](https://github.com/dotnet/winforms/tree/ff838ac7ac134944d5519c3eedf7e689b2d9f2e5)*
- `src/wpf`  
*[dotnet/wpf@ce09c88](https://github.com/dotnet/wpf/tree/ce09c88c666057407b355ea86e1ad8713ac674a1)*
- `src/xdt`  
*[dotnet/xdt@4ddd811](https://github.com/dotnet/xdt/tree/4ddd8113a29852380b7b929117bfe67f401ac320)*
- `src/xliff-tasks`  
*[dotnet/xliff-tasks@73f0850](https://github.com/dotnet/xliff-tasks/tree/73f0850939d96131c28cf6ea6ee5aacb4da0083a)*
<!-- component list end -->

The repository also contains a [JSON manifest](https://github.com/dotnet/dotnet/blob/main/src/source-manifest.json) listing all components in a machine-readable format.
