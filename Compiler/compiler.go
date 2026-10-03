package main

import (
	"fmt"
	"strconv"
	"strings"
)

type Instr struct {
	Op  string
	Arg any
}

type Compiler struct {
	code   []Instr
	labels int
}

func Compile(prog Stmt) []Instr {
	c := &Compiler{}
	end := c.newLabel("end")
	if c.stmt(prog, end) {
		c.emit("LABEL", end)
	}
	return c.code
}

func (c *Compiler) emit(op string, arg any) {
	c.code = append(c.code, Instr{Op: op, Arg: arg})
}

func (c *Compiler) newLabel(name string) string {
	l := fmt.Sprintf("L_%s_%d", name, c.labels)
	c.labels++
	return l
}

func (c *Compiler) expr(e Expr) {
	switch e := e.(type) {
	case Var:
		c.emit("LD", e.Name)
	case Const:
		c.emit("CONST", e.Value)
	case Binop:
		c.expr(e.Left)
		c.expr(e.Right)
		c.emit("BINOP", e.Op)
	}
}

func (c *Compiler) stmt(s Stmt, next string) bool {
	switch s := s.(type) {
	case Read:
		c.emit("READ", nil)
		c.emit("ST", s.Name)
	case Write:
		c.expr(s.Expr)
		c.emit("WRITE", nil)
	case Assign:
		c.expr(s.Expr)
		c.emit("ST", s.Name)
	case Skip:
	case Seq:
		mid := c.newLabel("seq")
		if c.stmt(s.First, mid) {
			c.emit("LABEL", mid)
		}
		return c.stmt(s.Second, next)
	case While:
		body, cond := c.newLabel("while_cody"), c.newLabel("while_cond")
		c.emit("JMP", cond)
		c.emit("LABEL", body)
		c.stmt(s.Body, cond)
		c.emit("LABEL", cond)
		c.expr(s.Cond)
		c.emit("JNZ", body)
	case DoWhile:
		body, cond := c.newLabel("while_cody"), c.newLabel("while_cond")
		c.emit("LABEL", body)
		c.stmt(s.Body, cond)
		c.emit("LABEL", cond)
		c.expr(s.Cond)
		c.emit("JNZ", body)
	case If:
		els := c.newLabel("else")
		c.expr(s.Cond)
		c.emit("JZ", els)
		c.stmt(s.Then, next)
		c.emit("JMP", next)
		c.emit("LABEL", els)
		c.stmt(s.Else, next)
		return true
	}
	return false
}

func ToJSON(code []Instr) string {
	var b strings.Builder
	b.WriteString("[\n")
	for i, in := range code {
		b.WriteString("  ")
		if in.Arg == nil {
			b.WriteString(strconv.Quote(in.Op))
		} else {
			fmt.Fprintf(&b, "{ %s: %s }", strconv.Quote(in.Op), jsonArg(in.Arg))
		}
		if i < len(code)-1 {
			b.WriteString(",")
		}
		b.WriteString("\n")
	}
	b.WriteString("]\n")
	return b.String()
}

func jsonArg(arg any) string {
	switch v := arg.(type) {
	case int:
		return strconv.Itoa(v)
	case string:
		return strconv.Quote(v)
	}
	return "null"
}
