# Interpreters

A collection of language-processing projects for a simple imperative language, covering direct AST interpretation, stack-machine execution, and compilation to stack-machine instructions.

## Projects

- [Interpreter](./Interpreter) — .NET interpreter for a simple imperative language that executes programs represented as a JSON AST.
- [StackMachine](./StackMachine) — .NET interpreter for an abstract stack machine that executes programs represented as JSON instruction sequences.
- [Compiler](./Compiler) — A Go compiler that parses source programs, generates JSON instructions for the stack machine, and executes them using a built-in virtual machine.