# Interpreter

A C# interpreter for a small imperative language. It executes a JSON AST produced
by the [reference frontend](https://dboulytchev.github.io/eval.html); it does not
parse source text. Values, input, arithmetic, comparison, and formatting come
from [Language.Runtime](../Shared/README.md), shared with the C# stack machine.

## Build and run

All commands below run from the repository root. The C# projects target
`net10.0`; use a .NET SDK that supports that target.

~~~sh
dotnet build Interpreter.slnx
dotnet run --project Interpreter/src/Interpreter.Cli -- <program.json> [input.txt]
~~~

The second argument is a file path. Without it, input is read from stdin until
EOF. The entire input is validated before execution, including unused values.
An empty input file is valid when the program does not execute `read`.

~~~sh
dotnet run --project Interpreter/src/Interpreter.Cli -- Interpreter/examples/input/features/program.json Interpreter/examples/input/features/input.txt
dotnet run --project Interpreter/src/Interpreter.Cli -- Interpreter/examples/input/constructors/program.json Interpreter/examples/input/constructors/input.txt
~~~

The constructors example prints:

~~~text
Foo (9, "x"), 2, "x"
~~~

Each example has its executable JSON in `examples/input/<name>/program.json`
and expected output in `examples/output/<name>.txt`. A `program.src` file,
where present, documents the source program; the CLI still consumes JSON.

## AST and execution

`Interpreter.Ast` defines expressions, statements, and a strict JSON parser.
Expressions produce values; statements change variables, mutate values, control
execution, or perform I/O. For example:

~~~json
{"write": {"binop": "+", "left": {"const": 1}, "right": {"const": 2}}}
~~~

| Expression | JSON AST |
| --- | --- |
| Integer | `{"const": 42}` |
| Variable | `{"var": "a"}` |
| String | `{"string": "abc"}` |
| Array | `{"array": [{"const": 1}, {"const": 2}]}` |
| Element | `{"elem": {"var": "a"}, "index": {"const": 0}}` |
| Length | `{"length": {"var": "a"}}` |
| Constructor | `{"sexp": "Cons", "args": [{"const": 1}, {"sexp": "Nil", "args": []}]}` |

Statements include `"skip"`, `seq`, `assn`, `set`, `read`, `write`, `if`,
`while`, and `do`. Element assignment uses:

~~~json
{
  "set": {
    "dst": {"var": "a"},
    "idx": {"const": 0},
    "value": {"const": 9}
  }
}
~~~

The parser checks required properties, property counts, types, identifiers, and
integer ranges. Invalid shapes report a JSON path such as `$.write.args[1]`.
Array literals must be non-empty, matching the source grammar; empty arrays in
input are supported. Constructor argument lists may be empty.

`Interpreter.Runtime` evaluates the parsed objects using explicit work stacks.
Binary expressions evaluate the right operand first; element access evaluates
the index expression before its source. Element assignment evaluates the value,
then the index expression, then the destination, before checking the index type
and bounds. These choices preserve the reference's error order.

## Values and operations

- Integers are signed 32-bit values. Addition, subtraction, and multiplication
  wrap on overflow. Division truncates toward zero; remainder has the dividend's
  sign. `-2147483648 / -1` yields `-2147483648`, and its remainder is zero.
  Division or remainder by zero is a language error.
- Zero is false; any other integer is true. `&&` and `!!` (OR) evaluate and
  validate both operands. Logic and comparisons return zero or one.
- Strings are mutable UTF-8 bytes. Length counts bytes: `"Я"` has length two,
  with bytes 208 and 175. Element writes require an integer from 0 to 255.
  Invalid UTF-8 after mutation decodes with replacement characters.
- Arrays have fixed length and heterogeneous elements. Strings, arrays, and
  S-expressions share references on assignment; mutations through aliases are
  visible. Every evaluation of a literal creates a fresh value.
- S-expressions contain a tag and mutable arguments. Length and indexing operate
  on those arguments. `Cons(1, Nil)` is a generic constructor; lists use the
  reserved lowercase `cons` and `nil` tags. The `:` operator constructs a list
  cell; reference-generated JSON can represent it directly as a `cons` node.
- Comparisons accept two integers, two strings, or two arrays. Strings compare
  by bytes. Arrays compare lengths first, then elements when lengths match.
  Different types and S-expressions are not comparable.
- Unknown variables, unsuitable types, invalid indices, and exhausted input
  produce controlled language errors.

## Input and output

Canonical input separates values with commas:

~~~text
42, "abc", [1, []], Cons(1, Nil), {1, 2}, {}
~~~

Input supports JSON string escapes, nested arrays, constructors, lists, and
empty strings/arrays/lists. Whitespace-separated values remain accepted for
legacy integer input files. Numeric literals outside `int32` are rejected.

Successful output joins `write` values with `, ` and adds one final newline.
With no writes, stdout is empty. Strings are quoted, arrays use commas,
constructors print as `Cons (1, Nil)`, and lists as `{1, 2}`.
Characters inside output strings are printed literally, so output is not
necessarily valid JSON.

Writes buffer references until the whole program succeeds. Consequently,
mutating a string or array after a write also changes that earlier write's final
representation. Any execution or formatting error discards all pending output.
Diagnostics go to stderr.

Exit codes: `0` success; `1` invalid file, JSON, AST, input, or execution;
`2` invalid arguments (including `--help`).

## Depth limits and nested sums

JSON AST nesting is limited to 512 JSON containers. Input, expression evaluation,
output, and value comparison use explicit stacks and a nesting limit of 4096.
Long statement sequences execute without recursive calls. Cycles encountered
during formatting or array comparison produce controlled errors; repeated
references to an acyclic child remain valid.

~~~sh
dotnet run --project Interpreter/src/Interpreter.Cli -- Interpreter/examples/input/nested-sum/program.json Interpreter/examples/input/nested-sum/input.txt
~~~

This example prints `21`. Each data element is either `[0, number]` or
`[1, nested array]`. A loop and explicit stack traverse the structure; tests
include generated trees and 1000 nested levels.

## Tests

Python 3 is needed for CLI regressions; no extra Python packages are required.

~~~sh
dotnet test Shared/tests/Language.Runtime.Tests/Language.Runtime.Tests.csproj
python Interpreter/tests/regression.py --no-build
~~~

Build first when using `--no-build`. Omit it to let the Python runner build the
root solution. The runner obtains the executable path from MSBuild; use
`--dll <path>` for another build output or `--dotnet <path>` for another SDK.

Tests cover CLI behavior, existing examples, generated nested sums, shared
runtime operations, and saved reference AST/SM programs. Reference fixtures are
local; tests do not access the website or invoke the Go compiler.
