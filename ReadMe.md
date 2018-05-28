## ResolveUR - Resolve Unused References
- 3.4: Added VS2017 install paths for latest MSBuild support
- 3.3: Fixed regression bug, project references not being cleaned.
- 3.2: VS 2017 release, bug fix for no package config case.

Update 4/2/2017: Visual studio extension is published at [Visual Studio Gallery](https://marketplace.visualstudio.com/items?itemName=battas.ResolveUR-ResolveUnusedReferences)

### What's new with 3.1
Having non-destructive edits to projects being resolved for unused references is now possible. 
- When a project's references are resolved, a text editor is provided which shows all references that are about to be removed on each line.
- User can exclude zero or all references by typing '#' at the beginning of the line. These ignored references are saved per project in a .refsignored file. Future attempts to clean will use this file to continue the same exclusions. *Note:* Please ensure to add refsignore to git ignore list so you don't get annoyed by this extra file.
- After user decides which references to remove, a final confirmation for removal is followed by actual removal of references.
- Nuget packages that are marked as development dependencies are excluded from removal.

### Introduction
Resolves project references for a Visual Studio Solution by <b>removing unused references</b> in each project of the solution.

This is done by removing a reference in a project, building project for errors and restoring removed reference if there were build errors.

Tested for few project types including console, windows, unit test and website project types.

Checks for MSBuild on local system in predetermined paths specified in app.config. In console app, platform to build against can be specified.

### Usage at command line

With just path, looks for x64 .net v4.0 msbuild, then x64 v3.5, x64 v2.0, x86 v4.0, x86 v3.5, x86 v2.0

Note: Path or platform arguments are without brackets

    ResolveUR.exe [Path of solution file]

If nuget packages are also to be resolved, add true/false next to path

    ResolveUR.exe [Path of solution file] [true/false]

With platform also specified, x86 for example looks only x86 .net MSBuild versions, highest first

    ResolveUR.exe [Path of solution file] [true/false] [platform]

### Change and Version History
- 4/2/2017  - v3.1 - Added Nuget packages marked as development dependency to be excluded
- 4/2/2017  - v3.0 - Updated with a preview mode allowing exclusion and final confirmation before removal of references
- 8/23/2014 - v2.2 - Make package solution optional since getting it right is much more involved, better left to developer discretion at this point. Plan to add a package developer edited exclusion list in future.
- 8/22/2014 - v2.1 - fixed regression bug. Resolution continued in spit of build errors
- 8/15/2014 - v2.0 - Remove NuGet package references as well as folders. v2.0
- 8/5/2014 - v1.3 - Fixed couple of bugs and permissions issue
- 8/2/2014 - Added setup project to install console app
- 8/1/2014 - v1.0 - VSIX project added and extension published
