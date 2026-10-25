# Interpreters

A collection of language-processing projects for a simple imperative language, covering direct AST interpretation, stack-machine execution, and compilation to stack-machine instructions.

## Projects

- [Interpreter](./Interpreter) — .NET interpreter for a simple imperative language that executes programs represented as a JSON AST.
- [StackMachine](./StackMachine) — .NET interpreter for an abstract stack machine that executes programs represented as JSON instruction sequences.
- [Compiler](./Compiler) — A Go compiler that parses source programs, generates JSON instructions for the stack machine, and executes them using a built-in virtual machine.

The two C# executors share [Language.Runtime](./Shared): values, signed 32-bit
arithmetic, structured input, comparison, and output formatting. Supported
values include integers, mutable UTF-8 strings, fixed-size arrays,
S-expressions, and lists.

## Build

C# projects target `net10.0` and require a compatible .NET SDK. The compiler
requires Go 1.18 or later. CLI regression and integration tests additionally
require Python 3.9 or later without extra packages.

From the repository root:

```sh
dotnet build Interpreter.slnx
```

From `Compiler/`:

```sh
go build -o ../artifacts/smcompiler .
```

On Windows, use `smcompiler.exe` as the output file name.
`Interpreter.slnx` contains all C# libraries, executables, and the test project.
The Go compiler is built separately.

## Execution pipeline

```text
Source text -> Go lexer/parser -> AST -> Go compiler -> JSON instructions
                                                        |-> Go VM
                                                        |-> C# StackMachine

JSON AST from the reference frontend -> C# Interpreter
```

The AST interpreter consumes JSON AST nodes; the stack machine consumes JSON
instructions. Their input formats are distinct.

From the repository root, compile without running the built-in VM, then
execute the generated program:

```sh
artifacts/smcompiler --compile-only --output artifacts/composite.json Compiler/examples/composite.txt
dotnet run --project StackMachine/src/StackMachine.Cli -- artifacts/composite.json Compiler/examples/composite.input
```

Both C# CLIs accept `<program.json> [input.txt]`. If the input file is omitted,
they read stdin until EOF. Input values use commas and JSON string escapes.
Output values use `, ` and a final newline; a program without writes produces
empty stdout. Runtime or formatting errors discard pending output.

## Verification

Run from the repository root after building:

```sh
dotnet test Shared/tests/Language.Runtime.Tests/Language.Runtime.Tests.csproj --no-build
python Interpreter/tests/regression.py --no-build
python StackMachine/tests/regression.py --no-build
python tests/integration.py --no-build
```

Also run `go test ./...`, `go vet ./...`, and `gofmt -l .` from `Compiler/`.
The formatting command should produce no output.

The integration runner builds a temporary Go executable, compiles source
programs, and compares Go VM output with the C# machine running the emitted
JSON. Matching saved AST fixtures are also executed by the C# interpreter.
The reference snapshot contains 39 programs; normal tests need no network.
Use `--go`, `--dotnet`, or `--compiler` to select alternative executables.

Depth limits are documented in each project's README. Go and C# use different
controlled nesting limits; cycles and excessive depth produce diagnostics.
