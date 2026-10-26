# CoreCLR iOS interpreter crashes on generic type with mutually constrained type parameters

SIGSEGV (null dereference) inside `libcoreclr` when the CoreCLR interpreter on iOS compiles a method that touches a
generic type where one type parameter is constrained by another type parameter *and* by `IComparable<T>` over itself.
The type must be in a ReadyToRun-compiled assembly. In a Debug build the iOS SDK does that for NuGet package
assemblies, so a package dependency is enough to trigger it.

This repro contains no application code. It only uses the .NET iOS SDK template plus ~40 lines of generic types.

## Environment

| | |
|---|---|
| .NET SDK | `11.0.100-rc.2.26504.105` (pinned by `global.json`) |
| iOS workload | `27.0.12212-net11-rc.2` (`Microsoft.iOS.Sdk.net11.0_27.0`) |
| Runtime packs | `Microsoft.NETCore.App.Runtime.iossimulator-arm64` `11.0.0-rc.2.26504.105` (CoreCLR) |
| Xcode | 27.0
| Host | macOS 26.6.2, Apple silicon |
| Target | iOS Simulator 27.0 (iPhone 18 Pro), `iossimulator-arm64`, Debug |

Not tested: physical devices, `ios-arm64`, Mono (`UseMonoRuntime=true`), NativeAOT, other .NET 11 previews.

## Reproduce

1. Install the SDK and workload that `global.json` pins, and boot an iOS simulator.
2. Run:

   ```sh
   ./run.sh Debug
   ```

   `run.sh` packs `RangedTypes` into `./local-feed`, builds `App` (`dotnet build -f net11.0-ios -c Debug -r iossimulator-arm64`),
   installs it on the booted simulator and launches it with the console attached. To target a specific simulator:
   `./run.sh Debug <name-or-udid>`.

3. Expected: the app prints `Repro: finished without crashing.`
   Actual: the process dies with `SIGSEGV` (see below). A crash report is written to
   `~/Library/Logs/DiagnosticReports/ConstraintCrashRepro-*.ips`.

Comparison runs (same sources):

| Command | Result |
|---|---|
| `./run.sh Debug` | **crash** |
| `./run.sh Debug booted -p:FilterReadyToRunAssemblies=false` | ok |
| `./run.sh Release` | ok |

## Output

Console:

```
Repro: calling Generic<Item, Position>.Run...
Fatal error.
Got a SIGSEGV while executing native code. ...
   at RangedTypes.Entry.Run()
   at ConstraintCrashRepro.AppDelegate.FinishedLaunching(UIKit.UIApplication, Foundation.NSDictionary)
   ...
```

Native stack of the faulting thread (`EXC_BAD_ACCESS`, `KERN_INVALID_ADDRESS at 0x0`):

```
 0 libcoreclr  TypeVarTypeDesc::ConstrainedAsObjRefHelper()
 1 libcoreclr  TypeVarTypeDesc::ConstrainedAsObjRefHelper()
 2 libcoreclr  TypeVarTypeDesc::ConstrainedAsObjRef()
 3 libcoreclr  TypeHandle::IsBoxedAndCanCastTo(TypeHandle, TypeHandlePairList*) const
 4 libcoreclr  MethodTable::CanCastByVarianceToInterfaceOrDelegate(MethodTable*, TypeHandlePairList*, MethodTable*)
 5 libcoreclr  MethodTable::CanCastToInterface(MethodTable*, TypeHandlePairList*)
 6 libcoreclr  MethodTable::CanCastTo(MethodTable*, TypeHandlePairList*)
 7 libcoreclr  TypeHandle::CanCastTo(TypeHandle, TypeHandlePairList*) const
 8 libcoreclr  TypeVarTypeDesc::SatisfiesConstraints(SigTypeContext*, TypeHandle, InstantiationContext const*)
 9 libcoreclr  SatisfiesClassConstraints(TypeHandle, TypeHandle, InstantiationContext const*)
10 libcoreclr  MethodTable::DoFullyLoad(Generics::RecursionGraph*, ClassLoadLevel, CUnorderedArray<TypeHandle, 40>*, int*, InstantiationContext const*)
11 libcoreclr  PushFinalLevels(TypeHandle, ClassLoadLevel, InstantiationContext const*)
12 libcoreclr  ClassLoader::LoadTypeHandleForTypeKey(...)
13 libcoreclr  ClassLoader::LoadConstructedTypeThrowing(...)
14 libcoreclr  ClassLoader::LoadGenericInstantiationThrowing(...)
15 libcoreclr  SigPointer::GetTypeHandleThrowing(...)
16 libcoreclr  CEEInfo::getCallInfo(...)
17 libcoreclr  InterpCompiler::EmitCall(...)
18 libcoreclr  InterpCompiler::GenerateCode(...)
19 libcoreclr  InterpCompiler::CompileMethod()
20 libcoreclr  CILInterp::compileMethod(...)
21 libcoreclr  UnsafeJitFunction(...)
...
27 libcoreclr  PrepareInterpreterCode(...)
28 libcoreclr  InterpExecMethod(...)
```

## The types

`RangedTypes/GenericTypes.cs`:

```csharp
public abstract class Reference : IComparable<Reference> { ... }

public interface IHasStart<TPoint> where TPoint : Reference, IComparable<TPoint>
{
	TPoint Start { get; }
}

public abstract class Generic<TRef, TPoint>
	where TRef : Reference, IHasStart<TPoint>
	where TPoint : TRef, IComparable<TPoint>   // constrained by another type parameter and by IComparable<TPoint>
{
	public string Run(TRef reference) => reference.Start.ToString() ?? "";
}

public abstract class Item : Reference, IHasStart<Position> { ... }
public class Position : Item, IComparable<Position> { ... }
public sealed class Impl : Generic<Item, Position> { ... }
```

The app calls `Impl.Instance.Run(new Position())` through a non-generic helper (`Entry.Run`). All type arguments are
reference types, so the shared canonical code `Generic<__Canon, __Canon>` is used.

## Workarounds

- Add `<FilterReadyToRunAssemblies>false</FilterReadyToRunAssemblies>` to the app project so Debug builds precompile
  everything. Breakpoints in application code no longer hit.
- Release builds precompile everything and did not crash.

## Origin

Found in a large app that depends on an internal library with a type of exactly this shape
(`StandardRangedDataType<TReference, TPoint, TRange>`), where a one-line method on the generic type crashed the app
as soon as it was first called in a Debug simulator build. The same assemblies ran under the previous .NET 10 / Mono
setup.
