package main

import (
	"encoding/json"
	"fmt"
	"strings"
)

type Instr struct {
	Op  string
	Arg any
}

type Compiler struct {
	code   []Instr
	labels int
	depth  int
}

func Compile(prog Stmt) []Instr {
	code, err := CompileChecked(prog)
	if err != nil {
		panic(err)
	}
	return code
}

type compileError struct{ err error }

func (e compileError) Error() string { return e.err.Error() }

func CompileChecked(prog Stmt) (code []Instr, err error) {
	defer func() {
		if recovered := recover(); recovered != nil {
			failure, ok := recovered.(compileError)
			if !ok {
				panic(recovered)
			}
			code, err = nil, failure.err
		}
	}()
	c := &Compiler{}
	end := c.newLabel("end")
	if c.stmt(prog, end) {
		c.emit("LABEL", end)
	}
	return c.code, nil
}

func (c *Compiler) emit(op string, arg any) {
	instruction := Instr{Op: op, Arg: arg}
	if err := validateInstruction(instruction); err != nil {
		panic(compileError{err})
	}
	c.code = append(c.code, instruction)
}

func (c *Compiler) enter() {
	if c.depth >= maxNestingDepth {
		panic(compileError{fmt.Errorf("AST nesting exceeds the limit of %d", maxNestingDepth)})
	}
	c.depth++
}

func (c *Compiler) leave() { c.depth-- }

func (c *Compiler) newLabel(name string) string {
	l := fmt.Sprintf("L_%s_%d", name, c.labels)
	c.labels++
	return l
}

func (c *Compiler) expr(e Expr) {
	c.enter()
	defer c.leave()
	switch e := e.(type) {
	case Var:
		c.emit("LD", e.Name)
	case Const:
		c.emit("CONST", e.Value)
	case String:
		c.emit("STRING", e.Value)
	case Array:
		for _, element := range e.Elements {
			c.expr(element)
		}
		c.emit("ARRAY", len(e.Elements))
	case Element:
		c.expr(e.Source)
		c.expr(e.Index)
		c.emit("ELEM", nil)
	case Length:
		c.expr(e.Source)
		c.emit("LENGTH", nil)
	case SExpression:
		for _, argument := range e.Arguments {
			c.expr(argument)
		}
		c.emit("SEXP", []any{e.Name, len(e.Arguments)})
	case Binop:
		c.expr(e.Left)
		c.expr(e.Right)
		c.emit("BINOP", e.Op)
	default:
		panic(compileError{fmt.Errorf("unsupported expression type %T", e)})
	}
}

func (c *Compiler) stmt(s Stmt, next string) bool {
	c.enter()
	defer c.leave()
	for {
		switch node := s.(type) {
		case Read:
			c.emit("READ", nil)
			c.emit("ST", node.Name)
		case Write:
			c.expr(node.Expr)
			c.emit("WRITE", nil)
		case Assign:
			c.expr(node.Expr)
			c.emit("ST", node.Name)
		case Set:
			c.expr(node.Destination)
			c.expr(node.Index)
			c.expr(node.Value)
			c.emit("SET", nil)
		case Skip:
		case Seq:
			mid := c.newLabel("seq")
			if c.stmt(node.First, mid) {
				c.emit("LABEL", mid)
			}
			s = node.Second
			continue
		case While:
			body, cond := c.newLabel("while_cody"), c.newLabel("while_cond")
			c.emit("JMP", cond)
			c.emit("LABEL", body)
			c.stmt(node.Body, cond)
			c.emit("LABEL", cond)
			c.expr(node.Cond)
			c.emit("JNZ", body)
		case DoWhile:
			body, cond := c.newLabel("while_cody"), c.newLabel("while_cond")
			c.emit("LABEL", body)
			c.stmt(node.Body, cond)
			c.emit("LABEL", cond)
			c.expr(node.Cond)
			c.emit("JNZ", body)
		case If:
			els := c.newLabel("else")
			c.expr(node.Cond)
			c.emit("JZ", els)
			c.stmt(node.Then, next)
			c.emit("JMP", next)
			c.emit("LABEL", els)
			c.stmt(node.Else, next)
			return true
		default:
			panic(compileError{fmt.Errorf("unsupported statement type %T", s)})
		}
		return false
	}
}

func ToJSON(code []Instr) string {
	text, err := EncodeProgram(code)
	if err != nil {
		panic(err)
	}
	return text
}

func EncodeProgram(code []Instr) (string, error) {
	var b strings.Builder
	b.WriteString("[\n")
	for i, in := range code {
		if err := validateInstruction(in); err != nil {
			return "", fmt.Errorf("instruction %d: %w", i, err)
		}
		op, err := jsonArg(in.Op)
		if err != nil {
			return "", fmt.Errorf("instruction %d: %w", i, err)
		}
		b.WriteString("  ")
		if in.Arg == nil {
			b.WriteString(op)
		} else {
			argument, err := jsonArg(in.Arg)
			if err != nil {
				return "", fmt.Errorf("instruction %d: %w", i, err)
			}
			fmt.Fprintf(&b, "{ %s: %s }", op, argument)
		}
		if i < len(code)-1 {
			b.WriteString(",")
		}
		b.WriteString("\n")
	}
	b.WriteString("]\n")
	return b.String(), nil
}

func jsonArg(arg any) (string, error) {
	var builder strings.Builder
	encoder := json.NewEncoder(&builder)
	encoder.SetEscapeHTML(false)
	if err := encoder.Encode(arg); err != nil {
		return "", err
	}
	return strings.TrimSuffix(builder.String(), "\n"), nil
}

func validateInteger(number int) error {
	if int64(number) < minInteger || int64(number) > maxInteger {
		return fmt.Errorf("integer %d is outside the signed 32-bit range", number)
	}
	return nil
}

func validateArity(count int) error {
	if count < 0 || int64(count) > maxInteger {
		return fmt.Errorf("arity %d must be between 0 and %d", count, maxInteger)
	}
	return nil
}

func validateInstruction(in Instr) error {
	switch in.Op {
	case "READ", "WRITE", "LENGTH", "ELEM", "SET":
		if in.Arg != nil {
			return fmt.Errorf("%s takes no argument", in.Op)
		}
	case "CONST":
		number, ok := in.Arg.(int)
		if !ok {
			return fmt.Errorf("CONST requires an integer")
		}
		return validateInteger(number)
	case "STRING", "LD", "ST", "LABEL", "JMP", "JZ", "JNZ":
		if _, ok := in.Arg.(string); !ok {
			return fmt.Errorf("%s requires a string argument", in.Op)
		}
	case "BINOP":
		op, ok := in.Arg.(string)
		if !ok {
			return fmt.Errorf("BINOP requires an operator")
		}
		switch op {
		case "+", "-", "*", "/", "%", "==", "!=", "<", "<=", ">", ">=", "&&", "!!":
		default:
			return fmt.Errorf("unknown operator %q", op)
		}
	case "ARRAY":
		count, ok := in.Arg.(int)
		if !ok {
			return fmt.Errorf("ARRAY requires an integer arity")
		}
		return validateArity(count)
	case "SEXP":
		arguments, ok := in.Arg.([]any)
		if !ok || len(arguments) != 2 {
			return fmt.Errorf("SEXP requires a name and an integer arity")
		}
		if _, ok := arguments[0].(string); !ok {
			return fmt.Errorf("SEXP requires a string name")
		}
		count, ok := arguments[1].(int)
		if !ok {
			return fmt.Errorf("SEXP requires an integer arity")
		}
		return validateArity(count)
	default:
		return fmt.Errorf("unknown instruction %q", in.Op)
	}
	return nil
}
