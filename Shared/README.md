# Language.Runtime

The common C# value model used by the
[AST interpreter](../Interpreter/README.md) and
[stack machine](../StackMachine/README.md). This library targets `net10.0`
and has no dependency on AST nodes, machine instructions, CLI, or Go code.

## Responsibilities

| Area | API | Purpose |
| --- | --- | --- |
| Values | `RuntimeValue`, `IntegerValue`, `StringValue`, `ArrayValue`, `SExpressionValue` | Represent language values and their mutable aliases |
| Arithmetic | `IntegerArithmetic`, `BinaryOperations` | Apply shared `int32` arithmetic, logic, comparisons, and cons |
| Input | `ValueInputReader` | Parse integers, strings, arrays, constructors, and lists |
| Formatting | `RuntimeValueFormatter` | Produce the common reference-compatible textual output |
| Comparison | `RuntimeValueComparer` | Compare integers, UTF-8 strings, and arrays |
| Errors | `LanguageRuntimeException` | Report controlled language failures with optional causes |
| Limits | `RuntimeLimits.MaximumNestingDepth` | Bound nested runtime work |

`RuntimeValue` exposes `AsInteger()`, `GetLength()`, `GetElement(index)`,
and `SetElement(index, value)`. Unsupported operations throw a language error.
Length is a method because it can fail for values such as integers.

Arrays own their element storage, while referenced child values remain shared.
Strings store mutable UTF-8 bytes. S-expressions store a tag and an array of
mutable arguments. Integer values are immutable. Constructors and literals must
be created anew for each evaluation; variable assignment preserves references.

## Usage

~~~csharp
IReadOnlyList<RuntimeValue> values =
    ValueInputReader.Parse("42, \"abc\", [1, []], Cons(1, Nil), {2, 3}");

RuntimeValue value = values[2];
value.SetElement(0, new IntegerValue(9));
string output = RuntimeValueFormatter.FormatOutput(values);
// 42, "abc", [9, []], Cons (1, Nil), {2, 3}
~~~

`ValueInputReader.Parse` validates the complete input. `Read()` and
`TryRead(out value)` are also available for readers that consume individual
values. The reader does not own or dispose its supplied `TextReader`.

Output formatting does not append a newline. `FormatOutput` joins values with
`, `; `Format` formats one value. Output strings include quotes but do not
JSON-escape their contents. The caller buffers values and publishes output only
after all execution and formatting succeeds.

The reserved lowercase S-expression tags `nil` and `cons` represent lists.
`SExpressionValue.FromList` builds their linked structure; `Cons` and `Nil`
with uppercase initials remain ordinary constructors. Improper lists cannot be
formatted.

## Semantics and limits

- Signed `int32` arithmetic wraps for `+`, `-`, and `*`. Division truncates
  toward zero and handles `int.MinValue / -1` explicitly. Zero divisors are
  controlled errors.
- Strings compare by bytes. Arrays compare length first, then elements.
  Different types and S-expressions cannot be compared.
- Input accepts comma-separated values, with whitespace separators for legacy
  files. Nested containers require commas. Empty arrays, strings, lists, and
  zero-argument constructors are supported; invalid input reports its position.
- Input parsing, formatting, and comparison use explicit work stacks, avoiding
  dependence on the host call stack. Their nesting limit is 4096. A list's
  sequence of tail cells is traversed iteratively during formatting; nesting of
  its element values still counts toward the limit.
- Cycles detected during output or comparison throw `LanguageRuntimeException`.
  Shared children in an acyclic graph do not count as cycles.

## Tests and reference fixtures

From the repository root:

~~~sh
dotnet build Interpreter.slnx
dotnet test Shared/tests/Language.Runtime.Tests/Language.Runtime.Tests.csproj --no-build
python Interpreter/tests/regression.py --no-build
python StackMachine/tests/regression.py --no-build
~~~

The test project covers shared APIs, AST evaluation, machine execution, repeated
machine runs, validation, depth limits, aliasing, and error propagation.

`tests/Language.Runtime.Tests/Fixtures/reference.json` contains 38 saved source
programs with their generated AST, machine instructions, output, and errors.
They were captured from both reference pages on 2026-10-25; the fixture metadata
records page URLs and the reference script's SHA-256 hash. Compatibility tests
exercise both executors against this snapshot without network calls or Go
binaries. The CLI regressions reuse the same cases to check observable output
and exit behavior.
