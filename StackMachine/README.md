# StackMachine

A C# stack machine that executes JSON instruction arrays. It supports signed
32-bit integers, mutable UTF-8 strings, arrays, S-expressions, and lists, using
[Language.Runtime](../Shared/README.md) shared with the AST interpreter.
Its instruction format follows the
[reference compiler and machine](https://dboulytchev.github.io/sm.html).

## Build and run

Run these commands from the repository root. All C# projects target `net10.0`;
use a .NET SDK that supports that target.

~~~sh
dotnet build Interpreter.slnx
dotnet run --project StackMachine/src/StackMachine.Cli -- <program.json> [input.txt]
dotnet run --project StackMachine/src/StackMachine.Cli -- StackMachine/examples/input/sum.json StackMachine/examples/input/sum.txt
~~~

The second argument is a file path. If omitted, input comes from stdin until EOF.
The entire input is parsed before execution, including unused values. An empty
input file is valid for programs without `READ`.

~~~sh
dotnet run --project StackMachine/src/StackMachine.Cli -- StackMachine/examples/input/constructors.json StackMachine/examples/input/constructors.txt
~~~

Output:

~~~text
Foo (9, "x"), 2, "x"
~~~

Each `examples/input/<name>.json` has an accompanying input `.txt` file and
an expected result in `examples/output/<name>.txt`.

## Instruction set

A program is a flat JSON array. Object instructions contain exactly one
property. Stack notation below places the top on the right.

| Instruction | Stack effect or action |
| --- | --- |
| `"READ"` | `... -> ..., next input value` |
| `"WRITE"` | `..., value -> ...`; buffer value for output |
| `{"CONST": n}` | `... -> ..., n`; signed `int32` |
| `{"STRING": "abc"}` | `... -> ..., fresh string` |
| `{"ARRAY": n}` | `..., v1, ..., vn -> ..., [v1, ..., vn]` |
| `{"SEXP": ["Tag", n]}` | `..., a1, ..., an -> ..., Tag(a1, ..., an)` |
| `"LENGTH"` | `..., source -> ..., length` |
| `"ELEM"` | `..., source, index -> ..., element` |
| `"SET"` | `..., source, index, value -> ...`; mutate source |
| `{"LD": "x"}` | `... -> ..., x` |
| `{"ST": "x"}` | `..., value -> ...`; assign variable |
| `{"BINOP": "op"}` | `..., left, right -> ..., result` |
| `{"LABEL": "L"}` | Define label; no stack change |
| `{"JMP": "L"}` | Jump to label |
| `{"JZ": "L"}` | Pop integer; jump if zero |
| `{"JNZ": "L"}` | Pop integer; jump if nonzero |

`ARRAY` and `SEXP` accept nonnegative integer counts, including zero. They
preserve element order and check stack size before allocating their result.
`BINOP` pops the right operand before the left.

For example, this program prints `[9, 2]`:

~~~json
[
  {"CONST": 1},
  {"CONST": 2},
  {"ARRAY": 2},
  {"ST": "a"},
  {"LD": "a"},
  {"CONST": 0},
  {"CONST": 9},
  "SET",
  {"LD": "a"},
  "WRITE"
]
~~~

Duplicate labels are rejected before execution. Unknown conditional jump targets
are errors only when that jump is taken. Runtime failures include the zero-based
instruction index and retain their underlying language error. Every `Execute`
call starts with fresh memory, stack, input queue, and pending output.

## Values and operators

Operators are `+ - * / %`, `== != < <= > >=`, `&&`, `!!` (OR), and `:`
(list cons). Arithmetic and logic require integers. Logic validates both
operands and returns zero or one.

Arithmetic uses signed 32-bit wrapping for addition, subtraction, and
multiplication. Division truncates toward zero; remainder has the dividend's
sign. `-2147483648 / -1` is `-2147483648`, and its remainder is zero.
Zero divisors are language errors; out-of-range numeric literals are rejected.

Strings contain mutable UTF-8 bytes. Indices start at zero; length counts bytes.
Writes to a string element require an integer in 0..255. Arrays have fixed
length and heterogeneous elements. S-expressions have a tag and mutable
arguments; `LENGTH`, `ELEM`, and `SET` operate on those arguments.

Assigning a string, array, or S-expression shares a reference. Literal
instructions create a fresh value each time they execute. Comparisons support
pairs of integers, strings, or arrays; arrays compare lengths first, then
elements. Mixed types and S-expressions are not comparable.

Generic constructors print as `Cons (1, Nil)`. Lists use the reserved
lowercase tags `cons` with two arguments and `nil` with none, and print as
`{1, 2}` or `{}`. The `:` operator creates a `cons` cell.

## Input, output, and errors

Input values are comma-separated, for example:

~~~text
42, "abc", [1, []], Cons(1, Nil), {1, 2}
~~~

JSON string escapes, nested/empty arrays, empty strings, constructors, and lists
are supported. Legacy whitespace-separated values remain accepted.

Output joins all `WRITE` values with `, `, followed by one newline. Strings
are quoted and array elements use commas. Characters within strings are printed
literally, so output is not necessarily JSON. A program without writes produces
empty stdout.

Writes retain references until successful completion: later mutations affect
previously written mutable values. Execution or formatting failure leaves stdout
empty. Diagnostics go to stderr.

Exit codes: `0` success; `1` file, JSON, instruction, input, or execution error;
`2` invalid arguments (including `--help`).

Input parsing, formatting, and comparison use explicit stacks with a nesting
limit of 4096. Cyclic output/comparison and excessive depth produce controlled
errors. Shared acyclic subvalues are valid. These limits do not restrict the
number of executed instructions; programs can still loop indefinitely.

## Layout and tests

- `src/StackMachine.Core`: typed instructions, program/input parsers, labels,
  per-run state, and execution engine.
- `src/StackMachine.Cli`: file/stdin handling, output, diagnostics, and exit codes.
- `../Shared/src/Language.Runtime`: common values and operations.
- `tests/regression.py`: CLI regression runner.

Run from the repository root after building:

~~~sh
dotnet test Shared/tests/Language.Runtime.Tests/Language.Runtime.Tests.csproj
python StackMachine/tests/regression.py --no-build
~~~

Python 3 is required without extra packages. Omit `--no-build` to build the root
solution first, or supply `--dll <path>` / `--dotnet <path>` for another build.

The tests exercise examples, all instruction families, invalid programs/input,
state isolation, errors, deep structures, and saved reference AST/SM cases.
Tests work offline and do not invoke the Go compiler.

Licensed under the MIT License.
