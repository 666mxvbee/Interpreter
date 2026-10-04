package main

import (
	"fmt"
	"strconv"
)

type parseError struct{ msg string }

func (e parseError) Error() string { return e.msg }

type Parser struct {
	toks []Token
	pos  int
}

func Parse(src string) (prog Stmt, err error) {
	toks, err := Lex(src)
	if err != nil {
		return nil, err
	}
	defer func() {
		if r := recover(); r != nil {
			pe, ok := r.(parseError)
			if !ok {
				panic(r)
			}
			prog, err = nil, pe
		}
	}()
	p := &Parser{toks: toks}
	return p.parseProgram(), nil
}

var kindNames = map[Kind]string{
	TokEOF: "end of file", TokIdent: "identifier", TokConst: "number", TokKeyword: "keyword",
	TokBinop: "operator", TokAssign: `"="`, TokLParen: `"("`, TokRParen: `")"`,
	TokLBrace: `"{"`, TokRBrace: `"}"`, TokSemi: `";"`,
}

func (p *Parser) peek() Token { return p.toks[p.pos] }

func (p *Parser) next() Token {
	t := p.toks[p.pos]
	if t.Kind != TokEOF {
		p.pos++
	}
	return t
}

func (p *Parser) fail(t Token, format string, args ...any) {
	panic(parseError{fmt.Sprintf("%d:%d: ", t.Line, t.Col) + fmt.Sprintf(format, args...)})
}

func (p *Parser) is(kind Kind, text string) bool {
	t := p.peek()
	return t.Kind == kind && (text == "" || t.Text == text)
}

func (p *Parser) accept(kind Kind, text string) bool {
	if p.is(kind, text) {
		p.next()
		return true
	}
	return false
}

func (p *Parser) expect(kind Kind, text string) Token {
	if !p.is(kind, text) {
		want := kindNames[kind]
		if text != "" {
			want = strconv.Quote(text)
		}
		p.fail(p.peek(), "expected %s, got %s", want, p.peek())
	}
	return p.next()
}

func (p *Parser) parseProgram() Stmt {
	prog := p.parseBlock()
	p.expect(TokEOF, "")
	return prog
}

func (p *Parser) parseBlock() Stmt {
	p.expect(TokLBrace, "")
	stmts := []Stmt{p.parseStmt()}
	for !p.is(TokRBrace, "") {
		stmts = append(stmts, p.parseStmt())
	}
	p.expect(TokRBrace, "")
	return seq(stmts)
}

func seq(stmts []Stmt) Stmt {
	s := stmts[len(stmts)-1]
	for i := len(stmts) - 2; i >= 0; i-- {
		s = Seq{First: stmts[i], Second: s}
	}
	return s
}

func (p *Parser) parseStmt() Stmt {
	t := p.peek()
	switch t.Kind {
	case TokLBrace:
		return p.parseBlock()
	case TokIdent:
		return p.parseAssign()
	case TokKeyword:
		switch t.Text {
		case "read":
			return p.parseRead()
		case "write":
			return p.parseWrite()
		case "while":
			return p.parseWhile()
		case "do":
			return p.parseDoWhile()
		case "for":
			return p.parseFor()
		case "if":
			return p.parseIf()
		case "skip":
			p.next()
			p.accept(TokSemi, "")
			return Skip{}
		}
	}
	p.fail(t, "expected statement, got %s", t)
	return nil
}

func (p *Parser) parseRead() Stmt {
	p.next()
	p.expect(TokLParen, "")
	name := p.expect(TokIdent, "").Text
	p.expect(TokRParen, "")
	p.accept(TokSemi, "")
	return Read{Name: name}
}

func (p *Parser) parseWrite() Stmt {
	p.next()
	p.expect(TokLParen, "")
	e := p.parseExpr()
	p.expect(TokRParen, "")
	p.accept(TokSemi, "")
	return Write{Expr: e}
}

func (p *Parser) parseAssign() Stmt {
	name := p.next().Text
	op := ""
	if p.is(TokBinop, "") {
		op = p.next().Text
	}
	p.expect(TokAssign, "")
	e := p.parseExpr()
	p.accept(TokSemi, "")
	if op != "" {
		e = Binop{Op: op, Left: Var{Name: name}, Right: e}
	}
	return Assign{Name: name, Expr: e}
}

func (p *Parser) parseWhile() Stmt {
	p.next()
	p.expect(TokLParen, "")
	cond := p.parseExpr()
	p.expect(TokRParen, "")
	body := p.parseStmt()
	return While{Cond: cond, Body: body}
}

func (p *Parser) parseDoWhile() Stmt {
	p.next()
	body := p.parseStmt()
	p.expect(TokKeyword, "while")
	p.expect(TokLParen, "")
	cond := p.parseExpr()
	p.expect(TokRParen, "")
	p.accept(TokSemi, "")
	return DoWhile{Body: body, Cond: cond}
}

func (p *Parser) parseFor() Stmt {
	p.next()
	p.expect(TokLParen, "")
	initStmt := p.parseStmt()
	cond := p.parseExpr()
	p.expect(TokSemi, "")
	step := p.parseStmt()
	p.expect(TokRParen, "")
	body := p.parseStmt()
	return Seq{First: initStmt, Second: While{Cond: cond, Body: Seq{First: body, Second: step}}}
}

func (p *Parser) parseIf() Stmt {
	p.next() // "if" или "elif"
	p.expect(TokLParen, "")
	cond := p.parseExpr()
	p.expect(TokRParen, "")
	then := p.parseStmt()
	var els Stmt = Skip{}
	if p.accept(TokKeyword, "else") {
		els = p.parseStmt()
	} else if p.is(TokKeyword, "elif") {
		els = p.parseIf()
	}
	return If{Cond: cond, Then: then, Else: els}
}

var precedence = map[string]int{
	"!!": 1,
	"&&": 2,
	"==": 3, "!=": 3, "<": 3, "<=": 3, ">": 3, ">=": 3,
	"+": 4, "-": 4,
	"*": 5, "/": 5, "%": 5,
}

func (p *Parser) parseExpr() Expr {
	return p.parseBinary(1)
}

func (p *Parser) parseBinary(minPrec int) Expr {
	left := p.parsePrimary()
	for p.is(TokBinop, "") && precedence[p.peek().Text] >= minPrec {
		op := p.next().Text
		right := p.parseBinary(precedence[op] + 1)
		left = Binop{Op: op, Left: left, Right: right}
	}
	return left
}

func (p *Parser) parsePrimary() Expr {
	t := p.next()
	switch t.Kind {
	case TokIdent:
		return Var{Name: t.Text}
	case TokConst:
		return Const{Value: t.Num}
	case TokLParen:
		e := p.parseExpr()
		p.expect(TokRParen, "")
		return e
	}
	p.fail(t, "expected expression, got %s", t)
	return nil
}
