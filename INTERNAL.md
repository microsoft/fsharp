# Links for internal team members to find build definitions, etc.

Note that usually only the most recent link in each section is interesting.  Older links are included for reference only.

## PR Build Definition

The PR build definition can be found [here](https://dev.azure.com/dnceng/public/_build?definitionId=496) or by
navigating through an existing PR.

There is also a duplicate scouting PR build that is identical to the normal PR build _except_ that it uses a different Windows
machine queue that always has the next preview build of Visual Studio installed.  This is to hopefully get ahead of any breaking
API changes.  That build definition is [here](https://dev.azure.com/dnceng/public/_build?definitionId=961).

## Signed Build Definitions

[VS 16.4 to current](https://dev.azure.com/dnceng/internal/_build?definitionId=499&_a=summary)

[VS 15.7 to 16.3](https://dev.azure.com/devdiv/DevDiv/_build/index?definitionId=8978)

[VS 15.6](https://dev.azure.com/devdiv/DevDiv/_build?definitionId=7239)

[VS 15.0 to 15.5](https://dev.azure.com/devdiv/DevDiv/_build?definitionId=5037)

## Branch auto-merge definitions

Branch auto-merge definitions are specified [here](https://github.com/dotnet/roslyn-tools/blob/master/src/GitHubCreateMergePRs/config.xml).

## VS Insertion Generators

VS 16.4 to current - part of the build definition.  [See below](#vs-insertions-as-part-of-the-build-definition).

The following insertion generators are automatically invoked upon successful completion of a signed build in each of
their respective branches.

[VS 16.3](https://dev.azure.com/devdiv/DevDiv/_release?definitionId=1839&_a=releases)

[VS 16.2](https://dev.azure.com/devdiv/DevDiv/_release?definitionId=1699&_a=releases)

[VS 16.1](https://dev.azure.com/devdiv/DevDiv/_release?definitionId=1669&_a=releases)

VS 16.0 and prior were done manually

## VS Insertions as part of the build definition

Starting with the 16.4 release and moving forwards, the VS insertion is generated as part of the build.  The relevant
bits can be found near the bottom of [`azure-pipelines.yml`](azure-pipelines.yml) under the `VS Insertion` header.  The
interesting parameters are `componentBranchName` and `insertTargetBranch`.  In short, when an internal signed build
completes and the name of the branch built exactly equals the value in the `componentBranchName` parameter, a component
insertion into VS will be created into the `insertTargetBranch` branch.  The link to the insertion PR will be found
near the bottom of the build under the title 'Insert into VS'.  Examine the log for 'Insert VS Payload' and near the
bottom you'll see a line that looks like `Created request #xxxxxx at https://...`.

Insertions generated to any `rel/*` branch will have to be manually verified and merged, and they'll be listed
[here](https://dev.azure.com/devdiv/DevDiv/_git/VS/pullrequests?createdBy=122d5278-3e55-4868-9d40-1e28c2515fc4&_a=active).
Note that insertions for other teams will also be listed.

Insertions to any other VS branch (e.g., `main`) will have the auto-merge flag set and should handle themselves, but
it's a good idea to check the previous link for any old or stalled insertions into VS `main`.

## Less interesting links

[FSharp.Core (Official NuGet Release)](https://dev.azure.com/dnceng/internal/_release?_a=releases&definitionId=72).
Uploads the final `FSharp.Core` package from the specified build to NuGet.  This should only be run when we know for
certain which build produced the final offical package.

[FSharp.Core (Preview NuGet Release)](https://dev.azure.com/dnceng/internal/_release?_a=releases&definitionId=92).
Uploads the preview `FSharp.Core.*-beta.*` package from the specified build to NuGet.  This should be run every time
a new SDK preview is released.

[FCS (Official NuGet Release)](https://dev.azure.com/dnceng/internal/_release?view=mine&_a=releases&definitionId=99).
Uploads the final `FSharp.Compiler.Service` package from the specified build to NuGet.  Only builds from the `release/fcs`
branch can be selected.  This should only be run when we're fairly certain that the package is complete.

[FCS (Preview NuGet Release)](https://dev.azure.com/dnceng/internal/_release?view=mine&_a=releases&definitionId=98).
Uploads the preview `FSharp.Compiler.Service.*-preview.*` package from the specified build to NuGet.  Only builds from the
`main` branch can be selected.  This can be run whenever we think we're ready to preview a new FCS build.

[Nightly VSIX (main) uploader](https://dev.azure.com/dnceng/internal/_release?_a=releases&definitionId=70).  Uploads
a package from every build of `main` to the [Nightly VSIX feed](README.md#using-nightly-releases-in-visual-studio).

[Nightly VSIX (preview) uploader](https://dev.azure.com/dnceng/internal/_release?_a=releases&definitionId=71).  Uploads
a package from every build of the branch that corresponds to the current Visual Studio preview to the
[Preview VSIX feed](README.md#using-nightly-releases-in-visual-studio).

[Internal source mirror](https://dev.azure.com/dnceng/internal/_git/dotnet-fsharp).
