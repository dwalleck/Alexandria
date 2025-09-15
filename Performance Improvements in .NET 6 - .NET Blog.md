Four years ago, around the time .NET Core 2.0 was being released, I wrote [Performance Improvements in .NET Core](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-core/) to highlight the quantity and quality of performance improvements finding their way into .NET. With its very positive reception, I did so again a year later with [Performance Improvements in .NET Core 2.1](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-core-2-1/), and an annual tradition was born. Then came [Performance Improvements in .NET Core 3.0](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-core-3-0/), followed by [Performance Improvements in .NET 5](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-5/). Which brings us to today.

The [dotnet/runtime](https://github.com/dotnet/runtime) repository is the home of .NET’s runtimes, runtime hosts, and core libraries. Since its main branch forked a year or so ago to be for .NET 6, there have been over 6500 merged PRs (pull requests) into the branch for the release, and that’s excluding automated PRs from bots that do things like flow dependency version updates between repos (not to discount the bots’ contributions; after all, they’ve actually received interview offers by email from recruiters who just possibly weren’t being particularly discerning with their candidate pool). I at least peruse if not review in depth the vast majority of all those PRs, and every time I see a PR that is likely to impact performance, I make a note of it in a running log, giving me a long list of improvements I can revisit when it’s blog time. That made this August a little daunting, as I sat down to write this post and was faced with the list I’d curated of almost 550 PRs. Don’t worry, I don’t cover all of them here, but grab a large mug of your favorite hot beverage, and settle in: this post takes a rip-roarin’ tour through ~400 PRs that, all together, significantly improve .NET performance for .NET 6.

Please enjoy!

### Table Of Contents

-   [Benchmarking Setup](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#benchmarking-setup)
-   [JIT](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#jit)
-   [GC](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#gc)
-   [Threading](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#threading)
-   [System Types](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#system-types)
-   [Arrays, Strings, Spans](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#arrays-strings-spans)
-   [Buffering](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#buffering)
-   [IO](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#io)
-   [Networking](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#networking)
-   [Reflection](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#reflection)
-   [Collections and LINQ](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#collections-and-linq)
-   [Cryptography](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#cryptography)
-   [“Peanut Butter”](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#peanut-butter)
-   [JSON](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#json)
-   [Interop](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#interop)
-   [Startup](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#startup)
-   [Tracing](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#tracing)
-   [Size](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#size)
-   [Blazor and mono](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#blazor-and-mono)
-   [Is that all?](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-6/#is-that-all)

### Benchmarking Setup

As in previous posts, I’m using [BenchmarkDotNet](https://github.com/dotnet/benchmarkdotnet) for the majority of the examples throughout. To get started, I created a new console application:

```
dotnet new console -o net6perf
cd net6perf
```

and added a reference to the [BenchmarkDotNet nuget package](https://www.nuget.org/packages/BenchmarkDotNet/):

```
dotnet add package benchmarkdotnet
```

That yielded a net6perf.csproj, which I then overwrote with the following contents; most importantly, this includes multiple target frameworks so that I can use BenchmarkDotNet to easily compare performance on them:

```
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFrameworks>net48;netcoreapp2.1;netcoreapp3.1;net5.0;net6.0</TargetFrameworks>
    <Nullable>annotations</Nullable>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <LangVersion>10</LangVersion>
    <ServerGarbageCollection>true</ServerGarbageCollection>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="benchmarkdotnet" Version="0.13.1" />
  </ItemGroup>

  <ItemGroup Condition=" '$(TargetFramework)' == 'net48' ">
    <Reference Include="System.Net.Http" />
  </ItemGroup>

</Project>
```

I then updated the generated Program.cs to contain the following boilerplate code:

```
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Order;
using Perfolizer.Horology;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Net.WebSockets;
using System.Numerics;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.IO.Compression;
#if NETCOREAPP3_0_OR_GREATER
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
#endif

[DisassemblyDiagnoser(maxDepth: 1)] // change to 0 for just the [Benchmark] method
[MemoryDiagnoser(displayGenColumns: false)]
public class Program
{
    public static void Main(string[] args) =>
        BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, DefaultConfig.Instance
            //.WithSummaryStyle(new SummaryStyle(CultureInfo.InvariantCulture, printUnitsInHeader: false, SizeUnit.B, TimeUnit.Microsecond))
            );

    // BENCHMARKS GO HERE
}
```

With minimal friction, you should be able to copy and paste a benchmark from this post to where it says `// BENCHMARKS GO HERE`, and run the app to execute the benchmarks. You can do so with a command like this:

```
dotnet run -c Release -f net48 -filter "**" --runtimes net48 net5.0 net6.0
```

This tells BenchmarkDotNet:

-   Build everything in a release configuration,
-   build it targeting the .NET Framework 4.8 surface area,
-   don’t exclude any benchmarks,
-   and run each benchmark on each of .NET Framework 4.8, .NET 5, and .NET 6.

In some cases, I’ve added additional frameworks to the list (e.g. `netcoreapp3.1`) to highlight cases where there’s a continuous improvement release-over-release. In other cases, I’ve only targeted .NET 6.0, such as when highlighting the difference between an existing API and a new one in this release. Most of the results in the post were generated by running on Windows, primarily so that .NET Framework 4.8 could be included in the result set. However, unless otherwise called out, all of these benchmarks show comparable improvements when run on Linux or on macOS. Simply ensure that you have installed each runtime you want to measure. I’m using a [nightly build of .NET 6 RC1](https://github.com/dotnet/installer/blob/main/README.md#installers-and-binaries), along with the latest [released downloads](https://dotnet.microsoft.com/download) of .NET 5 and .NET Core 3.1.

Final note and standard disclaimer: microbenchmarking can be very subject to the machine on which a test is run, what else is going on with that machine at the same time, and sometimes seemingly the way the wind is blowing. Your results may vary.

Let’s get started…

### JIT

Code generation is the foundation on top of which everything else is built. As such, improvements to code generation have a multiplicative effect, with the power to improve the performance of all code that runs on the platform. .NET 6 sees an unbelievable number of performance improvements finding their way into the JIT (just-in-time compiler), which is used to translate IL (intermediate language) into assembly code at run-time, and which is also used for AOT (ahead-of-time compilation) as part of [Crossgen2](https://devblogs.microsoft.com/dotnet/conversation-about-crossgen2/) and the [R2R format (ReadyToRun)](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/botr/readytorun-overview.md).

Since it’s so foundational to good performance in .NET code, let’s start by talking about inlining and devirtualization. “Inlining” is the process by which the compiler takes the code from a method callee and emits it directly into the caller. This avoids the overhead of the method call, but that’s typically only a minor benefit. The major benefit is it exposes the contents of the callee to the context of the caller, enabling subsequent (“knock-on”) optimizations that wouldn’t have been possible without the inlining. Consider a simple case:

```
[MethodImpl(MethodImplOptions.NoInlining)]
public static int Compute() => ComputeValue(123) * 11;

[MethodImpl(MethodImplOptions.NoInlining)]
private static int ComputeValue(int length) => length * 7;
```

Here we have a method, `ComputeValue`, which just takes an `int` and multiplies it by 7, returning the result. This method is simple enough to always be inlined, so for demonstration purposes I’ve used `MethodImplOptions.NoInlining` to tell the JIT to not inline it. If I then look at what assembly code the JIT produces for `Compute` and `ComputeValue`, we get something like this:

```
; Program.Compute()
       sub       rsp,28
       mov       ecx,7B
       call      Program.ComputeValue(Int32)
       imul      eax,0B
       add       rsp,28
       ret

; Program.ComputeValue(Int32)
       imul      eax,ecx,7
       ret
```

`Compute` loads the value `123` (`0x7b` in hex) into the `ecx` register, which holds the argument to `ComputeValue`, calls `ComputeValue`, then takes the result (from the `eax` register) and multiples it by `11` (`0xb` in hex), returning that result. We can see `ComputeValue` in turn takes the input from `ecx` and multiplies it by `7`, storing the result into `eax` for `Compute` to consume. Now, what happens if we remove the `NoInlining`:

```
; Program.Compute()
       mov       eax,24FF
       ret
```

The multiplications and method calls have evaporated, and we’re left with `Compute` simply returning the value `0x24ff`, as the JIT has computed at compile-time the result of `(123 * 7) * 11`, which is `9471`, or `0x24ff` in hex. In other words, we didn’t just save the method call, we also transformed the entire operation into a constant. Inlining is a very powerful optimization.

Of course, you also need to be careful with inlining. If you inline too much, you bloat the code in your methods, potentially very significantly. That can make microbenchmarks look very good in some circumstances, but it can also have some bad net effects. Let’s say all of the code associated with `Int32.Parse` is 1,000 bytes of assembly code (I’m making up that number for explanatory purposes), and let’s say we forced it to all always inline. Every call site to `Int32.Parse` will now end up carrying a (potentially optimized with knock-on effects) copy of the code; call it from 100 different locations, and you now have 100,000 bytes of assembly code rather than 1,000 that are reused. That means more memory consumption for the assembly code, and if it was AOT-compiled, more size on disk. But it also has other potentially deleterious affects. Computers use very fast and limited size instruction caches to store code to be run. If you have 1000 bytes of code that you invoke from 100 different places, each of those places can potentially reuse the bytes previously loaded into the cache. But give each of those places their own (likely mutated) copy, and as far as the hardware is concerned, that’s different code, meaning the inlining can result in code actually running slower due to forcing more evictions and loads from and to that cache. There’s also the impact on the JIT compiler itself, as the JIT has limits on things like the size of a method before it’ll give up on optimizing further; inline too much code, and you can exceed said limits.

Net net, inlining is hugely powerful, but also something to be employed carefully, and the JIT methodically (but necessarily quickly) weighs decisions it makes about what to inline and what not to with a variety of heuristics.

In this light, [dotnet/runtime#50675](https://github.com/dotnet/runtime/pull/50675), [dotnet/runtime#51124](https://github.com/dotnet/runtime/pull/51124), [dotnet/runtime#52708](https://github.com/dotnet/runtime/pull/52708), [dotnet/runtime#53670](https://github.com/dotnet/runtime/pull/53670), and [dotnet/runtime#55478](https://github.com/dotnet/runtime/pull/55478) improved the JIT by helping it to understand (and more efficiently understand) what methods were being invoked by the callee; by teaching the inliner about new things to look for, e.g. whether the callee could benefit from folding if handed constants; and by teaching the inliner how to inline various constructs it previously considered off-limits, e.g. switches. Let’s take just one example from a comment on one of those PRs:

```
private int _value = 12345;
private byte[] _buffer = new byte[100];

[Benchmark]
public bool Format() => Utf8Formatter.TryFormat(_value, _buffer, out _, new StandardFormat('D', 2));
```

Running this for .NET 5 vs .NET 6, we can see a few things changed:

| Method | Runtime | Mean | Ratio | Code Size |
| --- | --- | --- | --- | --- |
| Format | .NET 5.0 | 13.21 ns | 1.00 | 1,649 B |
| Format | .NET 6.0 | 10.37 ns | 0.78 | 590 B |

First, it got faster, yet there was little-to-no work done within `Utf8Formatter` itself in .NET 6 to improve the performance of this benchmark. Second, the code size (which is emitted thanks to using the `[DisassemblyDiagnoser]` attribute in our `Program.cs`) was cut to 35% of what it was in .NET 5. How is that possible? In both versions, the employed `TryFormat` call is a one-liner that delegates to a [private `TryFormatInt64` method](https://github.com/dotnet/runtime/blob/d019e70d2b7c2f7cd1137fac084dbcdc3d2e05f5/src/libraries/System.Private.CoreLib/src/System/Buffers/Text/Utf8Formatter/Utf8Formatter.Integer.Signed.cs#L16-L17), and the developer of that method decided to annotate it with `MethodImplOptions.AggressiveInlining`, which tells the JIT to override its heuristics and inline the method if it’s possible rather than if it’s possible and deemed useful. That method is a switch on the input `format.Symbol`, branching to call various other methods based on the format symbol employed (e.g. ‘D’ vs ‘G’ vs ‘N’). But we’ve actually already passed by the most interesting part, the `new StandardFormat('D', 2)` at the call site. In .NET 5, the JIT deems it not worthwhile to inline the `StandardFormat` constructor, and so we end up with a call to it:

```
       mov       edx,44
       mov       r8d,2
       call      System.Buffers.StandardFormat..ctor(Char, Byte)
```

As a result, even though `TryFormat` gets inlined, in .NET 5, the JIT is unable to connect the dots to see that the `'D'` passed into the `StandardFormat` constructor will influence which branch of that switch statement in `TryFormatInt64` gets taken. In .NET 6, the JIT does inline the `StandardFormat` constructor, the effect of which is that it effectively can shrink the contents of `TryFormatInt64` from:

```
if (format.IsDefault)
    return TryFormatInt64Default(value, destination, out bytesWritten);

switch (format.Symbol)
{
    case 'G':
    case 'g':
        if (format.HasPrecision)
            throw new NotSupportedException(SR.Argument_GWithPrecisionNotSupported);
        return TryFormatInt64D(value, format.Precision, destination, out bytesWritten);

    case 'd':
    case 'D':
        return TryFormatInt64D(value, format.Precision, destination, out bytesWritten);

    case 'n':
    case 'N':
        return TryFormatInt64N(value, format.Precision, destination, out bytesWritten);

    case 'x':
        return TryFormatUInt64X((ulong)value & mask, format.Precision, true, destination, out bytesWritten);

    case 'X':
        return TryFormatUInt64X((ulong)value & mask, format.Precision, false, destination, out bytesWritten);

    default:
        return FormattingHelpers.TryFormatThrowFormatException(out bytesWritten);
}
```

to the equivalent of just:

```
TryFormatInt64D(value, 2, destination, out bytesWritten);
```

avoiding the extra branches and not needing to inline the second copy of `TryFormatInt64D` (for the `'G'` case) or `TryFormatInt64N`, both which are `AggressiveInlining`.

Inlining also goes hand-in-hand with devirtualization, which is the act in which the JIT takes a virtual or interface method call, determines statically the actual end target of the invocation, and emits a direct call to that target, saving on the cost of the virtual dispatch. Once devirtualized, the target may also be inlined (subject to all of the same rules and heuristics), in which case it can avoid not only the virtual dispatch overhead, but also potentially benefit from the further optimizations inlining can enable. For example, consider a function like the following, which you might find in a collection implementation:

```
private int[] _values = Enumerable.Range(0, 100_000).ToArray();

[Benchmark]
public int Find() => Find(_values, 99_999);

private static int Find<T>(T[] array, T item)
{
    for (int i = 0; i < array.Length; i++)
        if (EqualityComparer<T>.Default.Equals(array[i], item))
            return i;

    return -1;
}
```

A previous release of .NET Core taught the JIT how to devirtualize `EqualityComparer<T>.Default` in such a use, resulting in an ~2x improvement over .NET Framework 4.8 in this example.

| Method | Runtime | Mean | Ratio | Code Size |
| --- | --- | --- | --- | --- |
| Find | .NET Framework 4.8 | 115.4 us | 1.00 | 127 B |
| Find | .NET Core 3.1 | 69.7 us | 0.60 | 71 B |
| Find | .NET 5.0 | 69.8 us | 0.60 | 63 B |
| Find | .NET 6.0 | 53.4 us | 0.46 | 57 B |

However, while the JIT has been able to devirtualize `EqualityComparer<T>.Default.Equals` (for value types), not so for its sibling `Comparer<T>.Default.Compare`. [dotnet/runtime#48160](https://github.com/dotnet/runtime/pull/48160) addresses that. This can be seen with a benchmark like the following, which compares `ValueTuple` instances (the `ValueTuple<>`.`CompareTo` method uses `Comparer<T>.Default` to compare each element of the tuple):

```
private (int, long, int, long) _value1 = (5, 10, 15, 20);
private (int, long, int, long) _value2 = (5, 10, 15, 20);

[Benchmark]
public int Compare() => _value1.CompareTo(_value2);
```

| Method | Runtime | Mean | Ratio | Code Size |
| --- | --- | --- | --- | --- |
| Compare | .NET Framework 4.8 | 17.467 ns | 1.00 | 240 B |
| Compare | .NET 5.0 | 9.193 ns | 0.53 | 209 B |
| Compare | .NET 6.0 | 2.533 ns | 0.15 | 186 B |

But devirtualization improvements have gone well beyond such known intrinsic methods. Consider this microbenchmark:

```
[Benchmark]
public int GetLength() => ((ITuple)(5, 6, 7)).Length;
```

The fact that I’m using a `ValueTuple'3` and the `ITuple` interface here doesn’t matter: I just selected an arbitrary value type that implements an interface. A previous release of .NET Core enabled the JIT to avoid the boxing operation here (from casting a value type to an interface it implements) and emit this purely as a constrained method call, and then a subsequent release enabled it to be devirtualized and inlined:

| Method | Runtime | Mean | Ratio | Code Size | Allocated |
| --- | --- | --- | --- | --- | --- |
| GetLength | .NET Framework 4.8 | 6.3495 ns | 1.000 | 106 B | 32 B |
| GetLength | .NET Core 3.1 | 4.0185 ns | 0.628 | 66 B | – |
| GetLength | .NET 5.0 | 0.1223 ns | 0.019 | 27 B | – |
| GetLength | .NET 6.0 | 0.0204 ns | 0.003 | 27 B | – |

Great. But now let’s make a small tweak:

```
[Benchmark]
public int GetLength()
{
    ITuple t = (5, 6, 7);
    Ignore(t);
    return t.Length;
}

[MethodImpl(MethodImplOptions.NoInlining)]
private static void Ignore(object o) { }
```

Here I’ve forced the boxing by needing the object to exist in order to call the `Ignore` method, and previously that was enough to disable the ability to devirtualize the `t.Length` call. But .NET 6 now “gets it.” We can also see this by looking at the assembly. Here’s what we get for .NET 5:

```
; Program.GetLength()
       push      rsi
       sub       rsp,30
       vzeroupper
       vxorps    xmm0,xmm0,xmm0
       vmovdqu   xmmword ptr [rsp+20],xmm0
       mov       dword ptr [rsp+20],5
       mov       dword ptr [rsp+24],6
       mov       dword ptr [rsp+28],7
       mov       rcx,offset MT_System.ValueTuple~3[[System.Int32, System.Private.CoreLib],[System.Int32, System.Private.CoreLib],[System.Int32, System.Private.CoreLib]]
       call      CORINFO_HELP_NEWSFAST
       mov       rsi,rax
       vmovdqu   xmm0,xmmword ptr [rsp+20]
       vmovdqu   xmmword ptr [rsi+8],xmm0
       mov       rcx,rsi
       call      Program.Ignore(System.Object)
       mov       rcx,rsi
       add       rsp,30
       pop       rsi
       jmp       near ptr System.ValueTuple~3[[System.Int32, System.Private.CoreLib],[System.Int32, System.Private.CoreLib],[System.Int32, System.Private.CoreLib]].System.Runtime.CompilerServices.ITuple.get_Length()
; Total bytes of code 92
```

and for .NET 6:

```
; Program.GetLength()
       push      rsi
       sub       rsp,30
       vzeroupper
       vxorps    xmm0,xmm0,xmm0
       vmovupd   [rsp+20],xmm0
       mov       dword ptr [rsp+20],5
       mov       dword ptr [rsp+24],6
       mov       dword ptr [rsp+28],7
       mov       rcx,offset MT_System.ValueTuple~3[[System.Int32, System.Private.CoreLib],[System.Int32, System.Private.CoreLib],[System.Int32, System.Private.CoreLib]]
       call      CORINFO_HELP_NEWSFAST
       mov       rcx,rax
       lea       rsi,[rcx+8]
       vmovupd   xmm0,[rsp+20]
       vmovupd   [rsi],xmm0
       call      Program.Ignore(System.Object)
       cmp       [rsi],esi
       mov       eax,3
       add       rsp,30
       pop       rsi
       ret
; Total bytes of code 92
```

Note in .NET 5 it’s tail calling to the interface implementation (jumping to the target method at the end rather than making a call that will need to return back to this method):

```
       jmp       near ptr System.ValueTuple~3[[System.Int32, System.Private.CoreLib],[System.Int32, System.Private.CoreLib],[System.Int32, System.Private.CoreLib]].System.Runtime.CompilerServices.ITuple.get_Length()
```

whereas in .NET 6 it’s not only devirtualized but also inlined the `ITuple.Length` call, with the assembly now limited to moving the answer (`3`) into the return register:

```
       mov       eax,3
```

Nice.

A multitude of other changes have impacted devirtualization as well. For example, [dotnet/runtime#53567](https://github.com/dotnet/runtime/pull/53567) improves devirtualization in AOT ReadyToRun images, and [dotnet/runtime#45526](https://github.com/dotnet/runtime/pull/45526) improves devirtualization with generics such that information about the exact class obtained is then made available to improve inlining.

Of course, there are many situations in which it’s impossible for the JIT to statically determine the exact target for a method call, thus preventing devirtualization and inlining… or does it?

One of my favorite features of .NET 6 is PGO (profile-guided optimization). PGO as a concept isn’t new; it’s been implemented in a variety of development stacks, and has existed in .NET in multiple forms over the years. But the implementation in .NET 6 is something special when compared to previous releases; in particular, from my perspective, “dynamic PGO”. The general idea behind profile-guided optimization is that a developer can first compile their app, using special tooling that instruments the binary to track various pieces of interesting data. They can then run their instrumented application through typical use, and the resulting data from the instrumentation can then be fed back into the compiler the next time around to influence how the compiler compiles the code. The interesting statement there is “next time”. Traditionally, you’d build your app, run the data gathering process, and then rebuild the app feeding in the resulting data, and typically this would all be automated as part of a build pipeline; that process is referred to as “static PGO”. However, with tiered compilation, a whole new world is available.

“Tiered compilation” is enabled by default since .NET Core 3.0. For JIT’d code, it represents a compromise between getting going quickly and running with highly-optimized code. Code starts in “tier 0,” during which the JIT compiler applies very few optimizations, which also means the JIT compiles code very quickly (optimizations are often what end up taking the most time during compilation). The emitted code includes some tracking data to count how frequently methods are invoked, and once members pass a certain threshold, the JIT queues them to be recompiled in “tier 1,” this time with all the optimizations the JIT can muster, and learning from the previous compilation, e.g. an accessed `static readonly int` can become a constant, as its value will have already been computed by the time the tier 1 code is compiled ([dotnet/runtime#45901](https://github.com/dotnet/runtime/pull/45901) improves the aforementioned queueing, using a dedicated thread rather than using the thread pool). You can see where this is going. With “dynamic PGO,” the JIT can now do further instrumentation during tier 0, to track not just call counts but all of the interesting data it can use for profile-guided optimization, and then it can employ that during the compilation of tier 1.

In .NET 6, dynamic PGO is off by default. To enable it, you need to set the `DOTNET_TieredPGO` environment variable:

```
# with bash
export DOTNET_TieredPGO=1

# in cmd
set DOTNET_TieredPGO=1

# with PowerShell
$env:DOTNET_TieredPGO="1"
```

That enables gathering all of the interesting data during tier 0. On top of that, there are some other environment variables you’ll also want to consider setting. Note that the core libraries that make up .NET are installed with ReadyToRun images, which means they’ve essentially already been compiled into assembly code. ReadyToRun images can participate in tiering, but they don’t go through a tier 0, rather they go straight from the ReadyToRun code to tier 1; that means there’s no opportunity for dynamic PGO to instrument the binary for dynamically gathering insights. To enable instrumenting the core libraries as well, you can disable ReadyToRun:

```
$env:DOTNET_ReadyToRun="0"
```

Then the core libraries will also participate. Finally, you can consider setting `DOTNET_TC_QuickJitForLoops`:

```
$env:DOTNET_TC_QuickJitForLoops="1"
```

which enables tiering for methods that contain loops: otherwise, anything that has a backward jump goes straight to tier 1, meaning it gets optimized immediately as if tiered compilation didn’t exist, but in doing so loses out on the benefits of first going through tier 0. You may hear folks working on .NET referring to “full PGO”: that’s the case of all three of these environment variables being set, as then everything in the app is utilizing “dynamic PGO”. (Note that the ReadyToRun code for the framework assemblies does include implementations optimized based on PGO, just “static PGO”. The framework assemblies are compiled with PGO, used to execute a stable of representative apps and services, and then the resulting data is used to generate the final code that’s part of the shipped assemblies.)

Enough setup… what does this do for us? Let’s take an example:

```
private IEnumerator<int> _source = Enumerable.Range(0, int.MaxValue).GetEnumerator();

[Benchmark]
public void MoveNext() => _source.MoveNext();
```

This is a pretty simple benchmark: we have an `IEnumerator<int>` stored in a field, and our benchmark is simply moving the iterator forward. When compiled on .NET 6 normally, we get this:

```
; Program.MoveNext()
       sub       rsp,28
       mov       rcx,[rcx+8]
       mov       r11,7FFF8BB40378
       call      qword ptr [7FFF8BEB0378]
       nop
       add       rsp,28
       ret
```

That assembly code is the interface dispatch to whatever implementation backs that `IEnumerator<int>`. Now let’s set:

```
$env:DOTNET_TieredPGO=1
```

and try it again. This time, the code looks very different:

```
; Program.MoveNext()
       sub       rsp,28
       mov       rcx,[rcx+8]
       mov       r11,offset MT_System.Linq.Enumerable+RangeIterator
       cmp       [rcx],r11
       jne       short M00_L03
       mov       r11d,[rcx+0C]
       cmp       r11d,1
       je        short M00_L00
       cmp       r11d,2
       jne       short M00_L01
       mov       r11d,[rcx+10]
       inc       r11d
       mov       [rcx+10],r11d
       cmp       r11d,[rcx+18]
       je        short M00_L01
       jmp       short M00_L02
M00_L00:
       mov       r11d,[rcx+14]
       mov       [rcx+10],r11d
       mov       dword ptr [rcx+0C],2
       jmp       short M00_L02
M00_L01:
       mov       dword ptr [rcx+0C],0FFFFFFFF
M00_L02:
       add       rsp,28
       ret
M00_L03:
       mov       r11,7FFF8BB50378
       call      qword ptr [7FFF8BEB0378]
       jmp       short M00_L02
```

A few things to notice, beyond it being much longer. First, the `mov r11,7FFF8BB40378` followed by `call qword ptr [7FFF8BEB0378]` sequence for doing the interface dispatch still exists here, but it’s at the end of the method. One optimization common in PGO implementations is “hot/cold splitting”, where sections of a method frequently executed (“hot”) are moved close together at the beginning of the method, and sections of a method infrequently executed (“cold”) are moved to the end of the method. That enables better use of instruction caches and minimizes loads necessary to bring in likely-unsed code. So, this interface dispatch has moved to the end of the method, as based on PGO data the JIT expects it to be cold / rarely invoked. Yet this is the entirety of the original implementation; if that’s cold, what’s hot? Now at the beginning of the method, we see:

```
       mov       rcx,[rcx+8]
       mov       r11,offset MT_System.Linq.Enumerable+RangeIterator
       cmp       [rcx],r11
       jne       short M00_L03
```

This is the magic. When the JIT instrumented the tier 0 code for this method, that included instrumenting this interface dispatch to track the concrete type of `_source` on each invocation. And the JIT found that every invocation was on a type called `Enumerable+RangeIterator`, which is a [private class](https://github.com/dotnet/runtime/blob/d019e70d2b7c2f7cd1137fac084dbcdc3d2e05f5/src/libraries/System.Linq/src/System/Linq/Range.cs#L31) used to implement `Enumerable.Range` inside of the `Enumerable` implementation. As such, for tier 1 the JIT has emitted a check to see whether the type of `_source` is that `Enumerable+RangeIterator`: if it isn’t, then it jumps to the cold section we previously highlighted that’s performing the normal interface dispatch. But if it is, which, based on the profiling data, is expected to be the case the vast majority of the time, it can then proceed to directly invoke the `Enumerable+RangeIterator.MoveNext` method, devirtualized. Not only that, but it decided it was profitable to inline that `MoveNext` method. That [`MoveNext`](https://github.com/dotnet/runtime/blob/d019e70d2b7c2f7cd1137fac084dbcdc3d2e05f5/src/libraries/System.Linq/src/System/Linq/Range.cs#L47-L67) implementation is then the assembly code that immediately follows. The net effect of this is a bit larger code, but optimized for the exact scenario expected to be most common:

| Method | Mean | Code Size |
| --- | --- | --- |
| PGO Disabled | 1.905 ns | 30 B |
| PGO Enabled | 0.7071 ns | 105 B |

The JIT optimizes for PGO data in a variety of ways. Given the data it knows about how the code behaves, it can be more aggressive about inlining, as it has more data about what will and won’t be profitable. It can perform this “guarded devirtualization” for most interface and virtual dispatch, emitting both one or more fast paths that are devirtualized and possibly inlined, with a fallback that performs the standard dispatch should the actual type not match the expected type. It can actually reduce code size in various circumstances by choosing to not apply optimizations that might otherwise increase code size (e.g. inlining, loop cloning, etc.) in blocks discovered to be cold. It can optimize for type casts, emitting checks that do a direct type comparison against the actual object type rather than always relying on more complicated and expensive cast helpers (e.g. ones that need to search ancestor hierarchies or interface lists or that can handle generic co- and contra-variance). The list will continue to grow over time as the JIT learns more and more how to, well, learn.

Lots of PRs contributed to PGO. Here are just a few:

-   [dotnet/runtime#44427](https://github.com/dotnet/runtime/pull/44427) added support to the inliner that utilized call site frequency to boost the profitability metric (i.e. how valuable would it be to inline a method).
-   [dotnet/runtime#45133](https://github.com/dotnet/runtime/pull/45133) added the initial support for determining the distribution of concrete types used at virtual and interface dispatch call sites, in order to enable guarded devirtualization. [dotnet/runtime#51157](https://github.com/dotnet/runtime/pull/51157) further enhanced this with regards to small struct types, while [dotnet/runtime#51890](https://github.com/dotnet/runtime/pull/51890) enabled improved code generation by chaining together guarded devirtualization call sites, grouping together the frequently-taken code paths where applicable.
-   [dotnet/runtime#52827](https://github.com/dotnet/runtime/pull/52827) added support for special-casing `switch` cases when PGO data is available to support it. If there’s a dominant `switch` case, where the JIT sees that branch being taken at least 30% of the time, the JIT can emit a dedicated `if` check for that case up front, rather than having it go through the `switch` with the rest of the cases. (Note this applies to actual switches in the IL; not all C# `switch` statements will end up as `switch` instructions in IL, and in fact many won’t, as the C# compiler will often optimize smaller or more complicated switches into the equivalent of a cascading set of `if`/`else if` checks.)

That’s probably enough for now about inlining. There are other categories of optimization critical to high-performance C# and .NET code, as well. For example, bounds checking. One of the great things about C# and .NET is that, unless you go out of your way to circumvent the protections put in place (e.g. by using the `unsafe` keyword, the `Unsafe` class, the `Marshal` or `MemoryMarshal` classes, etc.), it’s near impossible to experience typical security vulnerabilities like buffer overruns. That’s because all accesses to arrays, strings, and spans are automatically “bounds checked” by the JIT, meaning it ensures before indexing into one of these data structures that the index is properly within bounds. You can see that with a simple example:

```
public int M(int[] arr, int index) => arr[index];
```

for which the JIT will generate code similar to this:

```
; Program.M(Int32[], Int32)
       sub       rsp,28
       cmp       r8d,[rdx+8]
       jae       short M01_L00
       movsxd    rax,r8d
       mov       eax,[rdx+rax*4+10]
       add       rsp,28
       ret
M01_L00:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 28
```

The `rdx` register here stores the address of `arr`, and the length of `arr` is stored 8 bytes beyond that (in this 64-bit process), so `[rdx+8]` is `arr.Length`, and the `cmp r8d, [rdx+8]` instruction is comparing `arr.Length` against the `index` value stored in the `r8d` register. If the index is equal to or greater than the array length, it jumps to the end of the method, which calls a helper that throws an exception. That comparison is the “bounds check.”

Of course, such bounds checks add overhead. For most code, the overhead is negligible, but if you’re reading this post, there’s a good chance you’ve written code where it’s not. And you certainly rely on code where it’s not: a lot of lower-level routines in the core .NET libraries do rely on avoiding this kind of overhead wherever possible. As such, the JIT goes to great lengths to avoid emitting bounds checking when it can prove going out of bounds isn’t possible. The prototypical example is a loop from `0` to an array’s `Length`. If you write:

```
public int Sum(int[] arr)
{
    int sum = 0;
    for (int i = 0; i < arr.Length; i++) sum += arr[i];
    return sum;
}
```

the JIT will output code like this:

```
; Program.Sum(Int32[])
       xor       eax,eax
       xor       ecx,ecx
       mov       r8d,[rdx+8]
       test      r8d,r8d
       jle       short M02_L01
M02_L00:
       movsxd    r9,ecx
       add       eax,[rdx+r9*4+10]
       inc       ecx
       cmp       r8d,ecx
       jg        short M02_L00
M02_L01:
       ret
; Total bytes of code 29
```

Note there’s no tell-tale `call` followed by an `int3` instruction at the end of the method; that’s because no call to a throw helper is required here, as there’s no bounds checking needed. The JIT can see that, by construction, the loop can’t walk off either end of the array, and thus it needn’t emit a bounds check.

Every release of .NET sees the JIT become wise to more and more patterns where it can safely eliminate bounds checking, and .NET 6 follows suit. [dotnet/runtime#40180](https://github.com/dotnet/runtime/pull/40180) and [dotnet/runtime#43568](https://github.com/dotnet/runtime/pull/43568) from [@nathan-moore](https://github.com/nathan-moore) are great (and very helpful) examples. Consider the following benchmark:

```
private char[] _buffer = new char[100];

[Benchmark]
public bool TryFormatTrue() => TryFormatTrue(_buffer);

private static bool TryFormatTrue(Span<char> destination)
{
    if (destination.Length >= 4)
    {
        destination[0] = 't';
        destination[1] = 'r';
        destination[2] = 'u';
        destination[3] = 'e';
        return true;
    }

    return false;
}
```

This represents relatively typical code you might see in some lower-level formatting, where the length of a span is checked and then data written into the span. In the past, the JIT has been a little finicky about which guard patterns here are recognized and which aren’t, and .NET 6 makes that a whole lot better, thanks to the aforementioned PRs. On .NET 5, this benchmark would result in assembly like the following:

```
; Program.TryFormatTrue(System.Span~1<Char>)
       sub       rsp,28
       mov       rax,[rcx]
       mov       edx,[rcx+8]
       cmp       edx,4
       jl        short M01_L00
       cmp       edx,0
       jbe       short M01_L01
       mov       word ptr [rax],74
       cmp       edx,1
       jbe       short M01_L01
       mov       word ptr [rax+2],72
       cmp       edx,2
       jbe       short M01_L01
       mov       word ptr [rax+4],75
       cmp       edx,3
       jbe       short M01_L01
       mov       word ptr [rax+6],65
       mov       eax,1
       add       rsp,28
       ret
M01_L00:
       xor       eax,eax
       add       rsp,28
       ret
M01_L01:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
```

The beginning of the assembly loads the span’s reference into the `eax` register and the length of the span into the `edx` register:

```
       mov       rax,[rcx]
       mov       edx,[rcx+8]
```

and then each assignment into the span ends up checking against this length, as in this sequence from above where we’re executing `destination[2] = 'u'`:

```
       cmp       edx,2
       jbe       short M01_L01
       mov       word ptr [rax+4],75
```

To save you from having to look at an ASCII table, lowercase ‘u’ has an ASCII hex value of `0x75`, so this code is validating that `2` is less than the span’s length (and jumping to `call CORINFO_HELP_RNGCHKFAIL` if it’s not), then storing `'u'` into the 2nd element of the span (`[rax+4]`). That’s four bounds checks, one for each character in `"true"`, even though we know they’re all in-bounds. The JIT in .NET 6 knows that, too:

```
; Program.TryFormatTrue(System.Span~1<Char>)
       mov       rax,[rcx]
       mov       edx,[rcx+8]
       cmp       edx,4
       jl        short M01_L00
       mov       word ptr [rax],74
       mov       word ptr [rax+2],72
       mov       word ptr [rax+4],75
       mov       word ptr [rax+6],65
       mov       eax,1
       ret
M01_L00:
       xor       eax,eax
       ret
```

Much better. Those changes then also allowed undoing some hacks (e.g. [dotnet/runtime#49450](https://github.com/dotnet/runtime/pull/49450) from [@SingleAccretion](https://github.com/SingleAccretion)) in the core libraries that had previously been done to work around the lack of the bounds checking removal in such cases.

Another bounds-checking improvement comes in [dotnet/runtime#49271](https://github.com/dotnet/runtime/pull/49271) from [@SingleAccretion](https://github.com/SingleAccretion). In previous releases, there was an issue in the JIT where an inlined method call could cause subsequent bounds checks that otherwise would have been removed to now no longer be removed. This PR fixes that, the effect of which is evident in this benchmark

```
private long[] _buffer = new long[10];
private DateTime _now = DateTime.UtcNow;

[Benchmark]
public void Store() => Store(_buffer, _now);

[MethodImpl(MethodImplOptions.NoInlining)]
private static void Store(Span<long> span, DateTime value)
{
    if (!span.IsEmpty)
    {
        span[0] = value.Ticks;
    }
}
```

```
; .NET 5.0.9
; Program.Store(System.Span~1<Int64>, System.DateTime)
       sub       rsp,28
       mov       rax,[rcx]
       mov       ecx,[rcx+8]
       test      ecx,ecx
       jbe       short M01_L00
       cmp       ecx,0
       jbe       short M01_L01
       mov       rcx,0FFFFFFFFFFFF
       and       rdx,rcx
       mov       [rax],rdx
M01_L00:
       add       rsp,28
       ret
M01_L01:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 46

; .NET 6.0.0
; Program.Store(System.Span~1<Int64>, System.DateTime)
       mov       rax,[rcx]
       mov       ecx,[rcx+8]
       test      ecx,ecx
       jbe       short M01_L00
       mov       rcx,0FFFFFFFFFFFF
       and       rdx,rcx
       mov       [rax],rdx
M01_L00:
       ret
; Total bytes of code 27
```

In other cases, it’s not about whether there’s a bounds check, but what code is emitted for a bounds check that isn’t elided. For example, [dotnet/runtime#42295](https://github.com/dotnet/runtime/pull/42295) special-cases indexing into an array with a constant 0 index (which is actually fairly common) and emits a `test` instruction rather than a `cmp` instruction, which makes the code both slightly smaller and slightly faster.

Another bounds-checking optimization that’s arguably a category of its own is “loop cloning.” The idea behind loop cloning is the JIT can duplicate a loop, creating one variant that’s the original and one variant that removes bounds checking, and then at run-time decide which to use based on an additional up-front check. For example, consider this code:

```
public static int Sum(int[] array, int length)
{
    int sum = 0;
    for (int i = 0; i < length; i++)
    {
        sum += array[i];
    }
    return sum;
}
```

The JIT still needs to bounds check the `array[i]` access, as while it knows that `i >= 0` && `i < length`, it doesn’t know whether `length <= array.Length` and thus doesn’t know whether `i < array.Length`. However, doing such a bounds check on each iteration of the loop adds an extra comparison and branch on each iteration. Loop cloning enables the JIT to generate code that’s more like the equivalent of this:

```
public static int Sum(int[] array, int length)
{
    int sum = 0;
    if (array is not null && length <= array.Length)
    {
        for (int i = 0; i < length; i++)
        {
            sum += array[i]; // bounds check removed
        }
    }
    else
    {
        for (int i = 0; i < length; i++)
        {
            sum += array[i]; // bounds check not removed
        }
    }
    return sum;
}
```

We end up paying for the extra up-front one time checks, but as long as there’s at least a couple of iterations, the elimination of the bounds check pays for that and more. Neat. However, as with other bounds checking removal optimizations, the JIT is looking for very specific patterns, and things that deviate and fall off the golden path lose out on the optimization. That can include something as simple as the type of the array itself: change the previous example to use `byte[]` instead of `int[]`, and that’s enough to throw the JIT off the scent… or, at least it was in .NET 5. Thanks to [dotnet/runtime#48894](https://github.com/dotnet/runtime/pull/48894), in .NET 6 the loop is now cloned, as can be seen from this benchmark:

```
private byte[] _buffer = Enumerable.Range(0, 1_000_000).Select(i => (byte)i).ToArray();

[Benchmark]
public void Sum() => Sum(_buffer, 999_999);

public static int Sum(byte[] array, int length)
{
    int sum = 0;
    for (int i = 0; i < length; i++)
    {
        sum += array[i];
    }
    return sum;
}
```

| Method | Runtime | Mean | Ratio | Code Size |
| --- | --- | --- | --- | --- |
| Sum | .NET 5.0 | 471.3 us | 1.00 | 54 B |
| Sum | .NET 6.0 | 350.0 us | 0.74 | 97 B |

```
; .NET 5.0.9
; Program.Sum()
       sub       rsp,28
       mov       rax,[rcx+8]
       xor       edx,edx
       xor       ecx,ecx
       mov       r8d,[rax+8]
M00_L00:
       cmp       ecx,r8d
       jae       short M00_L01
       movsxd    r9,ecx
       movzx     r9d,byte ptr [rax+r9+10]
       add       edx,r9d
       inc       ecx
       cmp       ecx,0F423F
       jl        short M00_L00
       add       rsp,28
       ret
M00_L01:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 54

; .NET 6.0.0
; Program.Sum()
       sub       rsp,28
       mov       rax,[rcx+8]
       xor       edx,edx
       xor       ecx,ecx
       test      rax,rax
       je        short M00_L01
       cmp       dword ptr [rax+8],0F423F
       jl        short M00_L01
       nop       word ptr [rax+rax]
M00_L00:
       movsxd    r8,ecx
       movzx     r8d,byte ptr [rax+r8+10]
       add       edx,r8d
       inc       ecx
       cmp       ecx,0F423F
       jl        short M00_L00
       jmp       short M00_L02
M00_L01:
       cmp       ecx,[rax+8]
       jae       short M00_L03
       movsxd    r8,ecx
       movzx     r8d,byte ptr [rax+r8+10]
       add       r8d,edx
       mov       edx,r8d
       inc       ecx
       cmp       ecx,0F423F
       jl        short M00_L01
M00_L02:
       add       rsp,28
       ret
M00_L03:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 97
```

Not just bytes, but the same issue manifests for arrays of non-primitive structs. [dotnet/runtime#55612](https://github.com/dotnet/runtime/pull/55612) addressed that. Additionally, [dotnet/runtime#55299](https://github.com/dotnet/runtime/pull/55299) improved loop cloning for various loops over multidimensional arrays.

Since we’re on the topic of loop optimization, consider loop inversion. “Loop inversion” is a standard compiler transform that’s aimed at eliminating some branching from a loop. Consider a loop like:

```
while (i < 3)
{
    ...
    i++;
}
```

Loop inversion involves the compiler transforming this into:

```
if (i < 3)
{
    do
    {
        ...
        i++;
    }
    while (i < 3);
}
```

In other words, change the while into a do..while, moving the condition check from the beginning of each iteration to the end of each iteration, and then add a one-time condition check at the beginning to compensate. Now imagine that `i == 2`. In the original structure, we enter the loop, `i` is incremented, and then we jump back to the beginning to do the condition test, it’ll fail (as `i` is now 3), and we’ll then jump again to just past the end of the loop. Now consider the same situation with the inverted loop. We pass the `if` condition, as `i == 2`. We then enter the `do..while`, `i` is incremented, and we check the condition. The condition fails, and we’re already at the end of the loop, so we don’t jump back to the beginning and instead just keep running past the loop. Summary: we saved two jumps. And in either case, if `i` was `>= 3`, we have exactly the same number of jumps as we just jump to after the `while`/`if`. The inverted structure also often affords additional optimizations; for example, the JIT’s pattern recognition used for loop cloning and the hoisting of invariants depend on the loop being in an inverted form. Both [dotnet/runtime#50982](https://github.com/dotnet/runtime/pull/50982) and [dotnet/runtime#52347](https://github.com/dotnet/runtime/pull/52347) improved the JIT’s support for loop inversion.

Ok, we’ve talked about inlining optimizations, bounds checking optimizations, and loop optimizations. What about constants?

“Constant folding” is simply a fancy term to mean a compiler computing values at compile-time rather than leaving it to run-time. Folding can happen at various levels of compilation. If you write this C#:

```
public static int M() => 10 + 20 * 30 / 40 ^ 50 | 60 & 70;
```

the C# compiler will fold this while compiling to IL, computing the constant value `47` from all of those operations:

```
IL_0000: ldc.i4.s 47
IL_0002: ret
```

Folding can also happen in the JIT, which is particularly valuable in the face of inlining. If I have this C#:

```
public static int M() => 10 + N();
public static int N() => 20;
```

the C# compiler doesn’t (and in many cases shouldn’t) do any kind of interprocedural analysis to determine that `N` always returns `20`, so you end up with this IL for `M`:

```
IL_0000: ldc.i4.s 10
IL_0002: call int32 C::N()
IL_0007: add
IL_0008: ret
```

But with inlining, the JIT is able to generate this for `M`:

```
L0000: mov eax, 0x1e
L0005: ret
```

having inlined the `20`, constant folded `10 + 20`, and gotten the constant value `30` (hex `0x1e`). Constant folding also goes hand-in-hand with “constant propagation,” which is the practice of the compiler substituting a constant value into an expression, at which point compilers will often be able to iterate, apply more constant folding, do more constant propagation, and so on. Let’s say I have this non-trivial set of helper methods:

```
public bool ContainsSpace(string s) => Contains(s, ' ');

private static bool Contains(string s, char c)
{
    if (s.Length == 1)
    {
        return s[0] == c;
    }

    for (int i = 0; i < s.Length; i++)
    {
        if (s[i] == c)
            return true;
    }

    return false;
}
```

Based on whatever their needs were, the developer of `Contains(string, char)` decided that it would very frequently be called with string literals, and that single character literals were common. Now if I write:

```
[Benchmark]
public bool M() => ContainsSpace(" ");
```

the entirety of the generated code produced by the JIT for `M` is:

```
L0000: mov eax, 1
L0005: ret
```

How is that possible? The JIT inlines `Contains(string, char)` into `ContainsSpace(string)`, and inlines `ContainsSpace(string)` into `M()`. The implementation of `ContainsSpace(string, char)` is then exposed to the fact that `string s` is `" "` and `char c` is `' '`. It can then propagate the fact that `s.Length` is actually the constant `1`, which enables deleting as dead code everything after the `if` block. It can then see that `s[0]` is in-bounds, and remove any bounds checking, and can see that `s[0]` is the first character in the constant string `" "`, a `' '`, and can then see that `' ' == ' '`, making the entire operation return a constant `true`, hence the resulting `mov eax, 1`, which is used to return a Boolean value `true`. Neat, right? Of course, you may be asking yourself, “Does code really call such methods with literals?” And the answer is, absolutely, in lots of situations; the PR in .NET 5 that introduced the ability to treat `"literalString".Length` as a constant highlighted thousands of bytes of improvements in the generated assembly code across the core libraries. But a good example in .NET 6 that makes extra-special use of this is [dotnet/runtime#57217](https://github.com/dotnet/runtime/pull/57217). The methods being changed in this PR are expected to be called from C# compiler-generated code with literals, and being able to specialize based on the length of the string literal passed effectively enables multiple implementations of the method the JIT can choose from based on its knowledge of the literal used at the call site, resulting in faster and smaller code when such a literal is used.

But, the JIT needs to be taught what kinds of things can be folded. [dotnet/runtime#49930](https://github.com/dotnet/runtime/pull/49930) teaches it how to fold null checks when used with constant strings, which as in the previous example, is most valuable with inlining. Consider the `Microsoft.Extensions.Logging.Console.ConsoleFormatter` abstract base class. It exposes a protected constructor that looks like this:

```
protected ConsoleFormatter(string name)
{
    Name = name ?? throw new ArgumentNullException(nameof(name));
}
```

which is a fairly typical construct: validating that an argument isn’t null, throwing an exception if it is, and storing it if it’s not. Now look at one of the built-in types derived from it, like `JsonConsoleFormatter`:

```
public JsonConsoleFormatter(IOptionsMonitor<JsonConsoleFormatterOptions> options)
    : base(ConsoleFormatterNames.Json)
{
    ReloadLoggerOptions(options.CurrentValue);
    _optionsReloadToken = options.OnChange(ReloadLoggerOptions);
}
```

Note that `base (ConsoleFormatterNames.Json)` call. `ConsoleFormatterNames.Json` is defined as:

```
public const string Json = "json";
```

so this `base` call is really:

```
base("json")
```

When the JIT inlines the base constructor, it’ll now be able to see that the input is definitively not null, at which point it can eliminate as dead code the `?? throw new ArgumentNullException(nameof(name)`, and the entire inlined call will simply be the equivalent of `Name = "json"`.

[dotnet/runtime#50000](https://github.com/dotnet/runtime/pull/50000) is similar. As mentioned earlier, thanks to tiered compilation, `static readonly`s initialized in tier 0 can become consts in tier 1. This was enabled in previous .NET releases. For example, you might find code that dynamically enables or disables a feature based on an environment variable and then stores the result of that into a `static readonly bool`. When code reading that static field is recompiled in tier 1, the Boolean value can be considered a constant, enabling branches based on that value to be trimmed away. For example, given this benchmark:

```
private static readonly bool s_coolFeatureEnabled = GetCoolFeatureEnabled();

private static bool GetCoolFeatureEnabled()
{
    string envVar = Environment.GetEnvironmentVariable("EnableCoolFeature");
    return envVar == "1" || "true".Equals(envVar, StringComparison.OrdinalIgnoreCase);
}

[MethodImpl(MethodImplOptions.NoInlining)]
private static void UsedWhenCoolEnabled() { }

[MethodImpl(MethodImplOptions.NoInlining)]
private static void UsedWhenCoolNotEnabled() { }

[Benchmark]
public void CallCorrectMethod()
{
    if (s_coolFeatureEnabled)
    {
        UsedWhenCoolEnabled();
    }
    else
    {
        UsedWhenCoolNotEnabled();
    }
}
```

since I’ve not set the environment variable, when I run this and examine the resulting tier 1 assembly for `CallCorrectMethod`, I see this:

```
; Program.CallCorrectMethod()
       jmp       near ptr Program.UsedWhenCoolNotEnabled()
; Total bytes of code 5
```

That is the entirety of the implementation; there’s no call to `UsedWhenCoolEnabled` anywhere in sight, because the JIT was able to prune away the `if` block as dead code based on `s_coolFeatureEnabled` being a constant `false`. The aforementioned PR builds on that capability by enabling null folding for such values. Consider a library that exposes a method like:

```
public static bool Equals<T>(T i, T j, IEqualityComparer<T> comparer)
{
    comparer ??= EqualityComparer<T>.Default;
    return comparer.Equals(i, j);
}
```

comparing two values using the specified comparer, and if the specified comparer is null, using `EqualityComparer<T>.Default`. Now, with our benchmark we pass in `EqualityComparer<int>.Default`.

```
[Benchmark]
[Arguments(1, 2)]
public bool Equals(int i, int j) => Equals(i, j, EqualityComparer<int>.Default);

public static bool Equals<T>(T i, T j, IEqualityComparer<T> comparer)
{
    comparer ??= EqualityComparer<T>.Default;
    return comparer.Equals(i, j);
}
```

This is what the resulting assembly looks like with .NET 5 and .NET 6:

```
; .NET 5.0.9
; Program.Equals(Int32, Int32)
       mov       rcx,1503FF62D58
       mov       rcx,[rcx]
       test      rcx,rcx
       jne       short M00_L00
       mov       rcx,1503FF62D58
       mov       rcx,[rcx]
M00_L00:
       mov       r11,7FFE420C03A0
       mov       rax,[7FFE424403A0]
       jmp       rax
; Total bytes of code 51

; .NET 6.0.0
; Program.Equals(Int32, Int32)
       mov       rcx,1B4CE6C2F78
       mov       rcx,[rcx]
       mov       r11,7FFE5AE60370
       mov       rax,[7FFE5B1C0370]
       jmp       rax
; Total bytes of code 33
```

On .NET 5, those first two `mov` instructions are loading the `EqualityComparer<int>.Default`. Then with the call to `Equals<T>(int, int, IEqualityComparer<T>` inlined, that `test rcx, rcx` is the null check for the `EqualityComparer<int>.Default` passed as an argument. If it’s not null (it won’t be null), it then jumps to `M00_L00`, where those two `mov`s and a `jmp` are a tail call to the interface `Equals` method. On .NET 6, you can see those first two instructions are still there, and the last three instructions are still there, but the middle four instructions (`test`, `jne`, `mov`, `mov`) have evaporated, because the compiler is now able to propagate the non-nullness of the `static readonly` and eliminate completely the `comparer ??= EqualityComparer<T>.Default;` from the inlined helper.

[dotnet/runtime#47321](https://github.com/dotnet/runtime/pull/47321) also adds a lot of power with regards to folding. Most of the `Math` methods can now participate in constant folding, so if their inputs end up as constants for whatever reason, the results can become constants as well, and with constant propagation, this leads to the potential for serious reduction in run-time evaluation. Here’s a benchmark I created by copying some of the sample code from the [System.Math docs](https://docs.microsoft.com/dotnet/api/system.math), editing it to create a method that computes the height of a trapezoid.

```
[Benchmark]
public double GetHeight() => GetHeight(20.0, 10.0, 8.0, 6.0);

[MethodImpl(MethodImplOptions.AggressiveInlining)]
public static double GetHeight(double longbase, double shortbase, double leftLeg, double rightLeg)
{
    double x = (Math.Pow(rightLeg, 2.0) - Math.Pow(leftLeg, 2.0) + Math.Pow(longbase, 2.0) + Math.Pow(shortbase, 2.0) - 2 * shortbase * longbase) / (2 * (longbase - shortbase));
    return Math.Sqrt(Math.Pow(rightLeg, 2.0) - Math.Pow(x, 2.0));
}
```

These are what I get for benchmark results:

| Method | Runtime | Mean | Ratio | Code Size |
| --- | --- | --- | --- | --- |
| GetHeight | .NET 5.0 | 151.7852 ns | 1.000 | 179 B |
| GetHeight | .NET 6.0 | 0.0000 ns | 0.000 | 12 B |

Note the time spent for .NET 6 has dropped to nothing, and the code size has dropped from 179 bytes to 12. How is that possible? Because the entire operation became a single constant. The .NET 5 assembly looked like this:

```
; .NET 5.0.9
; Program.GetHeight()
       sub       rsp,38
       vzeroupper
       vmovsd    xmm0,qword ptr [7FFE66C31CA0]
       vmovsd    xmm1,qword ptr [7FFE66C31CB0]
       call      System.Math.Pow(Double, Double)
       vmovsd    qword ptr [rsp+28],xmm0
       vmovsd    xmm0,qword ptr [7FFE66C31CC0]
       vmovsd    xmm1,qword ptr [7FFE66C31CD0]
       call      System.Math.Pow(Double, Double)
       vmovsd    xmm2,qword ptr [rsp+28]
       vsubsd    xmm3,xmm2,xmm0
       vmovsd    qword ptr [rsp+30],xmm3
       vmovsd    xmm0,qword ptr [7FFE66C31CE0]
       vmovsd    xmm1,qword ptr [7FFE66C31CF0]
       call      System.Math.Pow(Double, Double)
       vaddsd    xmm2,xmm0,qword ptr [rsp+30]
       vmovsd    qword ptr [rsp+30],xmm2
       vmovsd    xmm0,qword ptr [7FFE66C31D00]
       vmovsd    xmm1,qword ptr [7FFE66C31D10]
       call      System.Math.Pow(Double, Double)
       vaddsd    xmm1,xmm0,qwor44562d ptr [rsp+30]
       vsubsd    xmm1,xmm1,qword ptr [7FFE66C31D20]
       vdivsd    xmm0,xmm1,[7FFE66C31D30]
       vmovsd    xmm1,qword ptr [7FFE66C31D40]
       call      System.Math.Pow(Double, Double)
       vmovsd    xmm2,qword ptr [rsp+28]
       vsubsd    xmm0,xmm2,xmm0
       vsqrtsd   xmm0,xmm0,xmm0
       add       rsp,38
       ret
; Total bytes of code 179
```

with at least five calls to `Math.Pow` on top of a bunch of double addition, subtraction, and square root operations, whereas with .NET 6, we get:

```
; .NET 6.0.0
; Program.GetHeight()
       vzeroupper
       vmovsd    xmm0,qword ptr [7FFE5B1BCE70]
       ret
; Total bytes of code 12
```

which is just returning a constant double value. It’s hard not to smile when seeing that.

There were additional folding-related improvements. [dotnet/runtime#48568](https://github.com/dotnet/runtime/pull/48568) from [@SingleAccretion](https://github.com/SingleAccretion) improved the handling of unsigned comparisons as part of constant folding and propagation; [dotnet/runtime#47133](https://github.com/dotnet/runtime/pull/47133) from [@SingleAccretion](https://github.com/SingleAccretion) changed in what phase of the JIT certain folding is performed in order to improve its impact on inlining; and [dotnet/runtime#43567](https://github.com/dotnet/runtime/pull/43567) improved the folding of commutative operators. Further, for ReadyToRun, [dotnet/runtime#42831](https://github.com/dotnet/runtime/pull/42831) from [@nathan-moore](https://github.com/nathan-moore) ensured that the `Length` of an array created from a constant could be propagated as a constant.

Most of the improvements we’ve talked about thus far are cross-cutting. Sometimes, though, improvements are much more focused, with a change intended to improve the code generated for a very specific pattern. And there have been a lot of those in .NET 6. Here are a few examples:

-   -   [dotnet/runtime#37245](https://github.com/dotnet/runtime/pull/37245). When implicitly casting a `string` to a `ReadOnlySpan<char>`, the operator performs a `null` check on the input, such that it’ll return an empty span if the string is null. The operator is aggressively inlined, however, and so if the call site can prove that the string is not null, the null check can be eliminated.
        
        ```
        [Benchmark]
        public ReadOnlySpan<char> Const() => "hello world";
        ```
        

```
; .NET 5.0.9
; Program.Const()
       mov       rax,12AE3A09B48
       mov       rax,[rax]
       test      rax,rax
       jne       short M00_L00
       xor       ecx,ecx
       xor       r8d,r8d
       jmp       short M00_L01
M00_L00:
       cmp       [rax],eax
       cmp       [rax],eax
       add       rax,0C
       mov       rcx,rax
       mov       r8d,0B
M00_L01:
       mov       [rdx],rcx
       mov       [rdx+8],r8d
       mov       rax,rdx
       ret
; Total bytes of code 53

; .NET 6.0.0
; Program.Const()
       mov       rax,18030C4A038
       mov       rax,[rax]
       add       rax,0C
       mov       [rdx],rax
       mov       dword ptr [rdx+8],0B
       mov       rax,rdx
       ret
; Total bytes of code 31
```

-   -   [dotnet/runtime#37836](https://github.com/dotnet/runtime/pull/37836). `BitOperations.PopCount` was added in .NET Core 3.0, and returns the “popcount”, or “population count”, of the input number, meaning the number of bits set. It’s implemented as a hardware intrinsic if the underlying hardware supports it, or via a software fallback otherwise, but it’s also easily computed at compile time if the input is a constant (or if it becomes a constant from the JIT’s perspective, e.g. if the input is a `static readonly`). This PR turns `PopCount` into a JIT intrinsic, enabling the JIT to substitute a value for the whole method invocation if it deems that appropriate.
        
        ```
        [Benchmark]
        public int PopCount() => BitOperations.PopCount(42);
        ```
        

```
; .NET 5.0.9
; Program.PopCount()
       mov       eax,2A
       popcnt    eax,eax
       ret
; Total bytes of code 10

; .NET 6.0.0
; Program.PopCount()
       mov       eax,3
       ret
; Total bytes of code 6
```

-   -   [dotnet/runtime#50997](https://github.com/dotnet/runtime/pull/50997). This is a great example of improvements being made to the JIT based on an evolving need from the kinds of things libraries end up doing. In particular, this came about because of improvements to string interpolation that we’ll discuss later in this post. Previously, if you wrote the interpolated string `$"{_nullableValue}"` where `_nullableValue` was, say, an `int?`, this would result in a `string.Format` call that passes `_nullableValue` as an `object` argument. Boxing that `int?` translates into either null if the nullable value is `null` or boxing its `int` value if it’s not null. With C# 10 and .NET 6, this will instead result in a call to a generic method, passing in the `_nullableValue` strongly-typed as `T`\==`int?`, and that generic method then checks for various interfaces on the `T` and uses them if they exist. In performance testing of the feature, this exposed a measurable performance cliff due to the code generation employed for the nullable value types, both in allocation and in throughput. This PR helped to avoid that cliff by optimizing the boxing involved for this pattern of interface checking and usage.
        
        ```
        private int? _nullableValue = 1;
        
        [Benchmark]
        public string Format() => Format(_nullableValue);
        
        private string Format(T value, IFormatProvider provider = null)
        {
            if (value is IFormattable)
            {
                return ((IFormattable)value).ToString(null, provider);
            }
        
            return value.ToString();
        }
        ```
        

| Method | Runtime | Mean | Ratio | Code Size | Allocated |
| --- | --- | --- | --- | --- | --- |
| Format | .NET 5.0 | 87.71 ns | 1.00 | 154 B | 48 B |
| Format | .NET 6.0 | 51.88 ns | 0.59 | 100 B | 24 B |

-   [dotnet/runtime#50112](https://github.com/dotnet/runtime/pull/50112). For hot code paths, especially those concerned about size, there’s a common “throw helper” pattern employed where the code to perform a throw is moved out into a separate method, as the JIT won’t inline a method that is discovered to always throw. If there’s a common check being employed, that’s often then put it into its own helper. So, for example, if you wanted a helper method that checked to see if some reference type argument was null and then threw an exception if it was, that might look like this:
    
    ```
    public static void ThrowIfNull(
        [NotNull] object? argument, [CallerArgumentExpression("argument")] string? paramName = null)
    {
        if (argument is null)
            Throw(paramName);
    }
    
    [DoesNotReturn]
    private static void Throw(string? paramName) => throw new ArgumentNullException(paramName);
    ```
    
    And, in fact, that’s exactly what the new `ArgumentNullException.ThrowIfNull` helper introduced in [dotnet/runtime#55594](https://github.com/dotnet/runtime/pull/55594) looks like. The trouble with this, however, is that in order to call the `ThrowIfNull` method with a string literal, we end up needing to materialize that string literal as a string object (e.g. for a `string input` argument, `nameof(input)`, aka `"input"`). If the check were being done inline, the JIT already has logic to deal with that, e.g. this:
    
    ```
    [Benchmark]
    [Arguments("hello")]
    public void ThrowIfNull(string input)
    {
        //ThrowIfNull(input, nameof(input));
        if (input is null)
            throw new ArgumentNullException(nameof(input));
    }
    ```
    
    produces on .NET 5:
    
    ```
    ; Program.ThrowIfNull(System.String)
           push      rsi
           sub       rsp,20
           test      rdx,rdx
           je        short M00_L00
           add       rsp,20
           pop       rsi
           ret
    M00_L00:
           mov       rcx,offset MT_System.ArgumentNullException
           call      CORINFO_HELP_NEWSFAST
           mov       rsi,rax
           mov       ecx,1
           mov       rdx,7FFE715BB748
           call      CORINFO_HELP_STRCNS
           mov       rdx,rax
           mov       rcx,rsi
           call      System.ArgumentNullException..ctor(System.String)
           mov       rcx,rsi
           call      CORINFO_HELP_THROW
           int       3
    ; Total bytes of code 74
    ```
    
    In particular, we’re talking about that `call CORINFO_HELP_STRCNS`. But with the check and throw moved into the helper, that lazy initialization of the string literal object doesn’t happen. We end up with the assembly for the check looking nice and slim, but from an overall memory perspective, it’s likely a regression to force all of those string literals to be materialized. This PR addressed that, by ensuring the lazy initialization still happens, only if we’re about to throw, even with the helper being used.
    
    ```
    [Benchmark]
    [Arguments("hello")]
    public void ThrowIfNull(string input)
    {
        ThrowIfNull(input, nameof(input));
    }
    
    private static void ThrowIfNull(
        [NotNull] object? argument, [CallerArgumentExpression("argument")] string? paramName = null)
    {
        if (argument is null)
            Throw(paramName);
    }
    
    [DoesNotReturn]
    private static void Throw(string? paramName) => throw new ArgumentNullException(paramName);
    ```
    
    ```
    ; .NET 5.0.9
    ; Program.ThrowIfNull(System.String)
           test      rdx,rdx
           jne       short M00_L00
           mov       rcx,1FC48939520
           mov       rcx,[rcx]
           jmp       near ptr Program.Throw(System.String)
    M00_L00:
           ret
    ; Total bytes of code 24
    
    ; .NET 6.0.0
    ; Program.ThrowIfNull(System.String)
           sub       rsp,28
           test      rdx,rdx
           jne       short M00_L00
           mov       ecx,1
           mov       rdx,7FFEBF512BE8
           call      CORINFO_HELP_STRCNS
           mov       rcx,rax
           add       rsp,28
           jmp       near ptr Program.Throw(System.String)
    M00_L00:
           add       rsp,28
           ret
    ; Total bytes of code 46
    ```
    

-   -   [dotnet/runtime#43811](https://github.com/dotnet/runtime/pull/43811) and [dotnet/runtime#46237](https://github.com/dotnet/runtime/pull/46237). It’s fairly common, in particular in the face of inlining, to end up with sequences that have redundant comparison operations. Consider a fairly typical expression when dealing with nullable value types: `if (i.HasValue) { Use(i.Value); }`. That `i.Value` access invokes the `Nullable<T>.Value` getter, which itself checks `HasValue`, leading to a redundant comparison with the developer-written `HasValue` check in the guard. This specific example has led some folks to adopt a pattern of using `GetValueOrDefault()` after a `HasValue` check, since somewhat ironically `GetValueOrDefault()` just returns the `value` field without any additional checks. But there shouldn’t be a penalty for writing the simpler code that makes logical sense. And thanks to this PR, there isn’t. The JIT will now walk the control flow graph to see if any [dominating block](https://en.wikipedia.org/wiki/Dominator_%28graph_theory%29) (basically, code we had to go through to get to this point) has a similar compare.
        
        ```
        [Benchmark]
        public bool IsGreaterThan() => IsGreaterThan(42, 40);
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool IsGreaterThan(int? i, int j) => i.HasValue && i.Value > j;
        ```
        

```
; .NET 5.0.9
; Program.IsGreaterThan(System.Nullable~1<Int32>, Int32)
       sub       rsp,28
       mov       [rsp+30],rcx
       movzx     eax,byte ptr [rsp+30]
       test      eax,eax
       je        short M01_L00
       test      eax,eax
       je        short M01_L01
       cmp       [rsp+34],edx
       setg      al
       movzx     eax,al
       add       rsp,28
       ret
M01_L00:
       xor       eax,eax
       add       rsp,28
       ret
M01_L01:
       call      System.ThrowHelper.ThrowInvalidOperationException_InvalidOperation_NoValue()
       int       3
; Total bytes of code 50

; .NET 6.0.0
; Program.IsGreaterThan(System.Nullable~1<Int32>, Int32)
       mov       [rsp+8],rcx
       cmp       byte ptr [rsp+8],0
       je        short M01_L00
       cmp       [rsp+0C],edx
       setg      al
       movzx     eax,al
       ret
M01_L00:
       xor       eax,eax
       ret
; Total bytes of code 26
```

-   -   [dotnet/runtime#49585](https://github.com/dotnet/runtime/pull/49585). Learning from others is very important. Division is typically a relatively slow operation on modern hardware, and thus compilers try to find ways to avoid it, especially when dividing by a constant. In such cases, the JIT will try to find an alternative, which typically involves some combination of shifting and multiplying by a “magic number” that’s derived from the particular constant. This PR implements the techniques from [Faster Unsigned Division by Constants](https://ridiculousfish.com/files/faster_unsigned_division_by_constants.pdf) to improve the magic number selected for a certain subset of constants, enabling better code generation when dividing by numbers like 7 or 365.
        
        ```
        private uint _value = 12345;
        
        [Benchmark]
        public uint Div7() => _value / 7;
        ```
        

```
; .NET 5.0.9
; Program.Div()
       mov       ecx,[rcx+8]
       mov       edx,24924925
       mov       eax,ecx
       mul       edx
       sub       ecx,edx
       shr       ecx,1
       lea       eax,[rcx+rdx]
       shr       eax,2
       ret
; Total bytes of code 23

; .NET 6.0.0
; Program.Div()
       mov       eax,[rcx+8]
       mov       rdx,492492492493
       mov       eax,eax
       mul       rdx
       mov       eax,edx
       ret
; Total bytes of code 21
```

-   -   [dotnet/runtime#45463](https://github.com/dotnet/runtime/pull/45463). It’s fairly common to see code check whether a value is even by using `i % 2 == 0`. The JIT can now transform that into code more like `i & 1 == 0` to arrive at the same answer but with less ceremony.
        
        ```
        [Benchmark]
        [Arguments(42)]
        public bool IsEven(int i) => i % 2 == 0;
        ```
        

```
; .NET 5.0.9
; Program.IsEven(Int32)
       mov       eax,edx
       shr       eax,1F
       add       eax,edx
       and       eax,0FFFFFFFE
       sub       edx,eax
       sete      al
       movzx     eax,al
       ret
; Total bytes of code 19

; .NET 6.0.0
; Program.IsEven(Int32)
       test      dl,1
       sete      al
       movzx     eax,al
       ret
; Total bytes of code 10
```

-   -   [dotnet/runtime#44562](https://github.com/dotnet/runtime/pull/44562). It’s common in high-performance code that uses cached arrays to see the code first store the arrays into locals and then operate on the locals. This enables the JIT to prove to itself, if it sees nothing else assigning into the array reference, that the array is invariant, such that it can learn from previous use of the array to optimize subsequent use. For example, if you iterate `for (int i = 0; i < arr.Length; i++) Use(arr[i]);`, it can eliminate the bounds check on the `arr[i]`, as it trusts `i < arr.Length`. However, if this had instead been written as `for (int i = 0; i < s_arr.Length; i++) Use(s_arr[i]);`, where `s_arr` is defined as `static readonly int[] s_arr = ...;`, the JIT would not eliminate the bounds check, as the JIT wasn’t satisfied that `s_arr` was definitely not going to change, despite the `readonly`. This PR fixed that, enabling the JIT to see this static readonly array as being invariant, which then enables subsequent optimizations like bounds check elimination and common subexpression elimination.
        
        ```
        static readonly int[] s_array = { 1, 2, 3, 4 };
        
        [Benchmark]
        public int Sum()
        {
            if (s_array.Length >= 4)
            {
                return s_array[0] + s_array[1] + s_array[2] + s_array[3];
            }
        
            return 0;
        }
        ```
        

```
; .NET 5.0.9
; Program.Sum()
       sub       rsp,28
       mov       rax,15434127338
       mov       rax,[rax]
       cmp       dword ptr [rax+8],4
       jl        short M00_L00
       mov       rdx,rax
       mov       ecx,[rdx+8]
       cmp       ecx,0
       jbe       short M00_L01
       mov       edx,[rdx+10]
       mov       r8,rax
       cmp       ecx,1
       jbe       short M00_L01
       add       edx,[r8+14]
       mov       r8,rax
       cmp       ecx,2
       jbe       short M00_L01
       add       edx,[r8+18]
       cmp       ecx,3
       jbe       short M00_L01
       add       edx,[rax+1C]
       mov       eax,edx
       add       rsp,28
       ret
M00_L00:
       xor       eax,eax
       add       rsp,28
       ret
M00_L01:
       call      CORINFO_HELP_RNGCHKFAIL
       int       3
; Total bytes of code 89

; .NET 6.0.0
; Program.Sum()
       mov       rax,28B98007338
       mov       rax,[rax]
       mov       edx,[rax+8]
       cmp       edx,4
       jl        short M00_L00
       mov       rdx,rax
       mov       edx,[rdx+10]
       mov       rcx,rax
       add       edx,[rcx+14]
       mov       rcx,rax
       add       edx,[rcx+18]
       add       edx,[rax+1C]
       mov       eax,edx
       ret
M00_L00:
       xor       eax,eax
       ret
; Total bytes of code 48
```

-   -   [dotnet/runtime#49548](https://github.com/dotnet/runtime/pull/49548). This PR optimized various patterns involving comparisons against 0. Given an expression like `a == 0 && b == 0`, the JIT can now optimize that to be equivalent to `(a | b) == 0`, replacing a branch and second comparison with an `or`.
        
        ```
        [Benchmark]
        public bool AreZero() => AreZero(1, 2);
        
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool AreZero(int x, int y) => x == 0 && y == 0;
        ```
        

```
; .NET 5.0.9
; Program.AreZero(Int32, Int32)
       test      ecx,ecx
       jne       short M01_L00
       test      edx,edx
       sete      al
       movzx     eax,al
       ret
M01_L00:
       xor       eax,eax
       ret
; Total bytes of code 16

; .NET 6.0.0
; Program.AreZero(Int32, Int32)
       or        edx,ecx
       sete      al
       movzx     eax,al
       ret
; Total bytes of code 9
```

I can’t cover all of the pattern changes in as much detail, but there have been many more, e.g.

-   [dotnet/runtime#46253](https://github.com/dotnet/runtime/pull/46253) converted the `Interlocked.And` and `Interlocked.Or` methods introduced in .NET 5 into JIT intrinsics on ARM64.
-   [dotnet/runtime#46243](https://github.com/dotnet/runtime/pull/46243) and [dotnet/runtime#45311](https://github.com/dotnet/runtime/pull/45311) avoided cast helpers from being emitted for `(T)array.Clone()` and `object.MemberwiseClone()`.
-   [dotnet/runtime#43947](https://github.com/dotnet/runtime/pull/43947) added support for unrolling single-iteration loops.
-   [dotnet/runtime#54864](https://github.com/dotnet/runtime/pull/54864) enabled more methods to be tail-called by allowing implicit widening.
-   [dotnet/runtime#53214](https://github.com/dotnet/runtime/pull/53214) eliminated redundant `test` instructions in some situations.
-   [dotnet/runtime#44419](https://github.com/dotnet/runtime/pull/44419) enabled common subexpression elimination (CSE) for floating-point constants.
-   [dotnet/runtime#45604](https://github.com/dotnet/runtime/pull/45604) from [@alexcovington](https://github.com/alexcovington) optimized division like `-i / 7` to instead be emitted as the equivalent of `i / -7`, saving on a negation operation.
-   [dotnet/runtime#48589](https://github.com/dotnet/runtime/pull/48589) extended support for throw helpers that are non-void returning.
-   [dotnet/runtime#52298](https://github.com/dotnet/runtime/pull/52298) optimized how floating-point constants are assigned to ref parameters.
-   [dotnet/runtime#32000](https://github.com/dotnet/runtime/pull/32000) from [@damageboy](https://github.com/damageboy) taught the JIT how to remove double-negation (e.g. `~(~x)`).
-   [dotnet/runtime#49238](https://github.com/dotnet/runtime/pull/49238) enabled the JIT to elide some additional null checks.
-   [dotnet/runtime#35627](https://github.com/dotnet/runtime/pull/35627) caused the JIT to emit better instructions for `i < 0` checks.
-   [dotnet/runtime#42164](https://github.com/dotnet/runtime/pull/42164) yielded better code generation for floating-point `-X` and `MathF.Abs(X)` operations.
-   [dotnet/runtime#41772](https://github.com/dotnet/runtime/pull/41772) enabled use of the BMI2 `rorx` instruction as part of rotate operations (`BitOperations.RotateRight`).
-   [dotnet/runtime#55614](https://github.com/dotnet/runtime/pull/55614) increased the number of loops in a given method that the JIT will optimize from 16 to 64.
-   [dotnet/runtime#51158](https://github.com/dotnet/runtime/pull/51158) avoided some unnecessary spilling when storing into fields.
-   [dotnet/runtime#50813](https://github.com/dotnet/runtime/pull/50813) updated the JIT’s knowledge of the execution characteristics of several operations (SQRT, RCP, RSQRT).

At this point, I’ve spent a lot of blog real estate writing a love letter to the improvements made to the JIT in .NET 6. There’s still a lot more, but rather than share long sections about the rest, I’ll make a few final shout outs here:

-   Value types have become more and more critical to optimize for, as developers focused on driving down allocations have turned to structs for salvation. However, historically the JIT hasn’t been able to optimize structs as well as one might have hoped, in particular around being able to keeps struct in registers aggressively. A lot of work happened in .NET 6 to improve the situation, and while there’s still some more to be done in .NET 7, things have come a long way. [dotnet/runtime#43870](https://github.com/dotnet/runtime/pull/43870), [dotnet/runtime#39326](https://github.com/dotnet/runtime/pull/39326), [dotnet/runtime#44555](https://github.com/dotnet/runtime/pull/44555), [dotnet/runtime#48377](https://github.com/dotnet/runtime/pull/48377), [dotnet/runtime#55045](https://github.com/dotnet/runtime/pull/55045), [dotnet/runtime#55535](https://github.com/dotnet/runtime/pull/55535), [dotnet/runtime#55558](https://github.com/dotnet/runtime/pull/55558), and [dotnet/runtime#55727](https://github.com/dotnet/runtime/pull/55727), among others, all contributed here.
-   Registers are really, really fast memory used to store data being used immediately by instructions. In any given code, there are typically many more variables in use than there are registers, and so something needs to determine which of those variables gets to live in which registers when. That process is referred to as “register allocation,” and getting it right contributes significantly to how well code performs. [dotnet/runtime#48308](https://github.com/dotnet/runtime/pull/48308) from [@alexcovington](https://github.com/alexcovington), [dotnet/runtime#54345](https://github.com/dotnet/runtime/pull/54345), [dotnet/runtime#47307](https://github.com/dotnet/runtime/pull/47307), [dotnet/runtime#45135](https://github.com/dotnet/runtime/pull/45135), and [dotnet/runtime#52269](https://github.com/dotnet/runtime/pull/52269) all contributed to improving the JIT’s register allocation heuristics in .NET 6. There’s also a [great write-up in dotnet/runtime](https://github.com/dotnet/runtime/blob/main/docs/design/coreclr/jit/lsra-heuristic-tuning.md) about some of these tuning efforts.
-   “Loop alignment” is a technique in which nop instructions are added before a loop to ensure that the beginning of the loop’s instructions fall at an address most likely to minimize the number of fetches required to load the instructions that make up that loop. Rather than trying to do justice to the topic, I recommend [Loop alignment in .NET 6](https://devblogs.microsoft.com/dotnet/loop-alignment-in-net-6/), which is very well written and provides excellent details on the topic, including highlighting the improvements that came from [dotnet/runtime#44370](https://github.com/dotnet/runtime/pull/44370), [dotnet/runtime#42909](https://github.com/dotnet/runtime/pull/42909), and [dotnet/runtime#55047](https://github.com/dotnet/runtime/pull/55047).
-   Checking whether a type implements an interface (e.g. `if (something is ISomething)`) can be relatively expensive, and in the worst case involves a linear walk through all of a type’s implemented interfaces to see whether the specified one is in the list. The implementation here is relegated by the JIT to several helper functions, which, as of .NET 5, are now written in C# and live in the `System.Runtime.CompilerServices.CastHelpers` type as the `IsInstanceOfInterface` and `ChkCastInterface` interface methods. It’s not an understatement to say that the performance of these methods is critical to many applications running efficiently. So, lots of folks were excited to see [dotnet/runtime#49257](https://github.com/dotnet/runtime/pull/49257) from [@benaadams](https://github.com/benaadams), which managed to improve the performance of these methods by ~15% to ~35%, depending on the usage.

### GC

There’s been a lot of work happening in .NET 6 on the GC (garbage collector), the vast majority of which has been in the name of switching the GC implementation to be based on “regions” rather than on “segments”. The initial commit for regions is in [dotnet/runtime#45172](https://github.com/dotnet/runtime/pull/45172), with over 30 PRs since expanding on it. [@maoni0](https://github.com/maoni0) is shepherding this effort and has already written on the topic; I encourage reading her post [Put a DPAD on that GC!](https://devblogs.microsoft.com/dotnet/put-a-dpad-on-that-gc/) to learn more in depth. But here are a few key statements from her post to help shed some light on the terminology:

> “So what are the key differences between segments and regions? Segments are large units or memory – on Server GC 64-bit if the segment sizes are 1GB, 2GB or 4GB each (for Workstation it’s much smaller – 256MB) on SOH. Regions are much smaller units, they are by default 4MB each. So you might ask, “so they are smaller, why is that significant?”
> 
> “\[Imagine\] a scenario where we have free spaces in one generation, say gen0 because there’s some async IO going on that caused us to demote a bunch of pins in gen0, that we don’t actually use (this could be due to not waiting for so long to do the next GC or we’d have accumulated too much survival which means the GC pause would be too long). Wouldn’t it be nice if we could use those free spaces for other generations if they need them! Same with free spaces in gen2 and LOH – you might have some free spaces in gen2, it would be nice to use them to allocate some large objects. We do decommit on a segment but only the end of the segment which is after the very last live object on that segment (denoted by the light gray space at the end of each segment). And if you have pinning that prevents the GC from retracting the end of the segment, then we can only form free spaces and free spaces are always committed memory. Of course you might ask, “why don’t you just decommit the middle of a segment that has large free spaces?”. But that requires bookkeeping to remember which parts in the middle of a segment are decommitted so we need to re-commit them when we want to use them to allocate objects. And now we are getting into the idea of regions anyway, which is to have much smaller amounts of memory being manipulated separately by the GC.”

Beyond regions, there have been other improvements to the GC in .NET 6:

-   [dotnet/runtime#45208](https://github.com/dotnet/runtime/pull/45208) optimized the “plan phase” of foreground GCs (gen0 and gen1 GCs done while a background GC is in progress) by enabling it to use its list of marked objects, shaving a significant amount of time off the operation.
-   [dotnet/runtime#41599](https://github.com/dotnet/runtime/pull/41599) helps reduce pause times by ensuring that the mark lists are distributely evenly across all of the GC heaps / threads in server GC.
-   [dotnet/runtime#55174](https://github.com/dotnet/runtime/pull/55174) added a time-based decay that enables gen 0 and gen1 budgets to shrink over time with inactivity after they’d previously significantly expanded.

### Threading

Moving up the stack a bit, let’s talk threading, starting with `ThreadPool`.

Sometimes performance optimizations are about eliminating unnecessary work, or making tradeoffs that optimize for the common case while slightly pessimizing niche cases, or taking advantage of new lower-level capabilities to do something faster, or any number of other things. But sometimes, performance optimizations are about finding ways to help bad-but-common code be a little less bad.

A thread pool’s job is simple: run work items. To do that, at its core a thread pool needs two things: a queue of work to be processed, and a set of threads to process them. We can write a functional, trivial thread pool, well, trivially:

```
static class SimpleThreadPool
{
    private static BlockingCollection<Action> s_work = new();

    public static void QueueUserWorkItem(Action action) => s_work.Add(action);

    static SimpleThreadPool()
    {
        for (int i = 0; i < Environment.ProcessorCount; i++)
            new Thread(() =>
            {
                while (true) s_work.Take()();
            }) { IsBackground = true }.Start();
    }
}
```

Boom, functional thread pool. But… not a very good one. The hardest part of a good thread pool is in the management of the threads, and in particular determining at any given point how many threads should be servicing the queue of work. Too many threads, and you can grind a system to a halt, as all threads are fighting for the system’s resources, adding huge overheads with context switching, and getting in each other’s way with cache thrashing. Too few threads, and you can grind a system to a halt, as work items aren’t getting processed fast enough or, worse, running work items are blocked waiting for other work items to run but without enough additional threads to run them. The .NET `ThreadPool` has multiple mechanisms in place for determining how many threads should be in play at any point in time. First, it has a starvation detection mechanism. This mechanism is a fairly straightforward gate that kicks in once or twice a second and checks to see whether any progress has been made on removing items from the pool’s queues: if progress hasn’t been made, meaning nothing has been dequeued, the pool assumes the system is starved and injects an additional thread. Second, it has a hill climbing algorithm that is constantly seeking to maximimize work item throughput by manipulating available thread count; after every N work item completions, it evaluates whether adding or removing a thread to/from circulation helps or hurts work item throughput, thereby making it adaptive to the current needs of the system. However, the hill climbing mechanism has a weakness: in order to properly do its job, work items need to be completing… if work items aren’t completing because, say, all of the threads in the pool are blocked, hill climbing becomes temporarily useless, and the only mechanism for injecting additional threads is the starvation mechanism, which is (by design) fairly slow.

Such a situation might emerge when a system is flooded with “sync over async” work, a term [coined](https://devblogs.microsoft.com/dotnet/should-i-expose-synchronous-wrappers-for-asynchronous-methods/) to mean kicking off asynchronous work and then synchronously blocking waiting for it to complete; in the common case, such an anti-pattern ends up blocking one thread pool thread that depends on another thread pool thread doing work in order to unblock the first, and that can quickly result in all thread pool threads being blocked until enough have been injected to enable everyone to make forward progress. Such “sync-over-async” code, which often manifests as calling an async method and then blocking waiting on the returned task (e.g. `int i = GetValueAsync().Result`) is invariably considered a no-no in production code meant to be scalable, but sometimes it’s unavoidable, e.g. you’re forced to implement an interface that’s synchronous and the only means at your disposal to do so is with functionality exposed only as an async method.

We can see the impact of this with a terrible repro:

```
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

var tcs = new TaskCompletionSource();
var tasks = new List<Task>();
for (int i = 0; i < Environment.ProcessorCount * 4; i++)
{
    int id = i;
    tasks.Add(Task.Run(() =>
    {
        Console.WriteLine($"{DateTime.UtcNow:MM:ss.ff}: {id}");
        tcs.Task.Wait();
    }));
}
tasks.Add(Task.Run(() => tcs.SetResult()));

var sw = Stopwatch.StartNew();
Task.WaitAll(tasks.ToArray());
Console.WriteLine($"Done: {sw.Elapsed}");
```

This queues a bunch of work items to the thread pool, all of which block waiting for a task to complete, but that task won’t complete until the final queued work item completes it to unblock all the other workers. Thus, we end up blocking every thread in the pool, waiting for the thread pool to detect the starvation and inject another thread, which the repro then dutifully blocks, and on and on, until finally there are enough threads that every queued work item can be running concurrently. On .NET Framework 4.8 and .NET 5, the above repro on my 12-logical-core machine takes ~32 seconds to complete. You can see the output here; pay attention to the timestamps on each work item, where you can see that after ramping up very quickly to have a number of threads equal to the number of cores, it then very slowly introduces additional threads.

```
07:54.51: 4
07:54.51: 8
07:54.51: 1
07:54.51: 5
07:54.51: 9
07:54.51: 0
07:54.51: 10
07:54.51: 2
07:54.51: 11
07:54.51: 3
07:54.51: 6
07:54.51: 7
07:55.52: 12
07:56.52: 13
07:57.53: 14
07:58.52: 15
07:59.52: 16
07:00.02: 17
07:01.02: 18
07:01.52: 19
07:02.51: 20
07:03.52: 21
07:04.52: 22
07:05.03: 23
07:06.02: 24
07:07.03: 25
07:08.01: 26
07:09.03: 27
07:10.02: 28
07:11.02: 29
07:11.52: 30
07:12.52: 31
07:13.52: 32
07:14.02: 33
07:15.02: 34
07:15.53: 35
07:16.51: 36
07:17.02: 37
07:18.02: 38
07:18.52: 39
07:19.52: 40
07:20.52: 41
07:21.52: 42
07:22.55: 43
07:23.52: 44
07:24.53: 45
07:25.52: 46
07:26.02: 47
Done: 00:00:32.5128769
```

I’m happy to say the situation improves here for .NET 6. This is not license to start writing more sync-over-async code, but rather a recognition that sometimes it’s unavoidable, especially in existing applications that may not be able to move to an asynchronous model all at once, that might have some legacy components, etc. [dotnet/runtime#53471](https://github.com/dotnet/runtime/pull/53471) teaches the thread pool about the most common form of blocking we see in these situations, waiting on a `Task` that hasn’t yet completed. In response, the thread pool becomes much more aggressive about increasing its target thread count while the blocking persists, and then immediately lowers the target count again as soon as the blocking has ended. Running the same console app again on .NET 6, we can see that ~32 seconds drops to ~1.5 seconds, with the pool injecting threads much faster in response to the blocking.

```
07:53.39: 5
07:53.39: 7
07:53.39: 6
07:53.39: 8
07:53.39: 9
07:53.39: 10
07:53.39: 1
07:53.39: 0
07:53.39: 4
07:53.39: 2
07:53.39: 3
07:53.47: 12
07:53.47: 11
07:53.47: 13
07:53.47: 14
07:53.47: 15
07:53.47: 22
07:53.47: 16
07:53.47: 17
07:53.47: 18
07:53.47: 19
07:53.47: 21
07:53.47: 20
07:53.50: 23
07:53.53: 24
07:53.56: 25
07:53.59: 26
07:53.63: 27
07:53.66: 28
07:53.69: 29
07:53.72: 30
07:53.75: 31
07:53.78: 32
07:53.81: 33
07:53.84: 34
07:53.91: 35
07:53.97: 36
07:54.03: 37
07:54.10: 38
07:54.16: 39
07:54.22: 40
07:54.28: 41
07:54.35: 42
07:54.41: 43
07:54.47: 44
07:54.54: 45
07:54.60: 46
07:54.68: 47
Done: 00:00:01.3649530
```

Interestingly, this improvement was made easier by another large thread pool related change in .NET 6: the implementation is now entirely in C#. In previous releases of .NET, the thread pool’s core dispatch routine was in managed code, but all of the logic around thread management was all still in native in the runtime. All of that logic was ported to C# previously in support of CoreRT and mono, but it wasn’t used for coreclr. As of .NET 6 and [dotnet/runtime#43841](https://github.com/dotnet/runtime/pull/43841), it now is used everywhere. This should make further improvements and optimizations easier and enable more advancements in the pool in future releases.

Moving on from the thread pool, [dotnet/runtime#55295](https://github.com/dotnet/runtime/pull/55295) is an interesting improvement. One of the things you find a lot in multithreaded code, whether direct usage in low-lock algorithms or indirect usage in concurrency primitives like locks and semaphores, is spinning. Spinning is based on the idea that blocking in the operating system waiting for something to happen is very efficient for longer waits but incurs non-trivial overheads at the start and end of the waiting operation; if the thing you’re waiting for will likely happen very, very soon, you might be better off just looping around to try again immediately or after a very short pause. My use of the word “pause” there is not coincidental, as the x86 instruction set includes the “PAUSE” instruction, which tells the processor the code is doing a spin-wait and helps it to optimize accordingly. However, the delay incurred by the “PAUSE” instruction can varely greatly across processor architectures, e.g. it might take only 9 cycles on an Intel Core i5, but 65 cycles on an AMD Ryzen 7, or 140 cycles on an Intel Core i7. That makes it challenging for tuning the behavior of higher-level code written using spin loops, which core code in the runtime and key concurrency-related types in the core libraries do. To address this discrepancy and provide a consistent view of pauses, previous releases of .NET have tried to measure at startup the duration of pauses, and then used those metrics to normalize how many pauses are used when one is needed. However, this approach has a few downsides. While the measurement wasn’t being done on the main thread of the startup path, it was still contributing milliseconds of CPU time to every process, a number that can add up over the millions or billions of .NET process invocations that happen every day. It also was only done once for a process, but for a variety of reasons that overhead could actually change during a process’ lifetime, for example if a VM was suspended and moved from one physical machine to another. To address this, the aforementioned PR changes its scheme. Rather than measuring once at startup for a longer period of time, it periodically does a short measurement and uses that to refresh its perspective on how long pauses take. This should lead to an overall decrease in CPU usage as well as a more up-to-date understanding of what these pauses cost, leading to a more consistent behavior of the apps and services that rely on it.

Let’s move on to `Task`, where there have been a multitude of improvements. One notable and long overdue change is enabling `Task.FromResult<T>` to return a cached instance. When async methods were added in .NET Framework 4.5, we added a cache that `async Task<T>` methods could use for synchronously-completing operations (synchronously completing async methods are counterintuitively extremely common; consider a method where the first invocation does I/O to fill a buffer, but subsequent operations simply consume from that buffer). Rather than constructing a new `Task<T>` for every invocation of such a method, the cache would be consulted to see if a singleton `Task<T>` could be used instead. The cache obviously can’t store a singleton for every possible value of every `T`, but it can special-case some `T`s and cache a few values for each. For example, it caches two `Task<bool>` instances, one for `true` and one for `false`, and around 10 `Task<int>` instances, one for each of the values between `-1` and `8`, inclusive. But `Task.FromResult<T>` never used this cache, always returning a new instance even if there was a task for it in the cache. This has led to one of two commonly-seen occurrences: either a developer using `Task.FromResult` recognizes this deficiency and has to maintain their own cache for values like `true` and `false`, or a developer using `Task.FromResult` doesn’t recognize it and ends up paying arguably unnecessary allocations. For .NET 6, [dotnet/runtime#43894](https://github.com/dotnet/runtime/pull/43894) changes `Task.FromResult<T>` to consult the cache, so creating tasks for a `bool` `true` or an `int` `1`, for example, no longer allocates. This adds a tiny bit of overhead (a branch or two) when `Task.FromResult<T>` is used with a type that can be cached but for which the specific value is not; however, on the balance it’s worthwhile given the savings for extremely common values.

Of course, tasks are very closely tied to async methods in C#, and it’s worth looking at a small but significant feature in C# 10 and .NET 6 that is likely to impact a lot of .NET code, directly or indirectly. This requires some backstory. When the C# compiler goes to implement an async method with the signature `async SomeTaskLikeType`, it consults the `SomeTaskLikeType` to see what “builder” should be used to help implement the method. For example, `ValueTask` is attributed with `[AsyncMethodBuilder(typeof(AsyncValueTaskMethodBuilder))]`, and so any `async ValueTask` method will cause the compiler to use `AsyncValueTaskMethodBuilder` as the builder for that method. We can see that if we compile a simple async method:

```
public static async ValueTask ExampleAsync() { }
```

for which the compiler produces approximately the following as the implementation of `ExampleAsync`:

```
public static ValueTask ExampleAsync()
{
    <ExampleAsync>d__0 stateMachine = default;
    stateMachine.<>t__builder = AsyncValueTaskMethodBuilder.Create();
    stateMachine.<>1__state = -1;
    stateMachine.<>t__builder.Start(ref stateMachine);
    return stateMachine.<>t__builder.Task;
}
```

This builder type is used in the generated code to create the builder instance (via a static `Create` method), to access the built task (via a `Task` instance property), to complete that built task (via `SetResult` and `SetException` instance methods), and to handle the state management associated with that built task when an await yields (via `AwaitOnCompleted` and `UnsafeAwaitOnCompleted` instance methods). And as there are four types built into the core libraries that are intended to be used as the return type from async methods (`Task`, `Task<T>`, `ValueTask`, and `ValueTask<T>`), the core libraries also include four builders (`AsyncTaskMethodBuilder`, `AsyncTaskMethodBuilder<T>`, `AsyncValueTaskMethodBuilder`, and `AsyncValueTaskMethodBuilder<T>`), all in `System.Runtime.CompilerServices`. Most developers should never see these types in any code they read or write.

One of the downsides to this model, however, is that which builder is selected is tied to the definition of the type being returned from the async method. So, if you want to define your async method to return `Task`, `Task<T>`, `ValueTask`, or `ValueTask<T>`, you have no way to control the builder that’s employed: it’s determined by that type and only by that type. Why would you want to change the builder? There are a variety of reasons someone might want to control the details of the lifecycle of the task, but one of the most prominent is pooling. When an `async Task`, `async ValueTask` or `async ValueTask<T>` method completes synchronously, nothing need be allocated: for `Task`, the implementation can just hand back `Task.CompletedTask`, for `ValueTask` it can just hand back `ValueTask.CompletedTask` (which is the same as `default(ValueTask)`), and for `ValueTask<T>` it can hand back `ValueTask.FromResult<T>`, which creates a struct that wraps the `T` value. However, when the method completes asynchronously, the implementations need to allocate some object (a `Task` or `Task<T>`) to uniquely identify this async operation and provide a conduit via which the completion information can be passed back to the caller awaiting the returned instance.

`ValueTask<T>` supports being backed not only by a `T` or a `Task<T>`, but also by an `IValueTaskSource<T>`, which allows enterprising developers to plug in a custom implementation, including one that could potentially be pooled. What if, instead of using the aforementioned builders, we could author a builder that used and pooled custom `IValueTaskSource<T>` instances? It could use those instead of `Task<T>` to back a `ValueTask<T>` returned from an asynchronously-completing `async ValueTask<T>` method. As outlined in the blog post [Async ValueTask Pooling in .NET 5](https://devblogs.microsoft.com/dotnet/async-valuetask-pooling-in-net-5/), .NET 5 included that as an opt-in experiment, where `AsyncValueTaskMethodBuilder` and `AsyncValueTaskMethodBuilder<T>` had a custom `IValueTaskSource`/`IValueTaskSource<T>` implementation they could instantiate and pool and use as the backing object behind a `ValueTask` or `ValueTask<T>`. The first time an async method needed to yield and move all its state from the stack to the heap, these builders would consult the pool and try to use an object already there, only allocating a new one if one wasn’t available in the pool. Then upon `GetResult()` being called via an `await` on the resulting `ValueTask`/`ValueTask<T>`, the object would be returned to the pool. That experiment is complete and the environment variable removed for .NET 6. In its stead, this capability is supported in a new form in .NET 6 and C# 10.

The `[AsyncMethodBuilder]` attribute we saw before can now be placed on methods in addition to on types, thanks to [dotnet/roslyn#54033](https://github.com/dotnet/roslyn/pull/54033); when an async method is attributed with `[AsyncMethodBuilder(typeof(SomeBuilderType))]`, the C# compiler will then prefer that builder over the default. And along with the C# 10 language/compiler feature, .NET 6 includes two new builder types, `PoolingAsyncValueTaskMethodBuilder` and `PoolingAsyncValueTaskMethodBuilder<T>`, thanks to [dotnet/runtime#50116](https://github.com/dotnet/runtime/pull/50116) and [dotnet/runtime#55955](https://github.com/dotnet/runtime/pull/55955). If we change our previous example to be:

```
[AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
public static async ValueTask ExampleAsync() { }
```

now the compiler generates:

```
public static ValueTask ExampleAsync()
{
    <ExampleAsync>d__0 stateMachine = default;
    stateMachine.<>t__builder = PoolingAsyncValueTaskMethodBuilder.Create();
    stateMachine.<>1__state = -1;
    stateMachine.<>t__builder.Start(ref stateMachine);
    return stateMachine.<>t__builder.Task;
}
```

which means `ExampleAsync` may now use pooled objects to back the returned `ValueTask` instances. We can see that with a simple benchmark:

```
const int Iters = 100_000;

[Benchmark(OperationsPerInvoke = Iters, Baseline = true)]
public async Task WithoutPooling()
{
    for (int i = 0; i < Iters; i++)
        await YieldAsync();

    async ValueTask YieldAsync() => await Task.Yield();
}

[Benchmark(OperationsPerInvoke = Iters)]
public async Task WithPooling()
{
    for (int i = 0; i < Iters; i++)
        await YieldAsync();

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    async ValueTask YieldAsync() => await Task.Yield();
}
```

| Method | Mean | Ratio | Allocated |
| --- | --- | --- | --- |
| WithoutPooling | 763.9 ns | 1.00 | 112 B |
| WithPooling | 781.9 ns | 1.02 | – |

Note the allocation per call dropping from 112 bytes to 0. So, why not just make this the default behavior of `AsyncValueTaskMethodBuilder` and `AsyncValueTaskMethodBuilder<T>`? Two reasons. First, it does create a functional difference. `Task`s are more capable than `ValueTask`s, supporting concurrent usage, multiple awaiters, and synchronous blocking. If consuming code was, for example, doing:

```
ValueTask vt = SomeMethodAsync();
await vt;
await vt;
```

that would have “just worked” when `ValueTask` was backed by a `Task`, but failed in one of multiple ways and varying levels of severity when pooling was enabled. Code analysis rule [CA2012](https://docs.microsoft.com/dotnet/fundamentals/code-analysis/quality-rules/ca2012) is meant to help avoid such code, but that alone is insufficient to prevent such breaks. Second, as you can see from the benchmark above, while the pooling avoided the allocation, it came with a bit more overhead. And not shown here is the additional overhead in memory and working set of having to maintain the pool at all, which is maintained per async method. There are also some potential overheads not shown here, things that are common pitfalls to any kind of pooling. For example, the GC is optimized to make gen0 collections really fast, and one of the ways it can do that is by not having to scan gen1 or gen2 as part of a gen0 GC. But if there are references to gen0 objects from gen1 or gen2, then it does need to scan portions of those generations (this is why storing references into fields involves “GC write barriers,” to see if a reference to a gen0 object is being stored into one from a higher generation). Since the entire purpose of pooling is to keep objects around for a long time, those objects will likely end up being in these higher generations, and any references they store could end up making GCs more expensive; that can easily be the case with these state machines, as every parameter and local used in the method could potentially need to be tracked as such. So, from a performance perspective, it’s best to use this capability only in places where it’s both likely to matter and where performance testing demonstrates it moves the needle in the right direction. We can see, of course, that there are scenarios where in addition to saving on allocation, it actually does improve throughput, which at the end of the day is typically what one is really focusing on improving when they’re measuring allocation reduction (i.e. reducing allocation to reduce time spent in garbage collection).

```
private const int Concurrency = 256;
private const int Iters = 100_000;

[Benchmark(Baseline = true)]
public Task NonPooling()
{
    return Task.WhenAll(from i in Enumerable.Range(0, Concurrency)
                        select Task.Run(async delegate
                        {
                            for (int i = 0; i < Iters; i++)
                                await A().ConfigureAwait(false);
                        }));

    static async ValueTask A() => await B().ConfigureAwait(false);

    static async ValueTask B() => await C().ConfigureAwait(false);

    static async ValueTask C() => await D().ConfigureAwait(false);

    static async ValueTask D() => await Task.Yield();
}

[Benchmark]
public Task Pooling()
{
    return Task.WhenAll(from i in Enumerable.Range(0, Concurrency)
                        select Task.Run(async delegate
                        {
                            for (int i = 0; i < Iters; i++)
                                await A().ConfigureAwait(false);
                        }));

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    static async ValueTask A() => await B().ConfigureAwait(false);

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    static async ValueTask B() => await C().ConfigureAwait(false);

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    static async ValueTask C() => await D().ConfigureAwait(false);

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    static async ValueTask D() => await Task.Yield();
}
```

| Method | Mean | Ratio | Allocated |
| --- | --- | --- | --- |
| NonPooling | 3.271 s | 1.00 | 11,800,058 KB |
| Pooling | 2.896 s | 0.88 | 214 KB |

Beyond these new builders, there have been other new APIs introduced in .NET 6 related to tasks. `Task.WaitAsync` was introduced in [dotnet/runtime#48842](https://github.com/dotnet/runtime/pull/48842) and provides an optimized implementation for creating a new `Task` that will complete when either the previous one completes or when a specified timeout has elapsed or a specified `CancellationToken` has had cancellation requested. This is useful in replacing a fairly common pattern that shows up (and that, unfortunately, developers often get wrong) with developers wanting to wait for a task to complete but with either or both a timeout and cancellation. For example, this:

```
Task t = ...;
using (var cts = new CancellationTokenSource())
{
    if (await Task.WhenAny(Task.Delay(timeout, cts.Token), t) != t)
    {
        throw new TimeoutException();
    }

    cts.Cancel();
    await t;
}
```

can now be replaced with just this:

```
Task t = ...;
await t.WaitAsync(timeout);
```

and be faster with less overhead. A good example of that comes from [dotnet/runtime#55262](https://github.com/dotnet/runtime/pull/55262), which used the new `Task.WaitAsync` to replace a similar implementation that existed inside of `SemaphoreSlim.WaitAsync`, such that the latter is now both simpler to maintain and faster with less allocation.

```
private SemaphoreSlim _sem = new SemaphoreSlim(0, 1);
private CancellationTokenSource _cts = new CancellationTokenSource();

[Benchmark]
public Task WithCancellationToken()
{
    Task t = _sem.WaitAsync(_cts.Token);
    _sem.Release();
    return t;
}

[Benchmark]
public Task WithTimeout()
{
    Task t = _sem.WaitAsync(TimeSpan.FromMinutes(1));
    _sem.Release();
    return t;
}

[Benchmark]
public Task WithCancellationTokenAndTimeout()
{
    Task t = _sem.WaitAsync(TimeSpan.FromMinutes(1), _cts.Token);
    _sem.Release();
    return t;
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| WithCancellationToken | .NET Framework 4.8 | 2.993 us | 1.00 | 1,263 B |
| WithCancellationToken | .NET Core 3.1 | 1.327 us | 0.44 | 536 B |
| WithCancellationToken | .NET 5.0 | 1.337 us | 0.45 | 496 B |
| WithCancellationToken | .NET 6.0 | 1.056 us | 0.35 | 448 B |
|  |  |  |  |  |
| WithTimeout | .NET Framework 4.8 | 3.267 us | 1.00 | 1,304 B |
| WithTimeout | .NET Core 3.1 | 1.768 us | 0.54 | 1,064 B |
| WithTimeout | .NET 5.0 | 1.769 us | 0.54 | 1,056 B |
| WithTimeout | .NET 6.0 | 1.086 us | 0.33 | 544 B |
|  |  |  |  |  |
| WithCancellationTokenAndTimeout | .NET Framework 4.8 | 3.838 us | 1.00 | 1,409 B |
| WithCancellationTokenAndTimeout | .NET Core 3.1 | 1.901 us | 0.50 | 1,080 B |
| WithCancellationTokenAndTimeout | .NET 5.0 | 1.929 us | 0.50 | 1,072 B |
| WithCancellationTokenAndTimeout | .NET 6.0 | 1.186 us | 0.31 | 544 B |

.NET 6 also sees the long-requested addition of `Parallel.ForEachAsync` ([dotnet/runtime#46943](https://github.com/dotnet/runtime/pull/46943)), which makes it easy to asynchronously enumerate an `IEnumerable<T>` or `IAsyncEnumerable<T>` and run a delegate for each yielded element, with those delegates executed in parallel, and with some modicum of control over how it happens, e.g. what `TaskScheduler` should be used, the maximum level of parallelism to enable, and what `CancellationToken` to use to cancel the work.

On the subject of `CancellationToken`, the cancellation support in .NET 6 has also seen performance improvements, both for existing functionality and for new APIs that enable an app to do even better. One interesting improvement is [dotnet/runtime#48251](https://github.com/dotnet/runtime/pull/48251), which is a good example of how one can design and implement and optimize for one scenario only to find that it’s making the wrong tradeoffs. When `CancellationToken` and `CancellationTokenSource` were introduced in .NET Framework 4.0, the expectation at the time was that the majority use case would be lots of threads registering and unregistering from the same `CancellationToken` in parallel. That led to a really neat (but complicated) lock-free implementation that involved quite a bit of allocation and overhead. If you were in fact registering and unregistering from the same token from lots of threads in parallel, the implementation was very efficient and resulted in good throughput. But if you weren’t, you were paying a lot of overhead for something that wasn’t providing reciprocal benefit. And, as luck would have it, that’s almost never the scenario these days. It’s much, much more common to have a `CancellationToken` that’s used serially, often with multiple registrations all in place at the same time, but with those registrations mostly having been added as part of the serial flow of execution rather than all concurrently. This PR recognizes this reality and reverts the implementation to a much simpler, lighterweight, and faster one that performs better for the vast majority use case (while taking a hit if it is actually hammered by multiple threads concurrently).

```
private CancellationTokenSource _source = new CancellationTokenSource();

[Benchmark]
public void CreateTokenDispose()
{
    using (var cts = new CancellationTokenSource())
        _ = cts.Token;
}

[Benchmark]
public void CreateRegisterDispose()
{
    using (var cts = new CancellationTokenSource())
        cts.Token.Register(s => { }, null).Dispose();
}

[Benchmark]
public void CreateLinkedTokenDispose()
{
    using (var cts = CancellationTokenSource.CreateLinkedTokenSource(_source.Token))
        _ = cts.Token;
}

[Benchmark(OperationsPerInvoke = 1_000_000)]
public void CreateManyRegisterDispose()
{
    using (var cts = new CancellationTokenSource())
    {
        CancellationToken ct = cts.Token;
        for (int i = 0; i < 1_000_000; i++)
            ct.Register(s => { }, null).Dispose();
    }
}

[Benchmark(OperationsPerInvoke = 1_000_000)]
public void CreateManyRegisterMultipleDispose()
{
    using (var cts = new CancellationTokenSource())
    {
        CancellationToken ct = cts.Token;
        for (int i = 0; i < 1_000_000; i++)
        {
            var ctr1 = ct.Register(s => { }, null);
            var ctr2 = ct.Register(s => { }, null);
            var ctr3 = ct.Register(s => { }, null);
            var ctr4 = ct.Register(s => { }, null);
            var ctr5 = ct.Register(s => { }, null);
            ctr5.Dispose();
            ctr4.Dispose();
            ctr3.Dispose();
            ctr2.Dispose();
            ctr1.Dispose();
        }
    }
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| CreateTokenDispose | .NET Framework 4.8 | 10.236 ns | 1.00 | 72 B |
| CreateTokenDispose | .NET Core 3.1 | 6.934 ns | 0.68 | 64 B |
| CreateTokenDispose | .NET 5.0 | 7.268 ns | 0.71 | 64 B |
| CreateTokenDispose | .NET 6.0 | 6.200 ns | 0.61 | 48 B |
|  |  |  |  |  |
| CreateRegisterDispose | .NET Framework 4.8 | 144.218 ns | 1.00 | 385 B |
| CreateRegisterDispose | .NET Core 3.1 | 79.392 ns | 0.55 | 352 B |
| CreateRegisterDispose | .NET 5.0 | 79.431 ns | 0.55 | 352 B |
| CreateRegisterDispose | .NET 6.0 | 56.715 ns | 0.39 | 192 B |
|  |  |  |  |  |
| CreateLinkedTokenDispose | .NET Framework 4.8 | 103.622 ns | 1.00 | 209 B |
| CreateLinkedTokenDispose | .NET Core 3.1 | 61.944 ns | 0.60 | 112 B |
| CreateLinkedTokenDispose | .NET 5.0 | 53.526 ns | 0.52 | 80 B |
| CreateLinkedTokenDispose | .NET 6.0 | 38.631 ns | 0.37 | 64 B |
|  |  |  |  |  |
| CreateManyRegisterDispose | .NET Framework 4.8 | 87.713 ns | 1.00 | 56 B |
| CreateManyRegisterDispose | .NET Core 3.1 | 43.491 ns | 0.50 | – |
| CreateManyRegisterDispose | .NET 5.0 | 41.124 ns | 0.47 | – |
| CreateManyRegisterDispose | .NET 6.0 | 35.437 ns | 0.40 | – |
|  |  |  |  |  |
| CreateManyRegisterMultipleDispose | .NET Framework 4.8 | 439.874 ns | 1.00 | 281 B |
| CreateManyRegisterMultipleDispose | .NET Core 3.1 | 234.367 ns | 0.53 | – |
| CreateManyRegisterMultipleDispose | .NET 5.0 | 229.483 ns | 0.52 | – |
| CreateManyRegisterMultipleDispose | .NET 6.0 | 192.213 ns | 0.44 | – |

`CancellationToken` also has new APIs that help with performance. [dotnet/runtime#43114](https://github.com/dotnet/runtime/pull/43114) added new overloads of `Register` and `Unregister` that, rather than taking an `Action<object>` delegate, accept an `Action<object, CancellationToken>` delegate. This gives the delegate access to the `CancellationToken` responsible for the callback being invoked, enabling code that was instantiating a new delegate and potentially a closure in order to get access to that information to instead be able to use a cached delegate instance (as the compiler generates for lambdas that don’t close over any state). And [dotnet/runtime#50346](https://github.com/dotnet/runtime/pull/50346) makes it easier to reuse `CancellationTokenSource` instances for applications that want to pool them. In the past there have been multiple requests to be able to reuse any `CancellationTokenSource`, enabling its state to be reset from one that’s had cancellation requested to one that hasn’t. That’s _not_ something we’ve done nor plan to do, as a _lot_ of code depends on the idea that once a `CancellationToken`‘s `IsCancellationRequested` is true it’ll always be true; if that’s not the case, it’s very difficult to reason about. However, the vast majority of `CancellationTokenSource`s are never canceled, and if they’re not canceled, there’s nothing that prevents them from continuing to be used, potentially stored into a pool for someone else to use in the future. This gets a bit tricky, however, if `CancelAfter` is used or if the constructor is used that takes a timeout, as both of those cause a timer to be created, and there are race conditions possible between the timer firing and someone checking to see whether `IsCancellationRequested` is true (to determine whether to reuse the instance). The new `TryReset` method avoids this race condition. If you do want to reuse such a `CancellationTokenSource`, call `TryReset`: if it returns true, it hasn’t had cancellation requested and any underlying timer has been reset as well such that it won’t fire without a new timeout being set. If it returns false, well, don’t try to reuse it, as no guarantees are made about its state. You can see how the Kestrel web server does this, via [dotnet/aspnetcore#31528](https://github.com/dotnet/aspnetcore/pull/31528) and [dotnet/aspnetcore#34075](https://github.com/dotnet/aspnetcore/pull/34075).

Those are some of the bigger performance-focused changes in threading. There are a myriad of smaller ones as well, for example the new `Thread.UnsafeStart` [dotnet/runtime#47056](https://github.com/dotnet/runtime/pull/47056), `PreAllocatedOverlapped.UnsafeCreate` [dotnet/runtime#53196](https://github.com/dotnet/runtime/pull/53196), and `ThreadPoolBoundHandle.UnsafeAllocateNativeOverlapped` APIs that make it easier and cheaper to avoid capturing `ExecutingContext`; [dotnet/runtime#43891](https://github.com/dotnet/runtime/pull/43891) and [dotnet/runtime#44199](https://github.com/dotnet/runtime/pull/44199) that avoided several volatile accesses in threading types (this is mainly impactful on ARM); [dotnet/runtime#44853](https://github.com/dotnet/runtime/pull/44853) from [@LeaFrock](https://github.com/LeaFrock) that optimized the `ElapsedEventArgs` constructor to avoid some unnecessary roundtripping of a `DateTime` through a `FILETIME`; [dotnet/runtime#38896](https://github.com/dotnet/runtime/pull/38896) from [@Bond-009](https://github.com/Bond-009) that added a fast path to `Task.WhenAny(IEnumerable<Task>)` for the relatively common case of the input being an `ICollection<Task>`; and [dotnet/runtime#47368](https://github.com/dotnet/runtime/pull/47368), which improved the code generation for `Interlocked.Exchange` and `Interlocked.CompareExchange` when used with `nint` (`IntPtr`) or `nuint` (`UIntPtr`) by enabling them to reuse the existing intrinsics for `int` and `long`:

```
private nint _value;

[Benchmark]
public nint CompareExchange() => Interlocked.CompareExchange(ref _value, (nint)1, (nint)0) + (nint)1;
```

```
; .NET 5
; Program.CompareExchange()
       sub       rsp,28
       cmp       [rcx],ecx
       add       rcx,8
       mov       edx,1
       xor       r8d,r8d
       call      00007FFEC051F8B0
       inc       rax
       add       rsp,28
       ret
; Total bytes of code 31

; .NET 6
; Program.CompareExchange()
       cmp       [rcx],ecx
       add       rcx,8
       mov       edx,1
       xor       eax,eax
       lock cmpxchg [rcx],rdx
       inc       rax
       ret
; Total bytes of code 22
```

### System Types

Every .NET app uses types from the core `System` namespace, and so improvements to these types often have wide-reaching impact. There have been many performance enhancements to these types in .NET 6.

Let’s start with `Guid`. `Guid` is used to provide unique identifiers for any number of things and operations. The ability to create them quickly is important, as is the ability to quickly format and parse them. Previous releases have seen significant improvements on all these fronts, but they get even better in .NET 6. Let’s take a simple benchmark for parsing:

```
private string _guid = Guid.NewGuid().ToString();

[Benchmark]
public Guid Parse() => Guid.Parse(_guid);
```

[dotnet/runtime#44918](https://github.com/dotnet/runtime/pull/44918) helped avoid some overheads involved in unnecessarily accessing `CultureInfo.CurrentCulture` during parsing, as culture isn’t necessary or desired when parsing hexadecimal digits. And [dotnet/runtime#55792](https://github.com/dotnet/runtime/pull/55792) and [dotnet/runtime#56210](https://github.com/dotnet/runtime/pull/56210) rewrote parsing for the ‘D’, ‘B’, ‘P’, and ‘N’ formats (all but the antiquated ‘X’) to be much more streamlined, with careful attention paid to avoidance of bounds checking, how data is moved around, number of instructions to be executed, and so on. The net result is a very nice increase in throughput:

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| Parse | .NET Framework 4.8 | 251.88 ns | 1.00 |
| Parse | .NET Core 3.1 | 100.78 ns | 0.40 |
| Parse | .NET 5.0 | 80.13 ns | 0.32 |
| Parse | .NET 6.0 | 33.84 ns | 0.13 |

I love seeing tables like this. A 2.5x speedup going from .NET Framework 4.8 to .NET Core 3.1, another 1.3x on top of that going from .NET Core 3.1 to .NET 5, and then another 2.3x going from .NET 5 to .NET 6. Just one small example of how the platform gets better every release.

One other `Guid` related improvement won’t actually show up as a performance improvement (potentially even a tiny regression), but is worth mentioning in this context, anway. `Guid.NewGuid` has never guaranteed that the values generated would employ cryptographically-secure randomness, however as an implementation detail, on Windows `NewGuid` was implemented with `CoCreateGuid` which was in turn implemented with `CryptGenRandom`, and developers starting using `Guid.NewGuid` as an easy source of randomness seeded by a cryptographically-secure generator. On Linux, `Guid.NewGuid` was then implemented using data read from `/dev/urandom`, which is also intended to provide cryptographic-level entropy, but on macOS, due to performance problems on macOS with `/dev/urandom`, `Guid.NewGuid` was years ago switched to using `arc4random_buf`, which is for non-cryptographic purposes. It was decided in [dotnet/runtime#42770](https://github.com/dotnet/runtime/pull/42770) in the name of defense-in-depth security that `NewGuid` should revert back to using `/dev/urandom` on macOS and accept the resulting regression. Thankfully, it doesn’t have to accept it; as of [dotnet/runtime#51526](https://github.com/dotnet/runtime/pull/51526), `Guid.NewGuid` on macOS is now able to use CommonCrypto’s `CCRandomGenerateBytes`, which not only returns cryptographically-strong random bits, but is also comparable in performance to `arc4random_buf`, such that there shouldn’t be a perceivable impact release-over-release:

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| NewGuid | .NET 5.0 | 94.94 ns | 1.00 |
| NewGuid | .NET 6.0 | 96.32 ns | 1.01 |

Moving on in `System`, `Version` is another such example of just getting better and better every release. `Version.ToString`/`Version.TryFormat` had been using a cached `StringBuilder` for formatting. [dotnet/runtime#48511](https://github.com/dotnet/runtime/pull/48511) rewrote `TryFormat` to format directly into the caller-supplied span, rather than first formatting into a `StringBuilder` and then copying to the span. Then `ToString` was implemented as a wrapper for `TryFormat`, stack-allocating a span with enough space to hold any possible version, formatting into that, and then slicing and `ToString`‘ing that span to produce the final string. [dotnet/runtime#56051](https://github.com/dotnet/runtime/pull/56051) then further improved upon this by being a little more thoughtful about how the code was structured. For example, it had been using `Int32.TryFormat` to format each of the `int` version components (`Major`, `Minor`, `Build`, `Revision`), but these components are guaranteed to never be negative, so we could actually format them as `uint` with no difference in behavior. Why is that helpful here? Because there’s an extra non-inlined function call on the `int` code path than there is on the `uint` code path, due to the former needing to be able to handle negative rendering as well, and when you’re counting nanoseconds at this low-level of the stack, such calls can make a measurable difference.

```
private Version _version = new Version(6, 1, 21412, 16);

[Benchmark]
public string VersionToString() => _version.ToString();
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| VersionToString | .NET Framework 4.8 | 184.50 ns | 1.00 | 56 B |
| VersionToString | .NET Core 3.1 | 107.35 ns | 0.58 | 48 B |
| VersionToString | .NET 5.0 | 67.75 ns | 0.37 | 48 B |
| VersionToString | .NET 6.0 | 44.83 ns | 0.24 | 48 B |

One of my personal favorite sets of changes in .NET 6 is the overhauling of `System.Random`. There are many ways performance improvements can come about, and one of the most elusive but also impactful is completely changing the algorithm used to something much faster. Until .NET 6, `Random` employed the same algorithm it had been using for the last two decades, a variant of Knuth’s subtractive random number generator algorithm that dates back to the 1980s. That served .NET well, but it was time for an upgrade. In the intervening years, a myriad number of pseudo-random algorithms have emerged, and for .NET 6 in [dotnet/runtime#47085](https://github.com/dotnet/runtime/pull/47085), we picked the `xoshiro**` family, using `xoshiro128**` on 32-bit and `xoshiro256**` on 64-bit. These algorithms were introduced by [Blackman and Vigna in 2018](https://prng.di.unimi.it/), are very fast, and yield good enough pseudo-randomness for `Random`‘s needs (for cryptographically-secure random number generation, `System.Security.Cryptography.RandomNumberGenerator` should be used instead). However, beyond the algorithm employed, the implementation is now smarter about overheads. For good or bad reasons, `Random` was introduced with almost all of its methods virtual. In addition to that leading to virtual dispatch overheads, it has additional impact on the evolution of the type: because someone could have overridden one of the methods, any new method we introduce has to be written in terms of the existing virtuals… so, for example, when we added the span-based `NextBytes` method, we had to implement that in terms of one of the existing `Next` methods, to ensure that any existing overrides would have their behavior respected (imagine if we didn’t, and someone had a `ThreadSafeRandom` derived type, which overrode all the methods, and locked around each one… except for the ones unavailable at the time the derived type was created). Now in .NET 6, we check at construction time whether we’re dealing with a derived type, and fall back to the old implementation if this is a derived type, otherwise preferring to use an implementation that needn’t be concerned about such compatibility issues. Similarly, over the years we’ve been hesitant to change `Random`‘s implementation for fear of changing the numerical sequence yielded if someone provided a fixed seed to `Random`‘s constructor (which is common, for example, in tests); now in .NET 6, just as for derived types, we fall back to the old implementation if a seed is supplied, otherwise preferring the new algorithm. This sets us up for the future where we can freely change and evolve the algorithm used by `new Random()` as better approaches present themselves. On top of that, [dotnet/runtime#47390](https://github.com/dotnet/runtime/pull/47390) from [@colgreen](https://github.com/colgreen) tweaked the `NextBytes` implementation further to avoid unnecessary moves between locals and fields, yielding another significant gain in throughput.

```
private byte[] _buffer = new byte[10_000_000];
private Random _random = new Random();

[Benchmark]
public Random Ctor() => new Random();

[Benchmark]
public int Next() => _random.Next();

[Benchmark]
public int NextMax() => _random.Next(64);

[Benchmark]
public int NextMinMax() => _random.Next(0, 64);

[Benchmark]
public double NextDouble() => _random.NextDouble();

[Benchmark]
public void NextBytes_Array() => _random.NextBytes(_buffer);

[Benchmark]
public void NextBytes_Span() => _random.NextBytes((Span<byte>)_buffer);
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| Ctor | .NET 5.0 | 1,473.7 ns | 1.00 |
| Ctor | .NET 6.0 | 112.9 ns | 0.08 |
|  |  |  |  |
| Next | .NET 5.0 | 7.653 ns | 1.00 |
| Next | .NET 6.0 | 2.033 ns | 0.27 |
|  |  |  |  |
| NextMax | .NET 5.0 | 10.146 ns | 1.00 |
| NextMax | .NET 6.0 | 3.032 ns | 0.30 |
|  |  |  |  |
| NextMinMax | .NET 5.0 | 10.537 ns | 1.00 |
| NextMinMax | .NET 6.0 | 3.110 ns | 0.30 |
|  |  |  |  |
| NextDouble | .NET 5.0 | 8.682 ns | 1.00 |
| NextDouble | .NET 6.0 | 2.354 ns | 0.27 |
|  |  |  |  |
| NextBytes\_Array | .NET 5.0 | 72,202,543.956 ns | 1.00 |
| NextBytes\_Array | .NET 6.0 | 1,199,496.150 ns | 0.02 |
|  |  |  |  |
| NextBytes\_Span | .NET 5.0 | 76,654,821.111 ns | 1.00 |
| NextBytes\_Span | .NET 6.0 | 1,199,474.872 ns | 0.02 |

The `Random` changes also highlight tradeoffs made in optimizations. The approach of dynamically choosing the implementation to use when the instance is constructed means we incur an extra virtual dispatch on each operation. For the `new Random()` case that utilizes a new, faster algorithm, that overhead is well worth it and is much less than the significant savings incurred. But for the `new Random(seed)` case, we don’t have those algorithmic wins to offset things. As the overhead is small (on my machine 1-2ns) and as the scenarios for providing a seed are a minority use case in situations where counting nanoseconds matters (passing a specific seed is often used in testing, for example, where repeatable results are required), we accepted the tradeoff. But even the smallest, planned regressions can nag at you, especially when discussing them very publicly in a blog post, so in [dotnet/runtime#57530](https://github.com/dotnet/runtime/pull/57530) we mitigated most of them (basically everything other than the simplest seeded `Next()` overload, which on my machine is ~4% slower in .NET 6 than in .NET 5) and even managed to turn most into improvements. This was done primarily by splitting the compat strategy implementation further into one for `new Random(seed)` and one for `new DerivedRandom()`, which enables the former to avoid any virtual dispatch between members (and for the latter, derived types can override to provide their own completion implementation). As previously noted, a method like \`Next(int, int)\` delegates to another virtual method on the instance, but that virtual delegation can now be removed entirely for the seeded case as well.

In addition to changes in the implementation, `Random` also gained new surface area in .NET 6. This includes new `NextInt64` and `NextSingle` methods, but also `Random.Shared` ([dotnet/runtime#50297](https://github.com/dotnet/runtime/pull/50297)). The static `Random.Shared` property returns a thread-safe instance that can be used from any thread. This means code no longer needs to pay the overheads of creating a new `Random` instance when it might sporadically want to get a pseudo-random value, nor needs to manage its own scheme for caching and using `Random` instances in a thread-safe manner. Code can simply do `Random.Shared.Next()`.

```
[Benchmark(Baseline = true)]
public int NewNext() => new Random().Next();

[Benchmark]
public int SharedNext() => Random.Shared.Next();
```

| Method | Mean | Ratio | Allocated |
| --- | --- | --- | --- |
| NewNext | 114.713 ns | 1.00 | 72 B |
| SharedNext | 5.377 ns | 0.05 | – |

Next, `Environment` provides access to key information about the current machine and process. [dotnet/runtime#45057](https://github.com/dotnet/runtime/pull/45057) and [dotnet/runtime#49484](https://github.com/dotnet/runtime/pull/49484) updated `GetEnvironmentVariables` to use `IndexOf` to search for the separators between key/value pairs, rather than using an open-coded loop. In addition to reducing the amount of code needed in the implementation, this takes advantage of the fact that `IndexOf` is heavily optimized using a vectorized implementation. The net result is much faster retrieval of all environment variables: on my machine, with the environment variables I have in my environment, I get results like these:

```
[Benchmark]
public IDictionary GetEnvironmentVariables() => Environment.GetEnvironmentVariables();
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| GetEnvironmentVariables | .NET 5.0 | 35.04 us | 1.00 | 33 KB |
| GetEnvironmentVariables | .NET 6.0 | 13.43 us | 0.38 | 33 KB |

.NET 6 also sees new APIs added to `Environment` to provide not only simpler access to commonly-accessed information, but also much faster access. It’s pretty common for apps, for example in logging code, to want to get the current process’ ID. To achieve that prior to .NET 5, code would often do something like `Process.GetCurrentProcess().Id`, and .NET 5 added `Environment.ProcessId` to make that easier and faster. Similarly, code that wants access to the current process’ executable’s path would typically use code along the lines of `Process.GetCurrentProcess().MainModule.FileName`; now in .NET 6 with [dotnet/runtime#42768](https://github.com/dotnet/runtime/pull/42768), that code can just use `Environment.ProcessPath`:

```
[Benchmark(Baseline = true)]
public string GetPathViaProcess() => Process.GetCurrentProcess().MainModule.FileName;

[Benchmark]
public string GetPathViaEnvironment() => Environment.ProcessPath;
```

| Method | Mean | Ratio | Allocated |
| --- | --- | --- | --- |
| GetPathViaProcess | 85,570.951 ns | 1.000 | 1,072 B |
| GetPathViaEnvironment | 1.174 ns | 0.000 | – |

The .NET 6 SDK also includes new analyzers, introduced in [dotnet/roslyn-analyzers#4909](https://github.com/dotnet/roslyn-analyzers/pull/4909), to help find places these new APIs might be valuable. There are other new analyzers as well:

-   [dotnet/roslyn-analyzers#4764](https://github.com/dotnet/roslyn-analyzers/pull/4764) from [@NewellClark](https://github.com/NewellClark) to help find places `String.Concat` can be used with spans.
-   [dotnet/roslyn-analyzers#4806](https://github.com/dotnet/roslyn-analyzers/pull/4806) from [@NewellClark](https://github.com/NewellClark) to help find places `AsSpan` can be used instead of `Substring`.
-   [dotnet/roslyn-analyzers#5116](https://github.com/dotnet/roslyn-analyzers/pull/5116) from [@NewellClark](https://github.com/NewellClark) to help find places `String.Equals` can be used instead of `String.Compare`.
-   [dotnet/roslyn-analyzers#4908](https://github.com/dotnet/roslyn-analyzers/pull/4908) from [@MeikTranel](https://github.com/MeikTranel) to help find places `String.Contains` can be used with a `char` rather than a `string`.
-   [dotnet/roslyn-analyzers#4687](https://github.com/dotnet/roslyn-analyzers/pull/4687) from [@NewellClark](https://github.com/NewellClark) to help find places `Dictionary<,>.Keys.Contains` is used but `Dictionary<,>.ContainsKey` would suffice.
-   [dotnet/roslyn-analyzers#4726](https://github.com/dotnet/roslyn-analyzers/pull/4726) from [@MeikTranel](https://github.com/MeikTranel) to help find `Stream`\-derived types that would benefit from the `Memory`\-based `ReadAsync`/`WriteAsync` overloads being overridden.
-   [dotnet/roslyn-analyzers#4841](https://github.com/dotnet/roslyn-analyzers/pull/4841) from [@ryzngard](https://github.com/ryzngard) to help find places `Task.WhenAll` and `Task.WaitAll` are used unnecessarily.

`Enum` has also seen both improvements to the performance of its existing methods (so that existing usage just gets faster) and new methods added to it (such that minor tweaks to how it’s being consumed in an app can yield further fruit). [dotnet/runtime#44355](https://github.com/dotnet/runtime/pull/44355) is a small PR with a sizeable impact, improving the performance of the generic `Enum.IsDefined`, `Enum.GetName`, and `Enum.GetNames`. There were several issues to be addressed here. First, originally there weren’t any generic APIs on `Enum` (since it was introduced before generics existed), and thus all input values for methods like `IsDefined` or `GetName` were typed as `object`. That then meant that internal helpers for doing things like getting the numerical value of an enum were also typed to accept `object`. When the generic overloads came along in .NET 5, they utilized the same internal helpers, and ended up boxing the strongly-typed input as an implementation detail. This PR fixes that by adding a strongly-typed internal helper, and it tweaks what existing methods these generic methods delegate to so as to use ones that can operate faster given the strongly-typed nature of the generic methods. The net result is some nice wins.

```
private DayOfWeek _value = DayOfWeek.Friday;

[Benchmark]
public bool IsDefined() => Enum.IsDefined(_value);

[Benchmark]
public string GetName() => Enum.GetName(_value);

[Benchmark]
public string[] GetNames() => Enum.GetNames<DayOfWeek>();
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| IsDefined | .NET 5.0 | 31.46 ns | 1.00 | 24 B |
| IsDefined | .NET 6.0 | 19.30 ns | 0.61 | – |
|  |  |  |  |  |
| GetName | .NET 5.0 | 50.23 ns | 1.00 | 24 B |
| GetName | .NET 6.0 | 19.77 ns | 0.39 | – |
|  |  |  |  |  |
| GetNames | .NET 5.0 | 36.78 ns | 1.00 | 80 B |
| GetNames | .NET 6.0 | 21.04 ns | 0.57 | 80 B |

And via [dotnet/runtime#43255](https://github.com/dotnet/runtime/pull/43255) from [@hrrrrustic](https://github.com/hrrrrustic), .NET 6 also sees additional generic `Parse` and `TryParse` overloads added that can parse `ReadOnlySpan<char>` in addition to the existing support for `string`. While not directly faster than their `string`\-based counterparts (in fact, the `string`\-based implementations eventually call into the same `ReadOnlySpan<char>`\-based logic), they enable code parsing out enums from larger strings to do so with zero additional allocations and copies.

Another very common operation in many apps is `DateTime.UtcNow` and `DateTimeOffset.UtcNow`, often used as part of tracing or logging code that’s designed to add as little overhead as possible. [dotnet/runtime#45479](https://github.com/dotnet/runtime/pull/45479) and [dotnet/runtime#45281](https://github.com/dotnet/runtime/pull/45281) streamlined `DateTime.UtcNow` and `DateTimeOffset.UtcNow`, respectively, by avoiding some duplicative validation, ensuring fast paths are appropriately inlined (and slow paths aren’t), and other such tweaks. Those changes impacted all operating systems. But the biggest impact came from negating the regressions incurred when leap seconds support was added in .NET Core 3.0 ([dotnet/coreclr#21420](https://github.com/dotnet/coreclr/pull/21420)). “Leap seconds” are rare, one-second adjustments made to UTC that stem from the fact that the Earth’s rotation speed can and does actually vary over time. When this support was added to .NET Core 3.0 (and to .NET Framework 4.8 at the same time), it (knowingly) regressed the performance of `UtcNow` by around 2.5x when the Windows feature is enabled. Thankfully, in .NET 6, [dotnet/runtime#50263](https://github.com/dotnet/runtime/pull/50263) provides a scheme for still maintaining the leap seconds support while avoiding the impactful overhead, getting back to the same throughput as without the feature.

```
[Benchmark]
public DateTime UtcNow() => DateTime.UtcNow;
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| UtcNow | .NET Core 2.1 | 20.96 ns | 0.40 |
| UtcNow | .NET Framework 4.8 | 52.25 ns | 1.00 |
| UtcNow | .NET Core 3.1 | 63.35 ns | 1.21 |
| UtcNow | .NET 5.0 | 58.22 ns | 1.11 |
| UtcNow | .NET 6.0 | 19.95 ns | 0.38 |

Other small but valuable changes have gone into various primitives. For example, the newly public `ISpanFormattable` interface was previously internal and implemented on a handful of primitive types, but it’s now also implemented by `Char` and `Rune` as of [dotnet/runtime#50272](https://github.com/dotnet/runtime/pull/50272), and by `IntPtr` and `UIntPtr` as of [dotnet/runtime#44496](https://github.com/dotnet/runtime/pull/44496). `ISpanFormattable` is already recognized by various string formatting implementations, including that used by `string.Format`; you can see the impact of these interface implementations with a little benchmark, which gets better on .NET 6 as each instance’s `TryFormat` is used to format directly into the target buffer rather than first having to `ToString`.

```
[Benchmark]
public string Format() => string.Format("{0} {1} {2} {3}", 'a', (Rune)'b', (nint)'c', (nuint)'d');
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| Format | .NET Core 3.1 | 212.3 ns | 1.00 | 312 B |
| Format | .NET 5.0 | 179.7 ns | 0.85 | 312 B |
| Format | .NET 6.0 | 137.1 ns | 0.65 | 200 B |

### Arrays, Strings, Spans

For many apps and services, creating and manipulating arrays, strings, and spans represent a significant portion of their processing, and lot of effort goes into finding ways to continually drive down the costs of these operations. .NET 6 is no exception.

Let’s start with `Array.Clear`. The current `Array.Clear` signature accepts the `Array` to clear, the starting position, and the number of elements to clear. However, if you look at usage, the vast majority use case is with code like `Array.Clear(array, 0, array.Length)`… in other words, clearing the whole array. For a fundamental operation that’s used on hot paths, the extra validation that’s required in order to ensure the offset and count are in-bounds adds up. [dotnet/runtime#51548](https://github.com/dotnet/runtime/pull/51548) and [dotnet/runtime#53388](https://github.com/dotnet/runtime/pull/53388) add a new `Array.Clear(Array)` method that avoids these overheads and changes many call sites across dotnet/runtime to use the new overload.

```
private int[] _array = new int[10];

[Benchmark(Baseline = true)]
public void Old() => Array.Clear(_array, 0, _array.Length);

[Benchmark]
public void New() => Array.Clear(_array);
```

| Method | Mean | Ratio |
| --- | --- | --- |
| Old | 5.563 ns | 1.00 |
| New | 3.775 ns | 0.68 |

In a similar vein is `Span<T>.Fill`, which doesn’t just zero but sets every element to a specific value. [dotnet/runtime#51365](https://github.com/dotnet/runtime/pull/51365) provides a significant improvement here: while for `byte[]` it’s already been able to directly invoke the `initblk` (`memset`) implementation, which is vectorized, for other `T[]` arrays where `T` is a primitive type (e.g. `char`), it can now also use a vectorized implementation, leading to quite nice speedups. Then [dotnet/runtime#52590](https://github.com/dotnet/runtime/pull/52590) from [@xtqqczze](https://github.com/xtqqczze) reuses `Span<T>.Fill` as the underlying implementation for `Array.Fill<T>` as well.

```
private char[] _array = new char[128];
private char _c = 'c';

[Benchmark]
public void SpanFill() => _array.AsSpan().Fill(_c);

[Benchmark]
public void ArrayFill() => Array.Fill(_array, _c);
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| SpanFill | .NET 5.0 | 32.103 ns | 1.00 |
| SpanFill | .NET 6.0 | 3.675 ns | 0.11 |
|  |  |  |  |
| ArrayFill | .NET 5.0 | 55.994 ns | 1.00 |
| ArrayFill | .NET 6.0 | 3.810 ns | 0.07 |

Interestingly, `Array.Fill<T>` can’t simply delegate to `Span<T>.Fill`, for a reason that’s relevant to others looking to rebase array-based implementations on top of (mutable) spans. Arrays of reference types in .NET are covariant, meaning given a reference type `B` that derives from `A`, you can write code like:

```
var arrB = new B[4];
A[] arrA = arrB;
```

Now you’ve got an `A[]` where you can happily read out instances as `A`s but that can only store `B` instances, e.g. this is fine:

```
arrA[0] = new B();
```

but this will throw an exception:

```
arrA[0] = new A();
```

along the lines of `System.ArrayTypeMismatchException: Attempted to access an element as a type incompatible with the array.` This also incurs measurable overhead every time an element is stored into an array of (most) reference types. When spans were introduced, it was recognized that if you create a writeable span, you’re very likely going to write to it, and thus if the cost of a check needs to be paid somewhere, it’s better to pay that cost once when the span is created rather than on every write into the span. As such, `Span<T>` is invariant and its constructor includes this code:

```
if (!typeof(T).IsValueType && array.GetType() != typeof(T[]))
    ThrowHelper.ThrowArrayTypeMismatchException();
```

The check, which is removed entirely by the JIT for value types and which is optimized heavily by the JIT for reference types, validates that the `T` specified matches the concrete type of the array. As an example, if you write this code:

```
new Span<A>(new B[4]);
```

that will throw an exception. Why is this relevant to `Array.Fill<T>`? It can accept arbitrary `T[]` arrays, and there’s no guarantee that the `T` exactly matches the array type, e.g.

```
var arr = new B[4];
Array.Fill<A>(new B[4], null);
```

If `Array.Fill<T>` were implemented purely as `new Span<T>(array).Fill(value)`, the above code would throw an exception from `Span<T>`‘s constructor. Instead, `Array.Fill<T>` itself performs the same check that `Span<T>`‘s constructor does; if the check passes, it creates the `Span<T>` and calls `Fill`, but if the check doesn’t pass, it falls back to a typical loop, writing the value into each element of the array.

As long as we’re on the topic of vectorization, other support in this release has been vectorized. [dotnet/runtime#44111](https://github.com/dotnet/runtime/pull/44111) takes advantage of SSSE3 hardware intrinsics (e.g. `Ssse3.Shuffle`) to optimize the implementation of the internal `HexConverter.EncodeToUtf16` which is used in a few places, including the public `Convert.ToHexString`:

```
private byte[] _data;

[GlobalSetup]
public void Setup()
{
    _data = new byte[64];
    RandomNumberGenerator.Fill(_data);
}

[Benchmark]
public string ToHexString() => Convert.ToHexString(_data);
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| ToHexString | .NET 5.0 | 130.89 ns | 1.00 |
| ToHexString | .NET 6.0 | 44.78 ns | 0.34 |

[dotnet/runtime#44088](https://github.com/dotnet/runtime/pull/44088) also takes advantage of vectorization, though indirectly, by using the already vectorized `IndexOf` methods to improve the performance of `String.Replace(String, String)`. This PR is another good example of “optimizations” frequently being tradeoffs, making some scenarios faster at the expense of making others slower, and needing to make a decision based on the expected frequency of these scenarios occurring. In this case, the PR improves three specific cases significantly:

-   If both inputs are just a single character (e.g. `str.Replace("\n", " ")`), then it can delegate to the already-optimized `String.Replace(char, char)` overload.
-   If the `oldValue` is a single character, the implementation can use `IndexOf(char)` to find it, rather than using a hand-rolled loop.
-   If the `oldValue` is multiple characters, the implementation can use the equivalent of `IndexOf(string, StringComparison.Ordinal)` to find it.

The second and third bullet points significantly speed up operation if the `oldValue` being searched for isn’t super frequent in the input, enabling the vectorization to pay for itself and more. If, however, it’s very frequent (like every or every other character in the input), this change can actually regress performance. Our bet, based on reviewing use cases in a variety of code bases, is this overall will be a very positive win.

```
private string _str;

[GlobalSetup]
public async Task Setup()
{
    using var hc = new HttpClient();
    _str = await hc.GetStringAsync("https://www.gutenberg.org/cache/epub/3200/pg3200.txt"); // The Entire Project Gutenberg Works of Mark Twain
}

[Benchmark]
public string Yell() => _str.Replace(".", "!");

[Benchmark]
public string ConcatLines() => _str.Replace("\n", "");

[Benchmark]
public string NormalizeEndings() => _str.Replace("\r\n", "\n");
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| Yell | .NET 5.0 | 32.85 ms | 1.00 |
| Yell | .NET 6.0 | 16.99 ms | 0.52 |
|  |  |  |  |
| ConcatLines | .NET 5.0 | 34.36 ms | 1.00 |
| ConcatLines | .NET 6.0 | 22.93 ms | 0.67 |
|  |  |  |  |
| NormalizeEndings | .NET 5.0 | 33.09 ms | 1.00 |
| NormalizeEndings | .NET 6.0 | 23.61 ms | 0.71 |

Also for vectorization, previous .NET releases saw vectorization added to various algorithms in `System.Text.Encodings.Web`, but specifically employing x86 hardware intrinsics, such that these optimizations didn’t end up applying on ARM. [dotnet/runtime#49847](https://github.com/dotnet/runtime/pull/49847) now augments that with support from the `AdvSimd` hardware intrinsics, enabling similar speedups on ARM64 devices. And as long as we’re looking at `System.Text.Encodings.Web`, it’s worth calling out [dotnet/runtime#49373](https://github.com/dotnet/runtime/pull/49373), which completely overhauls the implementation of the library, with a primary goal of significantly reducing the amount of `unsafe` code involved; in the process, however, as we’ve seen now time and again, using spans and other modern practices to replace `unsafe` pointer-based code often not only makes the code simpler and safer but also faster. Part of the change involved vectorizing the “skip over all ASCII chars which don’t require encoding” logic that all of the encoders utilize, helping to yield some significant speedups in common scenarios.

```
private string _text;

[Params("HTML", "URL", "JSON")]
public string Encoder { get; set; }

private TextEncoder _encoder;

[GlobalSetup]
public async Task Setup()
{
    using (var hc = new HttpClient())
        _text = await hc.GetStringAsync("https://www.gutenberg.org/cache/epub/3200/pg3200.txt");

    _encoder = Encoder switch
    {
        "HTML" => HtmlEncoder.Default,
        "URL" => UrlEncoder.Default,
        _ => JavaScriptEncoder.Default,
    };
}

[Benchmark]
public string Encode() => _encoder.Encode(_text);
```

| Method | Runtime | Encoder | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- | --- |
| Encode | .NET Core 3.1 | HTML | 106.44 ms | 1.00 | 128 MB |
| Encode | .NET 5.0 | HTML | 101.58 ms | 0.96 | 128 MB |
| Encode | .NET 6.0 | HTML | 43.97 ms | 0.41 | 36 MB |
|  |  |  |  |  |  |
| Encode | .NET Core 3.1 | JSON | 113.70 ms | 1.00 | 124 MB |
| Encode | .NET 5.0 | JSON | 96.36 ms | 0.85 | 124 MB |
| Encode | .NET 6.0 | JSON | 39.73 ms | 0.35 | 33 MB |
|  |  |  |  |  |  |
| Encode | .NET Core 3.1 | URL | 165.60 ms | 1.00 | 136 MB |
| Encode | .NET 5.0 | URL | 141.26 ms | 0.85 | 136 MB |
| Encode | .NET 6.0 | URL | 70.63 ms | 0.43 | 44 MB |

Another `string` API that’s been enhanced for .NET 6 is `string.Join`. One of the `Join` overloads takes the strings to be joined as an `IEnumerable<string?>`, which it iterates, appending to a builder as it goes. But there’s already a separate array-based code path that does two passes over the strings, one to count the size required and then another to fill in the resulting string of the required length. [dotnet/runtime#44032](https://github.com/dotnet/runtime/pull/44032) converts that functionality to be based on a `ReadOnlySpan<string?>` rather than `string?[]`, and then special-cases enumerables that are actually `List<string?>` to go through the span-based path as well, utilizing the `CollectionsMarshal.AsSpan` method to get a span for the `List<string?>`‘s backing array. [dotnet/runtime#56857](https://github.com/dotnet/runtime/pull/56857) then does the same for the `IEnumerable<T>`\-based overload.

```
private List<string> _strings = new List<string>() { "Hi", "How", "are", "you", "today" };

[Benchmark]
public string Join() => string.Join(", ", _strings);
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| Join | .NET Framework 4.8 | 124.81 ns | 1.00 | 120 B |
| Join | .NET 5.0 | 123.54 ns | 0.99 | 112 B |
| Join | .NET 6.0 | 51.08 ns | 0.41 | 72 B |

One of the biggest string-related improvements, though, comes from the new interpolated string handler support in C# 10 and .NET 6, with new language support added in [dotnet/roslyn#54692](https://github.com/dotnet/roslyn/pull/54692) and library support added in [dotnet/runtime#51086](https://github.com/dotnet/runtime/pull/51086) and [dotnet/runtime#51653](https://github.com/dotnet/runtime/pull/51653). If I write:

```
static string Format(int major, int minor, int build, int revision) =>
    $"{major}.{minor}.{build}.{revision}";
```

C# 9 would compile that as:

```
static string Format(int major, int minor, int build, int revision)
{
    var array = new object[4];
    array[0] = major;
    array[1] = minor;
    array[2] = build;
    array[3] = revision;
    return string.Format("{0}.{1}.{2}.{3}", array);
}
```

which incurs a variety of overheads, such as having to parse the composite format string on every call at run-time, box each of the `int`s, and allocate an array to store them. With C# 10 and .NET 6, that’s instead compiled as:

```
static string Format(int major, int minor, int build, int revision)
{
    var h = new DefaultInterpolatedStringHandler(3, 4);
    h.AppendFormatted(major);
    h.AppendLiteral(".");
    h.AppendFormatted(minor);
    h.AppendLiteral(".");
    h.AppendFormatted(build);
    h.AppendLiteral(".");
    h.AppendFormatted(revision);
    return h.ToStringAndClear();
}
```

with all of the parsing handled at compile-time, no additional array allocation, and no additional boxing allocations. You can see the impact of the changes with the aforementioned examples turned into a benchmark:

```
private int Major = 6, Minor = 0, Build = 100, Revision = 21380;

[Benchmark(Baseline = true)]
public string Old()
{
    object[] array = new object[4];
    array[0] = Major;
    array[1] = Minor;
    array[2] = Build;
    array[3] = Revision;
    return string.Format("{0}.{1}.{2}.{3}", array);
}

[Benchmark]
public string New()
{
    var h = new DefaultInterpolatedStringHandler(3, 4);
    h.AppendFormatted(Major);
    h.AppendLiteral(".");
    h.AppendFormatted(Minor);
    h.AppendLiteral(".");
    h.AppendFormatted(Build);
    h.AppendLiteral(".");
    h.AppendFormatted(Revision);
    return h.ToStringAndClear();
}
```

| Method | Mean | Ratio | Allocated |
| --- | --- | --- | --- |
| Old | 127.31 ns | 1.00 | 200 B |
| New | 69.62 ns | 0.55 | 48 B |

For an in-depth look, including discussion of various custom interpolated string handlers built-in to .NET 6 for improved support with `StringBuilder`, `Debug.Assert`, and `MemoryExtensions`, see the [String Interpolation in C# 10 and .NET 6](https://devblogs.microsoft.com/dotnet/string-interpolation-in-c-10-and-net-6).

### Buffering

Performance improvements can manifest in many ways: increasing throughput, reducing working set, reducing latencies, increasing startup speeds, lowering size on disk, and so on. Anyone paying attention to the performance of .NET will also notice a focus on reducing allocation. This is typically a means to an end rather than a goal in and of itself, as managed allocations themselves are easily trackable / measurable and incur varying costs, in particular the secondary cost of causing GCs to happen more frequently and/or take longer periods of time. Sometimes reducing allocations falls into the category of just stopping doing unnecessary work, or doing something instead that’s way cheaper; for example, [dotnet/runtime#42776](https://github.com/dotnet/runtime/pull/42776) changed an eight-byte array allocation to an eight-byte stack-allocation, the latter of which is very close to free (in particular as this code is compiled with `[SkipLocalsInit]` and thus doesn’t need to pay to zero out that stack-allocated space). Beyond that, though, there are almost always real tradeoffs. One common technique is pooling, which can look great on microbenchmarks because it drives down that allocation number, but it doesn’t always translate into a measurable improvement in one of the other metrics that’s actually an end goal. In fact, it can make things worse, such as if the overhead of renting and returning from the pool is higher than expected (especially if it incurs synchronization costs), if it leads to cache problems as something returned on one NUMA node ends up being consumed from another, if it leads to GCs taking longer by increasing the number of references from Gen1 or Gen2 objects to Gen0 objects, and so on. However, one place that pooling has shown to be quite effective is with arrays, in particular larger arrays of value types (e.g. `byte[]`, `char[]`), which has led to `ArrayPool<T>.Shared` being used _everywhere_. This places a high premium on `ArrayPool<T>.Shared` being as efficient as possible, and this release sees several impactful improvements in this area.

Probably the most visible change in this area in .NET 6 is the for-all-intents-and-purposes removal of the upper limit on the size of arrays `ArrayPool<T>.Shared` will cache. Previously, `ArrayPool<T>.Shared` would only cache up to approximately one million elements (`1024 * 1024`), a fact evident from this test run on .NET 5:

```
[Benchmark(Baseline = true)]
public void RentReturn_1048576() => ArrayPool<byte>.Shared.Return(ArrayPool<byte>.Shared.Rent(1024 * 1024));

[Benchmark]
public void RentReturn_1048577() => ArrayPool<byte>.Shared.Return(ArrayPool<byte>.Shared.Rent(1024 * 1024 + 1));
```

| Method | Mean | Ratio | Allocated |
| --- | --- | --- | --- |
| RentReturn\_1048576 | 21.90 ns | 1.00 | – |
| RentReturn\_1048577 | 18,210.30 ns | 883.37 | 1,048,598 B |

Ouch. That is a large cliff to fall off of, and either the developer is aware of the cliff and is forced to adapt to it in their code, or they’re not aware of it and end up having unexpected performance problems. This somewhat arbitrary limit was originally put in place before the pool had “trimming,” a mechanism that enabled the pool to drop cached arrays in response to Gen2 GCs, with varying levels of aggressiveness based on perceived memory pressure. But then that trimming was added, and the limit was never revisited… until now. [dotnet/runtime#55621](https://github.com/dotnet/runtime/pull/55621) raises the limit as high as the current implementation’s scheme enables, which means it can now cache arrays up to approximately one billion elements (`1024 * 1024 * 1024`); that should hopefully be larger than almost anyone wants to pool.

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| RentReturn\_1048576 | .NET 5.0 | 21.01 ns | 1.00 | – |
| RentReturn\_1048576 | .NET 6.0 | 16.36 ns | 0.78 | – |
|  |  |  |  |  |
| RentReturn\_1048577 | .NET 5.0 | 12,132.90 ns | 1.000 | 1,048,593 B |
| RentReturn\_1048577 | .NET 6.0 | 16.38 ns | 0.002 | – |

Of course, pooling such arrays means it’s important that trimming works as expected, and while there’s an unending amount of tuning we could do to the trimming heuristics, the main gap that stood out had to do with how arrays in the pool are stored. With today’s implementation, the pool is divided into buckets with sizes equal to powers of two, so for example there’s a bucket for arrays with a length up to 16, then up to 32, then up to 64, and so on: requesting an array of size 100 will garner you an array of size 128. The pool is also split into two layers. The first layer is stored in thread-local storage, where each thread can store at most one array of each bucket size. The second layer is itself split into `Environment.ProcessorCount` stacks, each of which is logically associated with one core, and each of which is individually synchronized. Code renting an array first consults the thread-local storage slot, and if it’s unable to get an array from there, proceeds to examine each of the stacks, starting with the one associated with the core it’s currently running on (which can of course change at any moment, so the affinity is quite soft and accesses require synchronization). Upon returning an array, a similar path is followed, with the code first trying to return to the thread-local slot, and then proceeding to try to find space in one of the stacks. The trimming implementation in .NET 5 and earlier is able to remove arrays from the stacks, and is given the opportunity on every Gen2 GC, but it will only ever drop arrays from the thread-local storage if there’s very high memory pressure. This can lead to some rarely-used arrays sticking around for a very long time, negatively impacting working set. [dotnet/runtime#56316](https://github.com/dotnet/runtime/pull/56316) addresses this by tracking approximately how long arrays have been sitting in thread-local storage, and enabling them to be culled regardless of high memory pressure, instead using memory pressure to indicate what’s an acceptable duration for an array to remain.

On top of these changes around what can be cached and for how long, more typical performance optimizations have also been done. [dotnet/runtime#55710](https://github.com/dotnet/runtime/pull/55710) and [dotnet/runtime#55959](https://github.com/dotnet/runtime/pull/55959) reduced typical overheads for renting and returning arrays. This entailed paying attention to where and why bounds checks were happening and avoiding them where possible, ordering checks performed to prioritize common cases (e.g. a request for a pooled size) over rare cases (e.g. a request for a size of 0), and reducing code size to make better use of instruction caches.

```
[Benchmark]
public void RentReturn_Single() => ArrayPool<char>.Shared.Return(ArrayPool<char>.Shared.Rent(4096));

private char[][] _arrays = new char[4][];

[Benchmark]
public void RentReturn_Multi()
{
    char[][] arrays = _arrays;

    for (int i = 0; i < arrays.Length; i++)
        arrays[i] = ArrayPool<char>.Shared.Rent(4096);

    for (int i = 0; i < arrays.Length; i++)
        ArrayPool<char>.Shared.Return(arrays[i]);
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| RentReturn\_Single | .NET Core 3.1 | 23.60 ns | 1.00 | – |
| RentReturn\_Single | .NET 5.0 | 18.48 ns | 0.78 | – |
| RentReturn\_Single | .NET 6.0 | 16.27 ns | 0.69 | – |
|  |  |  |  |  |
| RentReturn\_Multi | .NET Core 3.1 | 248.57 ns | 1.00 | – |
| RentReturn\_Multi | .NET 5.0 | 204.13 ns | 0.82 | – |
| RentReturn\_Multi | .NET 6.0 | 197.21 ns | 0.79 | – |

### IO

A good deal of effort in .NET 6 has gone into fixing the performance of one of the oldest types in .NET: `FileStream`. Every app and service reads and writes files. Unfortunately, `FileStream` has also been plagued over the years by numerous performance-related issues, most of which are part of its asynchronous I/O implementation on Windows. For example, a call to `ReadAsync` might have issued an overlapped I/O read operation, but typically that read would end up then blocking in a sync-over-async manner, in order to avoid potential race conditions in the implementation that could otherwise result. Or when flushing its buffer, even when flushing asynchronously, those flushes would end up doing synchronous writes. Such issues often ended up defeating any scalability benefits of using asynchronous I/O while still incurring the overheads associated with asynchronous I/O (async I/O often has higher overheads in exchange for being more scalable). All of this was complicated further by the `FileStream` code being a tangled web difficult to unravel, in large part because it was trying to integrate a bunch of different capabilities into the same code paths: using overlapped I/O or not, buffering or not, targeting disk files or pipes, etc., with different logic for each, all interwined. Combined, this has meant that, with a few exceptions, the `FileStream` code has remained largely untouched, until now.

.NET 6 sees `FileStream` entirely rewritten, and in the process, all of these issues resolved. The result is a much more maintainable implementation that’s also dramatically faster, in particular for asynchronous operations. There have been a plethora of PRs as part of this effort, but I’ll call out a few. First [dotnet/runtime#47128](https://github.com/dotnet/runtime/pull/47128) laid the groundwork for the new implementation, refactoring `FileStream` to be a wrapper around a “strategy” (as in the Strategy design pattern), which then enables the actual implementation to be substituted and composed at runtime (similar to the approach discussed with `Random`), with the existing implementation moved into one strategy that can be used in .NET 6 if maximum compatibility is required (it’s off by default but can be enabled with an environment variable or `AppContext` switch). [dotnet/runtime#48813](https://github.com/dotnet/runtime/pull/48813) and [dotnet/runtime#49750](https://github.com/dotnet/runtime/pull/49750) then introduced the beginnings of the new implementation, splitting it apart into several strategies on Windows, one for if the file was opened for synchronous I/O, one for if it was opened for asynchronous I/O, and one that enabled buffering to be layered on top of any strategy. [dotnet/runtime#55191](https://github.com/dotnet/runtime/pull/55191) then introduced a Unix-optimized strategy for the new scheme. All the while, additional PRs were flowing in to optimize various conditions. [dotnet/runtime#49975](https://github.com/dotnet/runtime/pull/49975) and [dotnet/runtime#56465](https://github.com/dotnet/runtime/pull/56465) avoided an expensive syscall made on every operation on Windows to track the file’s length, while [dotnet/runtime#44097](https://github.com/dotnet/runtime/pull/44097) removed an unnecessary seek when setting file length on Unix. [dotnet/runtime#50802](https://github.com/dotnet/runtime/pull/50802) and [dotnet/runtime#51363](https://github.com/dotnet/runtime/pull/51363) changed the overlapped I/O implementation on Windows to use a custom, reusable `IValueTaskSource`\-based implementation rather than one based on `TaskCompletionSource`, which enabled making (non-buffered) async reads and writes amortized-allocation-free when using async I/O. [dotnet/runtime#55206](https://github.com/dotnet/runtime/pull/55206) from [@tmds](https://github.com/tmds) used knowledge from an existing syscall being made on Unix to then avoid a subsequent unnecessary `stat` system call. [dotnet/runtime#56095](https://github.com/dotnet/runtime/pull/56095) took advantage of the new `PoolingAsyncValueTaskMethodBuilder` previously discussed to reduce allocations involved in async operations on `FileStream` when buffering is being used (which is the default). [dotnet/runtime#56387](https://github.com/dotnet/runtime/pull/56387) avoided a `ReadFile` call on Windows if we already had enough information to prove nothing would be available to read. And [dotnet/runtime#56682](https://github.com/dotnet/runtime/pull/56682) took the same optimizations that had been done for `Read/WriteAsync` on Unix and applied them to Windows as well when the `FileStream` was opened for synchronous I/O. In the end, all of this adds up to huge maintainability benefits for `FileStream`, huge performance improvements for `FileStream` (in particular for but not limited to asynchronous operations), and much better scalability for `FileStream`. Here are just a few microbenchmarks to highlight some of the impact:

```
private FileStream _fileStream;
private byte[] _buffer = new byte[1024];

[Params(false, true)]
public bool IsAsync { get; set; }

[Params(1, 4096)]
public int BufferSize { get; set; }

[GlobalSetup]
public void Setup()
{
    byte[] data = new byte[10_000_000];
    new Random(42).NextBytes(data);

    string path = Path.GetTempFileName();
    File.WriteAllBytes(path, data);

    _fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, IsAsync);
}

[GlobalCleanup]
public void Cleanup()
{
    _fileStream.Dispose();
    File.Delete(_fileStream.Name);
}

[Benchmark]
public void Read()
{
    _fileStream.Position = 0;
    while (_fileStream.Read(_buffer
#if !NETCOREAPP2_1_OR_GREATER
            , 0, _buffer.Length
#endif
            ) != 0) ;
}

[Benchmark]
public async Task ReadAsync()
{
    _fileStream.Position = 0;
    while (await _fileStream.ReadAsync(_buffer
#if !NETCOREAPP2_1_OR_GREATER
            , 0, _buffer.Length
#endif
            ) != 0) ;
}
```

| Method | Runtime | IsAsync | BufferSize | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- | --- | --- |
| Read | .NET Framework 4.8 | False | 1 | 30.717 ms | 1.00 | – |
| Read | .NET Core 3.1 | False | 1 | 30.745 ms | 1.00 | – |
| Read | .NET 5.0 | False | 1 | 31.156 ms | 1.01 | – |
| Read | .NET 6.0 | False | 1 | 30.772 ms | 1.00 | – |
|  |  |  |  |  |  |  |
| ReadAsync | .NET Framework 4.8 | False | 1 | 50.806 ms | 1.00 | 2,125,865 B |
| ReadAsync | .NET Core 3.1 | False | 1 | 44.505 ms | 0.88 | 1,953,592 B |
| ReadAsync | .NET 5.0 | False | 1 | 39.212 ms | 0.77 | 1,094,096 B |
| ReadAsync | .NET 6.0 | False | 1 | 36.018 ms | 0.71 | 247 B |
|  |  |  |  |  |  |  |
| Read | .NET Framework 4.8 | False | 4096 | 9.593 ms | 1.00 | – |
| Read | .NET Core 3.1 | False | 4096 | 9.761 ms | 1.02 | – |
| Read | .NET 5.0 | False | 4096 | 9.446 ms | 0.99 | – |
| Read | .NET 6.0 | False | 4096 | 9.569 ms | 1.00 | – |
|  |  |  |  |  |  |  |
| ReadAsync | .NET Framework 4.8 | False | 4096 | 30.920 ms | 1.00 | 2,141,481 B |
| ReadAsync | .NET Core 3.1 | False | 4096 | 23.758 ms | 0.81 | 1,953,592 B |
| ReadAsync | .NET 5.0 | False | 4096 | 25.101 ms | 0.82 | 1,094,096 B |
| ReadAsync | .NET 6.0 | False | 4096 | 13.108 ms | 0.42 | 382 B |
|  |  |  |  |  |  |  |
| Read | .NET Framework 4.8 | True | 1 | 413.228 ms | 1.00 | 2,121,728 B |
| Read | .NET Core 3.1 | True | 1 | 217.891 ms | 0.53 | 3,050,056 B |
| Read | .NET 5.0 | True | 1 | 219.388 ms | 0.53 | 3,062,741 B |
| Read | .NET 6.0 | True | 1 | 83.070 ms | 0.20 | 2,109,867 B |
|  |  |  |  |  |  |  |
| ReadAsync | .NET Framework 4.8 | True | 1 | 355.670 ms | 1.00 | 3,833,856 B |
| ReadAsync | .NET Core 3.1 | True | 1 | 262.625 ms | 0.74 | 3,048,120 B |
| ReadAsync | .NET 5.0 | True | 1 | 259.284 ms | 0.73 | 3,047,496 B |
| ReadAsync | .NET 6.0 | True | 1 | 119.573 ms | 0.34 | 403 B |
|  |  |  |  |  |  |  |
| Read | .NET Framework 4.8 | True | 4096 | 106.696 ms | 1.00 | 530,842 B |
| Read | .NET Core 3.1 | True | 4096 | 56.785 ms | 0.54 | 353,151 B |
| Read | .NET 5.0 | True | 4096 | 54.359 ms | 0.51 | 353,966 B |
| Read | .NET 6.0 | True | 4096 | 22.971 ms | 0.22 | 527,930 B |
|  |  |  |  |  |  |  |
| ReadAsync | .NET Framework 4.8 | True | 4096 | 143.082 ms | 1.00 | 3,026,980 B |
| ReadAsync | .NET Core 3.1 | True | 4096 | 55.370 ms | 0.38 | 355,001 B |
| ReadAsync | .NET 5.0 | True | 4096 | 54.436 ms | 0.38 | 354,036 B |
| ReadAsync | .NET 6.0 | True | 4096 | 32.478 ms | 0.23 | 420 B |

Some of the improvements in `FileStream` also involved moving the read/write aspects of its implementation out into a separate public class: `System.IO.RandomAccess`. Implemented in [dotnet/runtime#53669](https://github.com/dotnet/runtime/pull/53669), [dotnet/runtime#54266](https://github.com/dotnet/runtime/pull/54266), and [dotnet/runtime#55490](https://github.com/dotnet/runtime/pull/55490) (with additional optimizations in [dotnet/runtime#55123](https://github.com/dotnet/runtime/pull/55123) from [@teo-tsirpanis](https://github.com/teo-tsirpanis)), `RandomAccess` provides overloads that enable sync and async reading and writing, for both a single and multiple buffers at a time, and specifying the exact offset into the file at which the read or write should occur. All of these static methods accept a `SafeFileHandle`, which can now be obtained from the new `File.OpenHandle` method. This all means code is now able to access files without going through `FileStream` if the `Stream`\-based interface isn’t desirable, and it means code is able to issue concurrent reads or writes for the same `SafeFileHandle`, if parallel processing of a file is desired. Subsequent PRs like [dotnet/runtime#55150](https://github.com/dotnet/runtime/pull/55150) took advantage of these new APIs to avoid the extra allocations and complexity involved in using `FileStream` when all that was really needed was the handle and the ability to perform a single read or write. ([@adamsitnik](https://github.com/adamsitnik) is working on a dedicated blog post focused on these `FileStream` improvements; look for that on the [.NET Blog](https://devblogs.microsoft.com/dotnet/) soon.)

Of course, there’s more to working with files than just `FileStream`. [dotnet/runtime#55210](https://github.com/dotnet/runtime/pull/55210) from [@tmds](https://github.com/tmds) eliminated a `stat` syscall from `Directory/File.Exists` when the target doesn’t exist, [dotnet/runtime#47118](https://github.com/dotnet/runtime/pull/47118) from [@gukoff](https://github.com/gukoff) avoids a `rename` syscall when moving a file across volumes on Unix, and [dotnet/runtime#55644](https://github.com/dotnet/runtime/pull/55644) simplifies `File.WriteAllTextAsync` and makes it faster with less allocation (this benchmark of course also benefits from the `FileStream` improvements:

```
private static string s_contents = string.Concat(Enumerable.Range(0, 100_000).Select(i => (char)('a' + (i % 26))));
private static string s_path = Path.GetRandomFileName();

[Benchmark]
public Task WriteAllTextAsync() => File.WriteAllTextAsync(s_path, s_contents);
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| WriteAllTextAsync | .NET Core 3.1 | 1.609 ms | 1.00 | 23 KB |
| WriteAllTextAsync | .NET 5.0 | 1.590 ms | 1.00 | 23 KB |
| WriteAllTextAsync | .NET 6.0 | 1.143 ms | 0.72 | 15 KB |

And, of course, there’s more to I/O than just files. `NamedPipeServerStream` on Windows provides an overlapped I/O-based implementation very similar to that of `FileStream`. With `FileStream`‘s implementation being overhauled, [dotnet/runtime#52695](https://github.com/dotnet/runtime/pull/52695) from [@manandre](https://github.com/manandre) also overhauled the pipes implementation to mimic the same updated structure as that used in `FileStream`, and thereby incur many of the same benefits, in particular around allocation reduction due to a reusable `IValueTaskSource`\-based implementation rather than a `TaskCompletionSource`\-based implementation.

On the compression front, in addition to the introduction of the new `ZlibStream` ([dotnet/runtime#42717](https://github.com/dotnet/runtime/pull/42717)), the underlying `Brotli` code that’s used behind `BrotliStream`, `BrotliEncoder`, and `BrotliDecoder` was upgraded from v1.0.7 in [dotnet/runtime#44107](https://github.com/dotnet/runtime/pull/44107) from [@saucecontrol](https://github.com/saucecontrol) to v1.0.9. That upgrade brings with it various [performance improvements](https://github.com/google/brotli/releases/tag/v1.0.9), including code paths that make better use of intrinsics. Not all compression/decompression measurably benefits, but some certainly does:

```
private byte[] _toCompress;
private MemoryStream _destination = new MemoryStream();

[GlobalSetup]
public async Task Setup()
{
    using var hc = new HttpClient();
    _toCompress = await hc.GetByteArrayAsync(@"https://raw.githubusercontent.com/dotnet/performance/5584a8b201b8c9c1a805fae4868b30a678107c32/src/benchmarks/micro/corefx/System.IO.Compression/TestData/alice29.txt");
}

[Benchmark]
public void Compress()
{
    _destination.Position = 0;
    using var ds = new BrotliStream(_destination, CompressionLevel.Fastest, leaveOpen: true);
    ds.Write(_toCompress);
}
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| Compress | .NET 5.0 | 1,050.2 us | 1.00 |
| Compress | .NET 6.0 | 786.6 us | 0.75 |

[dotnet/runtime#47125](https://github.com/dotnet/runtime/pull/47125) from [@NewellClark](https://github.com/NewellClark) also added some missing overrides to various `Stream` types, including `DeflateStream`, which has an effect of reducing the overhead of `DeflateStream.WriteAsync`.

There’s one more interesting, performance-related improvement in `DeflateStream` (and `GZipStream` and `BrotliStream`). The `Stream` contract for asynchronous read operations is that, assuming you request at least one byte, the operation won’t complete until at least one byte is read; however, the contract makes no guarantees whatsoever that the operation will return all that you requested, in fact it’s rare to find a stream that does make such a guarantee, and it’s problematic in many cases when it does. Unfortunately, as an implementation detail, `DeflateStream` was in fact trying to return as much data as was requested, by issuing as many reads against the underlying stream as it needed to in order to make that happen, stopping only when it decoded a sufficient amount of data to satisfy the request or hit EOF (end of file) on the underlying stream. This is a problem for multiple reasons. First, it prevents overlapping the processing of any data that may have already been received with the waiting for more data to receive; if 100 bytes are already available, but I asked for 200, I’m then forced to wait to process the 100 until another 100 are received or the stream hits EOF. Second, and more impactful, is it effectively prevents `DeflateStream` from being used in any bidirectional communication scenario. Imagine a `DeflateStream` wrapped around a `NetworkStream`, and the stream is being used to send and receive compressed messages to and from a remote party. Let’s say I pass `DeflateStream` a 1K buffer, the remote party sends me a 100-byte message, and I’m supposed to read that message and respond (a response the remote party will be waiting for before sending me anything further). `DeflateStream`‘s behavior here will deadlock the whole system, as it will prevent the receipt of the 100-byte message waiting for another 900 bytes or EOF that will never arrive. [dotnet/runtime#53644](https://github.com/dotnet/runtime/pull/53644) fixes that by enabling `DeflateStream` (and a few other streams) to return once it has data to hand back, even if not the sum total requested. This has been [documented as a breaking change](https://docs.microsoft.com/dotnet/core/compatibility/core-libraries/6.0/partial-byte-reads-in-streams), not because the previous behavior was guaranteed (it wasn’t), but we’ve seen enough code erroneously depend on the old behavior that it was important to call out.

The PR also fixes one more performance-related thing. One issue scalable web servers need to be cognizant of is memory utilization. If you’ve got a thousand open connections, and you’re waiting for data to arrive on each connection, you could perform an asynchronous read on each using a buffer, but if that buffer is, say, 4K, that’s 4MB worth of buffers that are sitting there wasting working set. If you could instead issue a zero-byte read, where you perform an empty read simply to be notified when there is data that could be received, you can then avoid any working set impact from buffers, only allocating or renting a buffer to be used once you know there’s data to put in it. Lots of `Streams` intended for bidirectional communication, like `NetworkStream` and `SslStream`, support such zero-byte reads, not returning from an empty read operation until there’s at least one byte that could be read. For .NET 6, `DeflateStream` can now also be used in this capacity, with the PR changing the implementation to ensure that `DeflateStream` will still issue a read to its underlying `Stream` in the case the `DeflateStream`‘s output buffer is empty, even if the caller asked for zero bytes. Callers that don’t want this behavior can simply avoid making the 0-byte call.

Moving on, for `System.IO.Pipelines`, a couple of PRs improved performance. [dotnet/runtime#55086](https://github.com/dotnet/runtime/pull/55086) added overrides of `ReadByte` and `WriteByte` that avoid the asynchronous code paths when a byte to read is already buffered or space in the buffer is available to write the byte, respectively. And [dotnet/runtime#52159](https://github.com/dotnet/runtime/pull/52159) from [@manandre](https://github.com/manandre) added a `CopyToAsync` override to the `PipeReader` used for reading from `Stream`s, optimizing it to first copy whatever data was already buffered and then delegate to the `Stream`‘s `CopyToAsync`, taking advantage of whatever optimizations it may have.

Beyond that, there were a variety of small improvements. [dotnet/runtime#55373](https://github.com/dotnet/runtime/pull/55373) and [dotnet/runtime#56568](https://github.com/dotnet/runtime/pull/56568) from [@steveberdy](https://github.com/steveberdy) removed unnecessary `Contains('\0')` calls from `Path.GetFullPath(string, string)`; [dotnet/runtime#54991](https://github.com/dotnet/runtime/pull/54991) from [@lateapexearlyspeed](https://github.com/lateapexearlyspeed) improved `BufferedStream.Position`‘s setter to avoid pitching buffered read data if it would still be valuable for the new position; [dotnet/runtime#55147](https://github.com/dotnet/runtime/pull/55147) removed some casting overhead from the base `Stream` type; [dotnet/runtime#53070](https://github.com/dotnet/runtime/pull/53070) from [@DavidKarlas](https://github.com/DavidKarlas) avoided unnecessarily roundtripping a file time through local time in `File.GetLastWriteTimeUtc` on Unix; and [dotnet/runtime#43968](https://github.com/dotnet/runtime/pull/43968) consolidating the argument validation logic for derived `Stream` types into public helpers (`Stream.ValidateBufferArguments` and `Stream.ValidateCopyToArguments`), which, in addition to eliminating duplicated code and helping to ensure consistency of behavior, helps to streamline the validation logic using a shared, efficient implementation of the relevant checks.

### Networking

Let’s turn our attention to networking. It goes without saying that networking is at the heart of services and most significant apps today, and so improvements in networking performance are critical to the platform.

At the bottom of the networking stack, we have `System.Net.Sockets`. One of my favorite sets of changes in this release is that we finally rid the System.Net.Sockets.dll assembly of all custom `IAsyncResult` implementations; all of the remaining places where `Begin/EndXx` methods provided an implementation and a `Task`\-based `XxAsync` method wrapped that `Begin/EndXx` have now been flipped, with the `XxAsync` method providing the implementation, and the `Begin/EndXx` methods just delegating to the `Task`\-based methods. So, for example, [dotnet/runtime#43886](https://github.com/dotnet/runtime/pull/43886) reimplemented `Socket.BeginSend/EndSend` and `Socket.BeginReceive/EndReceive` as wrappers for `Socket.SendAsync` and `Socket.ReceiveAsync`, and [dotnet/runtime#43661](https://github.com/dotnet/runtime/pull/43661) rewrote `Socket.ConnectAsync` using tasks and `async/await`, and then `Begin/EndConnect` were just implemented in terms of that. Similarly, [dotnet/runtime#53340](https://github.com/dotnet/runtime/pull/53340) added new `AcceptAsync` overloads that are not only task-based but also cancelable (a long requested feature), and [dotnet/runtime#51212](https://github.com/dotnet/runtime/pull/51212) then deleted a lot of code by having the `Begin/EndAccept` methods just use the task-based implementation. These changes not only reduced the size of the assembly, reduced dependencies from System.Net.Sockets.dll (the custom `IAsyncResult` implementations were depending on libraries like System.Security.Windows.Principal.dll), and reduced the complexity of the code, they also reduced allocation. To see the impact, here’s a little microbenchmark that repeatedly establishes a new loopback connection:

```
[Benchmark(OperationsPerInvoke = 1000)]
public async Task ConnectAcceptAsync()
{
    using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    listener.Listen(1);

    for (int i = 0; i < 1000; i++)
    {
        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        await client.ConnectAsync(listener.LocalEndPoint);
        using var server = await listener.AcceptAsync();
    }
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| ConnectAcceptAsync | .NET Framework 4.8 | 282.3 us | 1.00 | 2,780 B |
| ConnectAcceptAsync | .NET 5.0 | 278.3 us | 0.99 | 1,698 B |
| ConnectAcceptAsync | .NET 6.0 | 273.8 us | 0.97 | 1,402 B |

Then for .NET 6, I can also add `CancellationToken.None` as an argument to `ConnectAsync` and `AcceptAsync`. Passing `CancellationToken.None` as the last argument changes the overload used; this overload doesn’t just enable cancellation (if you were to pass in a cancelable token), but those new overloads return `ValueTask<T>`s, further reducing allocation. With that, I get the following, for an additional reduction:

```
[Params(false, true)]
public bool NewOverload { get; set; }

[Benchmark(OperationsPerInvoke = 1000)]
public async Task ConnectAcceptAsync()
{
    using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    listener.Listen(1);

    for (int i = 0; i < 1000; i++)
    {
        using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        if (NewOverload)
        {
            await client.ConnectAsync(listener.LocalEndPoint, CancellationToken.None);
        }
        else
        {
            await client.ConnectAsync(listener.LocalEndPoint);
        }
        using var server = await listener.AcceptAsync();
    }
}
```

| Method | NewOverload | Mean | Allocated |
| --- | --- | --- | --- |
| ConnectAcceptAsync | False | 270.5 us | 1,403 B |
| ConnectAcceptAsync | True | 262.5 us | 1,324 B |

[dotnet/runtime#47781](https://github.com/dotnet/runtime/pull/47781) is another example of flipping the `Begin/End` and `Task`\-based implementations. It adds new task-based overloads for the UDP-focused sending and receiving operations on `Socket` (`SendTo`, `ReceiveFrom`, `ReceiveMessageFrom`), and then reimplements the existing `Begin/End` methods on top of the new task-based (actually `ValueTask`) methods. Here’s an example; note that technically these benchmarks are flawed given that UDP is lossy, but I’ve ignored that for the purposes of determining the costs of these methods.

```
private Socket _client;
private Socket _server;
private byte[] _buffer = new byte[1];

[GlobalSetup]
public void Setup()
{
    _client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    _client.Bind(new IPEndPoint(IPAddress.Loopback, 0));

    _server = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
    _server.Bind(new IPEndPoint(IPAddress.Loopback, 0));
}

[Benchmark(OperationsPerInvoke = 10_000)]
public async Task ReceiveSendAsync()
{
    for (int i = 0; i < 10_000; i++)
    {
        var receive = _client.ReceiveFromAsync(_buffer, SocketFlags.None, _server.LocalEndPoint);
        await _server.SendToAsync(_buffer, SocketFlags.None, _client.LocalEndPoint);
        await receive;
    }
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| ReceiveSendAsync | .NET Core 3.1 | 36.24 us | 1.00 | 1,888 B |
| ReceiveSendAsync | .NET 5.0 | 36.22 us | 1.00 | 1,672 B |
| ReceiveSendAsync | .NET 6.0 | 28.50 us | 0.79 | 384 B |

Then, as in the previous example, I can try adding in the additional `CancellationToken` argument:

```
[Benchmark(OperationsPerInvoke = 10_000, Baseline = true)]
public async Task Old()
{
    for (int i = 0; i < 10_000; i++)
    {
        var receive = _client.ReceiveFromAsync(_buffer, SocketFlags.None, _server.LocalEndPoint);
        await _server.SendToAsync(_buffer, SocketFlags.None, _client.LocalEndPoint);
        await receive;
    }
}

[Benchmark(OperationsPerInvoke = 10_000)]
public async Task New()
{
    for (int i = 0; i < 10_000; i++)
    {
        var receive = _client.ReceiveFromAsync(_buffer, SocketFlags.None, _server.LocalEndPoint, CancellationToken.None);
        await _server.SendToAsync(_buffer, SocketFlags.None, _client.LocalEndPoint, CancellationToken.None);
        await receive;
    }
}
```

| Method | Mean | Ratio | Allocated |
| --- | --- | --- | --- |
| Old | 28.95 us | 1.00 | 384 B |
| New | 27.83 us | 0.96 | 288 B |

Other new overloads have also been added in .NET 6 (almost all of the operations on `Socket` now have overloads accepting `ReadOnlySpan<T>` or `{ReadOnly}Memory<T>`, complete with functioning support for `CancellationToken`). [dotnet/runtime#47230](https://github.com/dotnet/runtime/pull/47230) from [@gfoidl](https://github.com/gfoidl) added a span-based overload of `Socket.SendFile`, enabling the pre- and post- buffers to be specified as `ReadOnlySpan<byte>` rather than `byte[]`, which makes it cheaper to send only a portion of some array (the alternative with the existing overloads would be to allocate a new array of the desired length and copy the relevant data into it), and then [dotnet/runtime#52208](https://github.com/dotnet/runtime/pull/52208) also from [@gfoidl](https://github.com/gfoidl) added a `Memory`\-based overload of `Socket.SendFileAsync`, returning a `ValueTask` (subsequently [dotnet/runtime#53062](https://github.com/dotnet/runtime/pull/53062) provided the cancellation support that had been stubbed out in the previous PR). On top of that, [dotnet/runtime#55232](https://github.com/dotnet/runtime/pull/55232), and then [dotnet/runtime#56777](https://github.com/dotnet/runtime/pull/56777) from [@huoyaoyuan](https://github.com/huoyaoyuan), reduced the overhead of these `SendFile{Async}` operations by utilizing the new `RandomAccess` class to create `SafeFileHandle` instances directly rather than going through `FileStream` to open the appropriate handles to the files to be sent. The net result is a nice reduction in overhead for these operations, beyond the improvements in usability.

As long as we’re on the subject of `SendFileAsync`, it’s somewhat interesting to look at [dotnet/runtime#55263](https://github.com/dotnet/runtime/pull/55263). This is a tiny PR that reduced the size of some allocations in the networking stack, including one in `SendFileAsync` (or, rather, in the `SendPacketsAsync` that `SendFileAsync` wraps). The internal `SocketPal.SendPacketsAsync` on Unix is implemented as an `async Task` method, which means that all “locals” in the method that need to survive across `await`s are lifted by the compiler to live as fields on the generated state machine type for that async method, and that state machine will end up being allocated to live on the heap if the async method needs to complete asynchronously. The fewer and smaller fields we can have on these state machines, the smaller the resulting allocation will be for asynchronously completing async methods. But locals written by the developer aren’t the only reason for fields being added. Let’s take a look at an example:

```
public class C
{
    public static async Task<int> Example1(int someParameter) =>
        Process(someParameter) + await Task.FromResult(42);

    public static async Task<int> Example2(int someParameter) =>
        await Task.FromResult(42) + Process(someParameter);

    private static int Process(int i) => i;
}
```

Just focusing on fields, the C# compiler will produce for `Example1` a type like this:

```
[StructLayout(LayoutKind.Auto)]
[CompilerGenerated]
private struct <Example1>d__0 : IAsyncStateMachine
{
    public AsyncTaskMethodBuilder<int> <>t__builder;
    public int <>1__state;
    public int someParameter;
    private TaskAwaiter<int> <>u__1;
    private int <>7__wrap1;
    ....
}
```

Let’s examine a few of these fields:

-   `<>t__builder` here is the “builder” we discussed earlier when talking about pooling in async methods.
-   `<>1__state` is the “state” of the state machine. The compiler rewrites an async method to have a jump table at the beginning, where the current state dictates to where in the method it jumps. `await`s are assigned a state based on their position in the source code, and the code for awaiting something that’s not yet completed involves updating `<>1__state` to refer to the await that should be jumped to when the continuation is invoked to re-enter the async method after the awaited task has completed.
-   `someParameter` is the argument to the method. It needs to be on the state machine to feed it into the `MoveNext` method generated by the compiler, but it would also need to be on the state machine if code after an `await` wanted to read its value.
-   `<>u__1` stores the awaiter for the `await` on the `Task<int>` returned by `Task.FromResult(42)`. The code generated for the await involves calling `GetAwaiter()` on the awaited thing, checking its `IsCompleted` property, and if that’s false, storing the awaiter into this field so that it can be read and its `GetResult()` method called upon completion of the task.

But… what is this `<>7__wrap1` thing? The answer has to do with order of operations. Let’s look at the code generated for `Example2`:

```
private struct <Example2>d__1 : IAsyncStateMachine
{
    public int <>1__state;
    public AsyncTaskMethodBuilder<int> <>t__builder;
    public int someParameter;
    private TaskAwaiter<int> <>u__1;
}
```

This code has the exact same fields as the state machine for `Example1`, except it’s missing the `<>7__wrap1` field. The reason is the compiler is required to respect the order of operations in an expression like `Process(someParameter) + await Task.FromResult(42)`. That means it must compute `Process(someParameter)` before it computes `await Task.FromResult(42)`. But `Process(someParameter)` returns an `int` value; where should that be stored while `await Task.FromResult(42)` is being processed? On the state machine. That “spilled” field is `<>7__wrap1`. This also explains why the field isn’t there in `Example2`: the order of operations was explicitly reversed by the developer to be `await Task.FromResult(42) + Process(someParameter)`, so we don’t have to stash the result of `Process(someParameter)` anywhere, as it’s no longer crossing an `await` boundary. So, back to the cited PR. The original line of code in question was `bytesTransferred += await socket.SendAsync(...)`, which is the same as `bytesTransferred = bytesTransferred + await socket.SendAsync(...)`. Look familiar? Technically the compiler needs to stash away the `bytesTransferred` value in order to preserve the order of operations with regards to the `SendAsync` operation, and so the PR just explicitly reverses this to be `bytesTransferred = await socket.SendAsync(...) + bytesTransferred` in order to make the state machine a little smaller. You can see more examples of this in [dotnet/runtime#55190](https://github.com/dotnet/runtime/pull/55190) for `BufferedStream`. In practice, the compiler should be able to special-case this constrained version of the issue, as it should be able to see that no other code would have access to `bytesTransferred` to modify it, and thus the defensive copy shouldn’t be necessary… maybe [some day](https://github.com/dotnet/roslyn/issues/54629).

Let’s move up the stack a little: DNS. `System.Net.Dns` is a relatively thin wrapper for OS functionality. It provides both synchronous and asynchronous APIs. On Windows, the asynchronous APIs are implemented on top of Winsock’s `GetAddrInfoExW` function (if available), which provides a scalable overlapped I/O-based model for performing name resolution asynchronously. The story is more convoluted on Unix, where POSIX provides `getaddrinfo` but no asynchronous counterpart. Linux does have `getaddrinfo_a`, which does provide an asynchronous version, and in fact [dotnet/runtime#34633](https://github.com/dotnet/runtime/pull/34633) from [@gfoidl](https://github.com/gfoidl) did temporarily change `Dns`‘s async APIs to use it, but that PR was subsequently reverted in [dotnet/runtime#48666](https://github.com/dotnet/runtime/pull/48666) upon realizing that the implementation was just queueing these calls to be executed synchronously on a limited size thread pool internal to glibc, and we could similarly employ an “async-over-sync” solution in managed code and with more control. Here [“async-over-sync”](https://devblogs.microsoft.com/dotnet/should-i-expose-asynchronous-wrappers-for-synchronous-methods/) is referring to the idea of implementing an asynchronous operation that’s just queueing a synchronous piece of work to be done on another thread, rather than having it employ truly asynchronous I/O all the way down to the hardware. This ends up blocking that other thread for the duration of the operation, which inherently limits scalability. It can also be a real bottleneck for something like DNS. Typically an operating system will cache some amount of DNS data, but in cases where a request is made for unavailable data, the OS has to reach out across the network to a DNS server to obtain it. If lots of requests are made concurrently for the same non-cached address, that can starve the pool with all of the operations performing the exact same request. To address this, [dotnet/runtime#49171](https://github.com/dotnet/runtime/pull/49171) implements that async-over-sync in `Dns` in a way that asynchronously serializes all requests for the same destination; that way, if bursts do show up, we only end up blocking one thread for all of them rather than one thread for each. This adds a small amount of overhead for individual operations, but significantly reduces the overhead in the bursty, problematic scenarios. In the future, we will hopefully be able to do away with this once we’re able to implement a true async I/O-based mechanism on Unix, potentially implemented directly on `Socket` in a managed DNS client, or potentially employing a library like [c-ares](https://c-ares.haxx.se/).

Another nice improvement in `Dns` comes in the form of new overloads introduced in [dotnet/runtime#33420](https://github.com/dotnet/runtime/pull/33420) for specifying the desired `AddressFamily`. By default, operations on `Dns` can return both IPv4 and IPv6 addresses, but if you know you only care about one or the other, you can now be explicit about it. Doing so can save on both the amount of data transferred and the resulting allocations to hand back that data.

```
private string _hostname = Dns.GetHostName();

[Benchmark(OperationsPerInvoke = 1000, Baseline = true)]
public async Task GetHostAddresses()
{
    for (int i = 0; i < 1_000; i++)
        await Dns.GetHostAddressesAsync(_hostname);
}

[Benchmark(OperationsPerInvoke = 1000)]
public async Task GetHostAddresses_OneFamily()
{
    for (int i = 0; i < 1_000; i++)
        await Dns.GetHostAddressesAsync(_hostname, AddressFamily.InterNetwork);
}
```

| Method | Mean | Ratio | Allocated |
| --- | --- | --- | --- |
| GetHostAddresses | 210.1 us | 1.00 | 808 B |
| GetHostAddresses\_OneFamily | 195.7 us | 0.93 | 370 B |

Moving up the stack, we start getting into specifying URLs, which typically uses `System.Uri`. `Uri` instances are created in many places, and being able to create them more quickly and with less GC impact is a boon for end-to-end performance of networking-related code. The internal `Uri.ReCreateParts` method is the workhorse behind a lot of the public `Uri` surface area, and is responsible for formatting into a `string` whatever parts of the `Uri` have been requested (e.g. `UriComponents.Path | UriComponents.Query | UriComponents.Fragment`) while also factoring in desired escaping (e.g. `UriFormat.Unescaped`). It also unfortunately had quite a knack for allocating `char[]` arrays. [dotnet/runtime#34864](https://github.com/dotnet/runtime/pull/34864) fixed that, using stack-allocated space for most `Uri`s (e.g. those whose length is <= 256 characters) and falling back to using `ArrayPool<char>.Shared` for longer lengths, while also cleaning up some code paths to make them a bit more streamlined. The impact of this is visible in these benchmarks:

```
private Uri _uri = new Uri("http://dot.net");

[Benchmark]
public string GetComponents() => _uri.GetComponents(UriComponents.PathAndQuery | UriComponents.Fragment, UriFormat.UriEscaped);

[Benchmark]
public Uri NewUri() => new Uri("http://dot.net");

[Benchmark]
public string PathAndQuery() => _uri.PathAndQuery;
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| GetComponents | .NET Framework 4.8 | 49.4856 ns | 1.00 | 241 B |
| GetComponents | .NET Core 3.1 | 47.8179 ns | 0.96 | 232 B |
| GetComponents | .NET 5.0 | 39.5046 ns | 0.80 | 232 B |
| GetComponents | .NET 6.0 | 31.0651 ns | 0.63 | 24 B |
|  |  |  |  |  |
| NewUri | .NET Framework 4.8 | 280.0722 ns | 1.00 | 168 B |
| NewUri | .NET Core 3.1 | 144.3990 ns | 0.52 | 72 B |
| NewUri | .NET 5.0 | 100.0479 ns | 0.36 | 56 B |
| NewUri | .NET 6.0 | 92.1300 ns | 0.33 | 56 B |
|  |  |  |  |  |
| PathAndQuery | .NET Framework 4.8 | 50.3840 ns | 1.00 | 241 B |
| PathAndQuery | .NET Core 3.1 | 48.7625 ns | 0.97 | 232 B |
| PathAndQuery | .NET 5.0 | 2.1615 ns | 0.04 | – |
| PathAndQuery | .NET 6.0 | 0.7380 ns | 0.01 | – |

Of course, not all URLs contain pure ASCII. Such cases often involve escaping these characters using percent-encoding, and [dotnet/runtime#32552](https://github.com/dotnet/runtime/pull/32552) optimized those code paths by changing a multi-pass scheme that involved both a temporary `byte[]` buffer and a temporary `char[]` buffer into a single-pass scheme that used stack-allocation with a fallback to the `ArrayPool<T>.Shared`.

```
[Benchmark]
public string Unescape() => Uri.UnescapeDataString("%E4%BD%A0%E5%A5%BD");
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| Unescape | .NET Framework 4.8 | 284.03 ns | 1.00 | 385 B |
| Unescape | .NET Core 3.1 | 144.55 ns | 0.51 | 208 B |
| Unescape | .NET 5.0 | 125.98 ns | 0.44 | 144 B |
| Unescape | .NET 6.0 | 69.85 ns | 0.25 | 32 B |

`UriBuilder` is also used in some applications to compose `Uri` instances. [dotnet/runtime#51826](https://github.com/dotnet/runtime/pull/51826) reduced the size of `UriBuilder` itself by getting rid of some fields that weren’t strictly necessary, avoided some string concatenations and substring allocations, and utilized stack-allocation and `ArrayPool<T>` as part of its `ToString` implementation. As a result, `UriBuilder` is now also lighterweight for most uses:

```
[Benchmark]
public string BuilderToString()
{
    var builder = new UriBuilder();
    builder.Scheme = "https";
    builder.Host = "dotnet.microsoft.com";
    builder.Port = 443;
    builder.Path = "/platform/try-dotnet";
    return builder.ToString();
}  
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| BuilderToString | .NET Framework 4.8 | 604.5 ns | 1.00 | 810 B |
| BuilderToString | .NET Core 3.1 | 446.7 ns | 0.74 | 432 B |
| BuilderToString | .NET 5.0 | 225.7 ns | 0.38 | 432 B |
| BuilderToString | .NET 6.0 | 171.7 ns | 0.28 | 216 B |

As noted previously, I love seeing this continual march of progress, with every release the exact same API getting faster and faster, as more and more opportunities are discovered, new capabilities of the underlying platform utilized, code generation improving, and on. Exciting.

Now we get to `HttpClient`. There were a few areas in which `HttpClient`, and specifically `SocketsHttpHandler`, was improved from a performance perspective (and many more from a functionality perspective, including preview support for HTTP/3, better standards adherence, distributed tracing integration, and more knobs for configuring how it should behave). One key area is around header management. Previous releases saw a lot of effort applied to driving down the overheads of the HTTP stack, but the public API for headers forced a particular set of work and allocations to be performed. Even within those constraints, we’ve driven down some costs, such as by no longer forcing headers added into the `HttpClient.DefaultRequestHeaders` collection to be parsed if the developer added them with `TryAddWithoutValidation` ([dotnet/runtime#49673](https://github.com/dotnet/runtime/pull/49673)), removing a lock that’s no longer necessary ([dotnet/runtime#54130](https://github.com/dotnet/runtime/pull/54130)), and enabling a singleton empty enumerator to be returned when enumerating an `HttpHeaderValueCollection` ([dotnet/runtime#47010](https://github.com/dotnet/runtime/pull/47010)).

```
[Benchmark(Baseline = true)]
public async Task Enumerate()
{
    var request = new HttpRequestMessage(HttpMethod.Get, s_uri);
    using var resp = await s_client.SendAsync(request, default);
    foreach (var header in resp.Headers) { }
    foreach (var contentHeader in resp.Content.Headers) { }
    await resp.Content.CopyToAsync(Stream.Null);
}

private static readonly Socket s_listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
private static readonly HttpMessageInvoker s_client = new HttpMessageInvoker(new HttpClientHandler { UseProxy = false, UseCookies = false, AllowAutoRedirect = false });
private static Uri s_uri;

[GlobalSetup]
public void CreateSocketServer()
{
    s_listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    s_listener.Listen(int.MaxValue);
    var ep = (IPEndPoint)s_listener.LocalEndPoint;
    s_uri = new Uri($"http://{ep.Address}:{ep.Port}/");
    byte[] response = Encoding.UTF8.GetBytes("HTTP/1.1 200 OK\r\nDate: Tue, 01 Jul 2021 12:00:00 GMT \r\nServer: Example\r\nAccess-Control-Allow-Credentials: true\r\nAccess-Control-Allow-Origin: *\r\nConnection: keep-alive\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: 5\r\n\r\nHello");
    byte[] endSequence = new byte[] { (byte)'\r', (byte)'\n', (byte)'\r', (byte)'\n' };

    Task.Run(async () =>
    {
        while (true)
        {
            Socket s = await s_listener.AcceptAsync();
            _ = Task.Run(() =>
            {
                using (var ns = new NetworkStream(s, true))
                {
                    byte[] buffer = new byte[1024];
                    int totalRead = 0;
                    while (true)
                    {
                        int read = ns.Read(buffer, totalRead, buffer.Length - totalRead);
                        if (read == 0) return;
                        totalRead += read;
                        if (buffer.AsSpan(0, totalRead).IndexOf(endSequence) == -1)
                        {
                            if (totalRead == buffer.Length) Array.Resize(ref buffer, buffer.Length * 2);
                            continue;
                        }

                        ns.Write(response, 0, response.Length);

                        totalRead = 0;
                    }
                }
            });
        }
    });
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| Enumerate | .NET Framework 4.8 | 145.97 us | 1.00 | 18 KB |
| Enumerate | .NET 5.0 | 85.51 us | 0.56 | 3 KB |
| Enumerate | .NET 6.0 | 82.45 us | 0.54 | 3 KB |

But the biggest impact in this area comes from the addition of the new `HttpHeaders.NonValidated` property ([dotnet/runtime#53555](https://github.com/dotnet/runtime/pull/53555)), which returns a view over the headers collection that does not force parsing or validation when reading/enumerating. This has both a functional and a performance benefit. Functionally, it means headers sent by a server can be inspected in their original form, for consumers that really want to see the data prior to it having been sanitized/transformed by `HttpClient`. But from a performance perspective, it has a significant impact, as it means that a) the validation logic we’d normally run on headers can be omitted entirely, and b) any allocations that would result from that validation are also avoided. Now if we run `Enumerate` and `EnumerateNew` on .NET 6, we can see the improvement that results from using the new API:

```
// Added to the previous benchmark
[Benchmark]
public async Task EnumerateNew()
{
    var request = new HttpRequestMessage(HttpMethod.Get, s_uri);
    using var resp = await s_client.SendAsync(request, default);
    foreach (var header in resp.Headers.NonValidated) { }
    foreach (var contentHeader in resp.Content.Headers.NonValidated) { }
    await resp.Content.CopyToAsync(Stream.Null);
}
```

| Method | Mean | Ratio | Allocated |
| --- | --- | --- | --- |
| Enumerate | 82.70 us | 1.00 | 3 KB |
| EnumerateNew | 67.36 us | 0.81 | 2 KB |

So, even with all the I/O and HTTP protocol logic being performed, tweaking the API used for header enumeration here results in an ~20% boost in throughput.

Another area that saw significant improvement was in `SocketsHttpHandler`‘s connection pooling. One change here comes in [dotnet/runtime#50545](https://github.com/dotnet/runtime/pull/50545), which simplifies the code and helps on all platforms, but in particular improves a long-standing potential performance issue on Windows (our Unix implementation generally didn’t suffer the same problem, because of differences in how asynchronous I/O is implemented). `SocketsHttpHandler` maintains a pool of connections that remain open to the server and that it can use to service future requests. By default, it needs to scavenge this pool periodically, to close connections that have been around for too long or that, more relevant to this discussion, the server has chosen to close. To determine whether the server has closed a connection, we need to poll the underlying socket, but in some situations, we don’t actually have access to the underlying socket in order to perform the poll (and, with the advent of `ConnectCallback` in .NET 5 that enables an arbitrary `Stream` to be provided for use with a connection, there may not even be a `Socket` involved at all). In such situations, the only way we can be notified of a connection being closed is to perform a read on the connection. Thus, if we were unable to poll the socket directly, we would issue an asynchronous read (which would then be used as the first read as part of handling the next request on that connection), and the scavenging logic could check the task for that read to see whether it had completed erroneously. Now comes the problem. On Windows, overlapped I/O read operations often involve pinning a buffer for the duration of the operation (on Unix, we implement asynchronous reads via epoll, and no buffer need be pinned for the duration); that meant if we ended up with a lot of connections in the pool, and we had to issue asynchronous reads for each, we’d likely end up pinning a whole bunch of sizeable buffers, leading to memory fragmentation and potentially sizeable working set growth. The fix is to use zero-byte reads. Rather than issuing the actual read using the connection’s buffer, we instead issue a read using an empty buffer. All of the streams `SocketsHttpHandler` uses by default (namely `NetworkStream` and `SslStream`) support the notion of zero-byte reads, where rather than returning immediately, they instead wait to complete the asynchronous read until at least some data is available, even though they won’t be returning any of that data as part of the operation. Then, only once that operation has completed, the actual initial read is issued, which is both necessary to actually get the first batch of response data, but also to handle arbitrary `Stream`s that may return immediately from a zero-byte read without actually waiting. Interestingly, though, just supporting zero-byte reads can sometimes be the “min bar”. `SslStream` has long supported zero-byte reads on it, but it did so by in turn issuing a read on the stream it wraps using its internal read buffer. That means `SslStream` was potentially itself holding onto a valuable buffer, and (on Windows) pinning it, even though that was unnecessary. [dotnet/runtime#49123](https://github.com/dotnet/runtime/pull/49123) addresses that by special-casing zero-byte reads to not use a buffer and to not force an internal buffer into existence if one isn’t currently available (`SslStream` returns buffers back to a pool when it’s not currently using them).

`SslStream` has seen multiple other performance-related PRs come through for .NET 6. Previously, `SslStream` would hand back to a caller of `Read{Async}` the data from at most one TLS frame. As of [dotnet/runtime#50815](https://github.com/dotnet/runtime/pull/50815), it can now hand back data from multiple TLS frames should those frames be available and a large enough buffer be provided to `Read{Async}`. This can help reduce the chattiness of `ReadAsync` calls, making better use of buffer space to reduce frequency of calls. [dotnet/runtime#51320](https://github.com/dotnet/runtime/pull/51320) from [@benaadams](https://github.com/benaadams) helped avoid some unnecessary buffer growth after he noticed some constants related to TLS frame sizes that had been in the code for a long time were no longer sufficient for newer TLS protocols, and [dotnet/runtime#51324](https://github.com/dotnet/runtime/pull/51324) also from [@benaadams](https://github.com/benaadams) helped avoid some casting overheads by being more explicit about the actual types being passed through the system.

[dotnet/runtime#53851](https://github.com/dotnet/runtime/pull/53851) provides another very interesting improvement related to connection pooling. Let’s say all of the connections for a given server are currently busy handling requests, and another request comes along. Unless you’ve configured a maximum limit on the number of connections per server and hit that limit, `SocketsHttpHandler` will happily create a new connection to service your request (in the case of HTTP/2, by default per the HTTP/2 specification there’s only one connection and a limit set by the server to the number of requests/streams multiplexed onto that connection, but `SocketsHttpHandler` allows you to opt-in to using more than one connection). The question then is, what happens to that request if, while waiting for the new connection to be established, one of the existing connections becomes available? Up until now, that request would just wait for and use the new connection. With the aforementioned PR, the request can now use whatever connection becomes available first, whether it be an existing one or a new one, and whatever connection isn’t used will simply find its way back to the pool. This should both improve latency and response time, and potentially reduce the number of connections needed in the pool, thus saving memory and networking resources.

.NET Core 3.0 introduced support for HTTP/2, and since then the use of the protocol has been growing. This has led us to discover where things worked well and where more work was needed. One area in particular that needed some love was around `SocketsHttpHandler`‘s HTTP/2 download performance. Investigations showed slowdowns here were due to `SocketsHttpHandler` using a fixed-size receive window (64KB), such that if the receive buffer wasn’t large enough to keep the network busy, the system could stall. To address that, the receive buffer needs to be large enough to handle the “bandwidth-delay product” (a network connection’s capacity multiplied by round-trip communication time). [dotnet/runtime#54755](https://github.com/dotnet/runtime/pull/54755) adds support for dynamically-sizing the receive window, as well as several knobs for tweaking the behavior. This should significantly help with performance in particular on networks with reasonably-high bandwidth along with some meaningful delay in communications (e.g. with geographically distributed data centers), while also not consuming too much memory.

There’s also been a steady stream of small improvements to `HttpClient`, things that on their own don’t account for much but when added together help to move the needle. For example, [dotnet/runtime#54209](https://github.com/dotnet/runtime/pull/54209) from [@teo-tsirpanis](https://github.com/teo-tsirpanis) converted a small class to a struct, saving an allocation per connection; [dotnet/runtime#50487](https://github.com/dotnet/runtime/pull/50487) removed a closure allocation from the `SocketsHttpHandler` connection pool, simply by changing the scope in which a variable was declared so that it wasn’t in scope of a hotter path; [dotnet/runtime#44750](https://github.com/dotnet/runtime/pull/44750) removed a string allocation from `MediaTypeHeaderValue` in the common case where it has a media type but no additional parameters; and [dotnet/runtime#45303](https://github.com/dotnet/runtime/pull/45303) optimized the loading of the Huffman static encoding table used by HTTP/2. The original code employed a single, long array of tuples, which required the C# compiler to generate a very large function for initializing each element of the array; the PR changed that to instead be two blittable `uint[]` arrays that are cheaply stored in the binary.

Finally, let’s look at `WebSockets`. `WebSocket.CreateFromStream` was introduced in .NET Core 2.1 and layers a managed implementation of the [websocket protocol](https://datatracker.ietf.org/doc/html/rfc6455) on top of an arbitrary bidirectional `Stream`; `ClientWebSocket` uses it with a `Stream` created by `SocketsHttpHandler` to enable client websockets, and Kestrel uses it to enable server websockets. Thus, any improvements we make to that managed implementation (the internal `ManagedWebSocket`) benefit both client and server. There have been a handful of small improvements in this area, such as with [dotnet/runtime#49831](https://github.com/dotnet/runtime/pull/49831) that saved a few hundred bytes in allocation as part of the websocket handshake by using span-based APIs to create the data for the headers used in the websocket protocol, and [dotnet/runtime#52022](https://github.com/dotnet/runtime/pull/52022) from [@zlatanov](https://github.com/zlatanov) that saved a few hundred bytes from each `ManagedWebSocket` by avoiding a `CancellationTokenSource` that was overkill for the target scenario. But there were two significant changes worth examining in more detail.

The first is websocket compression. The implementation for this came in [dotnet/runtime#49304](https://github.com/dotnet/runtime/pull/49304) from [@zlatanov](https://github.com/zlatanov), providing a long-requested feature of [per-message compression](https://datatracker.ietf.org/doc/html/rfc7692). Adding compression increases the CPU cost of sending and receiving, but it decreases the amount of data sent and received, which can in turn decrease the overall cost of communication, especially as networking latency increases. As such, the benefit of this one is harder to measure with BenchmarkDotNet, and I’ll instead just use a console app:

```
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;

ReadOnlyMemory<byte> dataToSend;
using (var hc = new HttpClient())
{
    dataToSend = await hc.GetByteArrayAsync("https://www.gutenberg.org/cache/epub/3200/pg3200.txt");
}
Memory<byte> receiveBuffer = new byte[dataToSend.Length];

foreach (bool compressed in new[] { false, true })
{
    using var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    listener.Listen();

    using var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    client.Connect(listener.LocalEndPoint);
    using Socket server = listener.Accept();

    using var clientStream = new PassthroughTrackingStream(new NetworkStream(client, ownsSocket: true));
    using var clientWS = WebSocket.CreateFromStream(clientStream, new WebSocketCreationOptions { IsServer = false, DangerousDeflateOptions = compressed ? new WebSocketDeflateOptions() : null });
    using var serverWS = WebSocket.CreateFromStream(new NetworkStream(server, ownsSocket: true), new WebSocketCreationOptions { IsServer = true, DangerousDeflateOptions = compressed ? new WebSocketDeflateOptions() : null });

    var sw = new Stopwatch();
    for (int trial = 0; trial < 5; trial++)
    {
        long before = clientStream.BytesRead;
        sw.Restart();

        await serverWS.SendAsync(dataToSend, WebSocketMessageType.Binary, true, default);
        while (!(await clientWS.ReceiveAsync(receiveBuffer, default)).EndOfMessage) ;

        sw.Stop();
        Console.WriteLine($"Compressed: {compressed,5} Bytes: {clientStream.BytesRead - before,10:N0} Time: {sw.ElapsedMilliseconds:N0}ms");
    }
}

sealed class PassthroughTrackingStream : Stream
{
    private readonly Stream _stream;
    public long BytesRead;

    public PassthroughTrackingStream(Stream stream) => _stream = stream;

    public override bool CanWrite => true;
    public override bool CanRead => true;

    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        int n = await _stream.ReadAsync(buffer, cancellationToken);
        BytesRead += n;
        return n;
    }

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken) =>
        _stream.WriteAsync(buffer, cancellationToken);

    protected override void Dispose(bool disposing) => _stream.Dispose();
    public override bool CanSeek => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() { }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
```

This app is creating a loopback socket connection and then layering on top of that a websocket connection created using `WebSocket.CreateFromStream`. But rather than just wrapping the `NetworkStream`s directly, the “client” end of the stream that’s receiving data sent by the “server” is wrapping the `NetworkStream` in an intermediary stream that’s tracking the number of bytes read, which it then exposes for the console app to print. That way, we can see how much data ends up actually being sent. The app is downloading the complete works of Mark Twain from Project Gutenberg, such that each sent message is ~15MB. When I run this, I get results like the following:

```
Compressed: False Bytes: 16,013,945 Time: 42ms
Compressed: False Bytes: 16,013,945 Time: 13ms
Compressed: False Bytes: 16,013,945 Time: 13ms
Compressed: False Bytes: 16,013,945 Time: 12ms
Compressed: False Bytes: 16,013,945 Time: 12ms
Compressed:  True Bytes:  6,326,310 Time: 580ms
Compressed:  True Bytes:  6,325,285 Time: 571ms
Compressed:  True Bytes:  6,325,246 Time: 569ms
Compressed:  True Bytes:  6,325,229 Time: 571ms
Compressed:  True Bytes:  6,325,168 Time: 571ms
```

So, we can see that on this very fast loopback connection, the cost of the operation is dominated by the compression; however, we’re sending only a third as much data. That could be a good tradeoff if communicating over a real network with longer latencies, where the additional few hundred milliseconds to perform the compression and decompression is minimal compared to the cost of sending and receiving an additional 10MB.

The second is amortized zero-allocation websocket receiving. In .NET Core 2.1, overloads were added to `WebSocket` for the `SendAsync` and `ReceiveAsync` methods. These overloads accepted `ReadOnlyMemory<byte>` and `Memory<byte>`, respectively, and returned `ValueTask` and `ValueTask<int>`, respectively. That `ValueTask<int>` in particular was important because it enabled `ReceiveAsync` to perform in an allocation-free manner when the operation completed synchronously, which would happen if the data being received was already available. When the operation completed asynchronously, however, it would still allocate a `Task<int>` to back the `ValueTask<int>`, and even with the advent of `IValueTaskSource<int>`, that still remained, given the complexity of the `ReceiveAsync` method and how difficult it would be to manually implement the function by hand without the assistance of `async` and `await`. However, as previously discussed, C# 10 and .NET 6 now have opt-in support for pooling with async methods. [dotnet/runtime#56282](https://github.com/dotnet/runtime/pull/56282) included adding `[AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]` to `ReceiveAsync`. On my 12-logical-core machine, this code:

```
private Connection[] _connections = Enumerable.Range(0, 256).Select(_ => new Connection()).ToArray();
private const int Iters = 1_000;

[Benchmark]
public Task PingPong() =>
    Task.WhenAll(from c in _connections
                    select Task.WhenAll(
                    Task.Run(async () =>
                    {
                        for (int i = 0; i < Iters; i++)
                        {
                            await c.Server.ReceiveAsync(c.ServerBuffer, c.CancellationToken);
                            await c.Server.SendAsync(c.ServerBuffer, WebSocketMessageType.Binary, endOfMessage: true, c.CancellationToken);
                        }
                    }),
                    Task.Run(async () =>
                    {
                        for (int i = 0; i < Iters; i++)
                        {
                            await c.Client.SendAsync(c.ClientBuffer, WebSocketMessageType.Binary, endOfMessage: true, c.CancellationToken);
                            await c.Client.ReceiveAsync(c.ClientBuffer, c.CancellationToken);
                        }
                    })));

private class Connection
{
    public readonly WebSocket Client, Server;
    public readonly Memory<byte> ClientBuffer = new byte[256];
    public readonly Memory<byte> ServerBuffer = new byte[256];
    public readonly CancellationToken CancellationToken = default;

    public Connection()
    {
        (Stream Stream1, Stream Stream2) streams = ConnectedStreams.CreateBidirectional();
        Client = WebSocket.CreateFromStream(streams.Stream1, isServer: false, subProtocol: null, Timeout.InfiniteTimeSpan);
        Server = WebSocket.CreateFromStream(streams.Stream2, isServer: true, subProtocol: null, Timeout.InfiniteTimeSpan);
    }
}
```

then yielded this improvement:

| Method | Runtime | Mean | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- |
| PingPong | .NET 5.0 | 148.7 ms | 1.00 | 29750.0000 | 3000.0000 | 250.0000 | 180,238 KB |
| PingPong | .NET 6.0 | 108.9 ms | 0.72 | – | – | – | 249 KB |

### Reflection

Reflection provides a very powerful mechanism for inspecting metadata about .NET assemblies and invoking functionality in those assemblies. That mechanism can incur non-trivial expense, however. While functionality exists to avoid that overhead for repeated calls (e.g. using `MethodInfo.CreateDelegate` to get a strongly-typed delegate directly to the target method), that’s not always relevant or appropriate. As such, it’s valuable to reduce the overhead associated with reflection, which .NET 6 does in multiple ways.

A variety of PRs targeted reducing the overhead involved in inspecting attributes on .NET types and members. [dotnet/runtime#54402](https://github.com/dotnet/runtime/pull/54402) significantly reduced the overhead of calling `Attribute.GetCustomAttributes` when specifying that inherited attributes should be included (even if there aren’t any to inherit); [dotnet/runtime#44694](https://github.com/dotnet/runtime/pull/44694) from [@benaadams](https://github.com/benaadams) reduced the memory allocation associated with `Attribute.IsDefined` via a dedicated code path rather than relegating the core logic to an existing shared method ([dotnet/runtime#45292](https://github.com/dotnet/runtime/pull/45292), from [@benaadams](https://github.com/benaadams) as well, also removed some low-level overhead from filtering attribute records); and [dotnet/runtime#54405](https://github.com/dotnet/runtime/pull/54405) eliminated the allocation from `MethodInfo.GetCustomAttributeData` when there aren’t any attributes (it’s common to call this API to check if there are, and thus it’s helpful to improve performance in the common case where there aren’t).

```
private MethodInfo _noAttributes = typeof(C).GetMethod("NoAttributes");
private PropertyInfo _hasAttributes = typeof(C).GetProperty("HasAttributes");

[Benchmark]
public IList<CustomAttributeData> GetCustomAttributesData() => _noAttributes.GetCustomAttributesData();

[Benchmark]
public bool IsDefined() => Attribute.IsDefined(_hasAttributes, typeof(ObsoleteAttribute));

[Benchmark]
public Attribute[] GetCustomAttributes() => Attribute.GetCustomAttributes(_hasAttributes, inherit: true);

class A { }

class C : A
{
    public void NoAttributes() { }
    [Obsolete]
    public bool HasAttributes { get; set; }
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| GetCustomAttributesData | .NET Framework 4.8 | 329.48 ns | 1.00 | 168 B |
| GetCustomAttributesData | .NET Core 3.1 | 85.27 ns | 0.26 | 48 B |
| GetCustomAttributesData | .NET 5.0 | 73.58 ns | 0.22 | 48 B |
| GetCustomAttributesData | .NET 6.0 | 69.59 ns | 0.21 | – |
|  |  |  |  |  |
| IsDefined | .NET Framework 4.8 | 640.15 ns | 1.00 | 144 B |
| IsDefined | .NET Core 3.1 | 399.75 ns | 0.62 | 136 B |
| IsDefined | .NET 5.0 | 292.01 ns | 0.46 | 48 B |
| IsDefined | .NET 6.0 | 252.00 ns | 0.39 | – |
|  |  |  |  |  |
| GetCustomAttributes | .NET Framework 4.8 | 5,155.93 ns | 1.00 | 1,380 B |
| GetCustomAttributes | .NET Core 3.1 | 2,702.26 ns | 0.52 | 1,120 B |
| GetCustomAttributes | .NET 5.0 | 2,406.51 ns | 0.47 | 1,056 B |
| GetCustomAttributes | .NET 6.0 | 446.29 ns | 0.09 | 128 B |

Code often looks up information beyond attributes, and it can be helpful for performance to special-case common patterns. [dotnet/runtime#44759](https://github.com/dotnet/runtime/pull/44759) recognizes that reflection-based code will often look at method parameters, which many methods don’t have, yet `GetParameters` was always allocating a `ParameterInfo[]`, even for zero parameters. A given `MethodInfo` will cache the array, but this would still result in an extra array for every individual method inspected. This PR fixes that.

Reflection is valuable not just for getting metadata but also for invoking members. If you ever do an allocation profile for code using reflection to invoke methods, you’ll likely see a bunch of `object[]` allocations showing up, typically coming from a method named `CheckArguments`. This is part of the runtime’s type safety validation. Reflection is going to pass the `object[]` of arguments you pass to `MethodInfo.Invoke` to the target method, which means it needs to validate that the arguments are of the right types the method expects… if they’re not, it could end up violating type safety by passing type A to a method that instead receives it as a completely unrelated type B, and now all use of that “B” is potentially invalid and corrupting. However, if a caller erroneously mutated the array concurrently with the reflection call, such mutation could happen after the type checks occurred, enabling type safety to be violated, anyway. So, the runtime is forced to make a defensive copy of the argument array and then validate the copy to which the caller doesn’t have access. That’s the `object[]` that shows up in these traces. [dotnet/runtime#50814](https://github.com/dotnet/runtime/pull/50814) addresses this by recognizing that most methods have at most only a few parameters, and special-cases methods with up to four parameters to instead use a stack-allocated `Span<object>` rather than a heap-allocated a `object[]` for storing that defensive copy.

```
private MethodInfo _method = typeof(Program).GetMethod("M");

public void M(int arg1, string arg2) { }

[Benchmark]
public void Invoke() => _method.Invoke(this, new object[] { 1, "two" });
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| Invoke | .NET Framework 4.8 | 195.5 ns | 1.00 | 104 B |
| Invoke | .NET Core 3.1 | 156.0 ns | 0.80 | 104 B |
| Invoke | .NET 5.0 | 141.0 ns | 0.72 | 104 B |
| Invoke | .NET 6.0 | 123.1 ns | 0.63 | 64 B |

Another very common form of dynamic invocation is when creating new instances via `Activator.CreateInstance`, which is usable directly but is also employed by the C# compiler to implement the `new()` constraint on generic parameters. [dotnet/runtime#32520](https://github.com/dotnet/runtime/pull/32520) overhauled the `Activator.CreateInstance` implementation in the runtime, employing a per-type cache of function pointers that can be used to quickly allocate an uninitialized object of the relevant type and invoke its constructor.

```
private T Create<T>() where T : new() => new T();

[Benchmark]
public Program Create() => Create<Program>();
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| Create | .NET Framework 4.8 | 49.496 ns | 1.00 | 24 B |
| Create | .NET Core 3.1 | 28.296 ns | 0.57 | 24 B |
| Create | .NET 5.0 | 26.350 ns | 0.53 | 24 B |
| Create | .NET 6.0 | 9.439 ns | 0.19 | 24 B |

Another common operation is creating closed generic types from open ones, e.g. given the type for `List<T>` creating a type for `List<int>`. [dotnet/runtime#45137](https://github.com/dotnet/runtime/pull/45137) special-cased the most common case of having just one type parameter in order to optimize that path, while also avoiding an extra `GetGenericArguments` call internally for all arities.

```
private Type[] _oneRef = new[] { typeof(string) };
private Type[] _twoValue = new[] { typeof(int), typeof(int) };

[Benchmark] public Type OneRefType() => typeof(List<>).MakeGenericType(_oneRef);
[Benchmark] public Type TwoValueType() => typeof(Dictionary<,>).MakeGenericType(_twoValue);
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| OneRefType | .NET Framework 4.8 | 363.1 ns | 1.00 | 128 B |
| OneRefType | .NET Core 3.1 | 266.8 ns | 0.74 | 128 B |
| OneRefType | .NET 5.0 | 248.7 ns | 0.69 | 128 B |
| OneRefType | .NET 6.0 | 171.6 ns | 0.47 | 32 B |
|  |  |  |  |  |
| TwoValueType | .NET Framework 4.8 | 418.9 ns | 1.00 | 160 B |
| TwoValueType | .NET Core 3.1 | 292.3 ns | 0.70 | 160 B |
| TwoValueType | .NET 5.0 | 290.5 ns | 0.69 | 160 B |
| TwoValueType | .NET 6.0 | 215.0 ns | 0.51 | 120 B |

Finally, sometimes optimizations are all about deleting code and just calling something else that already exists. [dotnet/runtime#42891](https://github.com/dotnet/runtime/pull/42891) just changed the implementation of one helper in the runtime to call another existing helper, in order to make `Type.IsPrimitive` measurably faster:

```
[Benchmark]
[Arguments(typeof(int))]
public bool IsPrimitive(Type type) => type.IsPrimitive;
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| IsPrimitive | .NET Framework 4.8 | 5.021 ns | 1.00 |
| IsPrimitive | .NET Core 3.1 | 3.184 ns | 0.63 |
| IsPrimitive | .NET 5.0 | 3.032 ns | 0.60 |
| IsPrimitive | .NET 6.0 | 2.376 ns | 0.47 |

Of course, reflection extends beyond just the core reflection APIs, and a number of PRs have gone in to improving areas of reflection higher in the stack. `DispatchProxy`, for example. `DispatchProxy` provides an interface-based alternative to the older remoting-based `RealProxy` ([Migrating RealProxy Usage to DispatchProxy](https://devblogs.microsoft.com/dotnet/migrating-realproxy-usage-to-dispatchproxy/) provides a good description). It utilizes reflection emit to generate IL at run-time, and [dotnet/runtime#47134](https://github.com/dotnet/runtime/pull/47134) optimizes both that process and the generated code in such a way that it saves several hundred bytes of allocation per method invocation on a `DispatchProxy`.

### Collections and LINQ

Every .NET release has seen the core collection types and LINQ get faster and faster. Even as a lot of the low-hanging fruit was picked in previous releases, developers contributing to .NET 6 have still managed to find meaningful improvements, some in the form of optimizing existing APIs, and some in the form of new APIs developers can use to make their own code fly.

Improvements to `Dictionary<TKey, TValue>` are always exciting, as it’s used _everywhere_, and performance improvements to it have a way of “moving the needle” on a variety of workloads. One improvement to `Dictionary<TKey, TValue>` in .NET 6 comes from [@benaadams](https://github.com/benaadams) in [dotnet/runtime#41944](https://github.com/dotnet/runtime/pull/41944). The PR improves the performance of creating one dictionary from another, by enabling the common case of the source dictionary and the new dictionary sharing a key comparer to copy the underlying buckets without rehashing.

```
private IEnumerable<KeyValuePair<string, int>> _dictionary = Enumerable.Range(0, 100).ToDictionary(i => i.ToString(), StringComparer.OrdinalIgnoreCase);

[Benchmark]
public Dictionary<string, int> Clone() => new Dictionary<string, int>(_dictionary);
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| Clone | .NET Core 3.1 | 3.224 us | 1.00 |
| Clone | .NET 5.0 | 2.880 us | 0.89 |
| Clone | .NET 6.0 | 1.685 us | 0.52 |

In [dotnet/runtime#45659](https://github.com/dotnet/runtime/pull/45659) and [dotnet/runtime#56634](https://github.com/dotnet/runtime/pull/56634), `SortedDictionary<TKey, TValue>` also gains a similar optimization:

```
private IDictionary<string, int> _dictionary = new SortedDictionary<string, int>(Enumerable.Range(0, 100).ToDictionary(i => i.ToString(), StringComparer.OrdinalIgnoreCase));

[Benchmark]
public SortedDictionary<string, int> Clone() => new SortedDictionary<string, int>(_dictionary);
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| Clone | .NET Framework 4.8 | 69.546 us | 1.00 |
| Clone | .NET Core 3.1 | 54.560 us | 0.78 |
| Clone | .NET 5.0 | 53.196 us | 0.76 |
| Clone | .NET 6.0 | 2.330 us | 0.03 |

[dotnet/runtime#49388](https://github.com/dotnet/runtime/pull/49388) from [@benaadams](https://github.com/benaadams) and [dotnet/runtime#54611](https://github.com/dotnet/runtime/pull/54611) from [@Sergio0694](https://github.com/Sergio0694) are examples of new APIs that developers can use with dictionaries when they want to eke out that last mile of performance. These APIs are defined on the `CollectionMarshal` class as they provide low-level access to internals of the dictionary, returning a ref into the `Dictionary<TKey, TValue>`s data structures; thus, you need to be careful when using them, but they can measurably improve performance in specific situations. `CollectionMarshal.GetValueRefOrNullRef` returns a `ref TValue` that will either point to an existing entry in the dictionary or be a null reference (e.g. `Unsafe.NullRef<T>()`) if the key could not be found. And `CollectionMarshal.GetValueRefOrAddDefault` returns a `ref TValue?`, returning a ref to the value if the key could be found, or adding an empty entry and returning a ref to it, otherwise. These can be used to avoid duplicate lookups as well as avoid potentially expensive struct value copies.

```
private Dictionary<int, int> _counts = new Dictionary<int, int>();

[Benchmark(Baseline = true)]
public void AddOld()
{
    for (int i = 0; i < 10_000; i++)
    {
        _counts[i] = _counts.TryGetValue(i, out int count) ? count + 1 : 1;
    }
}

[Benchmark]
public void AddNew()
{
    for (int i = 0; i < 10_000; i++)
    {
        CollectionsMarshal.GetValueRefOrAddDefault(_counts, i, out _)++;
    }
}
```

| Method | Mean | Ratio |
| --- | --- | --- |
| AddOld | 95.39 us | 1.00 |
| AddNew | 49.85 us | 0.52 |

`ImmutableSortedSet<T>` and `ImmutableList<T>` indexing also get faster, thanks to [dotnet/runtime#53266](https://github.com/dotnet/runtime/pull/53266) from [@L2](https://github.com/L2). Indexing into these collections performs a binary search through a tree of nodes, and each layer of the traversal was performing a range check on the index. But for all but the entry point check, that range validation is duplicative and can be removed, which is exactly what the PR does:

```
private ImmutableList<int> _list = ImmutableList.CreateRange(Enumerable.Range(0, 100_000));

[Benchmark]
public int Item() => _list[1];
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| Item | .NET Framework 4.8 | 17.468 ns | 1.00 |
| Item | .NET 5.0 | 16.296 ns | 0.93 |
| Item | .NET 6.0 | 9.457 ns | 0.54 |

`ObservableCollection<T>` also improves in .NET 6, specifically due to [dotnet/runtime#54899](https://github.com/dotnet/runtime/pull/54899), which reduces the allocations involved in creating NotifyCollectionChangedEventArgs (as such, this isn’t actually specific to `ObservableCollection<T>` and will help other systems that use the event arguments). The crux of the change is introducing an internal `SingleItemReadOnlyList` that’s used when an `IList` is needed to represent a single item; this replaces allocating an `object[]` that’s then wrapped in a `ReadOnlyList`.

```
private ObservableCollection<int> _collection = new ObservableCollection<int>();

[Benchmark]
public void ClearAdd()
{
    _collection.Clear();
    for (int i = 0; i < 100; i++)
    {
        _collection.Add(i);
    }
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| ClearAdd | .NET Framework 4.8 | 4.014 us | 1.00 | 17 KB |
| ClearAdd | .NET 5.0 | 3.104 us | 0.78 | 13 KB |
| ClearAdd | .NET 6.0 | 2.471 us | 0.62 | 9 KB |

There have been a variety of other changes, such as `HashSet<T>` shrinking in size by a reference field, thanks to [dotnet/runtime#49483](https://github.com/dotnet/runtime/pull/49483); `ConcurrentQueue<T>` and `ConcurrentBag<T>` avoiding some unnecessary writes when T doesn’t contain any references, thanks to [dotnet/runtime#53438](https://github.com/dotnet/runtime/pull/53438); new `EnsureCapacity` APIs for `List<T>`, `Stack<T>`, and `Queue<T>`, thanks to [dotnet/runtime#47149](https://github.com/dotnet/runtime/pull/47149) from [@lateapexearlyspeed](https://github.com/lateapexearlyspeed); and a brand new `PriorityQueue<TElement, TPriority>`, which was initially added in [dotnet/runtime#46009](https://github.com/dotnet/runtime/pull/46009) by [@pgolebiowski](https://github.com/pgolebiowski) and then subsequently optimized further in PRs like [dotnet/runtime#48315](https://github.com/dotnet/runtime/pull/48315), [dotnet/runtime#48324](https://github.com/dotnet/runtime/pull/48324), [dotnet/runtime#48346](https://github.com/dotnet/runtime/pull/48346), and [dotnet/runtime#50065](https://github.com/dotnet/runtime/pull/50065).

Having mentioned `HashSet<T>`, `HashSet<T>` gets a new customer in .NET 6: LINQ. Previous releases of LINQ brought with it its own internal `Set<T>` implementation, but in .NET 6 [dotnet/runtime#49591](https://github.com/dotnet/runtime/pull/49591) ripped that out and replaced it with the built-in `HashSet<T>`, benefiting LINQ from the myriad of performance improvements that have gone into `HashSet<T>` in the last few years (but especially in .NET 5), while also reducing code duplication.

```
private IEnumerable<string> _data = Enumerable.Range(0, 100_000).Select(i => i.ToString()).ToArray();

[Benchmark]
public int DistinctCount() => _data.Distinct().Count();
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| DistinctCount | .NET 5.0 | 5.154 ms | 1.04 | 5 MB |
| DistinctCount | .NET 6.0 | 2.626 ms | 0.53 | 2 MB |

`Enumerable.SequenceEqual` has also been accelerated when both enumerables are arrays, thanks to [dotnet/runtime#48287](https://github.com/dotnet/runtime/pull/48287) and [dotnet/runtime#48677](https://github.com/dotnet/runtime/pull/48677). The latter PR adds a `MemoryExtensions.SequenceEqual` overload that accepts an `IEqualityComparer<T>` (the existing overloads constrain `T` to being `IEquatable<T>`), which enables `Enumerable.SequenceEqual` to delegate to the span-based method and obtain vectorization of the comparison “for free” when the `T` used is amenable.

```
private IEnumerable<int> _e1 = Enumerable.Range(0, 1_000_000).ToArray();
private IEnumerable<int> _e2 = Enumerable.Range(0, 1_000_000).ToArray();

[Benchmark]
public bool SequenceEqual() => _e1.SequenceEqual(_e2);
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| SequenceEqual | .NET Framework 4.8 | 10,822.6 us | 1.00 |
| SequenceEqual | .NET 5.0 | 5,421.1 us | 0.50 |
| SequenceEqual | .NET 6.0 | 150.2 us | 0.01 |

`Enumerable.Min<T>` and `Enumerable.Max<T>` have also improved, thanks to [dotnet/runtime#48273](https://github.com/dotnet/runtime/pull/48273) and [dotnet/runtime#48289](https://github.com/dotnet/runtime/pull/48289) (and the aforementioned JIT improvements that recognize `Comparer<T>.Default` as an intrinsic). By special-casing the comparer being `Comparer<T>.Default`, a dedicated loop could then be written explicitly using `Comparer<T>.Default` rather than going through the `comparer` parameter, which enables all of the calls through `Comparer<T>.Default.Compare` to devirtualize when `T` is a value type.

```
private TimeSpan[] _values = Enumerable.Range(0, 1_000_000).Select(i => TimeSpan.FromMilliseconds(i)).ToArray();

[Benchmark]
public TimeSpan Max() => _values.Max();

[Benchmark]
public TimeSpan Min() => _values.Min();
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| Max | .NET Framework 4.8 | 5.984 ms | 1.00 |
| Max | .NET 5.0 | 4.926 ms | 0.82 |
| Max | .NET 6.0 | 4.222 ms | 0.71 |
|  |  |  |  |
| Min | .NET Framework 4.8 | 5.917 ms | 1.00 |
| Min | .NET 5.0 | 5.207 ms | 0.88 |
| Min | .NET 6.0 | 4.291 ms | 0.73 |

In addition, there have been several new APIs added to LINQ in .NET 6. A new `Enumerable.Zip` overload accepting three rather than only two sources was added in [dotnet/runtime#47147](https://github.com/dotnet/runtime/pull/47147) from [@huoyaoyuan](https://github.com/huoyaoyuan), making it both easier and faster to combine three sources:

```
private IEnumerable<int> _e1 = Enumerable.Range(0, 1_000);
private IEnumerable<int> _e2 = Enumerable.Range(0, 1_000);
private IEnumerable<int> _e3 = Enumerable.Range(0, 1_000);

[Benchmark(Baseline = true)]
public void Old()
{
    IEnumerable<(int, int, int)> zipped = _e1.Zip(_e2).Zip(_e3, (x, y) => (x.First, x.Second, y));
    foreach ((int, int, int) values in zipped)
    {
    }
}

[Benchmark]
public void New()
{
    IEnumerable<(int, int, int)> zipped = _e1.Zip(_e2, _e3);
    foreach ((int, int, int) values in zipped)
    {
    }
}
```

| Method | Mean | Ratio | Allocated |
| --- | --- | --- | --- |
| Old | 20.50 us | 1.00 | 304 B |
| New | 14.88 us | 0.73 | 232 B |

[dotnet/runtime#48559](https://github.com/dotnet/runtime/pull/48559) from [@Dixin](https://github.com/Dixin) and [dotnet/runtime#48634](https://github.com/dotnet/runtime/pull/48634) add a new overload of `Enumerable.Take` that accepts a `Range` (as well as an `ElementAt` that takes an `Index`). In addition to then enabling the C# 8 range syntax to be used with `Take`, it also reduces some overheads associated with needing to combine multiple existing combinators to achieve the same thing.

```
private static IEnumerable<int> Range(int count)
{
    for (int i = 0; i < count; i++) yield return i;
}

private IEnumerable<int> _e = Range(10_000);

[Benchmark(Baseline = true)]
public void Old()
{
    foreach (int i in _e.Skip(1000).Take(10)) { }
}

[Benchmark]
public void New()
{
    foreach (int i in _e.Take(1000..1010)) { }
}
```

| Method | Mean | Ratio | Allocated |
| --- | --- | --- | --- |
| Old | 2.954 us | 1.00 | 152 B |
| New | 2.935 us | 0.99 | 96 B |

And [dotnet/runtime#48239](https://github.com/dotnet/runtime/pull/48239) introduced `Enumerable.TryGetNonEnumeratedCount`, which enables getting the count of the number of items in an enumerable if that count can be determined quickly. This can be useful to avoid the overhead of resizes when presizing a collection that will be used to store the contents of the enumerable.

Lastly, it’s somewhat rare today to see code written against instances of `Array` rather than a strongly-typed array (e.g. `int[]` or `T[]`), but such code does exist. We don’t need to optimize heavily for such code, but sometimes the stars align and efforts to simplify such code actually make it significantly faster as well, as is the case with [dotnet/runtime#51351](https://github.com/dotnet/runtime/pull/51351), which simplified the implementation of the non-generic `ArrayEnumerator`, and in doing so made code like this much faster:

```
private Array _array = Enumerable.Range(0, 1000).Select(i => new object()).ToArray();

[Benchmark]
public int Count()
{
    int count = 0;
    foreach (object o in _array) count++;
    return count;
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| Count | .NET Framework 4.8 | 14.992 us | 1.00 | 32 B |
| Count | .NET Core 3.1 | 14.134 us | 0.94 | 32 B |
| Count | .NET 5.0 | 12.866 us | 0.86 | 32 B |
| Count | .NET 6.0 | 5.778 us | 0.39 | 32 B |

### Cryptography

Let’s turn to cryptography. A lot of work has gone into crypto for the .NET 6 release, mostly functional. However, there are have been a handful of impactful performance improvements in the space.

`CryptoStream` was improved over the course of multiple PRs. When async support was initially added to `CryptoStream`, it was decided that, because `CryptoStream` does compute-intensive work, it shouldn’t block the caller of the asynchronous method; as a result, `CryptoStream` was originally written to forcibly queue encryption and decryption operations to the thread pool. However, typical usage is actually very fast and doesn’t warrant a thread hop, and even if it wasn’t fast, guidance has evolved over the years such that now the recommendation wouldn’t be to queue, anyway. So, [dotnet/runtime#45150](https://github.com/dotnet/runtime/pull/45150) removed that queueing. On top of that, `CryptoStream` hadn’t really kept up with the times, and when new `Memory`/`ValueTask`\-based `ReadAsync` and `WriteAsync` overloads were introduced on `Stream` in .NET Core 2.1, `CryptoStream` didn’t provide overrides; for .NET 6, [dotnet/runtime#47207](https://github.com/dotnet/runtime/pull/47207) from [@NewellClark](https://github.com/NewellClark) addresses that deficiency by adding the appropriate overrides. As in the earlier discussion of `DeflateStream`, `CryptoStream` now also can complete a read operation once at least one byte of output is available and can be used for zero-byte reads.

`CryptoStream` works with arbitrary implementations of `ICryptoTransform`, of which one is `ToBase64Transform`; not exactly cryptography, but it makes it easy to Base64-encode a stream of data. `ICryptoTransform` is an interesting interface, providing a `CanTransformMultipleBlocks` property that dictates whether an implementation’s `TransformBlock` and `Transform` can transform just one or multiple “blocks” of data at a time. The interface expects that input is processed in blocks of a particular fixed number of input bytes which then yield a fixed number of output bytes, e.g. `ToBase64Transform` encodes blocks of three input bytes into blocks of four output bytes. Historically, `ToBase64Transform` returned `false` from `CanTransformMultipleBlocks`, which then forced `CryptoStream` to take the slower path of processing only three input bytes at a time. `ToBase64Transform` uses `Base64.EncodeToUtf8`, which is vectorized for fast processing, but three input bytes per call is too small to take advantage of the vectorized code paths, which ended up making `ToBase64Transform` quite slow. [dotnet/runtime#55055](https://github.com/dotnet/runtime/pull/55055) fixed this by teaching `ToBase64Transform` how to process multiple blocks, which in turn has a big impact on its performance when used with `CryptoStream`.

```
private byte[] _data = Enumerable.Range(0, 10_000_000).Select(i => (byte)i).ToArray();
private MemoryStream _destination = new MemoryStream();

[Benchmark]
public async Task Encode()
{
    _destination.Position = 0;
    using (var toBase64 = new ToBase64Transform())
    using (var stream = new CryptoStream(_destination, toBase64, CryptoStreamMode.Write, leaveOpen: true))
    {
        await stream.WriteAsync(_data, 0, _data.Length);
    }
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| Encode | .NET Framework 4.8 | 329.871 ms | 1.000 | 213,976,944 B |
| Encode | .NET Core 3.1 | 251.986 ms | 0.765 | 213,334,112 B |
| Encode | .NET 5.0 | 146.058 ms | 0.443 | 974 B |
| Encode | .NET 6.0 | 1.998 ms | 0.006 | 300 B |

Even as `CryptoStream` improves in .NET 6, sometimes you don’t need the power of a `Stream` and instead just want something simple and fast to handle encrypting and decrypting data you already have in memory. For that, [dotnet/runtime#52510](https://github.com/dotnet/runtime/pull/52510), [dotnet/runtime#55184](https://github.com/dotnet/runtime/pull/55184), and [dotnet/runtime#55480](https://github.com/dotnet/runtime/pull/55480) introduced new “one shot” `EncryptCbc`, `EncryptCfb`, `EncryptEcb`, `DecryptCbc`, `DecryptCfb`, and `DecryptEcb` methods on `SymmetricAlgorithm` (along with some protected virtual methods these delegate to) that support encrypting and decrypting `byte[]`s and `ReadOnlySpan<byte>`s without having to go through a `Stream`. This not only leads to simpler code when you already have the data to process, it’s also faster.

```
private byte[] _key, _iv, _ciphertext;

[GlobalSetup]
public void Setup()
{
    using Aes aes = Aes.Create();
    _key = aes.Key;
    _iv = aes.IV;
    _ciphertext = aes.EncryptCbc(Encoding.UTF8.GetBytes("This is a test.  This is only a test."), _iv);
}

[Benchmark(Baseline = true)]
public byte[] Old()
{
    using Aes aes = Aes.Create();

    aes.Key = _key;
    aes.IV = _iv;
    aes.Padding = PaddingMode.PKCS7;
    aes.Mode = CipherMode.CBC;

    using MemoryStream destination = new MemoryStream();
    using ICryptoTransform transform = aes.CreateDecryptor();
    using CryptoStream cryptoStream = new CryptoStream(destination, transform, CryptoStreamMode.Write);

    cryptoStream.Write(_ciphertext);
    cryptoStream.FlushFinalBlock();

    return destination.ToArray();
}

[Benchmark]
public byte[] New()
{
    using Aes aes = Aes.Create();

    aes.Key = _key;

    return aes.DecryptCbc(_ciphertext, _iv);
}
```

| Method | Mean | Ratio | Allocated |
| --- | --- | --- | --- |
| Old | 1.657 us | 1.00 | 1,320 B |
| New | 1.073 us | 0.65 | 664 B |

I previously mentioned improvements to `System.Random` in .NET 6. That’s for non-cryptographically-secure randomness. If you need cryptographically-secure randomness, `System.Security.Cryptography.RandomNumberGenerator` is your friend. This type has existed for years but it’s been receiving more love over the last several .NET releases. For example, `RandomNumberGenerator` is instantiable via the `Create` method, and instance methods do expose the full spread of the type’s functionality, but there’s no actual need for it to be its own instance, as the underlying OS objects used now on all platforms are thread-safe and implemented in a scalable manner. [dotnet/runtime#43221](https://github.com/dotnet/runtime/pull/43221) added a static `GetBytes` method that makes it simple and a bit faster to get a `byte[]` filled with cryptographically-strong random data:

```
[Benchmark(Baseline = true)]
public byte[] Old()
{
    using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
    {
        byte[] buffer = new byte[8];
        rng.GetBytes(buffer);
        return buffer;
    }
}

[Benchmark]
public byte[] New()
{
    return RandomNumberGenerator.GetBytes(8);
}
```

| Method | Mean | Ratio | Allocated |
| --- | --- | --- | --- |
| Old | 80.46 ns | 1.00 | 32 B |
| New | 78.10 ns | 0.97 | 32 B |

However, the `Old` case here is already improved on .NET 6 than on previous releases. [dotnet/runtime#52495](https://github.com/dotnet/runtime/pull/52495) recognizes that there’s little benefit to `Create` creating a new instance, and converts it into a singleton.

```
[Benchmark]
public byte[] GetBytes()
{
    using (RandomNumberGenerator rng = RandomNumberGenerator.Create())
    {
        byte[] buffer = new byte[8];
        rng.GetBytes(buffer);
        return buffer;
    }
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| GetBytes | .NET Framework 4.8 | 948.94 ns | 1.00 | 514 B |
| GetBytes | .NET 5.0 | 85.35 ns | 0.09 | 56 B |
| GetBytes | .NET 6.0 | 80.12 ns | 0.08 | 32 B |

The addition of the static `GetBytes` method continues a theme throughout crypto of exposing more “one-shot” APIs as static helpers. The `Rfc2898DeriveBytes` class enables code to derive bytes from passwords, and historically this has been done by instantiating an instance of this class and calling `GetBytes`. [dotnet/runtime#48107](https://github.com/dotnet/runtime/pull/48107) adds static `Pbkdf2` methods that use the PBKDF2 (Password-Based Key Derivation Function 2) key-derivation function to generate the requested bytes without explicitly creating an instance; this, in turn, enables the implementation to use any “one-shot” APIs provided by the underlying operating system, e.g. those from CommonCrypto on macOS.

```
private byte[] _salt = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

[Benchmark(Baseline = true)]
public byte[] Old()
{
    using Rfc2898DeriveBytes db = new Rfc2898DeriveBytes("my super strong password", _salt, 1000, HashAlgorithmName.SHA256);
    return db.GetBytes(16);
}

[Benchmark]
public byte[] New()
{
    return Rfc2898DeriveBytes.Pbkdf2("my super strong password", _salt, 1000, HashAlgorithmName.SHA256, 16);
}
```

| Method | Mean | Ratio | Allocated |
| --- | --- | --- | --- |
| Old | 637.5 us | 1.00 | 561 B |
| New | 554.9 us | 0.87 | 73 B |

Other improvements in crypto include avoiding unnecessary zero’ing for padding in symmetric encryption ([dotnet/runtime#52465](https://github.com/dotnet/runtime/pull/52465)); using the span-based support with `IncrementalHash.CreateHMAC` to avoid some `byte[]` allocations ([dotnet/runtime#43541](https://github.com/dotnet/runtime/pull/43541)); caching failed lookups in addition to successful lookups in `OidLookup.ToOid` ([dotnet/runtime#46819](https://github.com/dotnet/runtime/pull/46819)); using stack allocation in signature generation to avoid unnecessary allocation ([dotnet/runtime#46893](https://github.com/dotnet/runtime/pull/46893)); using better OS APIs on macOS for RSA/ECC keys ([dotnet/runtime#52759](https://github.com/dotnet/runtime/pull/52759) from [@filipnavara](https://github.com/filipnavara)); and avoiding closures in the interop layers of `X509Certificate`s on both Unix ([dotnet/runtime#50511](https://github.com/dotnet/runtime/pull/50511)) and Windows ([dotnet/runtime#50376](https://github.com/dotnet/runtime/pull/50376), [dotnet/runtime#50377](https://github.com/dotnet/runtime/pull/50377)). One of my favorites, simply because it eliminates an annoyance I hit now and again, is [dotnet/runtime#53129](https://github.com/dotnet/runtime/pull/53129) from [@hrrrrustic](https://github.com/hrrrrustic), which adds an implementation of the generic `IEnumerable<T>` to each of several `X509Certificate`\-related collections that previously only implemented the non-generic `IEnumerable`. This in turn removes the common need to use LINQ’s `OfType<X509Certificate2>` when enumerating `X509CertificateCollection`, both improving maintainability and reducing overhead.

```
private X509Certificate2Collection _certs;

[GlobalSetup]
public void Setup()
{
    using var store = new X509Store(StoreLocation.CurrentUser);
    _certs = store.Certificates;
}

[Benchmark(Baseline = true)]
public void Old()
{
    foreach (string s in _certs.OfType<X509Certificate2>().Select(c => c.Subject)) { }
}

[Benchmark]
public void New()
{
    foreach (string s in _certs.Select(c => c.Subject)) { }
}
```

| Method | Mean | Ratio | Allocated |
| --- | --- | --- | --- |
| Old | 63.45 ns | 1.00 | 160 B |
| New | 53.94 ns | 0.85 | 128 B |

### “Peanut Butter”

As has been shown in this post and in those that I’ve written for previous versions, there have been literally thousands of PRs into .NET over the last several years to improve its performance. Many of these changes on their own have a profound and very measurable impact to some scenario. However, a fair number of the changes are what we lovingly refer to as “peanut butter”, a thin layer of tiny performance-impacting changes that individually aren’t hugely meaningful but that over time add up to bigger impact. Sometimes these changes make one specific change in one place (e.g. removing one allocation), and it’s the aggregate of all such changes that helps .NET to get better and better. Sometimes it’s a pattern of change applied en mass across the stack. There are dozens of such changes in .NET 6, and I’ll walk through some of them here.

One of my favorite sets of changes, and a pattern which will hopefully be codified in a future release by an analyzer, shows up in [dotnet/runtime#49958](https://github.com/dotnet/runtime/pull/49958), [dotnet/runtime#50225](https://github.com/dotnet/runtime/pull/50225), and [dotnet/runtime#49969](https://github.com/dotnet/runtime/pull/49969). These PRs changed over 2300 internal and private classes across dotnet/runtime to be sealed. Why does that matter? For some of the types, it won’t, but there are multiple reasons why sealing types can measurably improve performance, and so we’ve adopted a general policy that all non-public types that can be sealed should be, so as to maximize the chances use of these types will simply be better than it otherwise would be.

One reason sealing helps is that virtual methods on a sealed type are more likely to be devirtualized by the runtime. If the runtime can see that a given instance on which a virtual call is being made is actually sealed, then it knows for certain what the actual target of the call will be, and it can invoke that target directly rather than doing a virtual dispatch operation. Better yet, once the call is devirtualized, it might be inlineable, and then if it’s inlined, all the previously discussed benefits around optimizing the caller+callee combined kick in.

```
private SealedType _sealed = new();
private NonSealedType _nonSealed = new();

[Benchmark(Baseline = true)]
public int NonSealed() => _nonSealed.M() + 42;

[Benchmark]
public int Sealed() => _sealed.M() + 42;

public class BaseType
{
    public virtual int M() => 1;
}

public class NonSealedType : BaseType
{
    public override int M() => 2;
}

public sealed class SealedType : BaseType
{
    public override int M() => 2;
}
```

| Method | Mean | Ratio | Code Size |
| --- | --- | --- | --- |
| NonSealed | 0.9837 ns | 1.000 | 26 B |
| Sealed | 0.0018 ns | 0.002 | 12 B |

```
; Program.NonSealed()
       sub       rsp,28
       mov       rcx,[rcx+10]
       mov       rax,[rcx]
       mov       rax,[rax+40]
       call      qword ptr [rax+20]
       add       eax,2A
       add       rsp,28
       ret
; Total bytes of code 26

; Program.Sealed()
       mov       rax,[rcx+8]
       cmp       [rax],eax
       mov       eax,2C
       ret
; Total bytes of code 12
```

Note the code gen difference. `NonSealed()` is doing a virtual dispatch (that series of `mov` instructions to find the address of the actual method to invoke followed by a `call`), whereas `Sealed()` isn’t calling anything: in fact, it’s been reduced to a null check followed by returning a constant value, as SealedType.M was devirtualized and inlined, at which point the JIT could constant fold the `2 + 42` into just `44` (hex 0x2C). BenchmarkDotNet actually issues a warning (a good warning in this case) about the resulting metrics as a result:

```
// * Warnings *
ZeroMeasurement
  Program.Sealed: Runtime=.NET 6.0, Toolchain=net6.0 -> The method duration is indistinguishable from the empty method duration
```

In order to measure the cost of a benchmark, it not only times how long it takes to invoke the benchmark but also how long it takes to invoke an empty benchmark with a similar signature, with the results presented subtracting the latter from the former. BenchmarkDotNet is then highlighting that with the method just returning a constant, the benchmark and the empty method are now indistinguishable. Cool.

Another benefit of sealing is that it can make type checks a lot faster. When you write code like `obj is SomeType`, there are multiple ways that could be emitted in assembly. If `SomeType` is sealed, then this check can be implemented along the lines of `obj is not null && obj.GetType() == typeof(SomeType)`, where the latter clause can be implemented simply by comparing the type handle of `obj` against the known type handle of `SomeType`; after all, if it’s sealed, it’s not possible there could be any type derived from `SomeType`, so there’s no other type than `SomeType` that need be considered. But if `SomeType` isn’t sealed, this check becomes a lot more complicated, needing to determine whether `obj` is not only `SomeType` but potentially something derived from `SomeType`, which means it needs to examine all of the type’s in `obj`‘s type’s parent hierarchy to see whether any of them are `SomeType`. There’s enough logic there that it’s actually factored out into a helper method the JIT can emit a call to, the internal `System.Runtime.CompilerServices.CastHelpers.IsInstanceOfClass`. We can see this in a benchmark:

```
private object _o = "hello";

[Benchmark(Baseline = true)]
public bool NonSealed() => _o is NonSealedType;

[Benchmark]
public bool Sealed() => _o is SealedType;

public class NonSealedType { }
public sealed class SealedType { }
```

| Method | Mean | Ratio | Code Size |
| --- | --- | --- | --- |
| NonSealed | 1.7694 ns | 1.00 | 37 B |
| Sealed | 0.0749 ns | 0.04 | 36 B |

```
; Program.NonSealed()
       sub       rsp,28
       mov       rdx,[rcx+8]
       mov       rcx,offset MT_Program+NonSealedType
       call      CORINFO_HELP_ISINSTANCEOFCLASS
       test      rax,rax
       setne     al
       movzx     eax,al
       add       rsp,28
       ret
; Total bytes of code 37

; Program.Sealed()
       mov       rax,[rcx+8]
       test      rax,rax
       je        short M00_L00
       mov       rdx,offset MT_Program+SealedType
       cmp       [rax],rdx
       je        short M00_L00
       xor       eax,eax
M00_L00:
       test      rax,rax
       setne     al
       movzx     eax,al
       ret
; Total bytes of code 36
```

Note the `NonSealed()` benchmark is making a `call` to the `CORINFO_HELP_ISINSTANCEOFCLASS` helper, whereas `Sealed()` is just directly comparing the type handle of `_o` (`mov rax,[rcx+8]`) against the type handle of `SealedType` (`mov rdx,offset MT_Program+SealedType`, `cmp [rax],rdx`), and the resulting impact that has on the cost of running this code.

Yet another benefit here comes when using arrays of types. As has been mentioned, arrays in .NET are covariant, which means if you have a type `B` that derives from type `A`, and you have an array of `B`s, you can store that `B[]` into a reference of type `A[]`. That, however, means the runtime needs to ensure that any `A` stored into an `A[]` is of an appropriate type for the actual array being referenced, e.g. in this case that every `A` is actually a `B` or something derived from `B`. Of course, if the runtime knows that for a given `T[]` the `T` being stored couldn’t possibly be anything other than `T` itself, it needn’t employ such a check. How could it know that? For one thing, if `T` is sealed. So given a benchmark like:

```
private SealedType _sealedInstance = new();
private SealedType[] _sealedArray = new SealedType[1_000_000];

private NonSealedType _nonSealedInstance = new();
private NonSealedType[] _nonSealedArray = new NonSealedType[1_000_000];

[Benchmark(Baseline = true)]
public void NonSealed()
{
    NonSealedType inst = _nonSealedInstance;
    NonSealedType[] arr = _nonSealedArray;
    for (int i = 0; i < arr.Length; i++)
    {
        arr[i] = inst;
    }
}

[Benchmark]
public void Sealed()
{
    SealedType inst = _sealedInstance;
    SealedType[] arr = _sealedArray;
    for (int i = 0; i < arr.Length; i++)
    {
        arr[i] = inst;
    }
}

public class NonSealedType { }
public sealed class SealedType { }
```

we get results like this:

| Method | Mean | Ratio | Code Size |
| --- | --- | --- | --- |
| NonSealed | 2.580 ms | 1.00 | 53 B |
| Sealed | 1.445 ms | 0.56 | 59 B |

Beyond arrays, this is also relevant to spans. As previously mentioned, `Span<T>` is invariant, and its constructor that takes a `T[]` prevents you from storing an array of a derived type with a check that validates the `T` and the element type of the actual array passed in are the same:

```
if (!typeof(T).IsValueType && array.GetType() != typeof(T[]))
    ThrowHelper.ThrowArrayTypeMismatchException();
```

You get where this is going. `Span<T>`‘s constructor is aggressively inlined, so the code from the constructor is exposed to the caller, which frequently allows the JIT to know the actual type of `T`; if it then knows that `T` is sealed, there’s no way that `array.GetType() != typeof(T[])`, so it can remove the whole check entirely. A very microbenchmark:

```
private SealedType[] _sealedArray = new SealedType[10];
private NonSealedType[] _nonSealedArray = new NonSealedType[10];

[Benchmark(Baseline = true)]
public Span<NonSealedType> NonSealed() => _nonSealedArray;

[Benchmark]
public Span<SealedType> Sealed() => _sealedArray;

public class NonSealedType { }
public sealed class SealedType { }
```

highlights this difference:

| Method | Mean | Ratio | Code Size |
| --- | --- | --- | --- |
| NonSealed | 0.2435 ns | 1.00 | 64 B |
| Sealed | 0.0379 ns | 0.16 | 35 B |

but it’s most visible in the generated assembly:

```
; Program.NonSealed()
       sub       rsp,28
       mov       rax,[rcx+10]
       test      rax,rax
       je        short M00_L01
       mov       rcx,offset MT_Program+NonSealedType[]
       cmp       [rax],rcx
       jne       short M00_L02
       lea       rcx,[rax+10]
       mov       r8d,[rax+8]
M00_L00:
       mov       [rdx],rcx
       mov       [rdx+8],r8d
       mov       rax,rdx
       add       rsp,28
       ret
M00_L01:
       xor       ecx,ecx
       xor       r8d,r8d
       jmp       short M00_L00
M00_L02:
       call      System.ThrowHelper.ThrowArrayTypeMismatchException()
       int       3
; Total bytes of code 64

; Program.Sealed()
       mov       rax,[rcx+8]
       test      rax,rax
       je        short M00_L01
       lea       rcx,[rax+10]
       mov       r8d,[rax+8]
M00_L00:
       mov       [rdx],rcx
       mov       [rdx+8],r8d
       mov       rax,rdx
       ret
M00_L01:
       xor       ecx,ecx
       xor       r8d,r8d
       jmp       short M00_L00
; Total bytes of code 35
```

where we can see the `call System.ThrowHelper.ThrowArrayTypeMismatchException()` doesn’t exist in the `Sealed()` version at all because the check that would lead to it was removed completely.

[dotnet/runtime#43474](https://github.com/dotnet/runtime/pull/43474) is another example of performing some cleanup operation across a bunch of call sites. The `System.Buffers.Binary.BinaryPrimitives` class was introduced in .NET Core 2.1 and has been getting a lot of use with its operations like `ReverseEndianness(Int32)` or `ReadInt32BigEndian(ReadOnlySpan<Byte>)`, but there were a bunch of places in the dotnet/runtime codebase still manually performing such operations when they could have been using these optimized helpers to do it for them. The PR addresses that, nicely changing complicated code like this in `TimeZoneInfo` on Unix:

```
private static unsafe long TZif_ToInt64(byte[] value, int startIndex)
{
    fixed (byte* pbyte = &value[startIndex])
    {
        int i1 = (*pbyte << 24) | (*(pbyte + 1) << 16) | (*(pbyte + 2) << 8) | (*(pbyte + 3));
        int i2 = (*(pbyte + 4) << 24) | (*(pbyte + 5) << 16) | (*(pbyte + 6) << 8) | (*(pbyte + 7));
        return (uint)i2 | ((long)i1 << 32);
    }
}
```

to instead be this:

```
private static long TZif_ToInt64(byte[] value, int startIndex) => 
    BinaryPrimitives.ReadInt64BigEndian(value.AsSpan(startIndex));
```

Ahhh, so much nicer. Not only is such code simpler, safer, and more readily understandable to a reader, it’s also faster:

```
private byte[] _buffer = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };

[Benchmark(Baseline = true)]
public long Old() => Old(_buffer, 0);

[Benchmark]
public long New() => New(_buffer, 0);

private static unsafe long Old(byte[] value, int startIndex)
{
    fixed (byte* pbyte = &value[startIndex])
    {
        int i1 = (*pbyte << 24) | (*(pbyte + 1) << 16) | (*(pbyte + 2) << 8) | (*(pbyte + 3));
        int i2 = (*(pbyte + 4) << 24) | (*(pbyte + 5) << 16) | (*(pbyte + 6) << 8) | (*(pbyte + 7));
        return (uint)i2 | ((long)i1 << 32);
    }
}

private static long New(byte[] value, int startIndex) =>
    BinaryPrimitives.ReadInt64BigEndian(value.AsSpan(startIndex));
```

| Method | Mean | Ratio |
| --- | --- | --- |
| Old | 1.9856 ns | 1.00 |
| New | 0.3853 ns | 0.19 |

Another example of such a cleanup is [dotnet/runtime#54004](https://github.com/dotnet/runtime/pull/54004), which changes several `{U}Int32/64.TryParse` call sites to explicitly use `CultureInfo.InvariantCulture` instead of `null`. Passing in `null` will cause the implementation to access `CultureInfo.CurrentCulture`, which incurs a thread-local storage access, but all of the changed call sites use `NumberStyles.None` or `NumberStyles.Hex`. The only reason the culture is required for parsing is to be able to parse a positive or negative symbol, but with these styles set, the implementation won’t actually use those symbol values, and thus the actual culture utilized doesn’t matter. Passing in `InvariantCulture` then means we’re paying only for a static field access rather than a thread-static field access. Beyond this, `TryParse` also improved for hexadecimal inputs, thanks to [dotnet/runtime#52470](https://github.com/dotnet/runtime/pull/52470), which changed an internal routine used to determine whether a character is valid hex, making it branchless (which makes its performance consistent regardless of inputs or branch prediction) and removing the dependency on a lookup table. Corresponding functionality on `Utf8Parser` also improved. Whereas a method like `Int32.TryParse` parses data from a sequence of `char`s (e.g. `ReadOnlySpan<char>`), `Utf8Parser.TryParse` parses data from a sequence of `byte`s (e.g. `ReadOnlySpan<byte>`) interpreted as UTF8 data. [dotnet/runtime#52423](https://github.com/dotnet/runtime/pull/52423) also improved the performance of `TryParse` for `long` and `ulong` values. This is another good example of an optimization tradeoff: the tweaks employed here benefit most values but end up slightly penalizing extreme values.

```
private byte[] _buffer = new byte[10];

[GlobalSetup]
public void Setup() => Utf8Formatter.TryFormat(12345L, _buffer, out _);

[Benchmark]
public bool TryParseInt64() => Utf8Parser.TryParse(_buffer, out long _, out int _);
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| TryParseInt64 | .NET Framework 4.8 | 26.490 ns | 1.00 |
| TryParseInt64 | .NET 5.0 | 7.724 ns | 0.29 |
| TryParseInt64 | .NET 6.0 | 6.552 ns | 0.25 |

Then there’s [dotnet/runtime#51190](https://github.com/dotnet/runtime/pull/51190), which recognizes that, at a very low-level, when extending a 32-bit value in a 64-bit process to be native word size, it’s ever so slightly more efficient from a codegen perspective to zero-extend rather than sign-extend; if the code is happening on a path where those are identical (i.e. we know by construction we don’t have negative values), on a really hot path it can be beneficial to change.

Along with the new and improved support for interpolated strings, a lot of cleanup across dotnet/runtime was also done with regards to string formatting. [dotnet/runtime#50267](https://github.com/dotnet/runtime/pull/50267), [dotnet/runtime#55738](https://github.com/dotnet/runtime/pull/55738), [dotnet/runtime#44765](https://github.com/dotnet/runtime/pull/44765), [dotnet/runtime#44746](https://github.com/dotnet/runtime/pull/44746), and [dotnet/runtime#55831](https://github.com/dotnet/runtime/pull/55831) all updated code to use better mechanisms. [dotnet/runtime#commits/91f39e](https://github.com/dotnet/runtime/pull/51653/commits/91f39e2c545b853ced0bfae08653c89381b32c42) alone updated over 3000 lines of string-formatting related code. Some of these changes are to use string interpolation where it wasn’t used before due to knowledge of the performance implications; for example, there’s code to read the `status` file in `procfs` on Linux, and that needs to compose the path to the file to use. Previously that code was:

```
internal static string GetStatusFilePathForProcess(int pid) =>
    RootPath + pid.ToString(CultureInfo.InvariantCulture) + StatusFileName;
```

which ends up first creating a string from the `int pid`, and then doing a `String.Concat` on the resulting strings. Now, it’s:

```
internal static string GetStatusFilePathForProcess(int pid) =>
    string.Create(null, stackalloc char[256], $"{RootPath}{(uint)pid}{MapsFileName}");
```

which takes advantage of the new `string.Create` overload that works with interpolated strings and enables doing the interpolation using stack-allocated buffer space. Also note the lack of the `CultureInfo.InvariantCulture` call; that’s because when formatting an `int`, the culture is only needed if the number is negative and would require looking up the negative sign symbol for the relevant culture, but here we know that process IDs are never negative, making the culture irrelevant. As a bonus, the implementation casts the known non-negative value to `uint`, which is slightly faster to format than `int`, exactly because we needn’t check for a sign.

Another pattern of cleanup in those PRs was avoiding creating strings in places spans would suffice. For example, this code from Microsoft.CSharp.dll:

```
int arity = int.Parse(t.Name.Substring("VariantArray".Length), CultureInfo.InvariantCulture);
```

was replaced by:

```
int arity = int.Parse(t.Name.AsSpan("VariantArray".Length), provider: CultureInfo.InvariantCulture);
```

avoiding the intermediate string allocation. Or this code from System.Private.Xml.dll:

```
if (s.Substring(i) == "INF")
```

which was replaced by:

```
if (s.AsSpan(i).SequenceEqual("INF"))
```

Another pattern is using something other than `string.Format` when the power of `string.Format` is unwarranted. For example, this code existed in Microsoft.Extensions.FileSystemGlobbing:

```
return string.Format("{0}/{1}", left, right);
```

where both `left` and `right` are strings. This is forcing the system to parse the composite format string and incur all the associated overhead, when at the end of the day this can be a simple concat operation, which the C# compiler will employ for an interpolated string when all the parts are strings and there are sufficiently few to enable using one of the non-params-array `string.Concat` overloads:

```
return $"{left}/{right}";
```

We can see that difference with a simple benchmark:

```
private string _left = "hello";
private string _right = "world";

[Benchmark(Baseline = true)]
public string Format() => string.Format("{0}/{1}", _left, _right);

[Benchmark]
public string Interpolated() => $"{_left}/{_right}";
```

| Method | Mean | Ratio | Allocated |
| --- | --- | --- | --- |
| Format | 58.74 ns | 1.00 | 48 B |
| Interpolated | 14.73 ns | 0.25 | 48 B |

.NET 6 also continues the trend of exposing more span-based APIs for things that would otherwise result in creating strings or arrays. For example, [dotnet/runtime#57357](https://github.com/dotnet/runtime/pull/57357) adds a new `ValueSpan` property to the `Capture` class in `System.Text.RegularExpressions` (to go along with the string-returning `Value` property that’s already there). That means code can now extract a `ReadOnlySpan<char>` for a `Match`, `Group`, or `Capture` rather than having to allocate a string to determine what matched.

Then there are the plethora of changes that remove an array or boxing allocation here, an unnecessary LINQ query there, and so on. For example:

-   [dotnet/runtime#56207](https://github.com/dotnet/runtime/pull/56207) from [@teo-tsirpanis](https://github.com/teo-tsirpanis) removed ~50 `byte[]` allocations from `System.Reflection.MetadataLoadContext`, by changing some static readonly `byte[]` fields to instead be `ReadOnlySpan<byte>` properties, taking advantage of the C# compiler’s ability to store constant data used in this capacity in a very efficient manner.
-   [dotnet/runtime#55652](https://github.com/dotnet/runtime/pull/55652) removed a `char[]` allocation from `System.Xml.UniqueId.ToString()`, replacing the use of a temporary `new char[length]` followed by a `new string(charArray)` to instead use a call to `string.Create` that was able to populate the string instance directly.
-   [dotnet/runtime#49485](https://github.com/dotnet/runtime/pull/49485) and [dotnet/runtime#49488](https://github.com/dotnet/runtime/pull/49488) removed `StringBuilder` allocations, where a `StringBuilder` was being allocated and then appended to multiple times, to instead use a single call to `string.Join` (which has a much more optimized implementation), making the code both simpler and faster. These also included a few changes where `StringBuilder`s were being allocated and then just a handful of appends were always being performed, when a simple `string.Concat` would suffice.
-   [dotnet/runtime#50483](https://github.com/dotnet/runtime/pull/50483) avoided a closure and delegate allocation in `System.ComponentModel.Annotations` by minimizing the scope of the data being closed over.
-   [dotnet/runtime#50502](https://github.com/dotnet/runtime/pull/50502) avoided a closure and delegate allocation in `ClientWebSocket.ConnectAsync` by open-coding a loop rather than using using `List<T>.Find` with a lambda that captured surrounding state.
-   [dotnet/runtime#50512](https://github.com/dotnet/runtime/pull/50512) avoided a closure and delegate in `Regex` that slipped in due to using a captured local rather than the exact same state that was already being passed into the lambda. These kinds of issues are easy to miss, and they’re one of the reasons I love being able to add `static` to lambdas, to ensure they’re not closing over anything unexpectedly.
-   [dotnet/runtime#50496](https://github.com/dotnet/runtime/pull/50496) and [dotnet/runtime#50387](https://github.com/dotnet/runtime/pull/50387) avoided closure and delegate allocations in `System.Diagnostics.Process`, by being more deliberate about how state is passed around.
-   [dotnet/runtime#50357](https://github.com/dotnet/runtime/pull/50357) avoided a closure and delegate allocation in the polling mechanism employed by `DiagnosticCounter`.
-   [dotnet/runtime#54621](https://github.com/dotnet/runtime/pull/54621) avoided cloning an immutable `Version` object. The original instance could be used just as easily; the only downside would be if someone was depending on object identity here for some reason, of which there’s very low risk.
-   [dotnet/runtime#51119](https://github.com/dotnet/runtime/pull/51119) fixed `DispatchProxyGenerator`, which was almost humorously cloning an array from another array just to get the new array’s length… when it could have just used the original array’s length.
-   [dotnet/runtime#47473](https://github.com/dotnet/runtime/pull/47473) is more complicated than some of these other PRs, but it removed the need for an `OrderedDictionary` (which itself creates an `ArrayList` and a `Hashtable`) in `TypeDescriptor.GetAttributes`, instead using a `List<T>` and a `HashSet<T>` directly.
-   [dotnet/runtime#44495](https://github.com/dotnet/runtime/pull/44495) changed `StreamWriter`‘s `byte[]` buffer to be lazily allocated. For scenarios where only small payloads are written synchronously, the `byte[]` may never be needed.
-   [dotnet/runtime#46455](https://github.com/dotnet/runtime/pull/46455) is fun, and a holdover from where this code originated in the .NET Framework. The PR deletes a bunch of code, including the preallocation of a `ThreadAbortException` that could be used by the system should one ever be needed and the system is too low on memory to allocate one. That might have been useful, if thread aborts were still a thing. Which they’re not. Goodbye.
-   [dotnet/runtime#47453](https://github.com/dotnet/runtime/pull/47453). Enumerating a `Hashtable` using a standard `foreach`, even if all of the keys and values are reference types, still incurs an allocation per iteration, as the `DictionaryEntry` struct yielded for each iteration gets boxed. To avoid this, `Hashtable`‘s enumerator implemented `IDictionaryEnumerable`, which provides strongly-typed access to the `DictionaryEntry` and enables direct use of `MoveNext`/`Entry` to avoid that allocation. This PR takes advantage of that to avoid a bunch of boxing allocations as part of `EnvironmentVariablesConfigurationProvider.Load`.
-   [dotnet/runtime#49883](https://github.com/dotnet/runtime/pull/49883). `Lazy<T>` is one of those types that’s valuable when used correctly, but that’s also easy to overuse. Creating a `Lazy<T>` involves at least one if not multiple allocations beyond the cost of whatever’s being lazily-created, plus a delegate invocation to create the thing. But sometimes the double-checked locking you get by default is overkill, and all you really need is an `Interlocked.CompareExchange` to provide simple and efficient optimistic concurrency. This PR avoids a `Lazy<T>` for just such a case in `UnnamedOptionsManager`.

### JSON

`System.Text.Json` was [introduced in .NET Core 3.0](https://devblogs.microsoft.com/dotnet/try-the-new-system-text-json-apis/) with performance as a primary goal. .NET 5 delivered an enhanced version of the library, providing [new APIs and even better performance](https://devblogs.microsoft.com/dotnet/whats-next-for-system-text-json/), and .NET 6 continues that trend.

There have been multiple PRs in .NET 6 to improve the performance of different aspects of `System.Text.Json`. [dotnet/runtime#46460](https://github.com/dotnet/runtime/pull/46460) from [@lezzi](https://github.com/lezzi) is a small but valuable change that avoids boxing every key in a dictionary with a value type `TKey`. [dotnet/runtime#51367](https://github.com/dotnet/runtime/pull/51367) from [@devsko](https://github.com/devsko) makes serializing `DateTime`s faster by reducing the cost of trimming off ending `0`s. And [dotnet/runtime#55350](https://github.com/dotnet/runtime/pull/55350) from [@CodeBlanch](https://github.com/CodeBlanch) cleans up a bunch of `stackalloc` usage in the library, including changing a bunch of call sites from using a variable to instead using a constant, the latter of which the JIT can better optimize.

But arguably the biggest performance improvement in `System.Text.Json` in .NET 6 comes from source generation. `JsonSerializer` needs information about the types it’s serializing to know what what to serialize and how to serialize it. It retrieves that data via reflection, examining for example what properties are exposed on a type and whether there are any customization attributes applied. But reflection is relatively expensive, and certainly not something you’d want to do every time you serialized an instance of a type, so `JsonSerializer` caches that information. That cached information may include, for example, delegates used to access the properties on an instance in order to retrieve the data that needs to be serialized. Depending on how the `JsonSerializer` is configured, that delegate might use reflection to invoke the property, or if the system permits it, it might point to specialized code emitted via reflection emit. Unfortunately, both of those techniques have potential downsides. Gathering all of this data, and potentially doing this reflection emit work, at run-time has a cost, and it can measurably impact both the startup performance and the working set of an application. It also leads to increased size, as all of the code necessary to enable this (including support for reflection emit itself) needs to be kept around just in case the serializer needs it. The new `System.Text.Json` source generator introduced in .NET 6 addresses this.

Generating source during a build is nothing new; these techniques have been used in and out of the .NET ecosystem for decades. What is new, however, is the C# compiler making the capability a first-class feature, and core libraries in .NET taking advantage of it. Just as the compiler allows for [analyzers](https://docs.microsoft.com/visualstudio/extensibility/getting-started-with-roslyn-analyzers) to be plugged into a build to add custom analysis as part of the compiler’s execution (with the compiler giving the analyzer access to all of the syntactical and semantic data it gathers and creates), the compiler now also enables a [source generator](https://docs.microsoft.com/dotnet/csharp/roslyn-sdk/source-generators-overview) to access the same information and then spit out additional C# code that’s incorporated into the same compilation unit. This makes it very attractive for doing certain operations at compile-time that code may have been doing previously via reflection and reflection emit at run-time… like analyzing types as part of a serializer in order to generate fast member accessors.

[dotnet/runtime#51149](https://github.com/dotnet/runtime/pull/51149), [dotnet/runtime#51300](https://github.com/dotnet/runtime/pull/51300), and [dotnet/runtime#51528](https://github.com/dotnet/runtime/pull/51528) introduce a new `System.Text.Json.SourceGeneration` component, included as part of the .NET 6 SDK. I create a new app, and I can see in Visual Studio the generator is automatically included:

[![Visual Studio Solution Explorer showing JSON source generator](https://devblogs.microsoft.com/dotnet/wp-content/uploads/sites/10/2021/08/JsonSourceGeneratorSolutionExplorer.png)](https://devblogs.microsoft.com/dotnet/wp-content/uploads/sites/10/2021/08/JsonSourceGeneratorSolutionExplorer.png)

Then I can add this to my program:

```
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonExample;

class Program
{
    public static void Main()
    {
        JsonSerializer.Serialize(Console.OpenStandardOutput(), new BlogPost { Title = ".NET 6 Performance", Author = "Me", PublicationYear = 2021 }, MyJsonContext.Default.BlogPost);
    }
}

internal class BlogPost
{
    public string? Title { get; set; }
    public string? Author { get; set; }
    public int PublicationYear { get; set; }
}

[JsonSerializable(typeof(BlogPost))]
internal partial class MyJsonContext : JsonSerializerContext { }
```

Over what I might have written in the past, note the addition of the partial `MyJsonContext` class (the name here doesn’t matter) and the additional `MyJsonContext.Default.BlogPost` argument to JsonSerializer.Serialize. As you’d expect, when I run it, I get this output:

```
{"Title":".NET 6 Performance","Author":"Me","PublicationYear":2021}
```

What’s interesting, however, is what happened behind the scenes. If you look again at Solution Explorer, you’ll see a bunch of code the JSON source generator output:

[![Generated JSON files](https://devblogs.microsoft.com/dotnet/wp-content/uploads/sites/10/2021/08/JsonSourceGeneratorSolutionExplorer_Populated.png)](https://devblogs.microsoft.com/dotnet/wp-content/uploads/sites/10/2021/08/JsonSourceGeneratorSolutionExplorer_Populated.png)

Those files essentially contain all of the glue code reflection and reflection emit would have generated, including lines like:

```
getter: static (obj) => ((global::JsonExample.BlogPost)obj).Title,
setter: static (obj, value) => ((global::JsonExample.BlogPost)obj).Title = value,
```

highlighting the property accessor delegates being generated as part of source generation. The `JsonSerializer` is then able to use these delegates just as it’s able to use ones that use reflection or that were generated via reflection emit.

As long as the source generator is spitting out all this code for doing at compile-time what was previously done at run-time, it can take things a step further. If I were writing my own serializer customized specifically for my `BlogPost` class, I wouldn’t use all this indirection… I’d just use a writer directly and write out each property, e.g.

```
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JsonExample;

class Program
{
    public static void Main()
    {
        using var writer = new Utf8JsonWriter(Console.OpenStandardOutput());
        BlogPostSerialize(writer, new BlogPost { Title = ".NET 6 Performance", Author = "Me", PublicationYear = 2021 });
        writer.Flush();
    }

    private static void BlogPostSerialize(Utf8JsonWriter writer, BlogPost value)
    {
        writer.WriteStartObject();
        writer.WriteString(nameof(BlogPost.Title), value.Title);
        writer.WriteString(nameof(BlogPost.Author), value.Author);
        writer.WriteNumber(nameof(BlogPost.PublicationYear), value.PublicationYear);
        writer.WriteEndObject();
    }
}

internal class BlogPost
{
    public string? Title { get; set; }
    public string? Author { get; set; }
    public int PublicationYear { get; set; }
}
```

There’s no reason the source generator shouldn’t be able to output such a streamlined implementation. And as of [dotnet/runtime#53212](https://github.com/dotnet/runtime/pull/53212), it can. The generated code contains this method on the `MyJsonContext` class:

```
private static void BlogPostSerialize(global::System.Text.Json.Utf8JsonWriter writer, global::Benchmarks.BlogPost value)
{
    if (value == null)
    {
        writer.WriteNullValue();
        return;
    }

    writer.WriteStartObject();
    writer.WriteString(TitlePropName, value.Title);
    writer.WriteString(AuthorPropName, value.Author);
    writer.WriteNumber(PublicationYearPropName, value.PublicationYear);

    writer.WriteEndObject();
}
```

Looks familiar. Note, too, that the design of this fast path code enables the `JsonSerializer` to use it as well: if the serializer is passed a `JsonSerializerContext` that has a fast-path delegate, it’ll use it, which means code only needs to explicitly call the fast-path if it really wants to eke out the last mile of performance.

```
private Utf8JsonWriter _writer = new Utf8JsonWriter(Stream.Null);
private BlogPost _blogPost = new BlogPost { Title = ".NET 6 Performance", Author = "Me", PublicationYear = 2021 };

[Benchmark(Baseline = true)]
public void JsonSerializerWithoutFastPath()
{
    _writer.Reset();
    JsonSerializer.Serialize(_writer, _blogPost);
    _writer.Flush();
}

[Benchmark]
public void JsonSerializerWithFastPath()
{
    _writer.Reset();
    JsonSerializer.Serialize(_writer, _blogPost, MyJsonContext.Default.BlogPost);
    _writer.Flush();
}

[Benchmark]
public void DirectFastPath()
{
    _writer.Reset();
    MyJsonContext.Default.BlogPost.Serialize(_writer, _blogPost);
    _writer.Flush();
}
```

| Method | Mean | Ratio | Allocated |
| --- | --- | --- | --- |
| JsonSerializerWithoutFastPath | 239.9 ns | 1.00 | – |
| JsonSerializerWithFastPath | 150.9 ns | 0.63 | – |
| DirectFastPath | 134.9 ns | 0.56 | – |

The impact of these improvements can be quite meaningful. [aspnet/Benchmarks#1683](https://github.com/aspnet/Benchmarks/pull/1683#issuecomment-864841394) is a good example. It updates the ASP.NET implementation of the [TechEmpower caching benchmark](https://www.techempower.com/benchmarks/) to use the JSON source generator. Previously, a significant portion of the time in that benchmark was being spent doing JSON serialization using `JsonSerializer`, making it a prime candidate. With the changes to use the source generator and benefit from the fast path implicitly being used, the benchmark gets ~30% faster.

The blog post [Try the new System.Text.Json source generator](https://devblogs.microsoft.com/dotnet/try-the-new-system-text-json-source-generator) provides a lot more detail and background.

### Interop

One of the really neat projects worked on during .NET 6 is another source generator, this time one related to interop. Since the beginning of .NET, C# code can call out to native C functions via the P/Invoke (Platform Invoke) mechanism, whereby a `static extern` method is annotated as `[DllImport]`. However, not all `[DllImport]`s are created equally. Certain `[DllImport]`s are referred to as being “blittable,” which really just means the runtime doesn’t need to do any special transformation or marshaling as part of the call (that includes the [signature’s types being blittable](https://docs.microsoft.com/dotnet/framework/interop/blittable-and-non-blittable-types), but also the `[DllImport(...)]` attribute itself not declaring the need for any special processing, like `SetLastError = true`). For those that aren’t blittable, the runtime needs to generate a “stub” that does any marshaling or manipulation necessary. For example, if you write:

```
[DllImport(SetLastError = true)]
private static extern bool GetValue(SafeHandle handle);
```

for a native API defined in C as something like the following on Windows:

```
BOOL GetValue(HANDLE h);
```

or the following on Unix:

```
int32_t GetValue(void* fileDescriptor);
```

there are three special things the runtime needs to handle:

1.  The `SafeHandle` needs to be marshaled as an `IntPtr`, and the runtime needs to ensure the `SafeHandle` won’t be released during the native call.
2.  The `bool` return value needs to be marshaled from a 4-byte integer value.
3.  The `SetLastError = true` needs to properly ensure any error from the native call is consumable appropriately.

To do so, the runtime effectively needs to translate that `[DllImport]` into something like:

```
private static bool GetValue(SafeHandle handle)
{
    bool success = false;
    try
    {
        handle.DangerousAddRef(ref success);
        IntPtr ptr = handle.DangerousGetHandle();

        Marshal.SetLastSystemError(0);
        int result = __GetValue(ptr);
        Marshal.SetLastPInvokeError(Marshal.GetLastSystemError());

        return result != 0;
    }
    finally
    {
        if (success)
        {
            handle.DangerousRelease();
        }
    }
}

[DllImport]
private static extern int __GetValue(IntPtr handle);
```

using dynamic code generation at run-time to generate a “stub” method that in turn calls an underlying `[DllImport]` that actually is blittable. Doing that at run-time has multiple downsides, including the startup impact on having to do this code generation on first use. So, for .NET 7 we plan to enable a source generator to do it, and the groundwork has been laid in .NET 6 by building out a prototype. While the [P/Invoke source generator](https://github.com/dotnet/runtime/issues/43060) won’t ship as part of .NET 6, as part of that prototype various investments were made that will ship in .NET 6, such as changing `[DllImport]`s that could easily be made blittable to be so. You can see an example of that in [dotnet/runtime#54029](https://github.com/dotnet/runtime/pull/54029), which changed a handful of `[DllImport]`s in the `System.IO.Compression.Brotli` library to be blittable. For example, this method:

```
[DllImport(Libraries.CompressionNative)]
internal static extern unsafe bool BrotliDecoderDecompress(nuint availableInput, byte* inBytes, ref nuint availableOutput, byte* outBytes);
```

required the runtime to generate a stub in order to 1) handle the return `bool` marshaling from a 4-byte integer, and 2) handle pinning the `availableOutput` parameter passed as a `ref`. Instead, it can be defined as:

```
[DllImport(Libraries.CompressionNative)]
internal static extern unsafe int BrotliDecoderDecompress(nuint availableInput, byte* inBytes, nuint* availableOutput, byte* outBytes);
```

which is blittable, and then a call site like:

```
nuint availableOutput = (nuint)destination.Length;
bool success = Interop.Brotli.BrotliDecoderDecompress((nuint)source.Length, inBytes, ref availableOutput, outBytes);
```

can be tweaked to:

```
nuint availableOutput = (nuint)destination.Length;
bool success = Interop.Brotli.BrotliDecoderDecompress((nuint)source.Length, inBytes, &availableOutput, outBytes) != 0;
```

Boom, a small tweak and we’ve saved an extra unlikely-to-be-inlined method call and avoided the need to even generate the stub in the first place. [dotnet/runtime#53968](https://github.com/dotnet/runtime/pull/53968) makes all of the `[DllImports]` for interop with `zlib` (System.IO.Compression) to be blittable. And [dotnet/runtime#54370](https://github.com/dotnet/runtime/pull/54370) fixes up more `[DllImport]`s across `System.Security.Cryptography`, `System.Diagnostics`, `System.IO.MemoryMappedFiles`, and elsewhere to be blittable, as well.

Another area in which we’ve seen cross-cutting improvements in .NET 6 is via the use of function pointers to simplify and streamline interop. C# 9 added support for function pointers, which, via the `delegate*` syntax, enable efficient access to the `ldftn` and `calli` IL instructions. Let’s say you’re the `PosixSignalRegistration` type, which was implemented in [dotnet/runtime#54136](https://github.com/dotnet/runtime/pull/54136) from [@tmds](https://github.com/tmds), [dotnet/runtime#55333](https://github.com/dotnet/runtime/pull/55333), and [dotnet/runtime#55552](https://github.com/dotnet/runtime/pull/55552) to enable code to register a callback to handle a POSIX signal. Both the Unix and Windows implementations of this type need to hand off to native code a callback to be invoked when a signal is received. On Unix, the native function that’s called to register the callback is declared as:

```
typedef int32_t (*PosixSignalHandler)(int32_t signalCode, PosixSignal signal);
void SystemNative_SetPosixSignalHandler(PosixSignalHandler signalHandler);
```

expecting a function pointer it can invoke. Thankfully, on the managed side we want to hand off a static method, so we don’t need to get bogged down in the details of how we pass an instance method, keep the relevant state rooted, and so on. Instead, we can declare the `[DllImport]` as:

```
[DllImport(Libraries.SystemNative, EntryPoint = "SystemNative_SetPosixSignalHandler")]
internal static extern unsafe void SetPosixSignalHandler(delegate* unmanaged<int, PosixSignal, int> handler);
```

Now, we define a method we want to be called that’s compatible with this function pointer type:

```
[UnmanagedCallersOnly]
private static int OnPosixSignal(int signo, PosixSignal signal) { ... }
```

and, finally, we can pass the address of this method to the native code:

```
Interop.Sys.SetPosixSignalHandler(&OnPosixSignal);
```

Nowhere did we need to allocate a delegate and store it into a static field to prevent it from being collected, just so we can hand off the address of this `OnPosixSignal` method; instead, we just pass down the method’s address. This ends up being simpler and more efficient, and multiple PRs in .NET 6 converted delegate-based interop to function pointer-based interop. [dotnet/runtime#43793](https://github.com/dotnet/runtime/pull/43793) and [dotnet/runtime#43514](https://github.com/dotnet/runtime/pull/43514) converted a bunch of interop on both Windows and Unix to use function pointers. [dotnet/runtime#54636](https://github.com/dotnet/runtime/pull/54636) and [dotnet/runtime#54884](https://github.com/dotnet/runtime/pull/54884) did the same for `System.Drawing` as part of a larger effort to migrate `System.Drawing` to use `System.Runtime.InteropServices.ComWrappers`. [dotnet/runtime#46690](https://github.com/dotnet/runtime/pull/46690) moved `DateTime` to being a fully managed implementation rather than using [“FCalls”](https://github.com/dotnet/runtime/blob/57bfe474518ab5b7cfe6bf7424a79ce3af9d6657/docs/design/coreclr/botr/corelib.md#calling-from-managed-to-native-code) into the runtime to get the current time, and in doing so used function pointers to be able to store a pointer to desired native OS function for getting the current time. [dotnet/runtime#52090](https://github.com/dotnet/runtime/pull/52090) converted the macOS implementation of `FileSystemWatcher` to use function pointers. And [dotnet/runtime#52192](https://github.com/dotnet/runtime/pull/52192) did the same for `System.Net.NetworkInformation`.

Beyond these cross-cutting changes, there was also more traditional optimization investment in interop. The `Marshal` class has long provided the `AllocHGlobal` and `FreeHGlobal` methods which .NET developers could use effectively as the equivalent of `malloc` and `free`, in situations where natively allocated memory was preferable to allocation controlled by the GC. [dotnet/runtime#41911](https://github.com/dotnet/runtime/pull/41911) revised the implementation of these and other `Marshal` methods as part of moving all of the `Marshal` allocation-related implementations out of native code in the runtimes up into C#. In doing so, a fair amount of overhead was removed, in particular on Unix where a layer of wrappers was removed, as is evident from this benchmark run on Ubuntu:

```
[Benchmark]
public void AllocFree() => Marshal.FreeHGlobal(Marshal.AllocHGlobal(100));
```

| Method | Runtime | Mean | Ratio |
| --- | --- | --- | --- |
| AllocFree | .NET 5.0 | 58.50 ns | 1.00 |
| AllocFree | .NET 6.0 | 28.21 ns | 0.48 |

In a similar area, the new `System.Runtime.InteropServices.NativeMemory` class ([dotnet/runtime#54006](https://github.com/dotnet/runtime/pull/54006)) provides fast APIs for allocating, reallocating, and freeing native memory, with options including requiring the memory having a particular alignment or having the memory be forcibly zeroed out (note the above numbers and the below numbers were taken on different machines, the above on Ubuntu and the below on Windows, and are not directly comparable).

```
[Benchmark(Baseline = true)]
public void AllocHGlobal() => Marshal.FreeHGlobal(Marshal.AllocHGlobal(100));

[Benchmark]
public void Alloc() => NativeMemory.Free(NativeMemory.Alloc(100));
```

| Method | Mean | Ratio | RatioSD |
| --- | --- | --- | --- |
| AllocHGlobal | 58.34 ns | 1.00 | 0.00 |
| Alloc | 48.33 ns | 0.83 | 0.02 |

There’s also the new `MemoryMarshal.CreateReadOnlySpanFromNullTerminated` method ([dotnet/runtime#47539](https://github.com/dotnet/runtime/pull/47539)), which provides two overloads, one for `char*` and one for `byte*`, and which is intended to simplify the handling of null-terminated strings received while doing interop. As an example, `FileSystemWatcher`‘s implementation on macOS would receive from the operating system a pointer to a null-terminated UTF8 string representing the path of the file that changed. With just the `byte*` pointer to the string, the implementation had code that looked like this:

```
byte* temp = nativeEventPath;
int byteCount = 0;
while (*temp != 0)
{
    temp++;
    byteCount++;
}
var span = new ReadOnlySpan<byte>(nativeEventPath, byteCount);
```

in order to create a span representing the string beginning to end. Now, the implementation is simply:

```
ReadOnlySpan<byte> eventPath = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(nativeEventPath);
```

More maintainable, safer code, but there’s also a performance benefit. `CreateReadOnlySpanFromNullTerminated` employs a vectorized search for the null terminator, making it typically much faster than the open-coded manual loop.

```
private IntPtr _ptr;

[GlobalSetup]
public void Setup() =>
    _ptr = Marshal.StringToCoTaskMemUTF8("And yet, by heaven, I think my love as rare. As any she belies with false compare.");

[GlobalCleanup]
public void Cleanup() =>
    Marshal.FreeCoTaskMem(_ptr);

[Benchmark(Baseline = true)]
public unsafe ReadOnlySpan<byte> Old()
{
    int byteCount = 0;
    for (byte* p = (byte*)_ptr; *p != 0; p++) byteCount++;
    return new ReadOnlySpan<byte>((byte*)_ptr, byteCount);
}

[Benchmark]
public unsafe ReadOnlySpan<byte> New() =>
    MemoryMarshal.CreateReadOnlySpanFromNullTerminated((byte*)_ptr);
```

| Method | Mean | Ratio |
| --- | --- | --- |
| Old | 38.536 ns | 1.00 |
| New | 6.597 ns | 0.17 |

### Tracing

.NET has multiple tracing implementations, with `EventSource` at the heart of those used in the most performance-sensitive systems. The runtime itself traces details and exposes counters for the JIT, GC, ThreadPool, and more through a `"System.Runtime"` event source, and many other components up and down the stack do the same with their own. Even just within the core libraries, among others you can find the `"System.Diagnostics.Metrics"` event source, which is intended to enable out-of-process tools to do ad-hoc monitoring of the new [OpenTelemtry](https://devblogs.microsoft.com/dotnet/opentelemetry-net-reaches-v1-0/) [Metrics APIs](https://devblogs.microsoft.com/dotnet/announcing-net-6-preview-5/#libraries-support-for-opentelemetry-metrics); the `"System.Net.Http"` event source that exposes information such as when requests start and complete; the `"System.Net.NameResolution"` event source that exposes information such as the number of DNS lookups that have been performed; the `"System.Net.Security"` event source that exposes data about TLS handshakes; the `"System.Net.Sockets"` event source that enables monitoring of connections being made and data being transferred; and the `"System.Buffers.ArrayPoolEventSource"` event source that gives a window into arrays being rented and returned and dropped and trimmed. This level of usage demands the system to be as efficient as possible.

`EventSource`\-derived types use overloads of `EventSource.WriteEvent` or `EventSource.WriteEventCore` to do the core of their logging. There are then multiple ways that data from an `EventSource` can be consumed. One way is via ETW (Event Tracing for Windows), through which another process can request an `EventSource` start tracing and the relevant data will be written by the operating system to a log for subsequent analysis with a tool like Visual Studio, PerfView, or Windows Performance Analyzer. The most general `WriteEvent` overload accepts an `object[]` of all the data to trace, and [dotnet/runtime#54925](https://github.com/dotnet/runtime/pull/54925) reduced the overhead of using this API, specifically when the data is being consumed by ETW, which has dedicated code paths in the implementation; the PR reduced allocation by 3-4x for basic use cases by avoiding multiple temporary `List<object>` and `object[]` arrays, leading also to an ~8% improvement in throughput.

Another increasingly common way `EventSource` data can be consumed is via [EventPipe](https://docs.microsoft.com/dotnet/core/diagnostics/eventpipe), which provides a cross-platform mechanism for serializing `EventSource` data either to a `.nettrace` file or to an out-of-process consumer, such as a tool like [dotnet-counters](https://docs.microsoft.com/dotnet/core/diagnostics/dotnet-counters). Given the high rate at which data can be generated, it’s important that this mechanism be as low-overhead as possible. [dotnet/runtime#50797](https://github.com/dotnet/runtime/pull/50797) changed how access to buffers in EventPipe were synchronized, leading to significant increases in event throughput, on all operating systems. [dotnet/runtime#46555](https://github.com/dotnet/runtime/pull/46555) also helps here. If either ETW or EventPipe was being used to consume events, `EventSource` would P/Invoke into native code for each, but if only one of them was being used, that would lead to an unnecessary P/Invoke; the PR addressed this simply by checking whether the P/Invoke is necessary based on the known state of the consumers.

Another way `EventSource` data can be consumed is in-process via a custom `EventListener`. Code can derive from `EventListener` and override a few methods to say what `EventSource` should be subscribed to and what should be done with the data. For example, here’s a simple app that uses an `EventListener` to dump to the console the events generated for a single HTTP request by the \`”System.Net.Http”“ event source:

```
using System;
using System.Diagnostics.Tracing;
using System.Linq;
using System.Net.Http;

using var listener = new HttpConsoleListener();
using var hc = new HttpClient();
await hc.GetStringAsync("https://dotnet.microsoft.com/");

sealed class HttpConsoleListener : EventListener
{
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource.Name == "System.Net.Http")
            EnableEvents(eventSource, EventLevel.LogAlways);
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        string? payload =
            eventData.Payload is null ? null :
            eventData.PayloadNames != null ? string.Join(", ", eventData.PayloadNames.Zip(eventData.Payload, (n, v) => $"{n}={v}")) :
            string.Join(", ", eventData.Payload);
        Console.WriteLine($"[{eventData.TimeStamp:o}] {eventData.EventName}: {payload}");
    }
}
```

and the output it produced when I ran it:

```
[2021-08-06T15:38:47.4758871Z] RequestStart: scheme=https, host=dotnet.microsoft.com, port=443, pathAndQuery=/, versionMajor=1, versionMinor=1, versionPolicy=0
[2021-08-06T15:38:47.5981990Z] ConnectionEstablished: versionMajor=1, versionMinor=1
[2021-08-06T15:38:47.5995700Z] RequestLeftQueue: timeOnQueueMilliseconds=86.1312, versionMajor=1, versionMinor=1
[2021-08-06T15:38:47.6011745Z] RequestHeadersStart:
[2021-08-06T15:38:47.6019475Z] RequestHeadersStop:
[2021-08-06T15:38:47.7591555Z] ResponseHeadersStart:
[2021-08-06T15:38:47.7628194Z] ResponseHeadersStop:
[2021-08-06T15:38:47.7648776Z] ResponseContentStart:
[2021-08-06T15:38:47.7665603Z] ResponseContentStop:
[2021-08-06T15:38:47.7667290Z] RequestStop:
[2021-08-06T15:38:47.7684536Z] ConnectionClosed: versionMajor=1, versionMinor=1
```

The other mechanisms for consuming events are more efficient, but being able to write a custom `EventListener` like this is very flexible and allows for a myriad of interesting uses, so we still want to drive down the overhead associated with all of these callbacks. [dotnet/runtime#44026](https://github.com/dotnet/runtime/pull/44026), [dotnet/runtime#51822](https://github.com/dotnet/runtime/pull/51822), [dotnet/runtime#52092](https://github.com/dotnet/runtime/pull/52092), and [dotnet/runtime#52455](https://github.com/dotnet/runtime/pull/52455) all contributed here, doing things like wrapping a `ReadOnlyCollection<object>` directly around an `object[]` created with the exact right size rather around an intermediate `List<object>` dynamically grown; using a singleton collection for empty payloads; avoiding unnecessary `[ThreadStatic]` accesses; avoiding recalcuating information and instead calculating it once and passing it to everywhere that needs the value; using `Type.GetTypeCode` to quickly jump to the handling code for the relevant primitive rather than a large cascade of `if`s; reducing the size of `EventWrittenEventArgs` in the common case by pushing off lesser-used fields to a contingently-allocated class; and so on. This benchmark shows an example impact of those changes.

```
private BenchmarkEventListener _listener;

[GlobalSetup]
public void Setup() => _listener = new BenchmarkEventListener();
[GlobalCleanup]
public void Cleanup() => _listener.Dispose();

[Benchmark]
public void Log()
{
    BenchmarkEventSource.Log.NoArgs();
    BenchmarkEventSource.Log.MultipleArgs("hello", 6, 0);
}

private sealed class BenchmarkEventListener : EventListener
{
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        if (eventSource is BenchmarkEventSource)
            EnableEvents(eventSource, EventLevel.LogAlways);
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData) { }
}

private sealed class BenchmarkEventSource : EventSource
{
    public static readonly BenchmarkEventSource Log = new BenchmarkEventSource();

    [Event(1)]
    public void NoArgs() => WriteEvent(1);

    [Event(2)]
    public void MultipleArgs(string arg1, int arg2, int arg3) => WriteEvent(2, arg1, arg2, arg3);
}
```

| Method | Runtime | Mean | Ratio | Allocated |
| --- | --- | --- | --- | --- |
| Log | .NET 5.0 | 394.4 ns | 1.00 | 472 B |
| Log | .NET 6.0 | 126.9 ns | 0.32 | 296 B |

### Startup

There are many things that impact how long it takes an application to start up. Code generation plays a large role, which is why .NET has technology like tiered JIT compilation and ReadyToRun. Managed code prior to an application’s `Main` method being invoked also plays a role (yes, there’s managed code that executes before `Main`); [dotnet/runtime#44469](https://github.com/dotnet/runtime/pull/44469), for example, was the result of profiling for allocations that occurred on the startup path of a simple “hello, world” console application and addressing a variety of issues:

-   `EqualityComparer<string>.Default` is used by various components on the startup path, but the `CreateDefaultEqualityComparer` that’s used to initialize that singleton wasn’t special-casing `type == typeof(string)`, which then meant it ended up going down the more allocation-heavy `CreateInstanceForAnotherGenericParameter` code path. The PR fixed it by special-casing `string`.
-   Any use of `Console` was forcing various `Encoding` objects to be instantiated, even if they wouldn’t otherwise be used, purely to access their `CodePage`. The PR fixed that by just hardcoding the relevant code page number in a constant.
-   Every process was registering with `AppContext.ProcessExit` in order to clean up after the runtime’s `EventSource`s that were being created, and that in turn resulted in several allocations. We can instead sacrifice a small amount of layering purity and just do the cleanup as part of the `AppContext.OnProcessExit` routine that’s already doing other work, like calling `AssemblyLoadContext.OnProcessExit` and invoking the `ProcessExit` event itself.
-   `AppDomain` was taking a lock to protect the testing-and-setting of some state, and that operation was easily transformed into an `Interlocked.CompareExchange`. The benefit to that here wasn’t reduced locking (which is also nice), but rather no longer needing to allocate the object that was there purely to be locked on.
-   `EventSource` was always allocating an `object` to be used as a lock necessary for synchronization in the `WriteEventString` method, which is only used for logging error messages about `EventSource`s; not a common case. That `object` can instead be lazily allocated with an `Interlocked.CompareExchange` only when there’s first a failure to log. `EventSource` was also allocating a pinning `GCHandle` in order to pass the address of a pinned array to a P/Invoke. That was just as easily (and more cheaply) done with a `fixed` statement.
-   Similarly, `EncodingProvider` was always allocating an `object` to be used for pessimistic locking, when an optimistic `CompareExchange` loop-based scheme was cheaper in the common case.

But beyond both of those, there’s the .NET host. Fundamentally, the .NET runtime is “just” a library that can be hosted inside of a larger application, the “host”; that host calls into various APIs that initialize the runtime and invoke static methods, like `Main`. While it’s possible for anyone to [build a custom host](https://docs.microsoft.com/dotnet/core/tutorials/netcore-hosting), there are hosts built into .NET, that are used as part of the `dotnet` tool and as part of building and publishing an app (when you build a .NET console app, for example, the `.exe` that pops out is a .NET host). What that host does or does not do can have a significant impact on the startup performance of the app, and investments were made in .NET 6 to reduce these hosting overheads.

One of the most expensive things a host can do is file I/O, especially if there’s a lot of it. [dotnet/runtime#50671](https://github.com/dotnet/runtime/pull/50671) tries to reduce startup time by avoiding the file existence checks that were being performed for each file listed in `deps.json` (which describes a set of dependencies that come from packages). On top of that, [dotnet/sdk#17014](https://github.com/dotnet/sdk/pull/17014) stopped generating the `<app>.runtimeconfig.dev.json` file as part of builds; this file contained additional probing paths that weren’t actually necessary and were causing the host to probe more than necessary and negating the wins from the previous PR. On top of that, [dotnet/runtime#53631](https://github.com/dotnet/runtime/pull/53631) also helped reduce overheads by removing unnecessary string copies in the hosting layer, shaving milliseconds off execution time.

All told, this adds up to sizeable reductions in app startup. For this example, I used:

```
D:\examples> dotnet new console -o app5 -f net5.0
D:\examples> dotnet new console -o app6 -f net6.0
```

to create two “Hello, World” apps, one targeting .NET 5 and one targeting .NET 6. Then I built them both with `dotnet build -c Release` in each directory, and then used PowerShell’s `Measure-Command` to time their execution.

```
D:\examples\app5> Measure-Command { D:\examples\app5\bin\Release\net5.0\app5.exe } | Select-Object -Property TotalMilliseconds

TotalMilliseconds
-----------------
          63.9716

D:\examples\app5> cd ..app6
D:\examples\app6> Measure-Command { D:\examples\app6\bin\Release\net6.0\app6.exe } | Select-Object -Property TotalMilliseconds

TotalMilliseconds
-----------------
          43.2652

D:\examples\app6>
```

highlighting an ~30% reduction in the cost of executing this “Hello, World” app.

### Size

When I’ve written about improving .NET performance, throughput and memory have been the primary two metrics on which I’ve focused. Of late, however, another metric has been getting a lot of attention: size, and in particular size-on-disk for a self-contained, trimmed application. That’s primarily because of the [Blazor WebAssembly (WASM)](https://docs.microsoft.com/aspnet/core/blazor) application model, where an entire .NET application, inclusive of the runtime, is downloaded to and executed in a browser. Some amount of work went into .NET 5 to reduce size, but _a lot_ of work has gone into .NET 6, inclusive of changes in dotnet/runtime as well as in mono/linker, which provides the trimmer that analyzes and rewrites assemblies to remove (or “trim”, or “tree shake”) unused functionality. A large percentage of the work in .NET 6 actually went into trimming safety, making it possible for any of the core libraries to be used in a trimmed application such that either everything that’s needed will be correctly kept or the trimmer will produce warnings about what’s wrong and how the developer can fix it. However, there was a sizable effort (pun intended, I’m so funny) on the size reduction itself.

To start, let’s take a look at what size looked like for .NET 5. I’ll create and run a new .NET 5 Blazor WASM application using `dotnet`

```
dotnet new blazorwasm --framework net5.0 --output app5
cd app5
dotnet run
```

[![Blazor App, Performance Improvements in .NET 6](https://devblogs.microsoft.com/dotnet/wp-content/uploads/sites/10/2021/08/BlazorTemplate.png)](https://devblogs.microsoft.com/dotnet/wp-content/uploads/sites/10/2021/08/BlazorTemplate.png)

It works, nice. Now, I can publish it, which will create and trim the whole application, and produce all the relevant assets ready for pushing to my server; that includes Brotli-compressing all the required components.

```
dotnet publish -c Release
```

I see output like the following, and it completes successfully:

```
D:examples\app5> dotnet publish -c Release
Microsoft (R) Build Engine version 17.0.0-preview-21411-06+b0bb46ab8 for .NET
Copyright (C) Microsoft Corporation. All rights reserved.

  Determining projects to restore...
  All projects are up-to-date for restore.
  You are using a preview version of .NET. See: https://aka.ms/dotnet-core-preview
  app5 -> D:\examples\app5\bin\Release\net5.0\app5.dll
  app5 (Blazor output) -> D:\examples\app5\bin\Release\net5.0\wwwroot
  Optimizing assemblies for size, which may change the behavior of the app. Be sure to test after publishing. See: https://aka.ms/dotnet-illink
  Compressing Blazor WebAssembly publish artifacts. This may take a while...
  app5 -> D:\examples\app5\bin\Release\net5.0\publish
D:\examples\app5>
```

The published compressed files end up for me in `D:\examples\app5\bin\Release\net5.0\publish\wwwroot_framework`, and if I sum up all of the `.br` files (except for `icudt_CJK.dat.br`, `icudt_EFIGS.dat.br`, `icudt_no_CJK.dat.br`, which are subsets of `icudt.dat.br` that’s also there), I get a total size of `2.10 MB`. That’s the entirety of the application, including the runtime, all of the library functionality used by the app, and the app code itself. Cool.

Now, let’s do the exact same thing, but with .NET 6:

```
dotnet new blazorwasm --framework net6.0 --output app6
cd app6
dotnet publish -c Release
```

which yields:

```
D:\examples\app6> dotnet publish -c Release
Microsoft (R) Build Engine version 17.0.0-preview-21411-06+b0bb46ab8 for .NET
Copyright (C) Microsoft Corporation. All rights reserved.

  Determining projects to restore...
  All projects are up-to-date for restore.
  You are using a preview version of .NET. See: https://aka.ms/dotnet-core-preview
  app6 -> D:\examples\app6\bin\Release\net6.0\app6.dll
  app6 (Blazor output) -> D:\examples\app6\bin\Release\net6.0\wwwroot
  Optimizing assemblies for size, which may change the behavior of the app. Be sure to test after publishing. See: https://aka.ms/dotnet-illink
  Compressing Blazor WebAssembly publish artifacts. This may take a while...
  app6 -> D:\examples\app6\bin\Release\net6.0\publish
D:\examples\app6>
```

as before. Except now when I sum up the relevant `.br` files in `D:\examples\app6\bin\Release\net6.0\publish\wwwroot_framework`, the total size is `1.88MB`. Just by upgrading from .NET 5 to .NET 6, we’ve shaved ~220KB off the total size, even as .NET 6 gains lots of additional code. _A lot_ of PRs contributed here, as most changes shave off a few bytes here and a few bytes there. Here are some example changes that were made in the name of size, as they can help to highlight the kinds of changes applications and libraries in general can make to help reduce their footprint:

-   [dotnet/runtime#44712](https://github.com/dotnet/runtime/pull/44712) and [dotnet/runtime#44706](https://github.com/dotnet/runtime/pull/44706). When run over an application, the trimmer identifies unused types and removes them; use a type just once, and it needs to be kept. Over the years, .NET has amassed variations on a theme, with multiple types that can be used for the same purpose, and there’s then value in consolidating all usage to just one of them such that the other is more likely to be trimmed away. A good example of this is `Tuple<>` and `ValueTuple<>`. Throughout the codebase there are occurrences of both; this PR replaces a bunch of `Tuple<>` use with `ValueTuple<>`, which not only helps to avoid allocations in some cases, it makes it much more likely that more `Tuple<>` types can be trimmed away.
-   [dotnet/runtime#43634](https://github.com/dotnet/runtime/pull/43634). The nature of `ValueTuple<>` is that there are lots of copies of `ValueTuple<>`, one for each arity (e.g. `ValueTuple<T1, T2>`, `ValueTuple<T1, T2, T3>`, etc.), and then because it’s a generic often used with value types, every generic instantiation of a given tuple type ends up duplicating all of the assembly for that type. Thus, it’s valuable to keep the types as slim as possible. This PR introduced a throw helper and then replaced ~20 throw sites from across the types, reducing the amount of assembly required for each.
-   [dotnet/runtime#45054](https://github.com/dotnet/runtime/pull/45054). This PR represents a typical course of action when trying to reduce the size of a trimmed application: cutting unnecessary references from commonly used code to less commonly used code. In this case, `RuntimeType` (the derivation of `Type` that’s most commonly used) has a dependency on the `Convert` type, which makes it so that the trimmer can’t trim away `Convert`‘s static constructor. This PR rewrites the relevant, small portions of `RuntimeType` to not need `Convert` at all.
-   [dotnet/runtime#45127](https://github.com/dotnet/runtime/pull/45127). If a type’s static constructor initializes a field, the trimmer is unable to remove the field or the thing it’s being initialized to, so for rarely used fields, it can be beneficial to initialize them lazily. This PR makes the `Task<T>.Factory` property lazily-initialized (`Task.Factory` remains non-lazily-initialized, as it’s much more commonly used), which then also makes it more likely that `TaskFactory<T>` can be trimmed away.
-   [dotnet/runtime#45239](https://github.com/dotnet/runtime/pull/45239). It’s very common when multiple overloads of something exist for the simpler overload to delegate to the more complicated overload. However, typically the more complicated overload has more inherent dependencies than would the simpler one, and so from a trimming perspective, it can actually be beneficial to invert the dependency chain, and have the more complicated overload delegate to the simpler one to handle the subset of functionality required for the simple one. This PR does that for `Utf8Encoding`‘s constructors and `TaskFactory`‘s constructors.
-   [dotnet/runtime#52681](https://github.com/dotnet/runtime/pull/52681) and [dotnet/runtime#52794](https://github.com/dotnet/runtime/pull/52794). Sometimes analyzing the code that remains after trimming makes you rethink whether functionality you have in your library or app is actually necessary. In doing so for System.Net.Http.dll, we realized we were keeping around a ton of mail address-related parsing code in the assembly, purely in the name of validating `From` headers in a way that wasn’t particularly useful, so we removed it. We also avoided including code into the WASM build of the assembly that wouldn’t actually be used in that build. These efforts shrunk the size of the assembly by almost 15%.
-   [dotnet/runtime#45296](https://github.com/dotnet/runtime/pull/45296) and [dotnet/runtime#45643](https://github.com/dotnet/runtime/pull/45643). To support various concepts from the globalization APIs when using the ICU globalization implementation, `System.Private.CoreLib` carries with it several sizeable data tables. These PRs significantly reduce the size of that data, by encoding it in a much more compact form and by accessing the blittable data as a `ReadOnlySpan<byte>` around the data directly from the assembly.
-   [dotnet/runtime#46061](https://github.com/dotnet/runtime/pull/46061). Similarly, to support ordinal case conversion, `System.Private.CoreLib` carries a large casing table. This table is stored in memory in a static `ushort[]?[]` array, and previously, it was initialized with collection-initialization syntax. That resulted in the generated static constructor for initializing the array being over 1KB of IL instructions. This PR changed it to actually store the data in the assembly encoded as bytes, and then in the constructor create the `ushort[]?[]` from a `ReadOnlySpan<byte>` over that data.
-   [dotnet/runtime#48906](https://github.com/dotnet/runtime/pull/48906). This is also in a similar vein to the previous ICU changes. `WebUtility` has a static `Dictionary<ulong, char>` lookup table. Previously, that dictionary was being initialized in a manner that led to `WebUtility`‘s static constructor being over 17KB of IL. This PR reduces it to less than 300B.
-   [dotnet/runtime#46211](https://github.com/dotnet/runtime/pull/46211). The trimmer looks at IL to determine whether some code uses some other code, but there are dependencies it can’t always see. There are multiple ways a developer can inform the trimmer it should keep some code around even if it doesn’t know why. One is via a special XML file that can be fed to the trimmer to tell it which members should be considered rooted and not trimmed away. That mechanism, however, is a very large hammer. The preferred mechanism is a set of attributes that allow for the information to be much more granular. In particular, the `DynamicDependencyAttribute` lets the developer declare that some member `A` should be kept if some other member `B` is kept. This PR switches some rooting with the XML file to instead use the attributes.
-   [dotnet/runtime#47918](https://github.com/dotnet/runtime/pull/47918). Since its porting to .NET Core, LINQ has received a lot of attention, as it’s a ripe area for optimization. A set of the optimizations that went into LINQ involved adding a few new internal interfaces that could then be implemented on a bunch of types representing the various LINQ combinators in order to communicate additional data between operators. This resulted in massive speedups for certain operations, however it also added a significant amount of IL to System.Linq.dll, around 20K uncompressed (around 6K compressed). And it has the potential to result in an order of magnitude more assembly code, depending on how these types are instantiated. Because of the latter issue, a special-build flavor was previously added to the assembly, so that it could be built without those optimizations that were contributing significantly to its size. This PR cleaned that up and extended it so that the size-optimized build could be used for Blazor WASM and other mobile targets.
-   [dotnet/runtime#53317](https://github.com/dotnet/runtime/pull/53317). `System.Text.Json`‘s `JsonSerializer` was using `[DynamicDependency]` to root all of the `System.Collections.Immutable` collections, just in case they were used. This PR undoes that dependency, saving ~28KB compressed in a default Blazor WASM application.
-   [dotnet/runtime#44696](https://github.com/dotnet/runtime/pull/44696) from [@benaadams](https://github.com/benaadams), [dotnet/runtime#44734](https://github.com/dotnet/runtime/pull/44734), [dotnet/runtime#44825](https://github.com/dotnet/runtime/pull/44825), [dotnet/runtime#47496](https://github.com/dotnet/runtime/pull/47496), [dotnet/runtime#47873](https://github.com/dotnet/runtime/pull/47873), [dotnet/runtime#53123](https://github.com/dotnet/runtime/pull/53123), and [dotnet/runtime#56937](https://github.com/dotnet/runtime/pull/56937). We have a love/hate relationship with LINQ. On the one hand, it’s an invaluable tool for quickly expressing complicated operations with very little code, and in the common case, it’s perfectly fine to use and we encourage applications to use it. On the other hand, from a performance perspective, LINQ isn’t stellar, even as we’ve invested significantly in improving it. From a throughput and memory perspective, simple LINQ operations will invariably be more expensive than hand-rolled versions of the same thing, if for no other reason than because the expressability it provides means functionality is passed around as delegates, enumerators are allocated, and so on. And from a size perspective, all that functionality comes with a lot of IL (and most of the time, any attempts we make to increase throughput also increase size). If in the libraries we can replace LINQ usage with only minimally larger open-coded replacements, we’ll typically do so.
-   [dotnet/runtime#39549](https://github.com/dotnet/runtime/pull/39549). `dotnet.wasm` contains the compiled WebAssembly for the mono runtime used in Blazor WASM apps. The more features unnecessary for this scenario (e.g. various debugging features, dead code in this configuration, etc.) that can be removed in the build, the smaller the file will be.

Now, let’s take the size reduction a step further. The runtime itself is contained in the `dotnet.wasm` file, but when we trim the app as part of publishing, we’re only trimming the managed assemblies, not the runtime, as the SDK itself doesn’t include the tools necessary to do so. We can rectify that by installing the `wasm-tools` workload via `dotnet` ([dotnet/runtime#43785](https://github.com/dotnet/runtime/pull/43785)):

```
dotnet workload install wasm-tools
```

With that installed, we can publish again, exactly as before:

```
dotnet publish -c Release
```

but now we see some extra output (and it takes longer to publish):

```
D:\examples\app6> dotnet publish -c Release
Microsoft (R) Build Engine version 17.0.0-preview-21411-06+b0bb46ab8 for .NET
Copyright (C) Microsoft Corporation. All rights reserved.

  Determining projects to restore...
  Restored D:\examples\app6\app6.csproj (in 245 ms).
  You are using a preview version of .NET. See: https://aka.ms/dotnet-core-preview
  app6 -> D:\examples\app6\bin\Release\net6.0\app6.dll
  app6 (Blazor output) -> D:\examples\app6\bin\Release\net6.0\wwwroot
  Optimizing assemblies for size, which may change the behavior of the app. Be sure to test after publishing. See: https://aka.ms/dotnet-illink
  Compiling native assets with emcc. This may take a while ...
  Linking with emcc. This may take a while ...
  Compressing Blazor WebAssembly publish artifacts. This may take a while...
  app6 -> D:\examples\app6\bin\Release\net6.0\publish
D:\examples\app6>
```

See those two extra `emcc`\-related lines. `emcc` is the Emscripten front-end compiler (Emscripten is a compiler toolchain for compiling to WebAssembly), and what we’re seeing here is `dotnet.wasm` being relinked so as to remove functionality from the binary that’s not used by the trimmed binaries in the application. If I now re-measure the size of the relevant `.br` files in `D:examplesapp6binReleasenet6.0publishwwwroot_framework`, it’s now `1.82MB`, so we’ve removed an additional `60KB` from the published application size.

We can go further, though. I’ll add two lines to my app6.csproj in the top `<PropertyGroup>...</PropertyGroup>` section:

```
    <InvariantGlobalization>true</InvariantGlobalization>
    <BlazorEnableTimeZoneSupport>false</BlazorEnableTimeZoneSupport>    
```

These are feature switches, and serve two purposes. First, they can be queried by code in the app (and, in particular, in the core libraries) to determine what functionality to employ. For example, if you search dotnet/runtime for “GlobalizationMode.Invariant”, you’ll find code along the lines of:

```
if (GlobalizationMode.Invariant)
{
    ... // only use invariant culture / functionality
}
else
{
    ... // use ICU or NLS for processing based on the appropriate culture
}
```

Second, the switch informs the trimmer that it can substitute in a fixed value for the property associated with the switch, e.g. setting `<InvariantGlobalization>true</InvariantGlobalization>` causes the trimmer to rewrite the `GlobalizationMode.Invariant` property to be hardcoded to return `true`, at which point it can then use that to prune away any visibly unreachable code paths. That means in an example like the code snippet above, the trimmer can elide the entire `else` block, and if that ends up meaning additional types and members become unused, they can be removed, as well. By setting the two aforementioned switches, we’re eliminating any need the app has for the ICU globalization library, which is a significant portion of the app’s size, both in terms of the logic linked into `dotnet.wasm` and the data necessary to drive it (`icudt.dat.br`). With those switches set, we can re-publish (after deleting the old `publish` directory). Two things I immediately notice. First, there aren’t any `icu*.br` files at all, as there’s no longer a need for anything ICU-related. And second, all of the `.br` files weigh in at only `1.07MB`, removing another 750KB from the app’s size, more than 40% of where we were before.

### Blazor and mono

Ok, so we’ve got our Blazor WASM app, and we’re able to ship a small package down to the browser to execute it. Does it run efficiently?

The `dotnet.wasm` file mentioned previously contains the .NET runtime used to execute these applications. The runtime is itself compiled to WASM, downloaded to the browser, and used to execute the application and library code on which the app depends. I say “the runtime” here, but in reality there are actually multiple incarnations of a runtime for .NET. In .NET 6, all of the .NET core libraries for all of the .NET app models, whether it be console apps or ASP.NET Core or Blazor WASM or mobile apps, come from the same source in dotnet/runtime, but there are actually two runtime implementations in dotnet/runtime: “coreclr” and “mono”. In this blog post, when I’ve talked about runtime improvements in components like “the” JIT and GC, I’ve actually been referring to coreclr, which is what’s used for console apps, ASP.NET Core, Windows Forms, and WPF. Blazor WebAssembly, however, relies on mono, which has been honed over the years to be small and agile for these kinds of scenarios, and has also received a lot of performance investment in .NET 6.

There are three significant areas of investment here. The first is around improvements to the IL interpreter in mono. Mono not only has a JIT capable of on-demand assembly generation ala coreclr, it also supports interpreting IL, which is valuable on platforms that for security reasons prohibit executing machine assembly code generated on the fly. [dotnet/runtime#46037](https://github.com/dotnet/runtime/pull/46037) overhauled the interpreter to move it from being stack-based (where IL instructions push and pop values from a stack) to being based on the concept of reading/writing local variables, a switch that both simplified the code base and gave it a performance boost. [dotnet/runtime#48513](https://github.com/dotnet/runtime/pull/48513) improved the interpreter’s ability to inline, in particular for methods attributed with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`, which is important with the libraries in dotnet/runtime as some of the lower-level processing routines make strategic use of `AggressiveInlining` in places it’s been measured to yield impactful gains. [dotnet/runtime#50361](https://github.com/dotnet/runtime/pull/50361), [dotnet/runtime#51273](https://github.com/dotnet/runtime/pull/51273), [dotnet/runtime#52130](https://github.com/dotnet/runtime/pull/52130), and [dotnet/runtime#52242](https://github.com/dotnet/runtime/pull/52242) all served to optimize how various kinds of instructions were encoded and invoked, and [dotnet/runtime#51309](https://github.com/dotnet/runtime/pull/51309) improved the efficiency of `finally` blocks by removing overhead associated with thread aborts, which no longer exist in .NET (.NET Framework 4.8 and earlier have the concept of a thread abort, where one thread can inject a special exception into another, and that exception could end up being thrown at practically any instruction; by default, however, they don’t interrupt `finally` blocks).

The second area of investment was around [hardware intrinsics](https://devblogs.microsoft.com/dotnet/hardware-intrinsics-in-net-core/). .NET Core 3.0 and .NET 5 added literally thousands of new methods, each of which map effectively 1:1 with some hardware-specific instruction, enabling C# code to directly target functionality from various ISAs (Instruction Set Architectures) like SSSE3 or AVX2. Of course, something needs to be able to translate the C# methods into the underlying instructions they represent, which means a lot of work to fully enable every code generator. Mono supports using LLVM for code generation, and a bunch of PRs improved the LLVM-enabled mono’s support for hardware intrinsics, whether it be [dotnet/runtime#49260](https://github.com/dotnet/runtime/pull/49260), [dotnet/runtime#49737](https://github.com/dotnet/runtime/pull/49737), [dotnet/runtime#48361](https://github.com/dotnet/runtime/pull/48361), and [dotnet/runtime#47482](https://github.com/dotnet/runtime/pull/47482) adding support for ARM64 AdvSimd APIs; [dotnet/runtime#48413](https://github.com/dotnet/runtime/pull/48413), [dotnet/runtime#47337](https://github.com/dotnet/runtime/pull/47337), and [dotnet/runtime#48525](https://github.com/dotnet/runtime/pull/48525) rounding out the support for the Sha1, Sha256, and Aes intrinsics; or [dotnet/runtime#54924](https://github.com/dotnet/runtime/pull/54924) and [dotnet/runtime#47028](https://github.com/dotnet/runtime/pull/47028) implementing foundational support with `Vector64<T>` and `Vector128<T>`. Many of the library performance improvements highlighted in previous blog posts rely on the throughput improvements from vectorization, which then accrue here as well, which includes when building Blazor WASM apps with AOT.

And that brings us to the third, and arguably most impactful, area of investment: AOT for Blazor WASM. I highlighted earlier that Blazor WASM apps targeting .NET 5 were interpreted, meaning while the runtime itself was compiled to WASM, the runtime then turned around and interpreted the IL for the app and the libraries it depends on. Now with .NET 6, a Blazor WASM app can be compiled ahead of time entirely to WebAssembly, avoiding the need for JIT’ing or interpreting at run-time. All of these improvements together lead to huge, cross-cutting performance improvements for Blazor WASM apps when targeting .NET 6 instead of .NET 5.

Let’s do one last benchmark. Continuing with the `app5` and `app6` examples from the previous section, we’ll do something that involves a bit of computation: SHA-256. The implementation of `SHA256` used for Blazor WASM on both .NET 5 and .NET 6 is exactly the same, and is implemented in C#, making it a reasonable test case. I’ve replaced the entire contents of the Counter.razor file in both of those projects with this, which in response to a button click is simply SHA-256 hashing a byte array of some UTF8 Shakespeare several thousand times.

```
@page "/counter"
@using System.Security.Cryptography
@using System.Diagnostics
@using System.Text

<h1>Hashing</h1>

<p>Time: @_time</p>

<button class="btn btn-primary" @onclick="Hash">Click me</button>

@code {
    private const string Sonnet18 =
@"Shall I compare thee to a summer’s day?
Thou art more lovely and more temperate:
Rough winds do shake the darling buds of May,
And summer’s lease hath all too short a date;
Sometime too hot the eye of heaven shines,
And often is his gold complexion dimm'd;
And every fair from fair sometime declines,
By chance or nature’s changing course untrimm'd;
But thy eternal summer shall not fade,
Nor lose possession of that fair thou ow’st;
Nor shall death brag thou wander’st in his shade,
When in eternal lines to time thou grow’st:
So long as men can breathe or eyes can see,
So long lives this, and this gives life to thee.";

    private TimeSpan _time;

    private void Hash()
    {
        byte[] bytes = Encoding.UTF8.GetBytes(Sonnet18);
        var sw = Stopwatch.StartNew();
        for (int i = 0; i < 2000; i++)
        {
            _ = SHA256.HashData(bytes);
        }
        _time = sw.Elapsed;
    }
}
```

I’ll start by publishing the `app5` app:

```
D:\examples\app5> dotnet publish -c Release
```

Then to run it, we need a web server to host the server side of the app, and to make that easy, I’ll use the [`dotnet serve`](https://www.nuget.org/packages/dotnet-serve/) global tool. To install it, run:

```
dotnet tool install --global dotnet-serve
```

at which point you can start a simple web server for the files in the published directory:

```
pushd D:\examples\app5\bin\Release\net5.0\publish\wwwroot
dotnet serve -o
```

click `Counter`, and then click the `Click Me` button a few times. I get resulting numbers like this:

[![SHA256 benchmark on .NET 5](https://devblogs.microsoft.com/dotnet/wp-content/uploads/sites/10/2021/08/Hashing1.png)](https://devblogs.microsoft.com/dotnet/wp-content/uploads/sites/10/2021/08/Hashing1.png)

so ~0.45 seconds on .NET 5. Now I can do the exact same thing on .NET 6 with the `app6` project:

```
popd
cd ..app6
dotnet publish -c Release
pushd D:\examples\app6\bin\Release\net6.0\publish\wwwroot
dotnet serve -o
```

and I get results like this:

[![SHA256 benchmark on .NET 6](https://devblogs.microsoft.com/dotnet/wp-content/uploads/sites/10/2021/08/Hashing2.png)](https://devblogs.microsoft.com/dotnet/wp-content/uploads/sites/10/2021/08/Hashing2.png)

so ~0.28 seconds on .NET 6. That ~40% improvement is due to the interpreter optimizations, as we’re otherwise running the exact same code.

Now, let’s try this out with AOT. I modify the `app6.csproj` to include this in the top `<PropertyGroup>...</PropertyGroup>` node:

```
<RunAOTCompilation>true</RunAOTCompilation>
```

Then I republish (and get a cup of coffee… the AOT step adds some time to the build process). With that, I now get results like this:

[![SHA256 benchmark on .NET 6 with AOT](https://devblogs.microsoft.com/dotnet/wp-content/uploads/sites/10/2021/08/Hashing3.png)](https://devblogs.microsoft.com/dotnet/wp-content/uploads/sites/10/2021/08/Hashing3.png)

so ~0.018 seconds, making it ~16x faster than it was before. A nice note to end this post on.

## Is that all?

Of course not! 🙂 Whether it be for `System.Xml` ([dotnet/runtime#49988](https://github.com/dotnet/runtime/pull/49988) from [@kronic](https://github.com/kronic), [dotnet/runtime#54344](https://github.com/dotnet/runtime/pull/54344), [dotnet/runtime#54299](https://github.com/dotnet/runtime/pull/54299), [dotnet/runtime#54346](https://github.com/dotnet/runtime/pull/54346), [dotnet/runtime#54356](https://github.com/dotnet/runtime/pull/54356), [dotnet/runtime#54836](https://github.com/dotnet/runtime/pull/54836)), or caching ([dotnet/runtime#51761](https://github.com/dotnet/runtime/pull/51761), [dotnet/runtime#45410](https://github.com/dotnet/runtime/pull/45410), [dotnet/runtime#45563](https://github.com/dotnet/runtime/pull/45563), [dotnet/runtime#45280](https://github.com/dotnet/runtime/pull/45280)), or `System.Drawing` ([dotnet/runtime#50489](https://github.com/dotnet/runtime/pull/50489) from [@L2](https://github.com/L2), [dotnet/runtime#50622](https://github.com/dotnet/runtime/pull/50622) from [@L2](https://github.com/L2)), or `System.Diagnostics.Process` ([dotnet/runtime#44691](https://github.com/dotnet/runtime/pull/44691), [dotnet/runtime#43365](https://github.com/dotnet/runtime/pull/43365) from [@am11](https://github.com/am11)), or any number of other areas, there have been an untold number of performance improvements in .NET 6 that I haven’t been able to do justice to in this post.

There are also many outstanding PRs in dotnet/runtime that haven’t yet been merged but may be for .NET 6. For example, [dotnet/runtime#57079](https://github.com/dotnet/runtime/pull/57079) enables support for TLS resumption on Linux, which has the potential to improve the time it takes to establish a secure connection by an order of magnitude. Or [dotnet/runtime#55745](https://github.com/dotnet/runtime/pull/55745), which enables the JIT to fold `TimeSpan.FromSeconds(constant)` (and other such \`From\` methods) into a single instruction. Or [dotnet/runtime#35565](https://github.com/dotnet/runtime/pull/35565) from [@sakno](https://github.com/sakno), which uses spans more aggressively throughout the implementation of `BigInteger`. So much goodness already merged and so much more on the way.

Don’t just take my word for it, though. Please [download .NET 6](https://dotnet.microsoft.com/download/dotnet/6.0) and give it a spin. I’m quite hopeful you’ll like what you see. If you do, tell us. If you don’t, tell us. We want to hear from you, and even more than that, we’d love your involvement. Of the ~400 merged PRs linked to in this blog post, over 15% of them came from the .NET community outside of Microsoft, and we’d love to see that number grow even higher. If you’ve got ideas for improvements or the inclination to try to make them a reality, please join us for a fun and fulfilling time in [dotnet/runtime](https://github.com/dotnet/runtime).

Happy coding!

## Author

![Stephen Toub - MSFT](https://devblogs.microsoft.com/dotnet/wp-content/uploads/sites/10/2022/03/stoub_square-96x96.jpg)

Partner Software Engineer

Stephen Toub is a developer on the .NET team at Microsoft.