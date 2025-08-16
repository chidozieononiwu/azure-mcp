# AOT Compatible Azure MCP Server

Azure MCP Server (`azmcp`) is designed to be fully compatible with .NET Native "Ahead Of Time" (AOT)   compilation, enabling faster startup times, reduced memory footprint, and elimination of .NET runtime dependencies. This AOT compatibility ensures that azmcp can be deployed as a self-contained native binary.

To maintain this compatibility, all code contributions and package dependencies must adhere to AOT-safe practices and pass our automated AOT compilation checks.

## CI AOT Gates

This section explains the Native AOT checks enforced in the `azmcp` CI and what partners need to do when a check fails.

### Overview

- When you open a PR, the CI pull request pipeline runs a **Native AOT compile** across all supported OS and architecture combinations.
- You can find this CI stage under the name **"(Native AOT) Build module"**.
- If this stage fails, it indicates potential **AOT violations** in:
  - Code you added to **azmcp**, or
  - A new package reference you introduced.

### What to do if it fails

1. **Run the build locally**

   Use:
   ```powershell
   ./eng/scripts/Build-Local.ps1 -BuildNative
   ```
   This will show you the AOT violations introduced by your changes.

2. **If violations come from an `Azure.ResourceManager.*` package**

   - No action is needed on your side to "fix" those violations, the Azure MCP Server core team will ensure we ship an AOT-fixed version of the `Azure.ResourceManager.*` package(s).
   - Add the ARM references under the `ItemGroup` with `'$(BuildNative)' == 'true'` condition in **AzureMcp.Cli.csproj**, [see](https://github.com/Azure/azure-mcp/blob/1b7006aa72db38ab17614911973b275c0f5dbfe9/core/src/AzureMcp.Cli/AzureMcp.Cli.csproj#L64).
   - Add the Area setup under a `!BUILD_NATIVE` conditional in **core/src/AzureMcp.Cli/Program.cs**, [see](https://github.com/Azure/azure-mcp/blob/1b7006aa72db38ab17614911973b275c0f5dbfe9/core/src/AzureMcp.Cli/Program.cs#L82).
   - Let reviewers know in the PR description and comment that the dependency is `Azure.ResourceManager.*`.

3. **If violations come from an external package**

   - Check if there is an **AOT-compatible** version of that package.
   - If not, look for an **alternative AOT-friendly** package that provides similar functionality.
   - If no AOT-safe option exists, we **cannot** merge your feature into `azmcp` with that package reference.

4. **If violations come from a library you own**

   - See the [Making Your Library AOT Compatible](#making-your-library-aot-compatible) section.

### Policy & Notes

- Do **not** add any external packages under the `'$(BuildNative)' == 'true'` ItemGroup in **AzureMcp.Cli.csproj**. This exception applies **only** to `Azure.ResourceManager.*` packages.
- Please call out any `Azure.ResourceManager.*` usage in your PR so reviewers can track it.

## Making Your Library AOT Compatible

To make your library AOT compatible, review the comprehensive guide: [Creating AOT Compatible Libraries](https://devblogs.microsoft.com/dotnet/creating-aot-compatible-libraries/)

   Key practices include:
   - Add `IsAotCompatible=true` to library's project file
   - Avoid reflection-based APIs
   - Use source generators instead of runtime code generation  
   - Mark AOT-incompatible APIs with `RequiresUnreferencedCode` or `RequiresDynamicCode` attributes
   - Test your library with AOT compilation