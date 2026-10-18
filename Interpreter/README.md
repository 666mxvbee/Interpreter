# Interpreter

A tree-walking interpreter for a small imperative language. It executes an
already-parsed AST supplied as JSON; parsing source text is out of scope.
Requires .NET SDK 11.0 or later.

## Build and run

```sh
dotnet build
dotnet run --project src/Interpreter.Cli -- <program.json> [input.txt]
```

`program.json` holds the AST. `input.txt` supplies values consumed by
`read`; when it is omitted they are taken from stdin. Program output goes to
stdout, diagnostics to stderr. Each command below prints the contents of the
matching file in `examples/output/`.

```sh
dotnet run --project src/Interpreter.Cli -- examples/input/countdown/program.json
dotnet run --project src/Interpreter.Cli -- examples/input/features/program.json examples/input/features/input.txt
```

## Strings and arrays

The JSON shapes follow the [reference interpreter](https://dboulytchev.github.io/eval.html).
Its displayed grammar omits strings, although its parser and evaluator support them.

| Expression | JSON AST |
| --- | --- |
| `"abc"` | `{"string": "abc"}` |
| `[1, 2]` | `{"array": [{"const": 1}, {"const": 2}]}` |
| `a[i]` | `{"elem": {"var": "a"}, "index": {"var": "i"}}` |
| `a.length` | `{"length": {"var": "a"}}` |

Element assignment is a statement:

```json
{
  "set": {
    "dst": {"var": "a"},
    "idx": {"const": 0},
    "value": {"const": 42}
  }
}
```

`dst` may itself be an `elem` expression for nested assignment. A compound
assignment uses the same `set` node with a `binop` expression as its `value`,
just as compound variable assignments use `assn` and `binop`.

- Arrays have fixed length, contain values of any supported type, and may be
  nested. Array literals are non-empty, matching the reference text grammar.
- Indices start at zero and must be integers. Reading or writing outside the
  bounds is a runtime error; assignment does not grow an array.
- Strings are mutable UTF-8 byte sequences. Indexing returns a byte as an integer
  from 0 to 255; assigning to an index requires an integer in that range.
  Length counts bytes, so `"Я".length` is 2 and its bytes are 208 and 175.
  Empty strings are supported. Output decodes bytes as UTF-8; invalid byte
  sequences are replaced by the Unicode replacement character.
- Assigning an array or string to another variable shares the value. Mutations
  through either variable are visible through both. Evaluating a literal creates
  a fresh value.
- Arithmetic and logical operations require integers; `+` does not concatenate
  strings. Comparisons support integers, strings, and arrays of the same type.
  Strings compare lexicographically. Arrays compare lengths first, then compare
  elements lexicographically when lengths are equal, matching the reference.
  Nested values compare recursively; incompatible element types are a runtime error.
- `write` prints strings without quotes and arrays as `[value; value]`.
  Cycles encountered during recursive output/comparison are diagnosed instead of
  overflowing the host stack.

Input values can be signed 32-bit integers, double-quoted strings with JSON
escapes, or non-empty arrays with comma-separated elements. Top-level values
can be separated by whitespace (preserving existing integer input files) or
commas. For example:

```text
42, "hello", [1, [2, 3], "abc"]
```

The browser reference currently drops the first and last character of strings
entered in its Input field (for example, `"abc"` is read as `b`). This input bug
is not reproduced: this interpreter reads the full string.

```sh
dotnet run --project src/Interpreter.Cli -- examples/input/strings-arrays/program.json
```

## Sum of an arbitrarily nested array

```sh
dotnet run --project src/Interpreter.Cli -- examples/input/nested-sum/program.json examples/input/nested-sum/input.txt
```

The example prints `21`. Each element is a pair `[0, number]` or
`[1, nested array]`. The root and every nested array contain these pairs.
Replace `input.txt` to sum another valid, acyclic array of this shape.
The sum uses the interpreter's existing signed 32-bit arithmetic, including
wraparound on overflow.

`program.src` documents the same algorithm; the executable input remains
`program.json`. The program traverses the data using a `while` loop and an
explicit stack of `[parent array, next index, previous frame]` arrays. It has
no hard-coded nesting depth and uses space proportional to the current depth.

## Regression tests

Requires Python 3 in addition to the .NET SDK; no extra Python packages:

```sh
python3 tests/regression.py
# After building, or with an explicitly selected SDK:
python3 tests/regression.py --no-build --dotnet /path/to/dotnet
```

Tests exercise old examples, reference AST shapes, aliases, UTF-8 string
mutation, input, type/bounds errors, and the sum program on generated trees
and 1,000 nested arrays.

## Exit codes

`0` — success. `1` — invalid JSON, invalid AST, a runtime error, or an I/O error. `2` — wrong
number of command-line arguments.

## Layout

- `src/Interpreter.Ast` — AST nodes and the JSON parser
- `src/Interpreter.Runtime` — evaluation engine
- `src/Interpreter.Cli` — command-line entry point
