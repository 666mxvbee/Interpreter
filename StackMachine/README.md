# StackMachine

A small stack-based virtual machine written in C# (.NET 10). It runs programs described
as JSON arrays of instructions, reads 32-bit integers from the input and prints the
values the program writes.

## Build and run

Requires the [.NET SDK 10.0](https://dotnet.microsoft.com/download).

```bash
dotnet build
dotnet run --project src/StackMachine.Cli -- <program.json> [input.txt]
```

The input file contains comma-separated integers (e.g. `-7, 2`). If it is omitted,
the input is read from stdin:

```bash
dotnet run --project src/StackMachine.Cli -- examples/input/sum.json examples/input/sum.txt
# 15
echo "-7, 2" | dotnet run --project src/StackMachine.Cli -- examples/input/binops.json
# -3; -1; 1; 1; 1; 0
```

Exit codes: `0` — success, `1` — invalid program or input, or a runtime error
(the message goes to stderr), `2` — wrong arguments.

## Instruction set

| Instruction                      | Effect                                          |
|----------------------------------|-------------------------------------------------|
| `"READ"` / `"WRITE"`             | push the next input value / pop and output it   |
| `{"CONST": n}`                   | push the integer `n`                            |
| `{"LD": "x"}` / `{"ST": "x"}`    | push variable `x` / pop a value into `x`        |
| `{"BINOP": "op"}`                | pop `b`, pop `a`, push `a op b`                 |
| `{"LABEL": "L"}` / `{"JMP": "L"}`| mark a jump target / jump to label `L`          |
| `{"JZ": "L"}` / `{"JNZ": "L"}`   | pop a value, jump if it is zero / non-zero      |

Operators: `+ - * / %`, `== != < <= > >=`, `&&` and `!!` (logical OR). Comparisons and
logical operators return `1` or `0`.

## Project structure

- `src/StackMachine.Core` — instruction model, JSON and input parsers, execution engine
- `src/StackMachine.Cli` — command-line entry point
- `examples/` — sample programs with inputs (`input/`) and expected outputs (`output/`)

Licensed under the MIT License.