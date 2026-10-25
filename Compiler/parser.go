package main

import (
	"fmt"
	"strconv"
)

type parseError struct{ msg string }

func (e parseError) Error() string { return e.msg }

type Parser struct {
	toks             []Token
	pos              int
	allowEmptyArrays bool
	depth            int
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
	TokLBracket: `"["`, TokRBracket: `"]"`, TokComma: `","`, TokDot: `"."`,
	TokString: "string", TokCharacter: "character", TokConstructor: "constructor",
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

func (p *Parser) enter() {
	if p.depth >= maxNestingDepth {
		p.fail(p.peek(), "syntax nesting exceeds the limit of %d", maxNestingDepth)
	}
	p.depth++
}

func (p *Parser) leave() { p.depth-- }

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
	p.enter()
	defer p.leave()
	t := p.peek()
	switch t.Kind {
	case TokLBrace:
		return p.parseBlockOrAssignment()
	case TokIdent, TokConstructor, TokString, TokCharacter, TokConst, TokLParen, TokLBracket:
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

func (p *Parser) parseBlockOrAssignment() Stmt {
	start := p.pos
	var assignment Stmt
	func() {
		defer func() {
			if r := recover(); r != nil {
				if _, ok := r.(parseError); !ok {
					panic(r)
				}
				p.pos = start
			}
		}()
		assignment = p.parseAssign()
	}()
	if assignment != nil {
		return assignment
	}
	return p.parseBlock()
}

func (p *Parser) parseAssign() Stmt {
	target := p.parsePostfix()
	op := ""
	if p.is(TokBinop, "") {
		op = p.next().Text
	}
	p.expect(TokAssign, "")
	value := p.parseExpr()
	p.accept(TokSemi, "")
	if op != "" {
		value = makeBinary(op, target, value)
	}
	switch target := target.(type) {
	case Var:
		return Assign{Name: target.Name, Expr: value}
	case Element:
		return Set{Destination: target.Source, Index: target.Index, Value: value}
	}
	p.fail(p.peek(), "assignment requires a variable or an indexed value")
	return nil
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
	p.enter()
	defer p.leave()
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
	":": 4,
	"+": 5, "-": 5,
	"*": 6, "/": 6, "%": 6,
}

func (p *Parser) parseExpr() Expr {
	return p.parseBinary(1)
}

func makeBinary(op string, left, right Expr) Expr {
	if op == ":" {
		return SExpression{Name: "cons", Arguments: []Expr{left, right}}
	}
	return Binop{Op: op, Left: left, Right: right}
}

func (p *Parser) parseBinary(minPrec int) Expr {
	p.enter()
	defer p.leave()
	left := p.parsePostfix()
	compared := false
	for p.is(TokBinop, "") && precedence[p.peek().Text] >= minPrec {
		token := p.next()
		op := token.Text
		prec := precedence[op]
		if prec == 3 {
			if compared {
				p.fail(token, "comparison operators cannot be chained without parentheses")
			}
			compared = true
		}
		nextPrec := prec + 1
		if op == ":" {
			nextPrec = prec
		}
		right := p.parseBinary(nextPrec)
		left = makeBinary(op, left, right)
	}
	return left
}

func (p *Parser) parsePostfix() Expr {
	value := p.parsePrimary()
	for {
		if p.accept(TokLBracket, "") {
			index := p.parseExpr()
			p.expect(TokRBracket, "")
			value = Element{Source: value, Index: index}
		} else if p.accept(TokDot, "") {
			p.expect(TokKeyword, "length")
			value = Length{Source: value}
		} else {
			return value
		}
	}
}

func (p *Parser) parseItems(closing Kind, allowEmpty bool) []Expr {
	var items []Expr
	if p.is(closing, "") {
		if !allowEmpty {
			p.fail(p.peek(), "expected at least one expression")
		}
		p.next()
		return items
	}
	for {
		items = append(items, p.parseExpr())
		if p.accept(closing, "") {
			return items
		}
		p.expect(TokComma, "")
	}
}

func (p *Parser) parsePrimary() Expr {
	p.enter()
	defer p.leave()
	t := p.next()
	switch t.Kind {
	case TokIdent:
		return Var{Name: t.Text}
	case TokConst, TokCharacter:
		if t.Num < minInteger || t.Num > maxInteger {
			p.fail(t, "constant is outside the signed 32-bit range")
		}
		return Const{Value: int(t.Num)}
	case TokBinop:
		if t.Text == "-" {
			number := p.expect(TokConst, "")
			if t.End != number.Start {
				p.fail(t, "a sign must be adjacent to its integer literal")
			}
			return Const{Value: int(-number.Num)}
		}
		if t.Text == "+" && p.allowEmptyArrays {
			number := p.expect(TokConst, "")
			if t.End != number.Start {
				p.fail(t, "a sign must be adjacent to its integer literal")
			}
			if number.Num > maxInteger {
				p.fail(number, "constant is outside the signed 32-bit range")
			}
			return Const{Value: int(number.Num)}
		}
	case TokString:
		return String{Value: t.Text}
	case TokConstructor:
		var arguments []Expr
		if p.accept(TokLParen, "") {
			arguments = p.parseItems(TokRParen, p.allowEmptyArrays)
		}
		return SExpression{Name: t.Text, Arguments: arguments}
	case TokLBracket:
		return Array{Elements: p.parseItems(TokRBracket, p.allowEmptyArrays)}
	case TokLBrace:
		items := p.parseItems(TokRBrace, true)
		var result Expr = SExpression{Name: "nil"}
		for i := len(items) - 1; i >= 0; i-- {
			result = SExpression{Name: "cons", Arguments: []Expr{items[i], result}}
		}
		return result
	case TokLParen:
		e := p.parseExpr()
		p.expect(TokRParen, "")
		return e
	}
	p.fail(t, "expected expression, got %s", t)
	return nil
}
