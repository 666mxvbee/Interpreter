# Compiler

A Go compiler for the [reference language](https://dboulytchev.github.io/eval.html),
with JSON output compatible with the C# stack machine and the
[reference compiler](https://dboulytchev.github.io/sm.html).

## Where the stack machine is

The built-in Go stack machine is implemented in [vm.go](./vm.go), with its
value types in [values.go](./values.go). It is part of this executable and does
not need a separate project. The separate C# implementation is in
[`../StackMachine`](../StackMachine), whose solution is
`../StackMachine/StackMachine.slnx`. Opening only `Interpreter.slnx` does not
include either of these projects.

## Build and Run

    go build -o smcompiler .
    ./smcompiler examples/example.txt 5

The source file is followed by optional input values supplied to `read()`.
Input supports integers, characters, strings, arrays, lists, and constructors.
Quote composite values for the shell, for example `'[1, [2, 3]]'`.

```sh
./smcompiler --compile-only --output output/program.json examples/example.txt
./smcompiler --input examples/composite.input --output output/composite.json examples/composite.txt
```

Use `--compile-only` to write JSON without executing the VM or reading input,
including for a source program containing `read`. Use `--input file` to read
values from a file, or `--input -` to explicitly read stdin. File input and
positional input values are mutually exclusive. Without either input form,
input is empty; programs without `read` need no input. Unused valid input
values are accepted. Use `--` before positional arguments whose names begin
with a dash; negative numeric input arguments work without it.

`--output path` selects the JSON destination, creating parent directories when
needed. The default remains `output/<source base name>.json`, relative to the
current working directory. The destination must differ from the source and
input files. The destination message and all diagnostics go to stderr.

Unless `--compile-only` is specified, the compiled program runs on the built-in
stack machine. On success, `write` values are separated by `, ` and followed
by one newline. With no values, stdout is empty. Execution and formatting
finish before anything is printed: on either error, stdout is empty, even if
the program executed earlier `write` statements.

Exit codes: `0` for success, `1` for file, program, input, or execution errors,
and `2` for invalid command-line arguments.

Tests: `go test ./...`

## Structure

| File          | What it does                                                                         |
|---------------|--------------------------------------------------------------------------------------|
| `lexer.go`    | Splits the text into tokens, skips comments `-- ...` and `(* ... *)`                 |
| `ast.go`      | Tree nodes for scalar and composite expressions, indexing, and statements                     |
| `parser.go`   | Recursive descent over the grammar; operator priorities via precedence climbing      |
| `compiler.go` | Generates SM instructions from the AST and prints them as JSON                       |
| `vm.go`       | SM interpreter for running the compiled program                                      |
| `values.go`   | Runtime values, reference sharing, comparison, indexing, and formatting |
| `input.go`    | Parses literal input values |
| `main.go`     | Parses arguments, reads the file, writes the result                                  |

## How Constructs Are Compiled

The parser reduces some constructs to simpler nodes:

    x op= e             ==> x = x op e
    { s1 s2 s3 }        ==> seq(s1, seq(s2, s3));   { s } ==> s
    for (i; c; st) s    ==> seq(i, while (c) seq(s, st))
    if (c) s            ==> if (c) s else skip
    elif (c) s ...      ==> nested if in the else branch

Code generation. Each statement receives a `next` label — the place where control should go
after it — and returns a `used` flag: whether a jump to `next` was emitted. The caller prints
the label only if `used = true`. `L_end_0` is allocated first for the end of the program;
numbers are assigned consecutively and are consumed by all labels, including unprinted ones.

    read(x)              READ; ST x                                        used = false
    write(e)             <e>; WRITE                                        used = false
    x = e                <e>; ST x                                         used = false
    skip                 nothing                                           used = false
    seq(s1, s2)          <s1 with next=L_seq>; [LABEL L_seq]; <s2 with next>    used = used(s2)
    while (c) s          JMP L_while_cond; LABEL L_while_cody; <s with next=L_while_cond>;
                         LABEL L_while_cond; <c>; JNZ L_while_cody         used = false
    do s while (c)       LABEL L_while_cody; <s with next=L_while_cond>;
                         LABEL L_while_cond; <c>; JNZ L_while_cody         used = false
    if (c) s1 else s2    <c>; JZ L_else; <s1>; JMP next; LABEL L_else; <s2>   used = true
                         (after s2, control falls through to next on its own)

Expressions: `LD` for variables, `CONST` for constants, and for `a op b` — the code for `a`, the code for `b`, then `BINOP op`.

Operator priorities (from lowest to highest): `!!` < `&&` < comparisons < `:` <
`+ -` < `* / %`. The list constructor `:` associates to the right. Comparisons
cannot be chained without parentheses; other operators associate to the left.
Indexing and `.length` bind more tightly than binary operators.
Comparisons and logical operations yield 1 or 0. Both operands are evaluated.
Arithmetic uses signed 32-bit values and wraps on overflow.
Literals and input outside `-2147483648..2147483647` are rejected. Division
truncates toward zero; the remainder has the dividend's sign. The overflow
case `-2147483648 / -1` yields `-2147483648`, and its remainder is zero.
Division or remainder by zero is an execution error.

## Strings, arrays, constructors, and lists

```text
{
    s = "abc";
    s[0] = 'Z';
    a = [1, [2, 3], s];
    a[1][0] += 5;
    node = Node(a, "say ""hello""");
    list = 1 : 2 : {};
    write(node);
    write(list);
}
```

Arrays are non-empty literals with mixed elements and fixed lengths. Strings
are mutable UTF-8 byte sequences: indexing returns a byte integer, mutation
requires 0..255, and `.length` counts bytes. A quote inside a source string is
doubled; backslashes are literal. Character literals support `\n`, `\t`, and
four single quotes for a quote character. Assigning a string, array, or
constructor shares its object.

Constructors have uppercase names and optional arguments. Lists desugar to
`cons(head, tail)` and `nil` S-expressions. Indexing and `.length` work on
constructor arguments. S-expressions, including lists, cannot be compared.
Integers, strings, and arrays compare with values of the same type; array
comparison checks lengths before elements.

New code-generation rules:

| Expression/statement | Instructions |
| --- | --- |
| `"text"` | `STRING text` |
| `'A'` | `CONST 65` |
| `[a, b]` | `<a>; <b>; ARRAY 2` |
| `a[i]` | `<a>; <i>; ELEM` |
| `a.length` | `<a>; LENGTH` |
| `a[i] = x` | `<a>; <i>; <x>; SET` |
| `Node(a, b)` | `<a>; <b>; SEXP Node 2` |

Compound element assignment desugars to `SET` with a binary expression as its
value. Output retains references until execution completes, matching the
reference. Strings are quoted, arrays print as `[1, 2]`, lists as `{1, 2}`, and
constructors as `Node (1, 2)`. Output values are separated by `, ` on one line.

`ARRAY` and `SEXP` accept zero arity in instruction sequences. Source array
literals must be non-empty; input arrays may be empty. Empty strings and
empty lists are accepted. A non-empty list is a `cons` cell with two mutable
arguments, so its `.length` is 2; index 0 is its head and index 1 is its tail.
The uppercase constructors `Cons` and `Nil` are ordinary constructors and
are distinct from the internal lowercase list tags.

The `examples/composite.txt` program uses the input in
`examples/composite.input`. Its expected output is:

```text
[], "Zbc", [1, [7, 3], "Zbc"], Cons (1, Nil), {1, 2}, 2, -2147483648
```

## Errors and nesting limits

Lexer and parser errors include a source line and column. Instruction errors
include a zero-based instruction index. Duplicate labels are rejected before
execution. A missing conditional-jump target is an error only when that jump
is actually taken. Wrong instruction arguments, unknown variables, exhausted
input, stack underflow, invalid indices, and wrong value types return errors.

Recursive parsing, AST compilation, input conversion, array comparison, and
output formatting use a limit of 512 active recursive levels. Parser helper
calls and desugared list cells count toward the relevant limit, so this is
not a promise to accept 512 nested source delimiters. Straight-line statement
sequences and list-tail output traversal are iterative. Programs may construct
deeper values at runtime and traverse them iteratively; recursive comparison
or output that exceeds the limit fails with a diagnostic. Cycles encountered
during comparison or output and malformed list tails also produce errors.

`CompileChecked` and `EncodeProgram` return errors for invalid AST nodes,
constants, arities, and instructions. CLI uses these checked functions.
The original `Compile` and `ToJSON` convenience functions remain available
for callers with valid AST/instructions and panic if that precondition is
violated. `RunValues` handles every runtime type; the original `Run` adapter
accepts and returns integers and rejects input outside the `int32` range.

## Compile the existing nested-sum program

From the Compiler directory:

```sh
./smcompiler --compile-only ../Interpreter/nested-sum.src
```

To compile and execute it with the built-in Go machine:

```sh
./smcompiler --input ../Interpreter/examples/input/nested-sum/input.txt ../Interpreter/nested-sum.src
```

The supplied data has sum `21`. The algorithm remains in the source program;
the compiler and machines do not special-case it.

The emitted `output/nested-sum.json` can be passed to the C# stack machine
after its corresponding instruction/value extensions are integrated.
