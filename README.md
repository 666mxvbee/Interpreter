# Interpreter

A tree-walking interpreter for a small imperative language. It executes an
already-parsed AST supplied as JSON; parsing source text is out of scope.
Requires .NET SDK 11.0 or later.

## Build and run

```sh
dotnet build
dotnet run --project src/Interpreter.Cli -- <program.json> [input.txt]
```

`program.json` holds the AST. `input.txt` supplies the integers consumed by
`read`; when it is omitted they are taken from stdin. Program output goes to
stdout, diagnostics to stderr. Each command below prints the contents of the
matching file in `examples/output/`.

```sh
dotnet run --project src/Interpreter.Cli -- examples/input/countdown/program.json
dotnet run --project src/Interpreter.Cli -- examples/input/features/program.json examples/input/features/input.txt
```

## Exit codes

`0` — success. `1` — invalid JSON, invalid AST, or an I/O error. `2` — wrong
number of command-line arguments.

## Layout

- `src/Interpreter.Ast` — AST nodes and the JSON parser
- `src/Interpreter.Runtime` — evaluation engine
- `src/Interpreter.Cli` — command-line entry point
