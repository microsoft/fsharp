Configuration ?= release
DotNetVersion = `cat DotnetCLIToolsVersion.txt`
DotNetToolPath = $(CURDIR)/artifacts/toolset/dotnet
DotNetExe = "$(DotNetToolPath)/dotnet"

all: proto restore build test

tools:
	$(CURDIR)/scripts/dotnet-install.sh --version $(DotNetVersion) --install-dir "$(DotNetToolPath)"

proto: tools
	$(DotNetExe) build-server shutdown
	$(DotNetExe) restore src/buildtools/buildtools.proj
	$(DotNetExe) restore src/fsharp/FSharp.Build/FSharp.Build.fsproj
	$(DotNetExe) restore src/fsharp/fsc/fsc.fsproj
	$(DotNetExe) build src/buildtools/buildtools.proj -c Proto
	$(DotNetExe) build src/fsharp/FSharp.Build/FSharp.Build.fsproj -c Proto
	$(DotNetExe) build src/fsharp/fsc/fsc.fsproj -v detailed -c Proto

restore:
	$(DotNetExe) restore src/fsharp/FSharp.Core/FSharp.Core.fsproj
	$(DotNetExe) restore src/fsharp/FSharp.Build/FSharp.Build.fsproj
	$(DotNetExe) restore src/fsharp/FSharp.Compiler.Private/FSharp.Compiler.Private.fsproj
	$(DotNetExe) restore src/fsharp/fsc/fsc.fsproj
	$(DotNetExe) restore src/fsharp/FSharp.Compiler.Interactive.Settings/FSharp.Compiler.Interactive.Settings.fsproj
	$(DotNetExe) restore src/fsharp/fsi/fsi.fsproj
	$(DotNetExe) restore tests/FSharp.Core.UnitTests/FSharp.Core.UnitTests.fsproj
	$(DotNetExe) restore tests/FSharp.Build.UnitTests/FSharp.Build.UnitTests.fsproj

build: proto restore
	$(DotNetExe) build-server shutdown
	$(DotNetExe) build -c $(Configuration) src/fsharp/FSharp.Core/FSharp.Core.fsproj
	$(DotNetExe) build -c $(Configuration) src/fsharp/FSharp.Build/FSharp.Build.fsproj
	$(DotNetExe) build -c $(Configuration) src/fsharp/FSharp.Compiler.Private/FSharp.Compiler.Private.fsproj
	$(DotNetExe) build -c $(Configuration) src/fsharp/fsc/fsc.fsproj
	$(DotNetExe) build -c $(Configuration) src/fsharp/FSharp.Compiler.Interactive.Settings/FSharp.Compiler.Interactive.Settings.fsproj
	$(DotNetExe) build -c $(Configuration) src/fsharp/fsi/fsi.fsproj
	$(DotNetExe) build -c $(Configuration) tests/FSharp.Core.UnitTests/FSharp.Core.UnitTests.fsproj
	$(DotNetExe) build -c $(Configuration) tests/FSharp.Build.UnitTests/FSharp.Build.UnitTests.fsproj

test: build
	$(DotNetExe) test -c $(Configuration) -v detailed --no-restore --no-build tests/FSharp.Core.UnitTests/FSharp.Core.UnitTests.fsproj -l "trx;LogFileName=$(CURDIR)/tests/TestResults/FSharp.Core.UnitTests.trx"
	$(DotNetExe) test -c $(Configuration) -v detailed --no-restore --no-build tests/FSharp.Build.UnitTests/FSharp.Build.UnitTests.fsproj -l "trx;LogFileName=$(CURDIR)/tests/TestResults/FSharp.Build.UnitTests.trx"

clean:
	rm -rf $(CURDIR)/artifacts
