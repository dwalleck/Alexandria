Back when we were getting ready to ship .NET Core 2.0, I wrote a [blog post](https://blogs.msdn.microsoft.com/dotnet/2017/06/07/performance-improvements-in-net-core/) exploring some of the many performance improvements that had gone into it. I enjoyed putting it together so much and received such a positive response to the post that I did it [again for .NET Core 2.1](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-core-2-1), a version for which performance was also a significant focus. With [//build](https://www.microsoft.com/en-us/build) last week and [.NET Core 3.0](https://dotnet.microsoft.com/download/dotnet-core/3.0)‘s release now on the horizon, I’m thrilled to have an opportunity to do it again. .NET Core 3.0 has a ton to offer, from Windows Forms and WPF, to single-file executables, to async enumerables, to platform intrinsics, to HTTP/2, to fast JSON reading and writing, to assembly unloadability, to enhanced cryptography, and on and on and on… there is a wealth of new functionality to get excited about. For me, however, performance is the primary feature that makes me excited to go to work in the morning, and there’s a staggering amount of performance goodness in .NET Core 3.0. In this post, we’ll take a tour through some of the many improvements, big and small, that have gone into the .NET Core runtime and core libraries in order to make your apps and services leaner and faster.

### Setup

[Benchmark.NET](http://github.com/dotnet/benchmarkdotnet) has become the preeminent tool for doing benchmarking of .NET libraries, and so as I did in my 2.1 post, I’ll use Benchmark.NET to demonstrate the improvements. Throughout the post, I’ll include the individual snippets of benchmarks that highlight the particular improvement being discussed. To be able to execute those benchmarks, you can use the following setup: 1. Ensure you have [.NET Core 3.0](https://dotnet.microsoft.com/download/dotnet-core/3.0) installed, as well as [.NET Core 2.1](https://dotnet.microsoft.com/download/dotnet-core/2.1) for comparison purposes. 2. Create a directory named `BlogPostBenchmarks`. 3. In that directory, run `dotnet new console`. 4. Replace the contents of BlogPostBenchmarks.csproj with the following:

```
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
    <TargetFrameworks>netcoreapp2.1;netcoreapp3.0</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="BenchmarkDotNet" Version="0.11.5" />
    <PackageReference Include="System.Drawing.Common" Version="4.5.0" />
    <PackageReference Include="System.IO.Pipelines" Version="4.5.0" />
    <PackageReference Include="System.Threading.Channels" Version="4.5.0" />
  </ItemGroup>

</Project>
```

1.  Replace the contents of Program.cs with the following:

```
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.CsProj;
using Microsoft.Win32.SafeHandles;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.IO.Pipelines;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using System.Xml;

[MemoryDiagnoser]
public class Program
{
    static void Main(string[] args) => BenchmarkSwitcher.FromTypes(new[] { typeof(Program) }).Run(args);

    // ... paste benchmark code here
}
```

To execute a particular benchmark, unless otherwise noted, copy and paste the relevant code to replace the 

`// ...`above, and execute `dotnet run -c Release -f netcoreapp2.1 --runtimes netcoreapp2.1 netcoreapp3.0 --filter "*Program*"`. This will compile and run the tests in release builds, on both .NET Core 2.1 and .NET Core 3.0, and print out the results for comparison in a table.

### Caveats A few caveats before we get started:

1.  Any discussion involving microbenchmark results deserves a caveat that measurements can and do vary from machine to machine. I’ve tried to pick stable examples to share (and have run these tests on multiple machines in multiple configurations to help validate that), but don’t be too surprised if your numbers differ from the ones I’ve shown; hopefully, however, the magnitude of the improvements demonstrated carries through. All of the shown results are from a nightly Preview 6 build for .NET Core 3.0. Here’s my configuration, as summarized by Benchmark.NET, on my Windows configuration and on my Linux configuration:

```
BenchmarkDotNet=v0.11.5, OS=Windows 10.0.17763.437 (1809/October2018Update/Redstone5)
Intel Core i7-7660U CPU 2.50GHz (Kaby Lake), 1 CPU, 4 logical and 2 physical cores
.NET Core SDK=3.0.100-preview6-011854
  [Host]     : .NET Core 2.1.9 (CoreCLR 4.6.27414.06, CoreFX 4.6.27415.01), 64bit RyuJIT
  Job-RODBZD : .NET Core 2.1.9 (CoreCLR 4.6.27414.06, CoreFX 4.6.27415.01), 64bit RyuJIT
  Job-TVOWAH : .NET Core 3.0.0-preview6-27712-03 (CoreCLR 3.0.19.26071, CoreFX 4.700.19.26005), 64bit RyuJIT

BenchmarkDotNet=v0.11.5, OS=ubuntu 18.04
Intel Xeon CPU E5-2673 v4 2.30GHz, 1 CPU, 4 logical and 2 physical cores
.NET Core SDK=3.0.100-preview6-011877
  [Host]     : .NET Core 2.1.10 (CoreCLR 4.6.27514.02, CoreFX 4.6.27514.02), 64bit RyuJIT
  Job-SSHMNT : .NET Core 2.1.10 (CoreCLR 4.6.27514.02, CoreFX 4.6.27514.02), 64bit RyuJIT
  Job-CHXNFO : .NET Core 3.0.0-preview6-27713-12 (CoreCLR 3.0.19.26071, CoreFX 4.700.19.26307), 64bit RyuJIT
```

1.  Unless otherwise mentioned, benchmarks were executed on Windows. In many cases, performance is equivalent between Windows and Unix, but in others, there can be non-trivial discrepancies between them, in particular in places where .NET relies on OS functionality, and the OS itself has different performance characteristics.
2.  I mentioned posts on .NET Core 2.0 and .NET Core 2.1, but I didn’t mention .NET Core 2.2. .NET Core 2.2 was primarily focused on ASP.NET, and while there were terrific performance improvements at the ASP.NET layer in 2.2, the release was primarily focused on servicing for the runtime and core libraries, with most improvements post-2.1 skipping 2.2 and going into 3.0. With that out of the way, let’s have some fun.

### Span and Friends

One of the more notable features introduced in .NET Core 2.1 was `Span<T>`, along with its friends `ReadOnlySpan<T>`, `Memory<T>`, and `ReadOnlyMemory<T>`. The introduction of these new types came with hundreds of new methods for interacting with them, some on new types and some with overloaded functionality on existing types, as well as optimizations in the just-in-time compiler (JIT) for making working with them very efficient. The release also included some internal usage of `Span<T>` to make existing operations leaner and faster while still enjoying maintainable and safe code. In .NET Core 3.0, much additional work has gone into further improving all such aspects of these types: making the runtime better at generating code for them, increasing the use of them internally to help improve many other operations, and improving the various library utilities that interact with them to make consumption of these operations faster. To work with a span, one first needs to get a span, and several PRs have made doing so faster. In particular, passing around a `Memory<T>` and then getting a `Span<T>` from it is a very common way of creating a span; this is, for example, how the various `Stream.WriteAsync` and `ReadAsync` methods work, accepting a `{ReadOnly}Memory<T>` (so that it can be stored on the heap) and then accessing its `Span` property once the actual bytes need to be read or written. PR [dotnet/coreclr#20771](https://github.com/dotnet/coreclr/pull/20771) improved this by removing an argument validation branch (both for `{ReadOnly}Memory<T>.Span` and for `{ReadOnly}Span<T>.Slice`), and while removing a branch is a small thing, in span-heavy code (such as when doing formatting and parsing), small things done over and over and over again add up. More impactful, PR [dotnet/coreclr#20386](https://github.com/dotnet/coreclr/pull/20386) plays tricks at the runtime level to safely eliminate some of the runtime checked casting and bit masking logic that had been used to enable `{ReadOnly}Memory<T>` to wrap various types, like `string`, `T[]`, and `MemoryManager<T>`, providing a seamless veneer over all of them. The net result of these PRs is a nice speed-up when fishing a `Span<T>` out of a `Memory<T>`, which in turn improves all other operations that do so.

```
private ReadOnlyMemory<byte> _mem = new byte[1];

[Benchmark]
public ReadOnlySpan<byte> GetSpan() => _mem.Span;
```

| Method | Toolchain | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- |
| GetSpan | netcoreapp2.1 | 3.873 ns | 0.0927 ns | 0.0822 ns | 1.00 |
| GetSpan | netcoreapp3.0 | 1.843 ns | 0.0401 ns | 0.0375 ns | 0.48 |

  Of course, once you get a span, you want to use it, and there are a myriad of ways to use one, many of which have also been optimized further in .NET Core 3.0. For example, just as with arrays, to pass the data from a span to native code via a P/Invoke, the data needs to be pinned (unless it’s already immovable, such as if the span were created to wrap some natively allocated memory not on the GC heap or if it were created for some data on the stack). To pin a span, the easiest way is to simply rely on the C# language’s support added in C# 7.3 that supports a pattern-based way to use any type with the 

`fixed` keyword. All a type need do is expose a `GetPinnableReference` method (or extension method) that returns a `ref T` to the data stored in that instance, and that type can be used with `fixed`. `{ReadOnly}Span<T>` does exactly this. However, even though `{ReadOnly}Span<T>.GetPinnableReference` generally gets inlined, a call it makes internally to `Unsafe.AsRef` was getting blocked from inlining; PR [dotnet/coreclr#18274](https://github.com/dotnet/coreclr/pull/18274) fixed this, enabling the whole operation to be inlined. Further, the aforementioned code was actually tweaked in PR [dotnet/coreclr#20428](https://github.com/dotnet/coreclr/pull/20428) to eliminate one branch on the hot path. Both of these combine to result in a measurable boost when pinning a span:

```
private readonly byte[] _bytes = new byte[10_000];

[Benchmark(OperationsPerInvoke = 10_000)]
public unsafe int PinSpan()
{
    Span<byte> s = _bytes;
    int total = 0;

    for (int i = 0; i < s.Length; i++)
        fixed (byte* p = s) // equivalent to `fixed (byte* p = &s.GetPinnableReference())`
            total += *p;

    return total;
}
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | RatioSD |
| --- | --- | --- | --- | --- | --- | --- |
| PinSpan | netcoreapp2.1 | 0.7930 ns | 0.0177 ns | 0.0189 ns | 1.00 | 0.00 |
| PinSpan | netcoreapp3.0 | 0.6496 ns | 0.0109 ns | 0.0102 ns | 0.82 | 0.03 |

  It’s worth noting, as well, that if you’re interested in these kinds of micro-optimizations, you might also want to avoid using the default pinning at all, at least on super hot paths. The 

`{ReadOnly}Span<T>.GetPinnableReference` method was designed to behave just like pinning of arrays and strings, where null or empty inputs result in a null pointer. This behavior requires an additional check to be performed to see whether the length of the span is zero:

```
// https://github.com/dotnet/coreclr/blob/52aff202cd382c233d903d432da06deffaa21868/src/System.Private.CoreLib/shared/System/Span.Fast.cs#L168-L174

[EditorBrowsable(EditorBrowsableState.Never)]
public unsafe ref T GetPinnableReference()
{
    // Ensure that the native code has just one forward branch that is predicted-not-taken.
    ref T ret = ref Unsafe.AsRef<T>(null);
    if (_length != 0) ret = ref _pointer.Value;
    return ref ret;
}
```

If in your code by construction you know that the span will not be empty, you can choose to instead use 

`MemoryMarshal.GetReference`, which performs the same operation but without the length check:

```
// https://github.com/dotnet/coreclr/blob/52aff202cd382c233d903d432da06deffaa21868/src/System.Private.CoreLib/shared/System/Runtime/InteropServices/MemoryMarshal.Fast.cs#L79

public static ref T GetReference<T>(Span<T> span) => ref span._pointer.Value;
```

Again, while a single check adds minor overhead, when executed over and over and over, that can add up:

```
private readonly byte[] _bytes = new byte[10_000];

[Benchmark(OperationsPerInvoke = 10_000, Baseline = true)]
public unsafe int PinSpan()
{
    Span<byte> s = _bytes;
    int total = 0;

    for (int i = 0; i < s.Length; i++)
        fixed (byte* p = s) // equivalent to `fixed (byte* p = &s.GetPinnableReference())`
            total += *p;

    return total;
}

[Benchmark(OperationsPerInvoke = 10_000)]
public unsafe int PinSpanExplicit()
{
    Span<byte> s = _bytes;
    int total = 0;

    for (int i = 0; i < s.Length; i++)
        fixed (byte* p = &MemoryMarshal.GetReference(s))
            total += *p;

    return total;
}
```

| Method | Mean | Error | StdDev | Ratio | RatioSD |
| --- | --- | --- | --- | --- | --- |
| PinSpan | 0.6524 ns | 0.0129 ns | 0.0159 ns | 1.00 | 0.00 |
| PinSpanExplicit | 0.5200 ns | 0.0111 ns | 0.0140 ns | 0.80 | 0.03 |

  Of course, there are many other (and generally preferred) ways to operate over a span’s data than to use 

`fixed`. For example, it’s a bit surprising that until `Span<T>` came along, .NET didn’t have a built-in equivalent of `memcmp`, but nevertheless, `Span<T>`‘s `SequenceEqual` and `SequenceCompareTo` methods have become go-to methods for comparing in-memory data in .NET. In .NET Core 2.1, both `SequenceEqual` and `SequenceCompareTo` were optimized to utilize `System.Numerics.Vector` for vectorization, but the nature of `SequenceEqual` made it more amenable to best take advantage. In PR [dotnet/coreclr#22127](https://github.com/dotnet/coreclr/pull/22127), @benaadams updated `SequenceCompareTo` to take advantage of the new hardware instrinsics APIs available in .NET Core 3.0 to specifically target AVX2 and SSE2, resulting in significant improvements when comparing both small and large spans. (For more information on hardware intrinsics in .NET Core 3.0, see [platform-intrinsics.md](https://github.com/dotnet/designs/blob/master/accepted/platform-intrinsics.md) and [using-net-hardware-intrinsics-api-to-accelerate-machine-learning-scenarios](https://devblogs.microsoft.com/dotnet/using-net-hardware-intrinsics-api-to-accelerate-machine-learning-scenarios/).)

```
private byte[] _orig, _same, _differFirst, _differLast;

[Params(16, 256)]
public int Length { get; set; }

[GlobalSetup]
public void Setup()
{
    _orig = Enumerable.Range(0, Length).Select(i => (byte)i).ToArray();
    _same = (byte[])_orig.Clone();

    _differFirst = (byte[])_orig.Clone();
    _differFirst[0] = (byte)(_orig[0] + 1);

    _differLast = (byte[])_orig.Clone();
    _differLast[_differLast.Length - 1] = (byte)(_orig[_orig.Length - 1] + 1);
}

[Benchmark]
public int CompareSame() => _orig.AsSpan().SequenceCompareTo(_same);

[Benchmark]
public int CompareDifferFirst() => _orig.AsSpan().SequenceCompareTo(_differFirst);

[Benchmark]
public int CompareDifferLast() => _orig.AsSpan().SequenceCompareTo(_differLast);
```

| Method | Toolchain | Length | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- | --- |
| CompareSame | netcoreapp2.1 | 16 | 16.955 ns | 0.2009 ns | 0.1781 ns | 1.00 |
| CompareSame | netcoreapp3.0 | 16 | 4.757 ns | 0.0938 ns | 0.0732 ns | 0.28 |
|  |  |  |  |  |  |  |
| CompareDifferFirst | netcoreapp2.1 | 16 | 11.874 ns | 0.1240 ns | 0.1100 ns | 1.00 |
| CompareDifferFirst | netcoreapp3.0 | 16 | 5.174 ns | 0.0543 ns | 0.0508 ns | 0.44 |
|  |  |  |  |  |  |  |
| CompareDifferLast | netcoreapp2.1 | 16 | 16.644 ns | 0.2146 ns | 0.2007 ns | 1.00 |
| CompareDifferLast | netcoreapp3.0 | 16 | 5.373 ns | 0.0479 ns | 0.0448 ns | 0.32 |
|  |  |  |  |  |  |  |
| CompareSame | netcoreapp2.1 | 256 | 43.740 ns | 0.8226 ns | 0.7292 ns | 1.00 |
| CompareSame | netcoreapp3.0 | 256 | 11.055 ns | 0.1625 ns | 0.1441 ns | 0.25 |
|  |  |  |  |  |  |  |
| CompareDifferFirst | netcoreapp2.1 | 256 | 12.144 ns | 0.0849 ns | 0.0752 ns | 1.00 |
| CompareDifferFirst | netcoreapp3.0 | 256 | 6.663 ns | 0.1044 ns | 0.0977 ns | 0.55 |
|  |  |  |  |  |  |  |
| CompareDifferLast | netcoreapp2.1 | 256 | 39.697 ns | 0.9291 ns | 2.6054 ns | 1.00 |
| CompareDifferLast | netcoreapp3.0 | 256 | 11.242 ns | 0.2218 ns | 0.1732 ns | 0.32 |

  As background, “vectorization” is an approach to parallelization that performs multiple operations as part of individual instructions on a single core. Some optimizing compilers can perform automatic vectorization, whereby the compiler analyzes loops to determine whether it can generate functionally equivalent code that would utilize such instructions to run faster. The .NET JIT compiler does not currently perform auto-vectorization, but it is possible to manually vectorize loops, and the options for doing so have significantly improved in .NET Core 3.0. Just as a simple example of what vectorization can look like, imagine having an array of bytes and wanting to search it for the first non-zero byte, returning the position of that byte. The simple solution is to just iterate through all of the bytes:

```
private byte[] _buffer = new byte[10_000].Concat(new byte[] { 42 }).ToArray();

[Benchmark(Baseline = true)]
public int LoopBytes()
{
    byte[] buffer = _buffer;
    for (int i = 0; i < buffer.Length; i++)
    {
        if (buffer[i] != 0)
            return i;
    }
    return -1;
}
```

That of course works functionally, and for very small arrays it’s fine. But for larger arrays, we end up doing significantly more work than is actually necessary. Consider instead in a 64-bit process re-interpreting the array of bytes as an array of longs, which 

`Span<T>` nicely supports. We then effectively compare 8 bytes at a time rather than 1 byte at a time, at the expense of added code complexity: once we find a non-zero long, we then need to look at each byte it contains to determine the position of the first non-zero one (though there are ways to improve that, too). Similarly, the array’s length may not evenly divide by 8, so we need to be able to handle the overflow.

```
[Benchmark]
public int LoopLongs()
{
    byte[] buffer = _buffer;
    int remainingStart = 0;

    if (IntPtr.Size == sizeof(long))
    {
        Span<long> longBuffer = MemoryMarshal.Cast<byte, long>(buffer);
        remainingStart = longBuffer.Length * sizeof(long);

        for (int i = 0; i < longBuffer.Length; i++)
        {
            if (longBuffer[i] != 0)
            {
                remainingStart = i * sizeof(long);
                break;
            }
        }
    }

    for (int i = remainingStart; i < buffer.Length; i++)
    {
        if (buffer[i] != 0)
            return i;
    }

    return -1;
}
```

For longer arrays, this yields really nice wins:

| Method | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- |
| LoopBytes | 5,462.3 ns | 107.093 ns | 105.180 ns | 1.00 |
| LoopLongs | 568.6 ns | 6.895 ns | 5.758 ns | 0.10 |

  I’ve glossed over some details here, but it should convey the core idea. .NET includes additional mechanisms for vectorizing as well. In particular, the aforementioned 

`System.Numerics.Vector` type allows for a developer to write code using `Vector` and then have the JIT compiler translate that into the best instructions available on the current platform.

```
[Benchmark]
public int LoopVectors()
{
    byte[] buffer = _buffer;
    int remainingStart = 0;

    if (Vector.IsHardwareAccelerated)
    {
        while (remainingStart <= buffer.Length - Vector<byte>.Count)
        {
            var vector = new Vector<byte>(buffer, remainingStart);
            if (!Vector.EqualsAll(vector, default))
            {
                break;
            }
            remainingStart += Vector<byte>.Count;
        }
    }

    for (int i = remainingStart; i < buffer.Length; i++)
    {
        if (buffer[i] != 0)
            return i;
    }

    return -1;
}
```

| Method | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- |
| LoopBytes | 5,462.3 ns | 107.093 ns | 105.180 ns | 1.00 |
| LoopLongs | 568.6 ns | 6.895 ns | 5.758 ns | 0.10 |
| LoopVectors | 306.0 ns | 4.502 ns | 4.211 ns | 0.06 |

  Further, .NET Core 3.0 includes new hardware intrinsics that allow a properly-motivated developer to eke out the best possible performance on supporting hardware, utilizing extensions like AVX or SSE that can compare well more than 8 bytes at a time. Many of the improvements in .NET Core 3.0 come from utilizing these techniques. Back to examples, copying spans has also improved, thanks to PRs 

[dotnet/coreclr#18006](https://github.com/dotnet/coreclr/pull/18006) from @benaadams and [dotnet/coreclr#17889](https://github.com/dotnet/coreclr/pull/17889), in particular for relatively small spans…

```
private byte[] _from = new byte[] { 1, 2, 3, 4 };
private byte[] _to = new byte[4];

[Benchmark]
public void CopySpan() => _from.AsSpan().CopyTo(_to);
```

| Method | Toolchain | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- |
| CopySpan | netcoreapp2.1 | 10.913 ns | 0.1960 ns | 0.1737 ns | 1.00 |
| CopySpan | netcoreapp3.0 | 3.568 ns | 0.0528 ns | 0.0494 ns | 0.33 |

  Searching is one of the most commonly performed operations in any program, and searches with spans are generally performed with 

`IndexOf` and its variants (e.g. `IndexOfAny` and `Contains`) In PR [dotnet/coreclr#20738](https://github.com/dotnet/coreclr/pull/20738), @benaadams again utilized vectorization, this time to improve the performance of `IndexOfAny` when operating over bytes, a particularly common case in many networking-related scenarios (e.g. parsing bytes off the wire as part of an HTTP stack). You can see the effects of this in the following microbenchmark:

```
private byte[] _arr = Encoding.UTF8.GetBytes("This is a test to see improvements to IndexOfAny.  How'd they work?");
[Benchmark] public int IndexOf() => new Span<byte>(_arr).IndexOfAny((byte)'.', (byte)'?');
```

| Method | Toolchain | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- |
| IndexOf | netcoreapp2.1 | 12.828 ns | 0.1805 ns | 0.1600 ns | 1.00 |
| IndexOf | netcoreapp3.0 | 4.504 ns | 0.0968 ns | 0.0858 ns | 0.35 |

  I love these kinds of improvements, because they’re low-enough in the stack that they end up having multiplicative effects across so much code. The above change only affected 

`byte`, but subsequent PRs were submitted to cover `char` as well, and then PR [dotnet/coreclr#20855](https://github.com/dotnet/coreclr/pull/20855) made a nice change that brought these same changes to other primitives of the same sizes. For example, we can recast the previous benchmark to use sbyte instead of byte, and as of that PR, a similar improvement applies:

```
private sbyte[] _arr = Encoding.UTF8.GetBytes("This is a test to see improvements to IndexOfAny.  How'd they work?").Select(b => (sbyte)b).ToArray();

[Benchmark]
public int IndexOf() => new Span<sbyte>(_arr).IndexOfAny((sbyte)'.', (sbyte)'?');
```

| Method | Toolchain | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- |
| IndexOf | netcoreapp2.1 | 24.636 ns | 0.2292 ns | 0.2144 ns | 1.00 |
| IndexOf | netcoreapp3.0 | 9.795 ns | 0.1419 ns | 0.1258 ns | 0.40 |

  As another example, consider PR 

[dotnet/coreclr#20275](https://github.com/dotnet/coreclr/pull/20275). That change similarly utilized vectorization to improve the performance of To{Upper/Lower}{Invariant}.

```
private string _src = "This is a source string that needs to be capitalized.";
private char[] _dst = new char[1024];
[Benchmark] public int ToUpperInvariant() => _src.AsSpan().ToUpperInvariant(_dst);
```

| Method | Toolchain | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- |
| ToUpperInvariant | netcoreapp2.1 | 64.36 ns | 0.8099 ns | 0.6763 ns | 1.00 |
| ToUpperInvariant | netcoreapp3.0 | 26.48 ns | 0.2411 ns | 0.2137 ns | 0.41 |

  PR 

[dotnet/coreclr#19959](https://github.com/dotnet/coreclr/pull/19959) optimizes the Trim{Start/End} helpers on `ReadOnlySpan<char>`, another very commonly-applied method, with equally exciting results (it’s hard to see with the white space in the results, but the results in the table go in order of the arguments in the Params attribute):

```
[Params("", " abcdefg ", "abcdefg")]
public string Data;

[Benchmark]
public ReadOnlySpan<char> Trim() => Data.AsSpan().Trim();
```

| Method | Toolchain | Data | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- | --- |
| Trim | netcoreapp2.1 |  | 12.999 ns | 0.1913 ns | 0.1789 ns | 1.00 |
| Trim | netcoreapp3.0 |  | 3.078 ns | 0.0349 ns | 0.0326 ns | 0.24 |
|  |  |  |  |  |  |  |
| Trim | netcoreapp2.1 | abcdefg | 17.618 ns | 0.3534 ns | 0.2951 ns | 1.00 |
| Trim | netcoreapp3.0 | abcdefg | 7.927 ns | 0.0934 ns | 0.0828 ns | 0.45 |
|  |  |  |  |  |  |  |
| Trim | netcoreapp2.1 | abcdefg | 15.522 ns | 0.2200 ns | 0.1951 ns | 1.00 |
| Trim | netcoreapp3.0 | abcdefg | 5.227 ns | 0.0750 ns | 0.0665 ns | 0.34 |

  Sometimes optimizations are just about being smarter about code management. PR 

[dotnet/coreclr#17890](https://github.com/dotnet/coreclr/pull/17890) removed an unnecessary layer of functions that were on many globalization-related code paths, and just removing those extra unnecessary method invocations results in measurable speed-ups when working with small spans, e.g.

```
[Benchmark]
public bool EndsWith() => "Hello world".AsSpan().EndsWith("world", StringComparison.OrdinalIgnoreCase);
```

| Method | Toolchain | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- |
| EndsWith | netcoreapp2.1 | 37.80 ns | 0.3290 ns | 0.2917 ns | 1.00 |
| EndsWith | netcoreapp3.0 | 12.26 ns | 0.1479 ns | 0.1384 ns | 0.32 |

  Of course, one of the great things about span is that it is a reusable building-block that enables many higher-level operations. That includes operations on both arrays and strings…

### Arrays and Strings

As a theme that’s emerged within .NET Core, wherever possible, new performance-focused functionality should not only be exposed for public use but also be used internally; after all, given the depth and breadth of functionality within .NET Core, if some performance-focused feature doesn’t meet the needs of .NET Core itself, there’s a reasonable chance it also won’t meet the public need. As such, internal usage of new features is a key benchmark as to whether the design is adequate, and in the process of evaluating such criteria, many additional code paths benefit, and these improvements have a multiplicative effect. This isn’t just about new APIs. Many of the language features introduced in C# 7.2, 7.3, and 8.0 are influenced by the needs of .NET Core itself and have been used to improve things that we couldn’t reasonably improve before (other than dropping down to unsafe code, which we try to avoid when possible). For example, PR [dotnet/coreclr#17891](https://github.com/dotnet/coreclr/pull/17891) speeds up Array.Reverse by taking advantage of the C# 7.2 ref locals feature and the 7.3 ref local reassignment feature. Using the new feature allows for the code to be expressed in a way that lets the JIT generate better code for the inner loop, and in turn results in a measurable speed-up:

```
private int[] _arr = Enumerable.Range(0, 256).ToArray();

[Benchmark]
public void Reverse() => Array.Reverse(_arr);
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | RatioSD |
| --- | --- | --- | --- | --- | --- | --- |
| Reverse | netcoreapp2.1 | 105.06 ns | 2.488 ns | 7.337 ns | 1.00 | 0.00 |
| Reverse | netcoreapp3.0 | 74.12 ns | 1.494 ns | 2.536 ns | 0.66 | 0.02 |

  Another example for arrays, the 

`Clear` method improved in PR [dotnet/coreclr#24302](https://github.com/dotnet/coreclr/pull/24302), which works around an alignment issue that results in the underlying memset used to implement the operation being up to 2x slower. The change manually clears up to a few bytes one by one, such that the pointer we then hand off to memset is properly aligned. If you got “lucky” previously and the array happened to be aligned, performance was fine, but if it wasn’t aligned, there was a non-trivial performance hit incurred. This benchmark simulates the unlucky case:

```
[GlobalSetup]
public void Setup()
{
    while (true)
    {
        var buffer = new byte[8192];
        GCHandle handle = GCHandle.Alloc(buffer, GCHandleType.Pinned);
        if (((long)handle.AddrOfPinnedObject()) % 32 != 0)
        {
            _handle = handle;
            _buffer = buffer;
            return;
        }
        handle.Free();
    }
}

[GlobalCleanup]
public void Cleanup() => _handle.Free();

private GCHandle _handle;
private byte[] _buffer;

[Benchmark] public void Clear() => Array.Clear(_buffer, 0, _buffer.Length);
```

| Method | Toolchain | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- |
| Clear | netcoreapp2.1 | 121.59 ns | 0.8349 ns | 0.6519 ns | 1.00 |
| Clear | netcoreapp3.0 | 87.91 ns | 1.7768 ns | 1.6620 ns | 0.73 |

  That said, many of the improvements are in fact based on new APIs. Span is a great example of this. It was introduced in .NET Core 2.1, and the initial push was to get it to be usable and expose sufficient surface area to allow it to be used meaningfully. But at the same time, we started utilizing it internally in order to both vet the design and benefit from the improvements it enables. Some of this was done in .NET Core 2.1, but the effort continues in .NET Core 3.0. Arrays and strings are both prime candidates for such optimizations. For example, many of the same vectorization optimizations applied to spans are similarly applied to arrays. PR 

[dotnet/coreclr#21116](https://github.com/dotnet/coreclr/pull/21116) from @benaadams optimized `Array.{Last}IndexOf` for both `byte`s and `char`s, utilizing the same internal helpers that were written to enable spans, and to similar effect:

```
private char[] _arr = "This is a test to see improvements to IndexOf.  How'd they work?".ToCharArray();

[Benchmark]
public int IndexOf() => Array.IndexOf(_arr, '.');
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | RatioSD |
| --- | --- | --- | --- | --- | --- | --- |
| IndexOf | netcoreapp2.1 | 34.976 ns | 0.6352 ns | 0.5631 ns | 1.00 | 0.00 |
| IndexOf | netcoreapp3.0 | 9.471 ns | 0.6638 ns | 1.1091 ns | 0.29 | 0.04 |

  And as with spans, thanks to PR 

[dotnet/coreclr#24293](https://github.com/dotnet/coreclr/pull/24293) from @dschinde, these `IndexOf`optimizations also now apply to other primitives of the same size.

```
private short[] _arr = "This is a test to see improvements to IndexOf.  How'd they work?".Select(c => (short)c).ToArray();

[Benchmark]
public int IndexOf() => Array.IndexOf(_arr, (short)'.');
```

| Method | Toolchain | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- |
| IndexOf | netcoreapp2.1 | 34.181 ns | 0.6626 ns | 0.6508 ns | 1.00 |
| IndexOf | netcoreapp3.0 | 9.600 ns | 0.1913 ns | 0.1598 ns | 0.28 |

  Vectorization optimizations have been applied to strings, too. You can see the effect of PR 

[dotnet/coreclr#21076](https://github.com/dotnet/coreclr/pull/21076) from @benaadams in this microbenchmark:

```
[Benchmark]
public int IndexOf() => "Let's see how fast we can find the period towards the end of this string.  Pretty fast?".IndexOf('.', StringComparison.Ordinal);
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| IndexOf | netcoreapp2.1 | 75.14 ns | 1.5285 ns | 1.6355 ns | 1.00 | 0.0151 | – | – | 32 B |
| IndexOf | netcoreapp3.0 | 11.70 ns | 0.2382 ns | 0.2111 ns | 0.16 | – | – | – | – |

  Also note in the above that the .NET Core 2.1 operation allocates (due to converting the search character into a string), whereas the .NET Core 3.0 implementation does not. That’s thanks to PR 

[dotnet/coreclr#19788](https://github.com/dotnet/coreclr/pull/19788) from @benaadams. There are of course pieces of functionality that are more unique to strings (albeit also applicable to new functionality exposed on spans), such as hash code computation with various string comparison methods. For example, PR [dotnet/coreclr#20309/](https://github.com/dotnet/coreclr/pull/20309/) improved the performance of `String.GetHashCode` when performing `OrdinalIgnoreCase` operations, which along with `Ordinal` (the default) represent the two most common modes.

```
[Benchmark]
public int GetHashCodeIgnoreCase() => "Some string".GetHashCode(StringComparison.OrdinalIgnoreCase);
```

| Method | Toolchain | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- |
| GetHashCodeIgnoreCase | netcoreapp2.1 | 47.70 ns | 0.5751 ns | 0.5380 ns | 1.00 |
| GetHashCodeIgnoreCase | netcoreapp3.0 | 14.28 ns | 0.1462 ns | 0.1296 ns | 0.30 |

`OrdinalsIgnoreCase` has been improved for other uses as well. For example, PR [dotnet/coreclr#20734](https://github.com/dotnet/coreclr/pull/20734) improved `String.Equals` when using `StringComparer.OrdinalIgnoreCase`by both vectorizing (checking two chars at a time instead of one) and removing branches from an inner loop:

```
[Benchmark]
public bool EqualsIC() => "Some string".Equals("sOME sTrinG", StringComparison.OrdinalIgnoreCase);
```

| Method | Toolchain | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- |
| EqualsIC | netcoreapp2.1 | 24.036 ns | 0.3819 ns | 0.3572 ns | 1.00 |
| EqualsIC | netcoreapp3.0 | 9.165 ns | 0.0589 ns | 0.0551 ns | 0.38 |

  The previous cases are examples of functionality in 

`String`‘s implementation, but there are lots of ancillary string-related functionality that have seen improvements as well. For example, various operations on `Char` have been improved, such as `Char.GetUnicodeCategory` via PRs [dotnet/coreclr#20983](https://github.com/dotnet/coreclr/pull/20983) and [dotnet/coreclr#20864](https://github.com/dotnet/coreclr/pull/20864):

```
[Params('.', 'a', '\x05D0')]
public char Char { get; set; }

[Benchmark]
public UnicodeCategory GetCategory() => char.GetUnicodeCategory(Char);
```

| Method | Toolchain | Char | Mean | Error | StdDev | Ratio | RatioSD |
| --- | --- | --- | --- | --- | --- | --- | --- |
| GetCategory | netcoreapp2.1 | . | 1.8001 ns | 0.0160 ns | 0.0142 ns | 1.00 | 0.00 |
| GetCategory | netcoreapp3.0 | . | 0.4925 ns | 0.0141 ns | 0.0132 ns | 0.27 | 0.01 |
|  |  |  |  |  |  |  |  |
| GetCategory | netcoreapp2.1 | a | 1.7925 ns | 0.0144 ns | 0.0127 ns | 1.00 | 0.00 |
| GetCategory | netcoreapp3.0 | a | 0.4957 ns | 0.0117 ns | 0.0091 ns | 0.28 | 0.01 |
|  |  |  |  |  |  |  |  |
| GetCategory | netcoreapp2.1 | ? | 3.7836 ns | 0.0493 ns | 0.0461 ns | 1.00 | 0.00 |
| GetCategory | netcoreapp3.0 | ? | 2.7531 ns | 0.0757 ns | 0.0633 ns | 0.73 | 0.02 |

  Those PRs also highlight another case of benefiting from a language improvement. As of C# 7.3, the C# compiler is able to optimize properties of the form:

```
static ReadOnlySpan<byte> s_byteData => new byte[] { … /* constant bytes */ }
```

Rather than emitting this exactly as written, which would allocate a new byte array on each call, the compiler takes advantage of the facts that a) the bytes backing the array are all constant and b) it’s being returned as a read-only span, which means the consumer is unable to mutate the data using safe code. As such, with PR [dotnet/roslyn#24621](https://github.com/dotnet/roslyn/pull/24621), the C# compiler instead emits this by writing the bytes as a binary blob in metadata, and the property then simply creates a span that points directly to that data, making it very fast to access the data, more so even than if this property returned a static byte\[\].

```
// Run with: dotnet run -c Release -f netcoreapp2.1 --filter *Program* --runtimes netcoreapp3.0

private static byte[] ArrayProp { get; } = new byte[] { 1, 2, 3 };

[Benchmark(Baseline = true)]
public ReadOnlySpan<byte> GetArrayProp() => ArrayProp;

private static ReadOnlySpan<byte> SpanProp => new byte[] { 1, 2, 3 };

[Benchmark]
public ReadOnlySpan<byte> GetSpanProp() => SpanProp;
```

| Method | Mean | Error | StdDev | Median | Ratio |
| --- | --- | --- | --- | --- | --- |
| GetArrayProp | 1.3362 ns | 0.0498 ns | 0.0416 ns | 1.3366 ns | 1.000 |
| GetSpanProp | 0.0125 ns | 0.0132 ns | 0.0110 ns | 0.0080 ns | 0.009 |

  Another string-related area that’s gotten some attention is 

`StringBuilder` (not necessarily improvements to `StringBuilder` itself, although it has received some of those, for example a new overload in PR [dotnet/coreclr#20773](https://github.com/dotnet/coreclr/pull/20773) from @Wraith2 that helps avoid accidentally boxing and creating a string from a `ReadOnlyMemory<char>` appended to the builder). Rather, in many situations `StringBuilder`s have been used for convenience but added cost, and with just a little work (and in some cases the new `String.Create` method introduced in .NET Core 2.1), we can eliminate that overhead, in both CPU usage and allocation. Here a few examples… \* PR [dotnet/corefx#33598](https://github.com/dotnet/corefx/pull/33598) removed a `StringBuilder` used in marshaling from `Dns.GetHostEntry`:

```
[Benchmark]
public IPHostEntry GetHostEntry() => Dns.GetHostEntry("34.206.253.53");
```

| Method | Toolchain | Mean | Error | StdDev | Median | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| GetHostEntry | netcoreapp2.1 | 532.7 us | 16.59 us | 46.79 us | 526.8 us | 1.00 | 0.00 | 1.9531 | – | – | 4888 B |
| GetHostEntry | netcoreapp3.0 | 527.7 us | 12.85 us | 37.06 us | 542.8 us | 1.00 | 0.11 | – | – | – | 616 B |

-   PR [dotnet/coreclr#21122](https://github.com/dotnet/coreclr/pull/21122) removed a `StringBuilder` used in Hebrew number formatting:

```
private static CultureInfo CreateCulture()
{
    var c = new CultureInfo("he-IL");
    c.DateTimeFormat.Calendar = new HebrewCalendar();
    return c;
}

private CultureInfo _hebrewIsrael = CreateCulture();

[Benchmark]
public string FormatHebrew() => new DateTime(2018, 11, 20).ToString(_hebrewIsrael);
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| FormatHebrew | netcoreapp2.1 | 626.0 ns | 7.917 ns | 7.405 ns | 1.00 | 0.00 | 0.2890 | – | – | 608 B |
| FormatHebrew | netcoreapp3.0 | 570.6 ns | 10.504 ns | 9.825 ns | 0.91 | 0.02 | 0.1554 | – | – | 328 B |

-   PR [dotnet/corefx#33592](https://github.com/dotnet/corefx/pull/33592) removed a `StringBuilder` used in `PhysicalAddress` formatting:

```
private readonly PhysicalAddress _short = new PhysicalAddress(new byte[1] { 42 });
private readonly PhysicalAddress _long = new PhysicalAddress(Enumerable.Range(0, 256).Select(i => (byte)i).ToArray());

[Benchmark]
public void PAShort() => _short.ToString();

[Benchmark]
public void PALong() => _long.ToString();
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| PAShort | netcoreapp2.1 | 33.68 ns | 1.0378 ns | 2.9271 ns | 1.00 | 0.00 | 0.0648 | – | – | 136 B |
| PAShort | netcoreapp3.0 | 17.12 ns | 0.4240 ns | 0.7313 ns | 0.55 | 0.04 | 0.0153 | – | – | 32 B |
|  |  |  |  |  |  |  |  |  |  |  |
| PALong | netcoreapp2.1 | 2,761.80 ns | 50.1515 ns | 46.9117 ns | 1.00 | 0.00 | 1.1940 | – | – | 2512 B |
| PALong | netcoreapp3.0 | 787.78 ns | 27.4673 ns | 80.1234 ns | 0.31 | 0.01 | 0.5007 | – | – | 1048 B |

-   PR [dotnet/corefx#29605](https://github.com/dotnet/corefx/pull/29605) removed `StringBuilder`s from various properties of `X509Certificate`:

```
private X509Certificate2 _cert = GetCert();

private static X509Certificate2 GetCert()
{
    using (var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
    {
        client.Connect("microsoft.com", 443);
        using (var ssl = new SslStream(new NetworkStream(client)))
        {
            ssl.AuthenticateAsClient("microsoft.com", null, SslProtocols.None, false);
            return new X509Certificate2(ssl.RemoteCertificate);
        }
    }
}

[Benchmark]
public string CertProp() => _cert.Thumbprint;
```

| Method | Toolchain | Mean | Error | StdDev | Median | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CertProp | netcoreapp2.1 | 209.30 ns | 4.464 ns | 10.435 ns | 204.35 ns | 1.00 | 0.00 | 0.1256 | – | – | 264 B |
| CertProp | netcoreapp3.0 | 95.82 ns | 1.822 ns | 1.704 ns | 96.43 ns | 0.45 | 0.02 | 0.0497 | – | – | 104 B |

  and so on. These PRs demonstrate that good gains can be had simply by making small tweaks that make existing code paths cheaper, and that expands well beyond 

`StringBuilder`. There are lots of places within .NET Core, for example, where `String.Substring` is used, and many of those cases can be replaced with use of `AsSpan` and `Slice`, for example as was done in PR [dotnet/corefx#29402](https://github.com/dotnet/corefx/pull/29402) by @juliushardt, or PRs [dotnet/coreclr#17916](https://github.com/dotnet/coreclr/pull/17916) and [dotnet/corefx#29539](https://github.com/dotnet/corefx/pull/29539), or as was done in PRs [dotnet/corefx#29227](https://github.com/dotnet/corefx/pull/29227) and [dotnet/corefx#29721](https://github.com/dotnet/corefx/pull/29721) to remove string allocations from FileSystemWatcher, delaying the creation of such strings until only when it was known they were absolutely necessary.

```
[Benchmark]
public void HtmlDecode() => WebUtility.HtmlDecode("水水水水水水水");
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| HtmlDecode | netcoreapp2.1 | 638.2 ns | 8.474 ns | 7.077 ns | 1.00 | 0.1516 | – | – | 320 B |
| HtmlDecode | netcoreapp3.0 | 153.7 ns | 2.776 ns | 2.461 ns | 0.24 | 0.0191 | – | – | 40 B |

  Another example of using new APIs to improve existing functionality is with 

`String.Concat`. .NET Core 3.0 has several new `String.Concat` overloads, ones that accept `ReadOnlySpan<char>` instead of `string`. These make it easy to avoid allocations/copies of substrings in cases where concatenating pieces of other strings: instead of using `String.Concat` with `String.Substring`, it’s used instead with `String.AsSpan(...)` or `Slice`. In fact, the PRs [dotnet/coreclr#21766](https://github.com/dotnet/coreclr/pull/21766) and [dotnet/corefx#34451](https://github.com/dotnet/corefx/pull/34451) that implemented, exposed, and added tests for these new overloads also added tens of call sites to the new overloads across .NET Core. Here’s an example of the impact one of those has, improving the performance of accessing `Uri.DnsSafeHost`:

```
[Benchmark]
public string DnsSafeHost() => new Uri("http://[fe80::3]%1").DnsSafeHost;
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| DnsSafeHost | netcoreapp2.1 | 733.7 ns | 14.448 ns | 17.20 ns | 1.00 | 0.00 | 0.2012 | – | – | 424 B |
| DnsSafeHost | netcoreapp3.0 | 450.1 ns | 9.013 ns | 18.41 ns | 0.63 | 0.02 | 0.1059 | – | – | 224 B |

  Another example, using 

`Path.ChangeExtension` to change from one non-null extension to another:

```
[Benchmark]
public string ChangeExtension() => Path.ChangeExtension("filename.txt", ".dat");
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ChangeExtension | netcoreapp2.1 | 30.57 ns | 0.7124 ns | 0.6664 ns | 1.00 | 0.0495 | – | – | 104 B |
| ChangeExtension | netcoreapp3.0 | 24.54 ns | 0.3398 ns | 0.2838 ns | 0.80 | 0.0229 | – | – | 48 B |

  Finally, a very closely related area is that of encoding. A bunch of improvements were made in .NET Core 3.0 around 

`Encoding`, both in general and for specific encodings, such as PR [dotnet/coreclr#18263](https://github.com/dotnet/coreclr/pull/18263) that allowed an existing corner-case optimization to be applied for `Encoding.Unicode.GetString` in many more cases, or [dotnet/coreclr#18487](https://github.com/dotnet/coreclr/pull/18487) that removed a bunch of unnecessary virtual indirections from various encoding implementations, or PR [dotnet/coreclr#20768](https://github.com/dotnet/coreclr/pull/20768) that improved the performance of `Encoding.Preamble` by taking advantage of the same metadata-blob span support discussed earlier, or PRs [dotnet/coreclr#21948](https://github.com/dotnet/coreclr/pull/21948) and [dotnet/coreclr#23098](https://github.com/dotnet/coreclr/pull/23098) that overhauled and streamlined the implementions of `UTF8Encoding` and `AsciiEncoding`.

```
private byte[] _data = Encoding.ASCII.GetBytes("This is a test of ASCII encoding. It's faster now.");

[Benchmark]
public string ASCII() => Encoding.ASCII.GetString(_data);
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ASCII | netcoreapp2.1 | 66.92 ns | 0.8942 ns | 0.8364 ns | 1.00 | 0.0609 | – | – | 128 B |
| ASCII | netcoreapp3.0 | 28.04 ns | 0.6325 ns | 0.9467 ns | 0.42 | 0.0612 | – | – | 128 B |

  These examples all served to highlight improvements made in and around strings. That’s all well and good, but where the improvements related to strings really start to shine is when looking at improvements around formatting and parsing.

### Parsing/Formatting

Parsing and formatting are the lifeblood of any modern web app or service: take data off the wire, parse it, manipulate it, format it back out. As such, in .NET Core 2.1 along with bringing up `Span<T>`, we invested in the formatting and parsing of primitives, from `Int32` to `DateTime`. Many of those changes can be read about in my previous blog posts, but one of the key factors in enabling those performance improvements was in moving a lot of native code to managed. That may be counter-intuitive, in that it’s “common knowledge” that C code is faster than C# code. However, in addition to the gap between them narrowing, having (mostly) safe C# code has made the code base easier to experiment in, so whereas we may have been skittish about tweaking the native implementations, the community-at-large has dived head first into optimizing these implementations wherever possible. That effort continues in full force in .NET Core 3.0, with some very nice rewards reaped. Let’s start with core integer primitives. PR [dotnet/coreclr#18897](https://github.com/dotnet/coreclr/pull/18897) added a variety of special paths for the parsing of `Integer`\-style signed values (e.g. `Int32` and `Int64`), PR [dotnet/coreclr#18930](https://github.com/dotnet/coreclr/pull/18930) added similar support for unsigned (e.g. `UInt32` and `UInt64`), and PR [dotnet/coreclr#18952](https://github.com/dotnet/coreclr/pull/18952) did a similar pass for hex. On top of those, PR [dotnet/coreclr#21365](https://github.com/dotnet/coreclr/pull/21365) layered in additional optimizations, for example utilizing those changes for primitives like `byte`, skipping unnecessary layers of functions, streamlining some calls to improve inlining, and further reducing branching. The net impact here are some significant improvements to the performance of parsing integer primitive types in this release.

```
[Benchmark]
public int ParseInt32Dec() => int.Parse("12345678");

[Benchmark]
public int ParseInt32Hex() => int.Parse("BC614E", NumberStyles.HexNumber);
```

| Method | Toolchain | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- |
| ParseInt32Dec | netcoreapp2.1 | 77.30 ns | 0.8710 ns | 0.8147 ns | 1.00 |
| ParseInt32Dec | netcoreapp3.0 | 16.08 ns | 0.2168 ns | 0.2028 ns | 0.21 |
|  |  |  |  |  |  |
| ParseInt32Hex | netcoreapp2.1 | 69.01 ns | 1.0024 ns | 0.9377 ns | 1.00 |
| ParseInt32Hex | netcoreapp3.0 | 17.39 ns | 0.1123 ns | 0.0995 ns | 0.25 |

  Formatting of such types was also improved, even though it had already been improved significantly between .NET Core 2.0 and .NET Core 2.1. PR 

[dotnet/coreclr#19551](https://github.com/dotnet/coreclr/pull/19551) tweaked the structure of the code to avoid needing to access the current culture number formatting data if it wouldn’t be needed (e.g. when formatting a value as hex, there’s no customization based on current culture), and PR [dotnet/coreclr#18935](https://github.com/dotnet/coreclr/pull/18935) improved decimal formatting performance, in large part by optimizing how data is passed around (or not passed at all).

```
[Benchmark]
public string DecimalToString() => 12345.6789m.ToString();
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| DecimalToString | netcoreapp2.1 | 88.79 ns | 1.4034 ns | 1.3127 ns | 1.00 | 0.0228 | – | – | 48 B |
| DecimalToString | netcoreapp3.0 | 76.62 ns | 0.5957 ns | 0.5572 ns | 0.86 | 0.0228 | – | – | 48 B |

  In fact, 

`System.Decimal` itself has been overhauled in .NET Core 3.0, as of PR [dotnet/coreclr#18948](https://github.com/dotnet/coreclr/pull/18948) now with an entirely managed implementation, and with additional performance work in PRs like [dotnet/coreclr#20305](https://github.com/dotnet/coreclr/pull/20305).

```
private decimal _a = 67891.2345m;
private decimal _b = 12345.6789m;

[Benchmark]
public decimal Add() => _a + _b;

[Benchmark]
public decimal Subtract() => _a - _b;

[Benchmark]
public decimal Multiply() => _a * _b;

[Benchmark]
public decimal Divide() => _a / _b;

[Benchmark]
public decimal Mod() => _a % _b;

[Benchmark]
public decimal Floor() => decimal.Floor(_a);

[Benchmark]
public decimal Round() => decimal.Round(_a);
```

| Method | Toolchain | Mean | Error | StdDev | Median | Ratio | RatioSD |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Add | netcoreapp2.1 | 12.021 ns | 0.6813 ns | 2.0088 ns | 11.507 ns | 1.00 | 0.00 |
| Add | netcoreapp3.0 | 8.300 ns | 0.0553 ns | 0.0518 ns | 8.312 ns | 0.87 | 0.04 |
|  |  |  |  |  |  |  |  |
| Subtract | netcoreapp2.1 | 13.026 ns | 0.2599 ns | 0.2431 ns | 13.046 ns | 1.00 | 0.00 |
| Subtract | netcoreapp3.0 | 8.613 ns | 0.2024 ns | 0.2770 ns | 8.488 ns | 0.66 | 0.03 |
|  |  |  |  |  |  |  |  |
| Multiply | netcoreapp2.1 | 19.215 ns | 0.2813 ns | 0.2631 ns | 19.229 ns | 1.00 | 0.00 |
| Multiply | netcoreapp3.0 | 7.182 ns | 0.1795 ns | 0.2457 ns | 7.131 ns | 0.38 | 0.01 |
|  |  |  |  |  |  |  |  |
| Divide | netcoreapp2.1 | 196.827 ns | 4.3572 ns | 4.6621 ns | 194.721 ns | 1.00 | 0.00 |
| Divide | netcoreapp3.0 | 75.456 ns | 1.5301 ns | 1.7007 ns | 75.089 ns | 0.38 | 0.01 |
|  |  |  |  |  |  |  |  |
| Mod | netcoreapp2.1 | 464.968 ns | 7.0295 ns | 6.5754 ns | 466.825 ns | 1.00 | 0.00 |
| Mod | netcoreapp3.0 | 13.756 ns | 0.2476 ns | 0.2316 ns | 13.729 ns | 0.03 | 0.00 |
|  |  |  |  |  |  |  |  |
| Floor | netcoreapp2.1 | 33.593 ns | 0.8348 ns | 2.2710 ns | 32.734 ns | 1.00 | 0.00 |
| Floor | netcoreapp3.0 | 12.109 ns | 0.1325 ns | 0.1239 ns | 12.085 ns | 0.33 | 0.02 |
|  |  |  |  |  |  |  |  |
| Round | netcoreapp2.1 | 32.181 ns | 0.5660 ns | 0.5294 ns | 32.018 ns | 1.00 | 0.00 |
| Round | netcoreapp3.0 | 12.798 ns | 0.1572 ns | 0.1394 ns | 12.808 ns | 0.40 | 0.01 |

  Back to formatting and parsing, there are even some new formatting special-cases that might look silly at first, but that represent optimizations targeting real-world cases. In some sizeable web applications, we found that a large number of strings on the managed heap were simple integral values like “0” and “1”. And since the fastest code is code you don’t need to execute at all, why bother allocating and formatting these small numbers over and over when we can instead just cache and reuse the results (effectively our own string interning pool)? That’s what PR 

[dotnet/coreclr#18383](https://github.com/dotnet/coreclr/pull/18383) does, creating a small, specialized cache of the strings for “0” through “9”, and any time we now find ourselves formatting a single-digit integer primitive, we instead just grab the relevant string from this cache.

```
private int _digit = 4;

[Benchmark]
public string SingleDigitToString() => _digit.ToString();
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| SingleDigitToString | netcoreapp2.1 | 17.72 ns | 0.3273 ns | 0.3061 ns | 1.00 | 0.0152 | – | – | 32 B |
| SingleDigitToString | netcoreapp3.0 | 11.57 ns | 0.1750 ns | 0.1551 ns | 0.65 | – | – | – | – |

  Enums have also seen sizable parsing and formatting improvements in .NET Core 3.0. PR 

[dotnet/coreclr#21214](https://github.com/dotnet/coreclr/pull/21214) improved the handling of `Enum.Parse` and `Enum.TryParse`, for both the generic and non-generic variants. PR [dotnet/coreclr#21254](https://github.com/dotnet/coreclr/pull/21254) improved the performance of `ToString` when dealing with `[Flags]` enums, and PR [dotnet/coreclr#21284](https://github.com/dotnet/coreclr/pull/21284) further improved other ToString cases. The net effect of these changes is a sizeable improvement in `Enum`\-related performance:

```
[Benchmark]
public DayOfWeek EnumParse() => Enum.Parse<DayOfWeek>("Thursday");

[Benchmark]
public string EnumToString() => NumberStyles.Integer.ToString();
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| EnumParse | netcoreapp2.1 | 154.42 ns | 1.6917 ns | 1.5824 ns | 1.00 | 0.0114 | – | – | 24 B |
| EnumParse | netcoreapp3.0 | 62.92 ns | 1.2239 ns | 1.1448 ns | 0.41 | – | – | – | – |
|  |  |  |  |  |  |  |  |  |  |
| EnumToString | netcoreapp2.1 | 85.81 ns | 1.6458 ns | 1.3743 ns | 1.00 | 0.0305 | – | – | 64 B |
| EnumToString | netcoreapp3.0 | 27.89 ns | 0.6076 ns | 0.7901 ns | 0.32 | 0.0114 | 0.0001 | – | 24 B |

  In .NET Core 2.1, 

`DateTime.TryFormat` and `ToString` were optimized for the commonly-used “o” and “r” formats; in .NET Core 3.0, the parsing equivalents get a similar treatment. PR [dotnet/coreclr#18800](https://github.com/dotnet/coreclr/pull/18800) significantly improves the performance of parsing `DateTime{Offset}`s formatted with the Roundtrip “o” format, and PR [dotnet/coreclr#18771](https://github.com/dotnet/coreclr/pull/18771) does the same for the RFC1123 “r” format. For any serialization formats heavy in `DateTime`s, these improvements can make a substantial impact:

```
private string _r = DateTime.Now.ToString("r");
private string _o = DateTime.Now.ToString("o");

[Benchmark]
public DateTime ParseR() => DateTime.ParseExact(_r, "r", null);

[Benchmark]
public DateTime ParseO() => DateTime.ParseExact(_o, "o", null);
```

| Method | Toolchain | Mean | Error | StdDev | Median | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ParseR | netcoreapp2.1 | 2,254.6 ns | 44.340 ns | 45.534 ns | 2,263.2 ns | 1.00 | 0.0420 | – | – | 96 B |
| ParseR | netcoreapp3.0 | 113.7 ns | 3.440 ns | 9.926 ns | 112.6 ns | 0.06 | – | – | – | – |
|  |  |  |  |  |  |  |  |  |  |  |
| ParseO | netcoreapp2.1 | 1,337.1 ns | 26.542 ns | 68.987 ns | 1,363.8 ns | 1.00 | 0.0744 | – | – | 160 B |
| ParseO | netcoreapp3.0 | 354.9 ns | 4.801 ns | 3.748 ns | 354.9 ns | 0.30 | – | – | – | – |

  Tying back to the 

`StringBuilder` discussion from earlier, default `DateTime` formatting was also improved by PR [dotnet/coreclr#22111](https://github.com/dotnet/coreclr/pull/22111), tweaking how `DateTime` internally interacts with a `StringBuilder` that’s used to build up the resulting state.

```
private DateTime _now = DateTime.Now;

[Benchmark]
public string DateTimeToString() => _now.ToString();
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| DateTimeToString | netcoreapp2.1 | 337.8 ns | 6.560 ns | 5.815 ns | 1.00 | 0.00 | 0.0834 | – | – | 176 B |
| DateTimeToString | netcoreapp3.0 | 269.4 ns | 5.274 ns | 5.416 ns | 0.80 | 0.02 | 0.0300 | – | – | 64 B |

`TimeSpan` formatting was also significantly improved, via PR [dotnet/coreclr#18990](https://github.com/dotnet/coreclr/pull/18990):

```
private TimeSpan _ts = new TimeSpan(3, 10, 2, 34, 567);

[Benchmark]
public string TimeSpanToString() => _ts.ToString();
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| TimeSpanToString | netcoreapp2.1 | 151.11 ns | 2.0037 ns | 1.874 ns | 1.00 | 0.0303 | – | – | 64 B |
| TimeSpanToString | netcoreapp3.0 | 34.73 ns | 0.7680 ns | 1.304 ns | 0.23 | 0.0305 | – | – | 64 B |

`Guid` parsing also got in the perf-optimization game, with PR [dotnet/coreclr#20183](https://github.com/dotnet/coreclr/pull/20183) improved parsing performance of `Guid`, primarily by avoiding overhead in helper routines, as well as by avoiding some searches used to determine which parsing routines to employ.

```
private string _guid = Guid.NewGuid().ToString("D");

[Benchmark]
public Guid ParseGuid() => Guid.ParseExact(_guid, "D");
```

| Method | Toolchain | Mean | Error | StdDev | Median | Ratio |
| --- | --- | --- | --- | --- | --- | --- |
| ParseGuid | netcoreapp2.1 | 287.5 ns | 11.606 ns | 28.688 ns | 277.2 ns | 1.00 |
| ParseGuid | netcoreapp3.0 | 111.7 ns | 2.199 ns | 2.057 ns | 112.4 ns | 0.33 |

  Related, PR 

[dotnet/coreclr#21336](https://github.com/dotnet/coreclr/pull/21336) again takes advantage of vectorization to improve `Guid`‘s construction and formatting to and from byte arrays and spans:

```
private Guid _guid = Guid.NewGuid();
private byte[] _buffer = new byte[16];

[Benchmark]
public void GuidToFromBytes()
{
    _guid.TryWriteBytes(_buffer);
    _guid = new Guid(_buffer);
}
```

| Method | Toolchain | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- |
| GuidToFromBytes | netcoreapp2.1 | 16.623 ns | 0.2917 ns | 0.2586 ns | 1.00 |
| GuidToFromBytes | netcoreapp3.0 | 5.701 ns | 0.1047 ns | 0.0980 ns | 0.34 |

### Regular Expressions

Often related to parsing is the area of regular expressions. A bit of work was done on `System.Text.RegularExpressions` in .NET Core 3.0. PR [dotnet/corefx#30474](https://github.com/dotnet/corefx/pull/30474) replaced some usage of an internal `StringBuilder` cache with a `ref struct`\-based builder that takes advantage of stack-allocated space and pooled buffers. And PR [dotnet/corefx#30632](https://github.com/dotnet/corefx/pull/30632) continued the effort by taking further advantage of spans. But the biggest improvement came in PR [dotnet/corefx#32899](https://github.com/dotnet/corefx/pull/32899) from @Alois-xx, which tweaks the code generated for a `RegexOptions.Compiled` `Regex` to avoid gratuitous thread-local accesses to look up the current culture. This is particularly impactful when also using `RegexOptions.IgnoreCase`. To see the impact, I found a complicated `Regex` that used both `Compiled` and `IgnoreCase`, and put it into a benchmark:

```
// Pattern and options copied from https://github.com/microsoft/referencesource/blob/aaca53b025f41ab638466b1efe569df314f689ea/System.ComponentModel.DataAnnotations/DataAnnotations/EmailAddressAttribute.cs#L54-L55
private Regex _regex = new Regex(
    @"^((([a-z]|\d|[!#\$%&'\*\+\-\/=\?\^_`{\|}~]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])+(\.([a-z]|\d|[!#\$%&'\*\+\-\/=\?\^_`{\|}~]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])+)*)|((\x22)((((\x20|\x09)*(\x0d\x0a))?(\x20|\x09)+)?(([\x01-\x08\x0b\x0c\x0e-\x1f\x7f]|\x21|[\x23-\x5b]|[\x5d-\x7e]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(\\([\x01-\x09\x0b\x0c\x0d-\x7f]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF]))))*(((\x20|\x09)*(\x0d\x0a))?(\x20|\x09)+)?(\x22)))@((([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])([a-z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])*([a-z]|\d|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])))\.)+(([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])|(([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])([a-z]|\d|-|\.|_|~|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])*([a-z]|[\u00A0-\uD7FF\uF900-\uFDCF\uFDF0-\uFFEF])))\.?$",
    RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ExplicitCapture);

[Benchmark]
public bool RegexCompiled() => _regex.IsMatch("someAddress@someCompany.com");
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | RatioSD |
| --- | --- | --- | --- | --- | --- | --- |
| RegexCompiled | netcoreapp2.1 | 1.946 us | 0.0406 us | 0.0883 us | 1.00 | 0.00 |
| RegexCompiled | netcoreapp3.0 | 1.209 us | 0.0432 us | 0.1254 us | 0.64 | 0.08 |

### Threading

Threading is one of those things that’s ever-present and yet most apps and libraries don’t need to explicitly interact with most of the time. That makes it an area ripe for runtime performance improvements to drive down overhead as much as possible, so that user code just gets faster. Previous releases of .NET Core saw a lot of investment in this area, and .NET Core 3.0 continues the trend. This is another area where new APIs have been exposed and then also used in .NET Core itself for further gain. For example, historically the only work item types that could be queued to the `ThreadPool` were ones implemented in the runtime, namely those created by `ThreadPool.QueueUserWorkItem` and friends, by `Task`, by `Timer`, and other such core types. But in .NET Core 3.0, the `ThreadPool` has an `UnsafeQueueUserWorkItem` overload that accepts the newly public `IThreadPoolWorkItem` interface. This interface is very simple, with a single method that just `Execute`s work, and that means that any object that implements this interface can be queued directly to the thread pool. This is advanced; most code is just fine using the existing work item types. But this additional option affords a lot of flexibility, in particular in being able to implement the interface on a reusable object that can be queued over and over again to the pool. This is now used in a bunch of additional places in .NET Core 3.0. One such place is in `System.Threading.Channels`. The `Channels` library introduced in .NET Core 2.1 already had a fairly low allocation profile, but there were still times it would allocate. For example, one of the options when creating a channel is whether continuations created by the library should run synchronously or asynchronously as part of a task completing (e.g. when a `TryWrite` call on a channel wakes up a corresponding `ReadAsync`, whether the continuation from that `ReadAsync` invoked synchronously or queued by the `TryWrite` call). The default is that continuations are never invoked synchronously, but that also then requires allocating an object as part of queueing the continuation to the thread pool. With PR [dotnet/corefx#33080](https://github.com/dotnet/corefx/pull/33080), the reusable `IValueTaskSource` implementation that already backs the `ValueTask`s returned from `ReadAsync` calls also implements `IThreadPoolWorkItem` and can thus itself be queued, avoiding that allocation. This can have a measurable impact on throughput.

```
// Run with: dotnet run -c Release -f netcoreapp2.1 --filter *Program*

private sealed class Config : ManualConfig // also add [Config(typeof(Config))] to the Program class
{
    public Config()
    {
        Add(Job.MediumRun.With(CsProjCoreToolchain.NetCoreApp21).WithNuGet("System.Threading.Channels", "4.5.0").WithId("4.5.0"));
        Add(Job.MediumRun.With(CsProjCoreToolchain.NetCoreApp30).WithNuGet("System.Threading.Channels", "4.6.0-preview5.19224.8").WithId("4.6.0-preview5.19224.8"));
    }
}

private Channel<int> _channel1 = Channel.CreateUnbounded<int>();
private Channel<int> _channel2 = Channel.CreateUnbounded<int>();

[GlobalSetup]
public void Setup()
{
    Task.Run(async () =>
    {
        var reader = _channel1.Reader;
        var writer = _channel2.Writer;
        while (true)
        {
            writer.TryWrite(await reader.ReadAsync());
        }
    });
}

[Benchmark]
public async Task PingPong()
{
    var writer = _channel1.Writer;
    var reader = _channel2.Reader;
    for (int i = 0; i < 10_000; i++)
    {
        writer.TryWrite(i);
        await reader.ReadAsync();
    }
}
```

| Method | Job | NuGetReferences | Toolchain | Mean | Error | StdDev | Gen 0 | Gen 1 | Gen 2 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| PingPong | 4.5.0 | System.Threading.Channels 4.5.0 | .NET Core 2.1 | 22.44 ms | 0.3246 ms | 0.4757 ms | 593.7500 | – | – |
| PingPong | 4.6.0-preview5.19224.8 | System.Threading.Channels 4.6.0-preview5.19224.8 | .NET Core 3.0 | 16.81 ms | 0.4246 ms | 0.6356 ms | 31.2500 | – | – |

`IThreadPoolWorkItem` is now also utilized in other places, like in `ConcurrentExclusiveSchedulerPair` (a little known but useful type that provides an exclusive scheduler that limits execution to only one task at a time, a concurrent scheduler that limits a user-defined number of tasks to run at a time, and that coordinate with each other so that no concurrent tasks may run while an exclusive task is running, ala a reader-writer lock), which now implements `IThreadPoolWorkItem` on an internally reusable work item object such that it also can avoid allocations when queueing its own processors. It’s also used in ASP.NET Core, and is one of the reasons key ASP.NET benchmarks are ammortized to 0 allocations per request. But by far the most impactful new implementer is in the async/await infrastructure. In .NET Core 2.1, the runtime’s support for async/await was overhauled, drastically reducing the overheads involved in async methods. Previously when an async method awaited for the first time an awaitable that wasn’t yet complete, the struct-based state machine for the async method would be boxed (literally a runtime box) to the heap. With .NET Core 2.1, we changed that to instead use a generic object that stores the struct as a field on it. This has a myriad of benefits, but one of these benefits is that it now enables us to implement additional interfaces on that object, such as implementing `IThreadPoolWorkItem`. PR [dotnet/coreclr#20159](https://github.com/dotnet/coreclr/pull/20159) does exactly that, and it enables another large swath of scenarios to have further reduced allocations, in particular situations where `TaskCreationOptions.RunContinuationsAsynchronously` was used with a `TaskCompletionSource<T>`. This can be seen in a benchmark like the following.

```
// Run with: dotnet run -c Release -f netcoreapp2.1 --filter *Program*

private sealed class Config : ManualConfig // also add [Config(typeof(Config))] to the Program class
{
    public Config()
    {
        Add(Job.MediumRun.With(CsProjCoreToolchain.NetCoreApp21).WithNuGet("System.Threading.Channels", "4.5.0").WithId("4.5.0"));
        Add(Job.MediumRun.With(CsProjCoreToolchain.NetCoreApp30).WithNuGet("System.Threading.Channels", "4.6.0-preview5.19224.8").WithId("4.6.0-preview5.19224.8"));
    }
}

private Channel<TaskCompletionSource<bool>> _channel = Channel.CreateUnbounded<TaskCompletionSource<bool>>();

[GlobalSetup]
public void Setup()
{
    Task.Run(async () =>
    {
        var reader = _channel.Reader;
        while (true) (await reader.ReadAsync()).TrySetResult(true);
    });
}

[Benchmark]
public async Task AsyncAllocs()
{
    var writer = _channel.Writer;
    for (int i = 0; i < 1_000_000; i++)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        writer.TryWrite(tcs);
        await tcs.Task;
    }
}
```

| Method | Job | NuGetReferences | Toolchain | Mean | Error | StdDev | Gen 0 | Gen 1 | Gen 2 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| AsyncAllocs | 4.5.0 | System.Threading.Channels 4.5.0 | .NET Core 2.1 | 2.396 s | 0.0486 s | 0.0728 s | 96000.0000 | – | – |
| AsyncAllocs | 4.6.0-preview5.19224.8 | System.Threading.Channels 4.6.0-preview5.19224.8 | .NET Core 3.0 | 1.512 s | 0.0256 s | 0.0359 s | 49000.0000 | – | – |

  That change allowed subsequent optimizations, such as PR 

[dotnet/coreclr#20186](https://github.com/dotnet/coreclr/pull/20186) using it to make `await Task.Yield();` allocation-free:

```
[Benchmark]
public async Task Yield()
{
    for (int i = 0; i < 1_000_000; i++)
    {
        await Task.Yield();
    }
}
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | RatioSD | Gen 0 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Yield | netcoreapp2.1 | 581.3 ms | 11.615 ms | 30.39 ms | 1.00 | 0.00 | 19000.0000 |
| Yield | netcoreapp3.0 | 464.4 ms | 9.087 ms | 10.46 ms | 0.81 | 0.06 | – |

  It’s even utilized further in 

`Task` itself. There’s an interesting race condition that has to be handled in awaitables: what happens if the awaited operation completes after the call to `IsCompleted` but before the call to `OnCompleted`? As a reminder, the code:

compiles down to code along the lines of:

```
var $awaiter = something.GetAwaiter();
if (!$awaiter.IsCompleted)
{
    _state = 42;
    AwaitOnCompleted(ref $awaiter);
    return;
}
Label42:
$awaiter.GetResult();
```

Once we go down the path of 

`IsCompleted` having returned `false`, we’re going to call `AwaitOnCompleted` and return. If the operation has completed by the time we call `AwaitOnCompleted`, we don’t want to synchronously invoke the continuation that re-enters this state machine, as we’ll be doing so further down the stack, and if that happened repeatedly, we’d “stack dive” and could end up overflowing the stack. Instead, we’re forced to queue the continuation. This case isn’t the common case, but it happens more often than you might expect, as it simply requires an operation that completes asynchronously very quickly (various networking operations often fall into this category). As of PR [dotnet/coreclr#22373](https://github.com/dotnet/coreclr/pull/22373), the runtime now takes advantage of the async state machine box object implementing `IThreadPoolWorkItem` to avoid the allocations in this case as well! In addition to `IThreadPoolWorkItem` being used with async/await to allow the async implementation to queue work items to the thread pool in a more allocation-friendly manner just as any other code can, changes were also made that give the `ThreadPool` 1st-hand knowledge of the state machine box in order to help it optimize additional cases. PR [dotnet/coreclr#21159](https://github.com/dotnet/coreclr/pull/21159) from @benaadams teaches the `ThreadPool` to re-route some `UnsafeQueueUserWorkItem(Action<object>, object, bool)` calls to instead use `UnsafeQueueUserWorkItem(IAsyncStateMachineBox, bool)` under the covers, so that higher-level libraries can get these allocation benefits without having to be aware of the box machinery. Another async-related area that’s seen measurable improvements are `Timer`s. In .NET Core 2.1, some important improvements were made to `System.Threading.Timers` to help improve throughput and minimize contention for a common case where timers aren’t firing, but instead are quickly created and destroyed. And while those changes help a bit with the case when timers do actually fire, they didn’t help with the majority costs and sources of contention in that case, which is that potentially a lot of work (proportional to the number of timers registered) was done while holding locks. .NET Core 3.0 makes some big improvements here. PR [dotnet/coreclr#20302](https://github.com/dotnet/coreclr/pull/20302) partitions the internal list of registered timers into two lists: one with timers that will soon fire and one with timers that won’t fire for a while. In most workloads that have a lot of registered timers, the majority of timers fall into the latter bucket at any given point in time, and this partitioning scheme enables the runtime to only consider the small bucket when firing timers most of the time. In doing so, it can significantly reduce the costs involved in firing timers, and as a result, also significantly reduce contention on the lock held while manipulating those lists. One customer who tried out these changes after having experienced issues due to tons of active timers had this to say about the impact:

\> “We got the change in production yesterday and the results are amazing, with 99% reduction in lock contention. We have also measured 4-5% CPU gains, and more importantly 0.15% improvement in reliability for our service (which is huge!).” >

The nature of the scenario makes it a little difficult to see the impact in a Benchmark.NET benchmark, so we’ll do something a little different. Rather than measuring the thing that was actually changed, we’ll measure something else that’s indirectly impacted. In particular, these changes didn’t directly impact the performance of creating and destroying timers; in fact, one of the goals was to avoid doing so (in particular to avoid harming that important path). But by reducing the costs of firing timers, we reduce how long locks are held, which then also reduces the contention that the creating/destroying of timers faces. So, our benchmark creates a bunch of timers, ranging in when and how often they fire, and then we time how long it takes to create and destroy a bunch of additional timers.

```
private Timer[] _timers;

[GlobalSetup]
public void Setup()
{
    _timers = new Timer[1_000_000];
    for (int i = 0; i < _timers.Length; i++)
    {
        _timers[i] = new Timer(_ => { }, null, i, i);
    }
    Thread.Sleep(1000);
}

[Benchmark]
public void CreateDestroy()
{
    for (int i = 0; i < 1_000; i++)
    {
        new Timer(_ => { }, 0, 100, 100).Dispose();
    }
}
```

| Method | Toolchain | Mean | Error | StdDev | Median | Ratio | RatioSD | Gen 0 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CreateDestroy | netcoreapp2.1 | 289.1 us | 7.131 us | 20.687 us | 282.8 us | 1.00 | 0.00 | 80.0781 |
| CreateDestroy | netcoreapp3.0 | 199.5 us | 3.983 us | 5.584 us | 199.2 us | 0.71 | 0.04 | 80.3223 |

`Timer` improvements have also taken other forms. For example, PR [dotnet/coreclr#22233](https://github.com/dotnet/coreclr/pull/22233) from @benaadams shrinks the allocation involved in `Task.Delay` when used without a `CancellationToken` by 24 bytes, and PR [dotnet/coreclr#20509](https://github.com/dotnet/coreclr/pull/20509) reduces the timer-related allocations involved in creating timed `CancellationTokenSource`s, which also has a nice effect on throughput:

```
[Benchmark]
public void CTSTimer()
{
    using (var cts = new CancellationTokenSource())
        cts.CancelAfter(1_000_000);
}
```

| Method | Toolchain | Mean | Error | StdDev | Median | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| CTSTimer | netcoreapp2.1 | 231.3 ns | 6.293 ns | 16.018 ns | 224.8 ns | 1.00 | 0.00 | 0.0987 | – | – | 208 B |
| CTSTimer | netcoreapp3.0 | 115.3 ns | 1.769 ns | 1.655 ns | 115.0 ns | 0.46 | 0.04 | 0.0764 | – | – | 160 B |

  There are other even lower-level improvements that have gone into the release. For example, PR 

[dotnet/coreclr#21328](https://github.com/dotnet/coreclr/pull/21328) from @benaadams improved `Thread.CurrentThread` by changing the implementation to store the relevant `Thread` in a `[ThreadStatic]` field rather than forcing `CurrentThread` to make an `InternalCall` into the native portions of the runtime.

```
[Benchmark]
public Thread CurrentThread() => Thread.CurrentThread;
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | RatioSD |
| --- | --- | --- | --- | --- | --- | --- |
| CurrentThread | netcoreapp2.1 | 6.101 ns | 0.2587 ns | 0.7547 ns | 1.00 | 0.00 |
| CurrentThread | netcoreapp3.0 | 2.822 ns | 0.0439 ns | 0.0389 ns | 0.45 | 0.04 |

  As other examples, PR 

[dotnet/coreclr#23747](https://github.com/dotnet/coreclr/pull/23747) taught the runtime to better respect Docker –cpu limits, PRs [dotnet/coreclr#21722](https://github.com/dotnet/coreclr/pull/21722) and [dotnet/coreclr#21586](https://github.com/dotnet/coreclr/pull/21586) improved spinning behavior when contention was encountered across a variety of synchronization sites, PR [dotnet/coreclr#22686](https://github.com/dotnet/coreclr/pull/22686) improved performance of `SemaphoreSlim` when consumers of an instance were mixing both synchronous `Wait`s and asynchronous `WaitAsync`s, and PR [dotnet/coreclr#18098](https://github.com/dotnet/coreclr/pull/18098) from @Quogu special-cased `CancellationTokenSource` created with a timeout of 0 to avoid `Timer`\-related costs.  

### Collections

Moving on from threading, let’s explore some of the performance improvements that have gone into collections. Collections are so commonly used in pretty much every program that they’ve received a lot of performance-focused attention in previous .NET Core releases. Even so, there continues to be areas for improvement. Here are some example such improvements in .NET Core 3.0.

`ConcurrentDictionary<TKey, TValue>` has an `IsEmpty` property that states whether the dictionary is empty at that moment-in-time. In previous releases, it took all of the dictionary’s locks in order to get a proper moment-in-time answer. But as it turns out, those locks only need to be held if we think the collection might be empty: if we see anything in any of the dictionary’s internals buckets, the locks aren’t needed, as we’d stop looking at additional buckets anyway the moment we found one bucket to contain anything. Thus, PR [dotnet/corefx#30098](https://github.com/dotnet/corefx/pull/30098) from @drewnoakes added a fast path that first checks each bucket without the locks, in order to optimize for the common case where the dictionary isn’t empty (the impact on the case where the dictionary is empty is minimal).

```
private ConcurrentDictionary<int, int> _cd;

[GlobalSetup]
public void Setup()
{
    _cd = new ConcurrentDictionary<int, int>();
    _cd.TryAdd(1, 1);
}

[Benchmark] public bool IsEmpty() => _cd.IsEmpty;
```

| Method | Toolchain | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- |
| IsEmpty | netcoreapp2.1 | 73.675 ns | 0.3934 ns | 0.3285 ns | 1.00 |
| IsEmpty | netcoreapp3.0 | 3.160 ns | 0.0402 ns | 0.0356 ns | 0.04 |

`ConcurrentDictionary` wasn’t the only concurrent collection to get some attention. An improvement came to `ConcurrentQueue<T>` in [dotnet/coreclr#18035](https://github.com/dotnet/coreclr/pull/18035), and it’s an interesting example in how performance optimization often is a trade-off between scenarios. In .NET Core 2.0, we overhauled the `ConcurrentQueue` implementation in a way that significantly improved throughput while also significantly reducing memory allocations, turning the `ConcurrentQueue` into a linked list of circular arrays. However, the change involved a concession: because of the producer/consumer nature of the arrays, if any operation needed to observe data in-place in a segment (rather than dequeueing it), the segment that was observed would be “frozen” for any further enqueues… this was to avoid problems where, for example, one thread was enumerating the contents of the segment while another thread was enqueueing and dequeueing. When there were multiple segments in the queue, accessing `Count` ended up being treated as an observation, but that meant that simply accessing the `ConcurrentQueue`‘s `Count` would render all of the multiple segments in the queue dead for further enqueues. The theory at the time was that such a trade-off was fine, because no one should be accessing the `Count` of the queue frequently enough for this to matter. That theory was wrong, and several customers reported significant slowdowns in their workloads because they were accessing the `Count` on every enqueue or dequeue. While the right solution is in general to avoid doing that, we wanted to fix this, and as it turns out, the fix was relatively straightforward, such that we could have our performance cake and eat it, too. The results are very obvious in the following benchmark.

```
private ConcurrentQueue<int> _cq;

[GlobalSetup]
public void Setup()
{
    _cq = new ConcurrentQueue<int>();
    for (int i = 0; i < 100; i++)
    {
        _cq.Enqueue(i);
    }
}

[Benchmark]
public void EnqueueCountDequeue()
{
    _cq.Enqueue(42);
    _ = _cq.Count;
    _cq.TryDequeue(out _);
}
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| EnqueueCountDequeue | netcoreapp2.1 | 708.48 ns | 23.8638 ns | 21.1546 ns | 1.00 | 0.1669 | 0.0830 | 0.0010 | 704 B |
| EnqueueCountDequeue | netcoreapp3.0 | 22.79 ns | 0.4471 ns | 0.4182 ns | 0.03 | – | – | – | – |

`ImmutableDictionary<TKey, TValue>` also got some attention. A customer reported that they’d compared `ImmutableDictionary<TKey, TValue>` and `Dictionary<TKey, TValue>` and found the former to be measurably slower for lookups. This is to be expected, as the types use very different data structures, with `ImmutableDictionary` optimized for being able to inexpensively create a copy of the dictionary with a mutation, something that’s quite expensive to do with `Dictionary`; the trade-off is that it ends up being slower for lookups. Still, it caused us to take a look at the costs involved in `ImmutableDictionary` lookups, and PR [dotnet/corefx#35759](https://github.com/dotnet/corefx/pull/35759) includes several tweaks to improve it, changing a recursive call to be non-recursive and inlinable and avoiding some unnecessary struct wrapping. While this doesn’t make `ImmutableDictionary` and `Dictionary` lookups equivalent, it does improve `ImmutableDictionary` measurably, especially when it contains just a few elements.

```
private ImmutableDictionary<int, int> _hundredInts;

[GlobalSetup]
public void Setup()
{
    _hundredInts = ImmutableDictionary.Create<int, int>();
    for (int i = 0; i < 100; i++)
    {
        _hundredInts = _hundredInts.Add(i, i);
    }
}

[Benchmark]
public int Lookup()
{
    int count = 0;
    {
        for (int i = 0; i < 100; i++)
        {
            for (int j = 0; j < 100; j++)
            {
                if (_hundredInts.TryGetValue(j, out _))
                {
                    count++;
                }
            }
        }
    }
    return count;
}
```

| Method | Toolchain | Mean | Error | StdDev | Median | Ratio | RatioSD |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Lookup | netcoreapp2.1 | 303.9 us | 7.271 us | 15.016 us | 297.8 us | 1.00 | 0.00 |
| Lookup | netcoreapp3.0 | 174.5 us | 3.360 us | 2.806 us | 174.5 us | 0.57 | 0.03 |

  Another collection that’s seen measurable improvements in .NET Core 3.0 is 

`BitArray`. Lots of operations, including construction, were optimized in PR [dotnet/corefx#33367](https://github.com/dotnet/corefx/pull/33367).

```
private byte[] _bytes = Enumerable.Range(0, 100).Select(i => (byte)i).ToArray();

[Benchmark]
public BitArray BitArrayCtor() => new BitArray(_bytes);
```

| Method | Toolchain | Mean | Error | StdDev | Median | Ratio | RatioSD |
| --- | --- | --- | --- | --- | --- | --- | --- |
| BitArrayCtor | netcoreapp2.1 | 82.28 ns | 2.601 ns | 7.546 ns | 77.89 ns | 1.00 | 0.00 |
| BitArrayCtor | netcoreapp3.0 | 46.87 ns | 2.738 ns | 8.030 ns | 44.63 ns | 0.57 | 0.10 |

  Core operations like 

`Set` and `Get` were further improved in PR [dotnet/corefx#35364](https://github.com/dotnet/corefx/pull/35364) from @omariom by streamlining the relevant methods and making them inlineable

```
private BitArray _ba = new BitArray(Enumerable.Range(0, 1000).Select(i => i % 2 == 0).ToArray());

[Benchmark]
public void GetSet()
{
    BitArray ba = _ba;
    for (int i = 0; i < 1000; i++)
    {
        ba.Set(i, !ba.Get(i));
    }
}
```

| Method | Toolchain | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- |
| GetSet | netcoreapp2.1 | 6.497 us | 0.0854 us | 0.0713 us | 1.00 |
| GetSet | netcoreapp3.0 | 2.049 us | 0.0233 us | 0.0218 us | 0.32 |

  while other operations like 

`Or`, `And`, and `Xor` were vectorized in PR [dotnet/corefx#33781](https://github.com/dotnet/corefx/pull/33781). This benchmark highlights some of the wins.

```
private BitArray _ba1 = new BitArray(Enumerable.Range(0, 1000).Select(i => i % 2 == 0).ToArray());
private BitArray _ba2 = new BitArray(Enumerable.Range(0, 1000).Select(i => i % 2 == 1).ToArray());

[Benchmark]
public void Xor() => _ba1.Xor(_ba2);
```

| Method | Toolchain | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- |
| Xor | netcoreapp2.1 | 28.57 ns | 0.4086 ns | 0.3822 ns | 1.00 |
| Xor | netcoreapp3.0 | 10.92 ns | 0.0924 ns | 0.0772 ns | 0.38 |

  Another example: 

`SortedSet<T>`. PR [dotnet/corefx#30921](https://github.com/dotnet/corefx/pull/30921) from @acerbusace tweaks how `GetViewBetween` changes how counts of the overall set and subset are managed, resulting in a nice performance boost.

```
private SortedSet<int> _set = new SortedSet<int>(Enumerable.Range(0, 1000));

[Benchmark]
public int EnumerateViewBetween()
{
    int count = 0;
    foreach (int item in _set.GetViewBetween(100, 200)) count++;
    return count;
}
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| EnumerateViewBetween | netcoreapp2.1 | 5.117 us | 0.0590 us | 0.0552 us | 1.00 | 0.2518 | – | – | 544 B |
| EnumerateViewBetween | netcoreapp3.0 | 2.510 us | 0.0307 us | 0.0287 us | 0.49 | 0.1373 | – | – | 288 B |

  Comparers have also seen some nice improvements in .NET Core 3.0. For example, PR 

[dotnet/coreclr#21604](https://github.com/dotnet/coreclr/pull/21604) overhauled how comparers for enums are implemented in the runtime, borrowing the approach used in CoreRT. It’s often the case that performance optimizations involve adding code; this is one of those fortuitous cases where the better approach is not only faster, it’s also simpler and smaller.

```
private enum ExampleEnum : byte { A, B }

[Benchmark]
public void CompareEnums()
{
    var comparer = Comparer<ExampleEnum>.Default;
    for (int i = 0; i < 100_000_000; i++)
    {
        comparer.Compare(ExampleEnum.A, ExampleEnum.B);
    }
}
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | RatioSD |
| --- | --- | --- | --- | --- | --- | --- |
| CompareEnums | netcoreapp2.1 | 239.5 ms | 10.130 ms | 10.403 ms | 1.00 | 0.00 |
| CompareEnums | netcoreapp3.0 | 131.7 ms | 2.479 ms | 2.319 ms | 0.55 | 0.03 |

### Networking

From the Kestrel web server running on `System.Net.Sockets` and `System.Net.Security` to applications accessing web services via `HttpClient`, `System.Net` now more than ever is critical path for many applications. It received a lot of attention in .NET Core 2.1, and continues to in .NET Core 3.0. Let’s start with `HttpClient`. One improvement made in PR [dotnet/corefx#32820](https://github.com/dotnet/corefx/pull/32820) was around how buffering is handled, and in particular better respecting larger buffer size requests made as part of copying the response data when a content length was provided by the server. On a fast connection and with a large response body (such as the 10MB in this example), this can make a sizeable difference in throughput due to reduced syscalls to transfer data.

```
private HttpClient _client = new HttpClient();
private Socket _listener;
private Uri _uri;

[GlobalSetup]
public void Setup()
{
    _listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
    _listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
    _listener.Listen(int.MaxValue);
    var ep = (IPEndPoint)_listener.LocalEndPoint;
    _uri = new Uri($"http://{ep.Address}:{ep.Port}");

    Task.Run(async () =>
    {
        while (true)
        {
            Socket s = await _listener.AcceptAsync();
            var ignored = Task.Run(async () =>
            {
                ReadOnlyMemory<byte> headers = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 10485760\r\n\r\n");
                ReadOnlyMemory<byte> data = new byte[10*1024*1024]; // 10485760

                using (var serverStream = new NetworkStream(s, true))
                using (var reader = new StreamReader(serverStream))
                {
                    while (true)
                    {
                        while (!string.IsNullOrEmpty(await reader.ReadLineAsync())) ;
                        await s.SendAsync(headers, SocketFlags.None);
                        await s.SendAsync(data, SocketFlags.None);
                    }
                }
            });
        }
    });
}

[Benchmark]
public async Task HttpDownload()
{
    using (HttpResponseMessage r = await _client.GetAsync(_uri, HttpCompletionOption.ResponseHeadersRead))
    using (Stream s = await r.Content.ReadAsStreamAsync())
    {
        await s.CopyToAsync(Stream.Null);
    }
}
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | RatioSD |
| --- | --- | --- | --- | --- | --- | --- |
| HttpDownload | netcoreapp2.1 | 8.792 ms | 0.1833 ms | 0.3397 ms | 1.00 | 0.00 |
| HttpDownload | netcoreapp3.0 | 4.615 ms | 0.0356 ms | 0.0278 ms | 0.52 | 0.02 |

  Now consider 

`SslStream`. Previous releases saw work done to make reads and writes on `SslStream` much more efficient, but additional work was done in .NET Core 3.0 as part of PRs [dotnet/corefx#35091](https://github.com/dotnet/corefx/pull/35091) and [dotnet/corefx#35209](https://github.com/dotnet/corefx/pull/35209) (and [dotnet/corefx#35367](https://github.com/dotnet/corefx/pull/35367) on Unix) to make initiating the connection more efficient, in particular in terms of allocations.

```
private NetworkStream _client;
private NetworkStream _server;

private static X509Certificate2 s_cert = GetServerCertificate();

private static X509Certificate2 GetServerCertificate()
{
    var certCollection = new X509Certificate2Collection();
    byte[] testCertBytes = Convert.FromBase64String(@"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");
    certCollection.Import(testCertBytes, "testcertificate", X509KeyStorageFlags.DefaultKeySet);
    return certCollection.Cast<X509Certificate2>().First(c => c.HasPrivateKey);
}

[GlobalSetup]
public void Setup()
{
    using (var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
    {
        listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        listener.Listen(1);

        var client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        client.Connect(listener.LocalEndPoint);
        Socket server = listener.Accept();

        _client = new NetworkStream(client);
        _server = new NetworkStream(server);
    }
}

[Benchmark]
public void SslConnect()
{
    using (var sslClient = new SslStream(_client, true, delegate { return true; }))
    using (var sslServer = new SslStream(_server, true, delegate { return true; }))
    {
        Task t = sslServer.AuthenticateAsServerAsync(s_cert, false, SslProtocols.None, false);
        sslClient.AuthenticateAsClient("localhost", null, SslProtocols.None, false);
        t.Wait();
    }
}
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| SslConnect | netcoreapp2.1 | 1,151.7 us | 34.85 us | 102.76 us | 1.00 | 0.00 | 5.8594 | – | – | 9.82 KB |
| SslConnect | netcoreapp3.0 | 915.5 us | 17.73 us | 26.54 us | 0.80 | 0.08 | 1.9531 | – | – | 4.13 KB |

  In 

`System.Net.Sockets` there’s another example of taking advantage of the `IThreadPoolWorkItem` interface discussed earlier. On Windows for asynchronous operations, we utilize “overlapped I/O”, utilizing threads from the I/O thread pool to execute continuations from socket operations; Windows queues I/O completion packets that these I/O pool threads then process, including invoking the continuations. On Unix, however, the mechanism is very different. There’s no concept of “overlapped I/O” on Unix, and instead asynchrony in `System.Net.Sockets` is achieved by using `epoll` (or `kqueues` on macOS), with all of the sockets in the system registered with an `epoll` file descriptor, and then one thread monitoring that `epoll` for changes. Any time an asynchronous operation completes for a socket, the `epoll` is signaled and the thread blocking on it wakes up to process it. If that thread were to run the socket continuation action then and there, it would end up potentially running unbounded work that could stall every other socket’s handling indefinitely, and in the extreme case, deadlock. Instead, this thread queues a work item back to the thread pool and then immediately goes back to processing any other socket work. Prior to .NET Core 3.0, that queueing involved an allocation, which meant that every asynchronously completing socket operation on Unix involved at least one allocation. As of PR [dotnet/corefx#32919](https://github.com/dotnet/corefx/pull/32919), that number drops to zero, as a cached object already being used (and reused) to represent asynchronous operations was changed to also implement `IThreadPoolWorkItem` and be queueable directly to the thread pool. Other areas of `System.Net` have benefited from the efforts already alluded to previously, as well. For example, `Dns.GetHostName` used to use `StringBuilder` in its marshaling, but as of PR [dotnet/corefx#29594](https://github.com/dotnet/corefx/pull/29594) it no longer does.

```
[Benchmark]
public string GetHostName() => Dns.GetHostName();
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| GetHostName | netcoreapp2.1 | 85.77 us | 1.656 us | 1.5489 us | 1.00 | 0.00 | 0.4883 | – | – | 1176 B |
| GetHostName | netcoreapp3.0 | 81.42 us | 1.016 us | 0.9503 us | 0.95 | 0.02 | – | – | – | 48 B |

  And 

`IPAddress.HostToNetworkOrder/NetworkToHostOrder` have benefiting indirectly from the intrinsics push that was mentioned previously. In .NET Core 2.1, `BinaryPrimitives.ReverseEndianness` was added with an optimized software implementation, and these `IPAddress` methods were rewritten as simple wrappers for `ReverseEndianness`. Now in .NET Core 3.0, PR [dotnet/coreclr#18398](https://github.com/dotnet/coreclr/pull/18398) turned `ReverseEndianness` into a JIT intrinsic for which the JIT can emit a very efficient `BSWAP` instruction, with the resulting throughput improvements accruing to `IPAddress` as well.

```
private long _value = 1234567890123456789;

[Benchmark]
public long HostToNetworkOrder() => IPAddress.HostToNetworkOrder(_value);
```

| Method | Toolchain | Mean | Error | StdDev | Median | Ratio | RatioSD |
| --- | --- | --- | --- | --- | --- | --- | --- |
| HostToNetworkOrder | netcoreapp2.1 | 0.4986 ns | 0.0398 ns | 0.0408 ns | 0.4758 ns | 1.000 | 0.00 |
| HostToNetworkOrder | netcoreapp3.0 | 0.0043 ns | 0.0090 ns | 0.0076 ns | 0.0000 ns | 0.009 | 0.02 |

### System.IO

Often going hand in hand with networking is compression, which has also seen some improvements in .NET Core 3.0. Most notably is that a key dependency was updated. On Unix, `System.IO.Compression` just uses the zlib library available on the machine, as it’s a standard part of most any distro/version. On Windows, however, zlib is generally nowhere to be found, and so it’s built and shipped as part of .NET Core on Windows. Rather than shipping the standard zlib, .NET Core includes a version modified by Intel with additional performance improvements not yet merged upstream. In .NET Core 3.0, we’ve sync’d to the latest available version of ZLib-Intel, version 1.2.11. This brings some very measurable performance improvements, in particular around decompression. There have also been compression-related improvements that take advantage of previous improvements elsewhere in .NET Core. For example, the synchronous `Stream.CopyTo` was originally non-virtual, but as gains were found by overriding the asynchronous `CopyToAsync` and specializing its implementation for particular concrete stream types, `CopyTo` was made virtual to enjoy similar improvements. PR [dotnet/corefx#29751](https://github.com/dotnet/corefx/pull/29751) capitalized on this to override `CopyTo` on `DeflateStream`, employing similar optimizations in the synchronous implementation as were employed in the asynchronous implementation, essentially entailing minimizing the interop costs with zlib.

```
private byte[] _compressed;

[GlobalSetup]
public void Setup()
{
    var ms = new MemoryStream();
    using (var ds = new DeflateStream(ms, CompressionLevel.Fastest))
    {
        ds.Write(Enumerable.Range(0, 1_000_000).Select(i => (byte)i).ToArray(), 0, 1_000_000);
    }
    _compressed = ms.ToArray();
}

[Benchmark]
public void DeflateDecompress()
{
    using (var ds = new DeflateStream(new MemoryStream(_compressed), CompressionMode.Decompress))
    {
        ds.CopyTo(Stream.Null);
    }
}
```

| Method | Toolchain | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- |
| DeflateDecompress | netcoreapp2.1 | 310.6 us | 1.960 us | 1.6367 us | 1.00 |
| DeflateDecompress | netcoreapp3.0 | 144.9 us | 1.050 us | 0.9819 us | 0.47 |

  Improvements were also made to 

`BrotliStream` (which as of .NET Core 3.0 is also used by `HttpClient` to automatically decompress Brotli-encoded content). Previously every new `BrotliStream` would also allocate a large buffer, but as of PR [dotnet/corefx#35492](https://github.com/dotnet/corefx/pull/35492), that buffer is pooled, as it is with `DeflateStream` (additionally, `BrotliStream` now as of PR [dotnet/corefx#30135](https://github.com/dotnet/corefx/pull/30135) overrides `ReadByte` and `WriteByte` to avoid allocations in the base implementation).

```
[Benchmark]
public void BrotliWrite()
{
    using (var bs = new BrotliStream(Stream.Null, CompressionLevel.Fastest))
    {
        for (int i = 0; i < 1_000; i++)
        {
            bs.WriteByte((byte)i);
        }
    }
}
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| BrotliWrite | netcoreapp2.1 | 743.2 us | 10.056 us | 9.406 us | 1.00 | 44.9219 | – | – | 97680 B |
| BrotliWrite | netcoreapp3.0 | 575.5 us | 9.181 us | 8.588 us | 0.77 | – | – | – | 136 B |

  Moving on from compression, it’s worth highlighting that formatting applies in more situations than just formatting individual primitives. 

`TextWriter`, for example, has multiple methods for writing with format strings, e.g. `public override void Write(string format, object arg0, arg1)`. PR [dotnet/coreclr#19235](https://github.com/dotnet/coreclr/pull/19235) improved on that for StreamWriter by providing specialized overrides that take a more efficient path that reduces allocation:

```
private StreamWriter _writer = new StreamWriter(Stream.Null);

[Benchmark]
public void StreamWriterFormat() => _writer.Write("Writing out a value: {0}", 42);
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| StreamWriterFormat | netcoreapp2.1 | 207.4 ns | 2.103 ns | 1.864 ns | 1.00 | 0.0455 | – | – | 96 B |
| StreamWriterFormat | netcoreapp3.0 | 170.2 ns | 1.800 ns | 1.595 ns | 0.82 | 0.0114 | – | – | 24 B |

  As another example, PR 

[dotnet/coreclr#22102](https://github.com/dotnet/coreclr/pull/22102) from @TomerWeisberg improved the parsing performance of various primitive types on `BinaryReader` by special-casing the common situation where the `BinaryReader` wraps a `MemoryStream`. Or consider PR [dotnet/corefx#30667](https://github.com/dotnet/corefx/pull/30667) from @MarcoRossignoli, who added overrides to `StringWriter` for the `Write{Line}{Async}` methods that take a `StringBuilder` argument. `StringWriter` is just a wrapper around a `StringBuilder`, and `StringBuilder` knows how to append another `StringBuilder` to it, so these overrides on `StringWriter` can feed them right through.

```
private StringBuilder _underlying;
private StringWriter _writer;
private StringBuilder _sb;

[GlobalSetup]
public void Setup()
{
    _underlying = new StringBuilder();
    _writer = new StringWriter(_underlying);
    _sb = new StringBuilder("This is a test. This is only a test.");
}

[Benchmark]
public void Write()
{
    _underlying.Clear();
    _writer.Write(_sb);
}
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Write | netcoreapp2.1 | 30.15 ns | 0.6065 ns | 0.5673 ns | 1.00 | 0.0495 | – | – | 104 B |
| Write | netcoreapp3.0 | 18.57 ns | 0.1513 ns | 0.1416 ns | 0.62 | – | – | – | – |

`System.IO.Pipelines` is another IO-related library that’s received a lot of attention in .NET Core 3.0. Pipelines was introduced in .NET Core 2.1, and provides buffer-management as part of an I/O pipeline, used heavily by ASP.NET Core. A variety of PRs have gone into improving its performance. For example, PR [dotnet/corefx#35171](https://github.com/dotnet/corefx/pull/35171)special-cases the common and default case where the `Pool` specified to be used by a `Pipe` is the default `MemoryPool<byte>.Shared`. Rather than go through `MemoryPool<byte>.Shared` in this case, the `Pipe` now bypasses it and goes to the underlying `ArrayPool<byte>.Shared` directly, which removes a layer of indirection but also the allocation of `IMemoryOwner<byte>` objects returned from `MemoryPool<byte>.Rent`. (Note that for this benchmark, since `System.IO.Pipelines` is part of a NuGet package rather than in the shared framework, I’ve added a Benchmark.NET config that specifies what package version to use with each run in order to show the improvements.)

```
// Run with: dotnet run -c Release -f netcoreapp2.1 --filter *Program*

private sealed class Config : ManualConfig // also add [Config(typeof(Config))] to the Program class
{
    public Config()
    {
        Add(Job.MediumRun.With(CsProjCoreToolchain.NetCoreApp21).WithNuGet("System.IO.Pipelines", "4.5.0").WithId("4.5.0"));
        Add(Job.MediumRun.With(CsProjCoreToolchain.NetCoreApp30).WithNuGet("System.IO.Pipelines", "4.6.0-preview5.19224.8").WithId("4.6.0-preview5.19224.8"));
    }
}

private readonly Pipe _pipe = new Pipe();
private byte[] _buffer = new byte[1024];

[Benchmark]
public async Task ReadWrite()
{
    var reader = _pipe.Reader;
    var writer = _pipe.Writer;

    for (int i = 0; i < 1000; i++)
    {
        ValueTask<ReadResult> vt = reader.ReadAsync();
        await writer.WriteAsync(_buffer);
        ReadResult rr = await vt;
        reader.AdvanceTo(rr.Buffer.End);
    }
}
```

| Method | Job | NuGetReferences | Toolchain | Mean | Error | StdDev | Gen 0 | Gen 1 | Gen 2 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ReadWrite | 4.5.0 | System.IO.Pipelines 4.5.0 | .NET Core 2.1 | 406.8 us | 12.774 us | 17.907 us | 11.2305 | – | – |
| ReadWrite | 4.6.0-preview5.19224.8 | System.IO.Pipelines 4.6.0-preview5.19224.8 | .NET Core 3.0 | 324.6 us | 3.208 us | 4.702 us | – | – | – |

  PR 

[dotnet/corefx#33658](https://github.com/dotnet/corefx/pull/33658) from @benaadams allows `Pipe` to use the `UnsafeQueueUserWorkItem`boxing-related optimizations described earlier, PR [dotnet/corefx#33755](https://github.com/dotnet/corefx/pull/33755) avoids queueing unnecessary work items, PR [dotnet/corefx#35939](https://github.com/dotnet/corefx/pull/35939) tweaks the defaults used to better handle buffering in common cases, PR [dotnet/corefx#35216](https://github.com/dotnet/corefx/pull/35216) reduces the amount of slicing performed in various pipe operations, PR [dotnet/corefx#35234](https://github.com/dotnet/corefx/pull/35234) from @benaadams reduces the locking used in core operations, PR [dotnet/corefx#35509](https://github.com/dotnet/corefx/pull/35509) reduces argument validation (decreasing branching costs), PR [dotnet/corefx#33000](https://github.com/dotnet/corefx/pull/33000) focused on reducing costs associated with `ReadOnlySequence<byte>` that’s the main exchange type pipelines passes around, and PR [dotnet/corefx#29837](https://github.com/dotnet/corefx/pull/29837) further optimizes operations like `GetSpan` and `Advance` on the `Pipe`. The net result is to whittle away at already low CPU and allocation overheads.

```
// Run with: dotnet run -c Release -f netcoreapp2.1 --filter *Program*

private sealed class Config : ManualConfig // also add [Config(typeof(Config))] to the Program class
{
    public Config()
    {
        Add(Job.MediumRun.With(CsProjCoreToolchain.NetCoreApp21).WithNuGet("System.IO.Pipelines", "4.5.0").WithId("4.5.0"));
        Add(Job.MediumRun.With(CsProjCoreToolchain.NetCoreApp30).WithNuGet("System.IO.Pipelines", "4.6.0-preview5.19224.8").WithId("4.6.0-preview5.19224.8"));
    }
}

private readonly Pipe _pipe1 = new Pipe();
private readonly Pipe _pipe2 = new Pipe();
private byte[] _buffer = new byte[1024];

[GlobalSetup]
public void Setup()
{
    Task.Run(async () =>
    {
        var reader = _pipe2.Reader;
        var writer = _pipe1.Writer;
        while (true)
        {
            ReadResult rr = await reader.ReadAsync();
            foreach (ReadOnlyMemory<byte> mem in rr.Buffer)
            {
                await writer.WriteAsync(mem);
            }
            reader.AdvanceTo(rr.Buffer.End);
        }
    });
}

[Benchmark]
public async Task ReadWrite()
{
    var reader = _pipe1.Reader;
    var writer = _pipe2.Writer;

    for (int i = 0; i < 1000; i++)
    {
        await writer.WriteAsync(_buffer);
        long count = 0;
        while (count < _buffer.Length)
        {
            ReadResult rr = await reader.ReadAsync();
            count += rr.Buffer.Length;
            reader.AdvanceTo(rr.Buffer.End);
        }
    }
}
```

| Method | Job | NuGetReferences | Toolchain | Mean | Error | StdDev | Gen 0 | Gen 1 | Gen 2 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ReadWrite | 4.5.0 | System.IO.Pipelines 4.5.0 | .NET Core 2.1 | 3.261 ms | 0.0732 ms | 0.1002 ms | 46.8750 | – | – |
| ReadWrite | 4.6.0-preview5.19224.8 | System.IO.Pipelines 4.6.0-preview5.19224.8 | .NET Core 3.0 | 2.947 ms | 0.1281 ms | 0.1837 ms | – | – | – |

### System.Console

`Console` isn’t something one normally thinks of as being performance-sensitive. However, there are two changes in this release that I think are worth calling attention to here. First, there is one area of Console about which we’ve heard numerous concerns related to performance, where the performance impact visibly impacts users. In particular, interactive console applications generally do a lot of manipulation of the cursor, which also entails asking where the cursor currently is. On Windows, both the setting and getting of the cursor are relatively fast operations, with P/Invoke calls made to functions exported from kernel32.dll. On Unix, things are more complicated. There’s no standard POSIX function for getting or setting a terminal’s cursor position. Instead, there’s a standard convention for interacting with the terminal via ANSI escape sequences. To set the cursor position, one writes a sequence of characters to stdout (e.g. “ESC \[ 12 ; 34 H” to indicate 12th row, 34th column) and the terminal interprets that and reacts accordingly. Getting the cursor position is more of an ordeal. To get the current cursor position, an application writes to stdout a request (e.g. “ESC \[ 6 n”), and in response the terminal writes back to the application’s stdin a response something like “ESC \[ 12 ; 34 R”, to indicate the cursor is at the 12th row and 34th column. That response then needs to be read from stdin and parsed. So, in contrast to a fast interop call on Windows, on Unix we need to write, read, and parse text, and do so in a way that doesn’t cause problems with a user sitting at a keyboard using the app concurrently… not particularly cheap. When just getting the cursor position now and then, it’s not a big deal. But when getting it frequently, and when porting code originally written for Windows where the operation was so cheap the code being ported may not have been very frugal with how often it asked for the position (asking for it more than is really needed), this has resulted in visible performance problems. Thankfully, the issue has been addressed in .NET Core 3.0, by PR [dotnet/corefx#36049](https://github.com/dotnet/corefx/pull/36049) from @tmds. The change caches the current position and then manually handles updating that cached value based on user interactions, such as handling typing or resizing the terminal window. (Note that Benchmark.NET operates in a way that redirects standard input and output for the process running the test, and that makes Console.CursorLeft/Top return 0 immediately, so for this test, I’ve just done a simple console app with a `Stopwatch`, which is, as you’ll see, more than sufficient given the discrepancy between costs in versions.)

```
using System;
using System.Diagnostics;

public class Program
{
    static void Main()
    {
        var sw = new Stopwatch();
        for (int iter = 0; iter < 5; iter++)
        {
            sw.Restart();
            for (int i = 0; i < 1_000; i++) { _ = Console.CursorLeft; }
            sw.Stop();
            Console.WriteLine(sw.Elapsed.TotalSeconds);
        }
    }
}
```

```
~/BlogPostBenchmarks$ dotnet run -c Release -f netcoreapp2.1
18.2152636
17.9935087
18.2676408
17.7891821
17.4141348
~/BlogPostBenchmarks$ dotnet run -c Release -f netcoreapp3.0
0.0648111
0.0001539
0.00013979999999999998
0.00013529999999999998
0.0001459
```

Another place where `Console` has been improved affects both Windows and Unix. Interestingly, this change was made for functional reasons (in particular for when running on Windows), but it has performance benefits as well for all OSes. In .NET, most of the times we specify buffer sizes it’s for performance reasons and represents a trade-off: the smaller the buffer size, the less memory is used but the more times operations may need to be performed to fill that buffer, and conversely the larger the buffer size, the more memory is used but the fewer times the buffer will need to be filled. It’s rare that the buffer size has a functional impact, but it actually can in `Console`. On Windows to read from the console, one calls either the `ReadFile` or `ReadConsole` functions, both of which accept a buffer to store the read data into. By default on Windows, reading from the console will not return until a newline, but Windows also needs somewhere to store the typed data, and it does so into the supplied buffer. Thus, Windows won’t let the user type more characters than can fit into the buffer, which means the line length a user can type is limited by the buffer size. For whatever historical reason, .NET has used a buffer size of 256 characters, limiting the typeable line length to that amount. PR [dotnet/corefx#36212](https://github.com/dotnet/corefx/pull/36212) expands that to 4096 characters, which much better matches other programming environments and allows for a much more reasonable line length. However, as is the case when increasing buffer sizes, relevant throughput involving that buffer improves as well, in particular when reading from files piped to stdin. For example, reading 8K of input data from stdin previously would have required 32 calls to `ReadFile`; with a 4K buffer, only 2 calls are required. The impact of that can be seen in this benchmark. (Again, this is harder to test with Benchmark.NET, so I’ve again just used a simple console app.)

```
using System;
using System.Diagnostics;
using System.IO;

public class Program
{
    static void Main()
    {
        //using (var writer = new StreamWriter(@"tmp.dat"))
        //{
        //    for (int i = 0; i < 10_000_000; i++)
        //    {
        //        writer.WriteLine("This is a test.  This is only a test.");
        //    }
        //}

        var sw = Stopwatch.StartNew();
        while (Console.ReadLine() != null) ;
        Console.WriteLine(sw.Elapsed.TotalSeconds);
    }
}
```

```
c:\BlogPostBenchmarks>dotnet run -c Release -f netcoreapp2.1 < c:\BlogPostBenchmarks\bin\Release\netcoreapp2.1\tmp.dat
4.8151814

c:\BlogPostBenchmarks>dotnet run -c Release -f netcoreapp3.0 < c:\BlogPostBenchmarks\bin\Release\netcoreapp2.1\tmp.dat
1.3161175999999999
```

### System.Diagnostics.Process

There have been various functional improvements to the `Process` class in .NET Core 3.0, in particular on Unix, but there are a couple of performance-focused improvements I want to call out. PR [dotnet/corefx#31236](https://github.com/dotnet/corefx/pull/31236) is another nice example of introducing a new performance-focused API and, at the same time, using it within .NET Core to further improve the performance of core libraries. In this case, it’s a low-level API on MemoryMarshal that enables efficiently reading structs from spans, something that’s done in spades as part of the interop in `System.Diagnostics.Process`. I like that example, not because it makes for a massive performance improvement, but because it highlights the general pattern I like to see: adding new APIs for others to consume and in the same breath using those APIs to better the technology itself. A more impactful example, though, comes from @joshudson in PR [dotnet/corefx#33289](https://github.com/dotnet/corefx/pull/33289), which changed the native code used to fork a new process from using the `fork` function to instead using the `vfork` function. The benefit of `vfork` is that it avoids copying the page tables of the parent process into the child process, with the assumption that the child process is then just going to overwrite everything anyway via an almost immediate `exec` call. `fork` does copy-on-write, but if the process is modifying a lot of state concurrently (e.g. with the garbage collector running), this can get expensive quickly and unnecessarily. For this benchmark, I’ve just written a nop C program in a test.c file:

and compiled it with GCC:

```
gcc -o test test.c
```

to give us a target for Process.Start to invoke.

```
[Benchmark]
public void ProcessStartWait() => Process.Start("/home/stephentoub/BlogPostBenchmarks/test").WaitForExit();
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| ProcessStartWait | netcoreapp2.1 | 1,663.0 us | 32.79 us | 67.72 us | 1.00 | 0.00 | – | – | – | 21.45 KB |
| ProcessStartWait | netcoreapp3.0 | 536.0 us | 10.64 us | 28.40 us | 0.32 | 0.02 | 1.9531 | – | – | 16.65 KB |

### LINQ

Previous releases have seen a ton of investment in optimizing LINQ. There’s less of that in .NET Core 3.0, as a lot of the common patterns have already been covered well. However, there are still some nice improvements to be found in the release. It’s relatively rare that new operators are added to `System.Linq` itself, as the very nature of extension methods makes it easy for anyone to build up and share their own library of extension methods they consider to be useful (and several well-established such libraries exist). Even so, .NET Core 2.0 saw a new `TakeLast` method added. In .NET Core 3.0, PR [dotnet/corefx#36051](https://github.com/dotnet/corefx/pull/36051) by @Romasz updated `TakeLast` to integrate with the internal `IPartition<T>` interface that enables several operators to cooperate, helping to optimize (in some situations quite heavily) various uses of the operator.

```
private IEnumerable<int> _enumerable = new int[1000].Select(i => i);

[Benchmark]
public int SumLast10() => _enumerable.TakeLast(10).Sum();
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| SumLast10 | netcoreapp2.1 | 11,935.5 ns | 102.793 ns | 85.837 ns | 1.00 | 0.1526 | – | – | 344 B |
| SumLast10 | netcoreapp3.0 | 141.4 ns | 1.310 ns | 1.225 ns | 0.01 | 0.0267 | – | – | 56 B |

  Just recently, PR 

[dotnet/corefx#37410](https://github.com/dotnet/corefx/pull/37410) optimized the relatively common pattern of using `Enumerable.Range(...).Select(…)`, teaching `Select` about the object generated by `Range` and allowing for the enumeration performed by `Select` to skip going through `IEnumerable<T>` and instead just loop through the intended numerical range directly.

```
[Benchmark]
public int[] RangeSelectToArray() => Enumerable.Range(0, 100).Select(i => i * 2).ToArray();
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| RangeSelectToArray | netcoreapp2.1 | 953.9 ns | 20.232 ns | 28.363 ns | 1.00 | 0.00 | 0.2460 | – | – | 520 B |
| RangeSelectToArray | netcoreapp3.0 | 358.0 ns | 7.650 ns | 7.156 ns | 0.37 | 0.02 | 0.2441 | – | – | 512 B |

`Enumerable.Empty<T>()` was also changed in PR [dotnet/corefx#31025](https://github.com/dotnet/corefx/pull/31025) to better compose with optimizations already elsewhere in .NET Core’s System.Linq implementation. While no one should be writing code that explicitly calls additional LINQ operators directly on the result of `Enumerable.Empty<T>()`, it is common to return the result of `Empty<T>()` as one possible return value from an `IEnumerable<T>`\-returning method, and then for the caller to tack on additional operators, such that this optimization does actually have a meaningful effect.

```
[Benchmark]
public int[] EmptyTakeSelectToArray() => Enumerable.Empty<int>().Take(10).Select(i => i).ToArray();
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| EmptyTakeSelectToArray | netcoreapp2.1 | 71.80 ns | 1.4205 ns | 1.1861 ns | 1.00 | 0.0495 | – | – | 104 B |
| EmptyTakeSelectToArray | netcoreapp3.0 | 30.09 ns | 0.1550 ns | 0.1295 ns | 0.42 | – | – | – | – |

  Across .NET Core, we’re also paying more attention to assembly size, in particular as it can impact ahead-of-time (AOT) compilation. PRs like 

[dotnet/corefx#35213](https://github.com/dotnet/corefx/pull/35213), which employs “ThrowHelpers” in the heavily-generic LINQ, help to reduce generated code size, which has benefits in and of itself but can also help with other areas of performance.

### Interop

Interop is another one of those areas that’s critically important both to customers of .NET as well as to .NET itself, as a lot of functionality in .NET is layered on top of underlying operating system functionality that requires interop to access. As such, performance improvements in interop itself end up impacting a wide array of components. One notable improvement is in `SafeHandle`, and it’s another example of where moving code from native to managed helped improve performance. SafeHandle is the recommended way for managing the lifetime of native resources, whether represented by handles on Windows or by file descriptors on Unix, and it’s used in exactly that way internally in all of our managed libraries in coreclr and corefx. One of the reasons it’s the recommended solution is that it uses appropriate synchronization to ensure that these native resources aren’t closed from managed code while they’re still being used, and that means that the interop layer needs to track every time a P/Invoke call is made with a SafeHandle, invoking DangerousAddRef prior to the call, DangerousRelease after the call, and DangerousGetHandle to extract the actual pointer value to pass to the native function. In previous releases of .NET, the core pieces of those implementations were in the runtime, which meant managed code needed to make `InternalCall`s to native code in the runtime for each of those operations. In .NET Core 3.0 as of PR [dotnet/coreclr#22564](https://github.com/dotnet/coreclr/pull/22564), those operations have been ported to managed code, removing the overhead associated with each of those transitions.

```
private SafeFileHandle _sfh = new SafeFileHandle((IntPtr)12345, ownsHandle: false);

[Benchmark]
public IntPtr SafeHandleOps()
{
    bool success = false;
    try
    {
        _sfh.DangerousAddRef(ref success);
        return _sfh.DangerousGetHandle();
    }
    finally
    {
        if (success)
        {
            _sfh.DangerousRelease();
        }
    }
}
```

| Method | Toolchain | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- |
| SafeHandleOps | netcoreapp2.1 | 36.72 ns | 0.7285 ns | 0.6458 ns | 1.00 |
| SafeHandleOps | netcoreapp3.0 | 16.04 ns | 0.1322 ns | 0.1104 ns | 0.44 |

  There are also examples for improvements to marshaling. Earlier in this post, I highlighted a variety of cases where 

`StringBuilder` was used as part of marshaling and interop. For the record, I personally dislike `StringBuilder` being used in interop, as it adds cost and complexity for relatively little benefit, and as a result did work in PRs like [dotnet/corefx#33780](https://github.com/dotnet/corefx/pull/33780) and [dotnet/coreclr#21120](https://github.com/dotnet/coreclr/pull/21120) to remove almost all use of `StringBuilder` marshaling in coreclr and corefx. However, there is still a lot of code built around `StringBuilder`, and it deserves to be as fast as possible. PR [dotnet/coreclr#17928](https://github.com/dotnet/coreclr/pull/17928) avoids a bunch of unnecessary work and allocation that happens as part of `StringBuilder` marshaling, and leads to improvements like this:

```
private const int MAX_PATH = 260;
private StringBuilder _sb = new StringBuilder(MAX_PATH);

[DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
private static extern uint GetTempPathW(int bufferLen, [Out]StringBuilder buffer);

[Benchmark]
public void StringBuilderMarshal() => GetTempPathW(MAX_PATH, _sb);
```

| Method | Toolchain | Mean | Error | StdDev | Ratio | RatioSD | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| StringBuilderMarshal | netcoreapp2.1 | 359.4 ns | 7.643 ns | 13.386 ns | 1.00 | 0.00 | 0.2584 | – | – | 544 B |
| StringBuilderMarshal | netcoreapp3.0 | 289.1 ns | 5.773 ns | 7.707 ns | 0.80 | 0.04 | – | – | – | – |

  And of course, specific uses of interop and marshaling have also improved. For example, 

`FileSystemWatcher`‘s interop on macOS had been using `MarshalAs` attributes, which forced the runtime to do additional marshaling work on every OS callback, including allocating arrays. PR [dotnet/corefx#34715](https://github.com/dotnet/corefx/pull/34715) moved `FileSystemWatcher`‘s interop to use a more efficient scheme that doesn’t entail additional allocations nor marshaling directives. Or consider [dotnet/corefx#30099](https://github.com/dotnet/corefx/pull/30099), where `System.Drawing` was switched to using a much more efficient scheme of marshaling and interop, with a managed array being pinned and passed directly to native code instead of allocating additional memory and copying to it.

```
// Run with: dotnet run -c Release -f netcoreapp2.1 --filter *Program*

private sealed class Config : ManualConfig // also add [Config(typeof(Config))] to the Program class
{
    public Config()
    {
        Add(Job.MediumRun.With(CsProjCoreToolchain.NetCoreApp21).WithNuGet("System.Drawing.Common", "4.5.1").WithId("4.5.1"));
        Add(Job.MediumRun.With(CsProjCoreToolchain.NetCoreApp30).WithNuGet("System.Drawing.Common", "4.6.0-preview5.19224.8").WithId("4.6.0-preview5.19224.8"));
    }
}

private Bitmap _image;
private Graphics _graphics;
private Point[] _points;

[GlobalSetup]
public void Setup()
{
    _image = new Bitmap(100, 100);
    _graphics = Graphics.FromImage(_image);
    _points = new[]
    {
        new Point(10, 10), new Point(20, 1), new Point(35, 5), new Point(50, 10),
        new Point(60, 15), new Point(65, 25), new Point(50, 30)
    };
}

[Benchmark]
public void TransformPoints()
{
    _graphics.TransformPoints(CoordinateSpace.World, CoordinateSpace.Page, _points);
    _graphics.TransformPoints(CoordinateSpace.Device, CoordinateSpace.World, _points);
    _graphics.TransformPoints(CoordinateSpace.Page, CoordinateSpace.Device, _points);
}
```

| Method | Job | NuGetReferences | Toolchain | Mean | Error | StdDev | Gen 0 | Gen 1 | Gen 2 | Allocated |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| TransformPoints | 4.5.1 | System.Drawing.Common 4.5.1 | .NET Core 2.1 | 11,010.3 ns | 490.050 ns | 718.309 ns | 0.5798 | – | – | 1248 B |
| TransformPoints | 4.6.0-preview5.19224.8 | System.Drawing.Common 4.6.0-preview5.19224.8 | .NET Core 3.0 | 364.0 ns | 6.704 ns | 9.827 ns | – | – | – | – |

### Peanut butter

In previous sections of this post, I highlighted groups of PRs that addressed various areas of .NET in an impactful way, where some piece of mainstream functionality was significantly improved. But those aren’t the only areas or kinds of PRs that matter. In .NET we also have what we sometimes refer to as “peanut butter”. We have a ton of code that’s generally great for most applications but that has a myriad of small opportunities for improvements. Those improvements alone don’t make anything better, but they fix a smearing of performance penalties across a large swath of code, and the more of such issues we can fix, the better performance becomes overall. An allocation removed here, some unnecessary cycles eliminated there, some unnecessary code removed there. Here are just a sampling of PRs that went in to address such “peanut butter”:

-   **Lower bounds explicitly provided to `Array.Copy`.** Calling `Array.Copy(src, dst, length)` requires the runtime to call `GetLowerBound` on each of the src and the dst arrays. When working with `T[]`s, the lower bound is 0, and we can just explicitly pass in 0 for both bounds and avoid the implicit `GetLowerBound` calls. PR [dotnet/coreclr#21756](https://github.com/dotnet/coreclr/pull/21756) does that in a variety of places.
-   **Cheaper copying to new arrays.** In a variety of places, a `List<T>` stored some data, a new array was then allocated based on the length of the list, and the contents then copied to the array with `CopyTo`. PR [dotnet/coreclr#22101](https://github.com/dotnet/coreclr/pull/22101) from @benaadams recognized the silliness of this and replaced that pattern with simply using `List<T>.ToArray`.
-   **`Nullable<T>.Value` vs `GetValueOrDefault`.** `Nullable<T>` has two main members to access the value: `Value` and `GetValueOrDefault`. It’s initially counter-intuitive, but `GetValueOrDefault` is actually cheaper: `Value` needs to check whether the instance has a value or not, throwing if it doesn’t, whereas `GetValueOrDefault` just always returns the value field, and it’ll be `default` if there was no value. PR [dotnet/coreclr#22297](https://github.com/dotnet/coreclr/pull/22297) fixed up a variety of call sites where `GetValueOrDefault` could be used instead.
-   **`Array.Empty<T>()`.** In previous releases, lots of zero-length array allocations were changed to instead use `Array.Empty<T>()`, both in libraries and via compiler changes for things like `params` arrays. That trend continues in .NET Core 3.0, with PR [dotnet/corefx#30235](https://github.com/dotnet/corefx/pull/30235) doing another sweep through corefx and replacing even more zero-length allocations with the cached `Array.Empty<T>()`.
-   **Avoiding lots of little allocations all over the place.** For new code being written, we’re very cost-conscious and keep an eye out for allocations that, even if small and rare, could be easily replaced by something less expensive. For existing code, the most impactful allocations show up in profiling of key scenarios and are squashed whenever possible. But there are a lot of small allocations here and there that generally don’t pop up on our radar until we have another reason to review and profile the relevant code. In every release, we end up removing a bunch of these. For example, all of these PRs contributed to reducing the allocation peanut butter across coreclr and corefx in .NET Core 3.0:
    -   In System.Collections: [dotnet/corefx#30528](https://github.com/dotnet/corefx/pull/30528)
    -   In System.Data: [dotnet/corefx#30130](https://github.com/dotnet/corefx/pull/30130)
    -   In System.Data.SqlClient: [dotnet/corefx#34044](https://github.com/dotnet/corefx/pull/34044), [dotnet/corefx#34047](https://github.com/dotnet/corefx/pull/34047), [dotnet/corefx#34234](https://github.com/dotnet/corefx/pull/34234),  [dotnet/corefx#34999](https://github.com/dotnet/corefx/pull/34999), [dotnet/corefx#35549](https://github.com/dotnet/corefx/pull/35549), [dotnet/corefx#34048](https://github.com/dotnet/corefx/pull/34048), [dotnet/corefx#34390](https://github.com/dotnet/corefx/pull/34390), and [dotnet/corefx#34393](https://github.com/dotnet/corefx/pull/34393), all from @Wraith2
    -   In System.Diagnostics: [dotnet/coreclr#21752](https://github.com/dotnet/coreclr/pull/21752)
    -   In System.IO: [dotnet/corefx#30509](https://github.com/dotnet/corefx/pull/30509), [dotnet/corefx#30514](https://github.com/dotnet/corefx/pull/30514), [dotnet/coreclr#21760](https://github.com/dotnet/coreclr/pull/21760), [dotnet/corefx#37546](https://github.com/dotnet/corefx/pull/37546)
    -   In System.Globalization: [dotnet/coreclr#18546](https://github.com/dotnet/coreclr/pull/18546), [dotnet/coreclr#21121](https://github.com/dotnet/coreclr/pull/21121)
    -   In System.Net: [dotnet/corefx#30521](https://github.com/dotnet/corefx/pull/30521), [dotnet/corefx#30530](https://github.com/dotnet/corefx/pull/30530), [dotnet/corefx#30508](https://github.com/dotnet/corefx/pull/30508), [dotnet/corefx#30529](https://github.com/dotnet/corefx/pull/30529), [dotnet/corefx#34356](https://github.com/dotnet/corefx/pull/34356), [dotnet/corefx#36021](https://github.com/dotnet/corefx/pull/36021)
    -   In System.Reflection: [dotnet/coreclr#21770](https://github.com/dotnet/coreclr/pull/21770), [dotnet/coreclr#21758](https://github.com/dotnet/coreclr/pull/21758)
    -   In System.Security: [dotnet/corefx#30512](https://github.com/dotnet/corefx/pull/30512), [dotnet/corefx#29612](https://github.com/dotnet/corefx/pull/29612)
    -   In System.Uri: [dotnet/corefx#33641](https://github.com/dotnet/corefx/pull/33641), [dotnet/corefx#36056](https://github.com/dotnet/corefx/pull/36056)
    -   In System.Xml: [dotnet/corefx#34196](https://github.com/dotnet/corefx/pull/34196)
-   **Avoiding explicit static cctors.** Any type that has static fields initialized ends up with a static constructor (cctor) to run that initialization. But depending on how the initialization is authored can impact performance. In particular, if the developer explicitly writes a static cctor rather than initializing the fields as part of the static field declarations, the C# compiler will not mark the type as `beforefieldinit`. Having the type marked `beforefieldinit` can be beneficial for performance, because it allows the runtime more flexibility in when it performs the initialization, which in turn allows the JIT more flexibility about how it can optimize, and whether locking might be needed when accessing static methods on the type. PRs like [dotnet/coreclr#21718](https://github.com/dotnet/coreclr/pull/21718) and [dotnet/coreclr#21715](https://github.com/dotnet/coreclr/pull/21715) from @benaadams have removed such static cctors that can layer in small costs across a wide swath of accessing code.
-   **Using a cheaper, sufficient equivalent.** `IndexOf` on strings and spans returns the position of a found element, whereas `Contains` just returns whether the element was found. The latter can be slightly more efficient, because it doesn’t need to track the exact location of an element, just that it existed. Even so, lots of call sites that could have used `Contains` instead used `IndexOf`. PRs [dotnet/coreclr#19874](https://github.com/dotnet/coreclr/pull/19874) and [dotnet/corefx#32249](https://github.com/dotnet/corefx/pull/32249) by @grant-d addressed that. Another example, `SocketsHttpHandler`(the default `HttpMessageHandler` behind `HttpClient`) was using `DateTime.UtcNow` when determining whether a connection could be reused for the next request or not, but `Environment.TickCount` is cheaper and has sufficient resolution and accuracy for this purpose, so PR [dotnet/corefx#35401](https://github.com/dotnet/corefx/pull/35401) switched it to use that. Another example, PR [dotnet/corefx#37548](https://github.com/dotnet/corefx/pull/37548) tweaks the overloads of Array.Copy used in a bunch of places to avoid unnecessary `GetLowerBound()` calls to lookup the lower bound for arrays we know have a lower bound of 0.
-   **Simplifying interop.** The interop infrastructure in .NET is quite powerful and comprehensive, with lots of knobs that allow for specifying how calls should be made and how data should be transformed. However, many come with a cost, such as needing the runtime to generate a marshaling stub to perform the various required transformations. PRs [dotnet/corefx#36544](https://github.com/dotnet/corefx/pull/36544) and [dotnet/corefx#36071](https://github.com/dotnet/corefx/pull/36071), for example, tweaked interop signatures to avoid overheads associated with such marshaling code.
-   **Avoiding unnecessary globalization.** Due to how various `System.String` APIs were designed almost two decades ago, it can be easy to accidentally employ culture-aware string comparisons when it’s not intended. Such comparisons can be functionally incorrect for a given task and also more costly, involving more expensive calls to the operating system or globalization library. In particular, `String.IndexOf` with a `char` argument uses ordinal comparison, but `String.IndexOf` with a `string` argument (even if it’s a single character) uses the current culture to perform the comparison. PRs [dotnet/corefx#37499](https://github.com/dotnet/corefx/pull/37499) addresses a bunch of such cases in `System.Net`, an area in which one almost always wants to do ordinal comparisons, generally the case when doing parsing for text-based protocols.
-   **Avoiding unnecessary `ExecutionContext` flow.** `ExecutionContext` is the primary vehicle for ambient state “flowing” through a program and across asynchronous calls, in particular `AsyncLocal<T>`. In order to achieve such flow, code that spawns an async operation (e.g. `Task.Run`, `Timer`, etc.) or code that creates a continuation to run when some other operation finishes (e.g. `await`) needs to “capture” the current `ExecutionContext`, hang on to it, and then later when executing the relevant work, use that captured `ExecutionContext`‘s `Run` method to do so. If the work being performed doesn’t actually require the `ExecutionContext`, we can avoid flowing it to avoid the small associated overhead. PRs [dotnet/corefx#37551](https://github.com/dotnet/corefx/pull/37551), [dotnet/corefx#33235](https://github.com/dotnet/corefx/pull/33235), and [dotnet/corefx#33080](https://github.com/dotnet/corefx/pull/33080) are examples: they switch several uses of `CancellationToken.Register`over to the new `CancellationToken.UnsafeRegister` method, the only difference compared to `Register` being that it doesn’t flow `ExecutionContext`. As another example, PR [dotnet/coreclr#18670](https://github.com/dotnet/coreclr/pull/18670) changed `CancellationTokenSource` so that when it creates a `Timer`, it doesn’t unnecessarily capture `ExecutionContext`. Or consider PR [dotnet/coreclr#20294](https://github.com/dotnet/coreclr/pull/20294), which ensures that any such captured `ExecutionContext` is dropped as soon as it’s not needed from completed `Task`s.
-   **Centralized / optimized bit operations.** PR [dotnet/coreclr#22118](https://github.com/dotnet/coreclr/pull/22118) from @benaadams introduced a `BitOperations` class that serves to centralize a bunch of bit-twiddling operations (rotating, leading zero count, population count, log, etc.). This type was later augmented and enhanced in PRs from @grant-d like [dotnet/coreclr#22497](https://github.com/dotnet/coreclr/pull/22497), [dotnet/coreclr#22584](https://github.com/dotnet/coreclr/pull/22584), and [dotnet/coreclr#22630](https://github.com/dotnet/coreclr/pull/22630), which also serve to use these shared helpers from everywhere across `System.Private.Corelib` where such bit-twiddling operations are required. This ensures that all such call sites (of which there are currently ~70) get the best implementation the runtime can muster, whether that be an implementation that takes advantage of the current hardware’s instruction set or one that utilizes a software fallback.

### GC

No blog post on performance would be complete without discussing the garbage collector. Many of the improvements cited thus far have involved reducing allocations, which is in part about reducing direct costs but more so about reducing the load placed on the garbage collector and minimizing the work it needs to do. But improving the GC itself is also a key focus, and one that’s gotten attention in this release, as it has in previous releases. PR [dotnet/coreclr#21523](https://github.com/dotnet/coreclr/pull/21523) includes a variety of performance improvements, from improvements to locking to better free list management. PR [dotnet/coreclr#23251](https://github.com/dotnet/coreclr/pull/23251) from @mjsabby adds support to the GC for Large Pages (“Huge Pages” on Linux), which can be opted-into by very large applications that experience bottlenecks due to the translation lookaside buffer (TLB). And PR [dotnet/coreclr#22003](https://github.com/dotnet/coreclr/pull/22003) further optimized the write barriers employed by the GC. One notable piece of work is improving behavior on machines with a large number of processors, e.g. PR [dotnet/coreclr#23824](https://github.com/dotnet/coreclr/pull/23824). Rather than trying to explain it here, I’ll simply refer to @Maoni0’s blog post on the subject: [https://blogs.msdn.microsoft.com/maoni/2019/04/03/making-cpu-configuration-better-for-gc-on-machines-with-64-cpus/](https://blogs.msdn.microsoft.com/maoni/2019/04/03/making-cpu-configuration-better-for-gc-on-machines-with-64-cpus/). Similarly, a lot of work has gone into the release to improve the behavior and performance of the GC when operating in a containerized environment (and in particular in one that’s heavily constrained), such as in PR [dotnet/coreclr#22180](https://github.com/dotnet/coreclr/pull/22180). Again, @Maoni0 can do a much better job than I can describing this work, and you can read all about it her two blog posts, [running-with-server-gc-in-a-small-container-scenario-part-0](https://blogs.msdn.microsoft.com/maoni/2018/11/16/running-with-server-gc-in-a-small-container-scenario-part-0/) and [running-with-server-gc-in-a-small-container-scenario-part-1-hard-limit-for-the-gc-heap](https://blogs.msdn.microsoft.com/maoni/2019/02/04/running-with-server-gc-in-a-small-container-scenario-part-1-hard-limit-for-the-gc-heap/).

### JIT

A lot of goodness has gone into the just-in-time (JIT) compiler in .NET Core 3.0. One of the most impactful changes is tiered compilation (this is split across many PRs, but for example PR [dotnet/coreclr#23599](https://github.com/dotnet/coreclr/pull/23599)). Tiered compilation is a solution for the problem that very good compilation from MSIL to native code takes time; the more analysis to be done, the more optimizations to be applied, the longer it takes. But with a JIT compiler that does that code generation at runtime, that time comes at the direct expense of application start-up, and so you’re left with a trade-off: do you spend more time generating better code but take longer to get going, or do you spend less time generating less-good code but get going faster? Tiered compilation is a scheme for accomplishing both. The idea is that methods are first compiled with a fast pass that applies few-to-no optimizations but that completes very quickly, and then as methods are seen to execute again and again, those methods are re-JIT’d, this time with more time spent on code quality. Interestingly, though, tiered compilation isn’t just about start-up time. There are optimizations that the re-compilation can take advantage of that weren’t available the first time around. For example, tiered compilation can apply to ready-to-run (R2R) images, a form of precompilation employed by assemblies in the .NET Core shared framework. These assemblies contain precompiled native code, but in some ways the optimizations that can be applied during that native code generation are limited in order to aid in version resiliency, e.g. cross-module inlining doesn’t happen with R2R. So, the R2R code can help enable faster start-up, but then methods found to be used frequently can be re-compiled via tiered compilation, thereby taking advantage of such optimizations the original precompiled code was restricted from using. Here’s an example of that. First, we can run the following benchmark.

```
private XmlDocument _doc = new XmlDocument();

[Benchmark]
public void LoadXml()
{
    _doc.RemoveAll();
    _doc.LoadXml("<Root><Element attrib=\"foo\" attrib2=\"foo2\">foo</Element><Element attrib=\"foo\" attrib2=\"foo2\">foo</Element><Element attrib=\"foo\" attrib2=\"foo2\">foo</Element><Element attrib=\"foo\" attrib2=\"foo2\">foo</Element><Element attrib=\"foo\" attrib2=\"foo2\">foo</Element><Element attrib=\"foo\" attrib2=\"foo2\">foo</Element><Element attrib=\"foo\" attrib2=\"foo2\">foo</Element><Element attrib=\"foo\" attrib2=\"foo2\">foo</Element></Root>");
}
```

| Method | Toolchain | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- |
| LoadXml | netcoreapp2.1 | 9.576 us | 0.1523 us | 0.1425 us | 1.00 |
| LoadXml | netcoreapp3.0 | 7.414 us | 0.0980 us | 0.0868 us | 0.78 |

  Then, we can run it again, but this time with tiered compilation disabled by setting the 

`COMPlus_TieredCompilation`environment variable to 0.

| Method | Toolchain | Mean | Error | StdDev | Ratio | RatioSD |
| --- | --- | --- | --- | --- | --- | --- |
| LoadXml | netcoreapp2.1 | 9.650 us | 0.1638 us | 0.1279 us | 1.00 | 0.00 |
| LoadXml | netcoreapp3.0 | 9.002 us | 0.2018 us | 0.2073 us | 0.93 | 0.03 |

  There are a variety of environment variables that configure tiered compilation and in what situations it’s enabled. For more details, see 

[https://github.com/dotnet/coreclr/issues/24064](https://github.com/dotnet/coreclr/issues/24064). Another really cool improvement in the JIT comes in PR [dotnet/coreclr#20886](https://github.com/dotnet/coreclr/pull/20886). In previous releases of .NET, the JIT could optimize the usage of some primitive type `static readonly` fields as if they were constants. For example, if a `static readonly int` field were initialized to the value `42` by the time some code that used that field was JIT compiled, the JIT compiler would effectively treat that field instead as a `const`, and do constant folding and all other forms of optimizations that would otherwise apply. In .NET Core 3.0, the JIT can now utilize the type of `static readonly`fields to do additional optimizations. For example, if a `static readonly` field is typed as a base type but is then set to a derived type, the JIT might be able to see the actual type of the object stored in the field, and then when a virtual method is called on it, devirtualize the call and even potentially inline it.

```
private static readonly Base s_base;

static Program() => s_base = new Derived();

[Benchmark]
public void AccessStatic() => s_base.Method();

private sealed class Derived : Base { public override void Method() { } }
private abstract class Base { public abstract void Method(); }
```

| Method | Toolchain | Mean | Error | StdDev | Median | Ratio |
| --- | --- | --- | --- | --- | --- | --- |
| AccessStatic | netcoreapp2.1 | 0.5625 ns | 0.0147 ns | 0.0130 ns | 0.5616 ns | 1.000 |
| AccessStatic | netcoreapp3.0 | 0.0015 ns | 0.0060 ns | 0.0062 ns | 0.0000 ns | 0.003 |

  That highlights some improvements that have gone into devirtualization, but there are others, such as in PRs 

[dotnet/coreclr#20447](https://github.com/dotnet/coreclr/pull/20447), [dotnet/coreclr#20292](https://github.com/dotnet/coreclr/pull/20292), and [dotnet/coreclr#20640](https://github.com/dotnet/coreclr/pull/20640) which, when combined with PRs like [dotnet/coreclr#20637](https://github.com/dotnet/coreclr/pull/20637) from @benaadams, help with APIs like `ArrayPool<T>.Shared<span style="color: #52595e; font-family: Arimo, Helvetica Neue, Arial, sans-serif;"><span style="font-size: 16px; background-color: #f7f7f9;">.</span></span>`

```
[Benchmark]
public void RentReturn() => ArrayPool<byte>.Shared.Return(ArrayPool<byte>.Shared.Rent(256));
```

| Method | Toolchain | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- |
| RentReturn | netcoreapp2.1 | 32.92 ns | 0.3357 ns | 0.2803 ns | 1.00 |
| RentReturn | netcoreapp3.0 | 25.74 ns | 0.2392 ns | 0.1867 ns | 0.78 |

  Another nice improvement is around zeroing of locals. Even when the 

`initlocals` flag isn’t set (as of PR [dotnet/corefx#34406](https://github.com/dotnet/corefx/pull/34406), it’s cleared for all assemblies in coreclr and corefx), the JIT still needs to zero out references in locals so that the GC doesn’t see and misinterpret garbage, and that zero’ing can take a measurable amount of time, in particular in methods that do a lot of work with spans. PRs [dotnet/coreclr#23498](https://github.com/dotnet/coreclr/pull/23498) and [dotnet/coreclr#13868](https://github.com/dotnet/coreclr/pull/13868) make some nice improvements in this area.

```
private byte[] _bytes = new byte[1];

[Benchmark]
public void StackZero()
{
    Span<byte> a, b;
    a = _bytes;
    b = _bytes;
    Nop(a, b);
}

[MethodImpl(MethodImplOptions.NoInlining)]
private void Nop(Span<byte> a, Span<byte> b) { }
```

| Method | Toolchain | Mean | Error | StdDev | Ratio |
| --- | --- | --- | --- | --- | --- |
| StackZero | netcoreapp2.1 | 8.948 ns | 0.2479 ns | 0.2546 ns | 1.00 |
| StackZero | netcoreapp3.0 | 2.389 ns | 0.0740 ns | 0.0727 ns | 0.27 |

  Another example relates to structs. As more and more recognition has come to .NET performance, in particular around allocation, there’s been a significant increase in the use of value types, often wrapping one another. For example, awaiting a 

`ValueTask<T>` results in calling `GetAwaiter()` on that value task, and that returns a `ValueTaskAwaiter<T>` that wraps the `ValueTask<T>`. PR [dotnet/coreclr#19429](https://github.com/dotnet/coreclr/pull/19429) improves the situation by removing unnecessary copies involved in these operations.

```
[Benchmark]
public int WrapUnwrap() => ValueTuple.Create(ValueTuple.Create(ValueTuple.Create(42))).Item1.Item1.Item1;
```

| Method | Toolchain | Mean | Error | StdDev | Median | Ratio |
| --- | --- | --- | --- | --- | --- | --- |
| WrapUnwrap | netcoreapp2.1 | 1.2198 ns | 0.0717 ns | 0.0599 ns | 1.2095 ns | 1.000 |
| WrapUnwrap | netcoreapp3.0 | 0.0002 ns | 0.0007 ns | 0.0006 ns | 0.0000 ns | 0.000 |

### What’s Next?

As I write this post, I count 29 pending performance-focused PRs in the coreclr repo and another 8  in the corefx repo. Some of those are likely to be merged in time for the .NET Core 3.0 release, as will, I’m sure, additional PRs that haven’t even been opened yet. In short, even after all of the improvements detailed in for [.NET Core 2.0](https://blogs.msdn.microsoft.com/dotnet/2017/06/07/performance-improvements-in-net-core/), [.NET Core 2.1](https://devblogs.microsoft.com/dotnet/performance-improvements-in-net-core-2-1), and now in this post for .NET Core 3.0, and even with all of those improvements contributing to ASP.NET Core being one of the fastest web servers on the planet, there is still incredible opportunity for performance to keep getting better and better, and for you to help achieve that. Hopefully this post has made you excited about the potential .NET Core 3.0 holds. I look forward to reviewing your PRs as we all contribute to this exciting future together!

## Author

![Stephen Toub - MSFT](https://devblogs.microsoft.com/dotnet/wp-content/uploads/sites/10/2022/03/stoub_square-96x96.jpg)

Partner Software Engineer

Stephen Toub is a developer on the .NET team at Microsoft.