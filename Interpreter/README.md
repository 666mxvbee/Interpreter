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
`while`, `do`, and `switch`. Element assignment uses:

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

## Switch and pattern matching

The AST interpreter supports the reference `switch` extension observed on
2026-11-06. This change applies to the C# AST interpreter only. The reference
compiler does not yet compile `switch`; the Go compiler and both stack machines
retain their existing instruction set.

~~~text
{
    x = 100;
    switch (Pair(1, 2)) {
        case Pair(x, y) -> write(x + y);
        | case _ -> write(0);
    }
    write(x);
}
~~~

Expected output is `3, 100`. Obtain executable JSON AST from
the [reference frontend](https://dboulytchev.github.io/eval.html), then pass it
to the interpreter CLI as usual. Source text is not accepted by this CLI.

A standalone switch statement has this JSON shape:

~~~json
{
  "switch": {
    "scrutinee": {"sexp": "Pair", "args": [{"const": 1}, {"const": 2}]},
    "branches": [
      {
        "pattern": {
          "sexp": "Pair",
          "args": [
            {"bind": {"name": "x", "pattern": {"wildcard": []}}},
            {"bind": {"name": "y", "pattern": {"wildcard": []}}}
          ]
        },
        "branch": {"write": {"binop": "+", "left": {"var": "x"}, "right": {"var": "y"}}}
      },
      {"pattern": {"wildcard": []}, "branch": {"write": {"const": 0}}}
    ]
  }
}
~~~

This standalone JSON prints `3`; it omits the surrounding assignment and write
from the source example. Patterns are distinct AST nodes from expressions:

| Source pattern | JSON pattern |
| --- | --- |
| `_` | `{"wildcard": []}` |
| `x` | `{"bind": {"name": "x", "pattern": {"wildcard": []}}}` |
| `x @ #int` | `{"bind": {"name": "x", "pattern": "#int"}}` |
| `42` or a character with code 42 | `42` |
| `"hello"` | `"hello"` |
| `[_, _]` | `{"array": [{"wildcard": []}, {"wildcard": []}]}` |
| `Pair(_, _)` | `{"sexp": "Pair", "args": [{"wildcard": []}, {"wildcard": []}]}` |
| `{}` | `{"sexp": "nil", "args": []}` |
| `#int`, `#string`, `#array`, `#sexp` | The corresponding JSON string |

List patterns and `head : tail` use nested `cons`/`nil` S-expression patterns.
Array patterns require an exact length; constructor patterns require an exact
tag and argument count. String literals compare UTF-8 bytes, while character
patterns compare integer codes. The reference parser accepts integer patterns
even though its displayed `patternPrimary` grammar omits `CONST`.

The scrutinee is evaluated once. Branches are tried in source order, and only
the first matching branch executes. No match produces a `match failure on ...`
language error, discarding buffered output. Bindings are collected separately
until a whole pattern succeeds. Repeated names overwrite earlier bindings in
left-to-right matching order; an `x @ pattern` binding is installed after its
inner pattern matches.

`RuntimeState` maintains a stack of dictionaries. Pattern variables belong to
a temporary scope, shadow outer variables, and disappear on leaving the branch.
Nested switches add nested scopes. Assignment and `read` update the nearest
existing binding; a previously unknown name is created in the global scope,
matching the reference. Mutations through bound strings, arrays, and
S-expressions remain visible through their other references. Scope cleanup also
runs after execution errors. Ordinary blocks do not introduce new scopes.

Pattern matching uses an explicit work stack and the existing runtime depth
limit. Pattern JSON uses the existing strict shape validation and JSON depth
limit. The reference JSON format represents both a type pattern and a literal
string equal to `"#int"`, `"#string"`, `"#array"`, or `"#sexp"` identically;
these four JSON strings are interpreted as type patterns because their original
source spelling cannot be recovered from the exported AST.

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
