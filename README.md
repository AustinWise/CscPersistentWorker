# CSC Persistent Worker Support

Bazel has the concept of [Persistent Workers](https://bazel.build/remote/persistent).
This is a compiler that supports a client/server model of interaction. The client
requests some files to be compiled and server replies with a response. This type
of system is useful when the server benefits from "warming up" its just-in-time
compilation or other caches. Additionally on platforms like Windows
where creating a process is expensive, it is profitable to avoid creating a new
process.

The C# compiler has
[support for this mode of operation](https://github.com/dotnet/roslyn/blob/main/docs/compilers/Compiler%20Server.md).
MSBuild, the .NET build system, will create a `VBCSCompiler.exe` server to
handle all compilation requests (intead of `exec`ing a `csc.exe` process for every compilation request).
The C# compiler, `csc` also supports a
`/shared` flag that will attempt to start the compiler server and use it for
the compilation.

## How to support persistent workers in Bazel

There are 3 obvious paths forward for adding support for persistent workers to
rules_dotnet:

1. Create a wrapper that just calls `csc.exe` with the `/shared` argument.
2. Write persistent worker that knows how to start `VBCSCompiler.exe`, based on
   on the open source code in the Roslyn repo.
3. Request that the upstream `csc.exe` add support for the `--persistent_worker`
   flag.

The first one is simple enough to implement and has some performance benefits. This repo implements
that approach.

The second and third options would have more performance benefits, as they would not need to `exec`
any process to handle a build request. Option number 2 would be more involved, as the build
server protocol is not officially documented or exposed as an API. It is an implementation detail
of `csc.exe`. It goes as far as to
[embed the Git commit hash of the complier into the protocol](https://github.com/dotnet/roslyn/blob/70514c118e2ce45e3b6684d69c7563ac9e2b6f58/src/Compilers/Core/CommandLine/BuildProtocol.cs#L38).

## Other levels of build servers

The `dotnet build` command has a similar
[MSBuild server feature](https://learn.microsoft.com/en-us/visualstudio/msbuild/msbuild-server).
Note that this is distinct from the msbuild `-nodeReuse` feature.
