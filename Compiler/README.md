# Compiler

A compiler written in Go.

## Build and Run

    go build -o smcompiler .
    ./smcompiler examples/example.txt 5

The first argument is the path to the program file; the following space-separated arguments are the values supplied to `read()`.
The compilation result is saved to `output/<file name>.json` (the folder is created next to the current
directory). After compilation, the program is executed immediately on the built-in stack machine,
and the values passed to `write()` are printed to stdout, which makes it convenient to check the compiler.

Tests: `go test ./...`

## Structure

| File          | What it does                                                                         |
|---------------|--------------------------------------------------------------------------------------|
| `lexer.go`    | Splits the text into tokens, skips comments `-- ...` and `(* ... *)`                 |
| `ast.go`      | Tree nodes: expressions (`Var`, `Const`, `Binop`) and statements                     |
| `parser.go`   | Recursive descent over the grammar; operator priorities via precedence climbing      |
| `compiler.go` | Generates SM instructions from the AST and prints them as JSON                       |
| `vm.go`       | SM interpreter for running the compiled program                                      |
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

Operator priorities (from lowest to highest): `!!` < `&&` < comparisons < `+ -` < `* / %`.
All operators are left-associative. Comparisons and logical operations yield 1 or 0.
