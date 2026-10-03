package main

import (
	"fmt"
	"strconv"
)

// parseError — ошибка разбора. Внутри парсера она бросается через panic,
// а Parse ловит её через recover и возвращает как обычную ошибку.
type parseError struct{ msg string }

func (e parseError) Error() string { return e.msg }

// Parser — парсер рекурсивного спуска по списку токенов.
type Parser struct {
	toks []Token
	pos  int
}

// Parse разбирает текст программы и возвращает её AST.
func Parse(src string) (prog Stmt, err error) {
	toks, err := Lex(src)
	if err != nil {
		return nil, err
	}
	defer func() {
		if r := recover(); r != nil {
			pe, ok := r.(parseError)
			if !ok {
				panic(r) // не наша ошибка — пробрасываем дальше
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

// next возвращает текущий токен и переходит к следующему (на EOF не двигается).
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

// is проверяет вид (и, если text != "", текст) текущего токена.
func (p *Parser) is(kind Kind, text string) bool {
	t := p.peek()
	return t.Kind == kind && (text == "" || t.Text == text)
}

// accept съедает текущий токен, если он подходит.
func (p *Parser) accept(kind Kind, text string) bool {
	if p.is(kind, text) {
		p.next()
		return true
	}
	return false
}

// expect требует токен указанного вида, иначе сообщает об ошибке.
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

// program: "{" stmt+ "}"
func (p *Parser) parseProgram() Stmt {
	prog := p.parseBlock()
	p.expect(TokEOF, "") // после программы ничего быть не должно
	return prog
}

// "{" stmt+ "}"  ==>  seq(s1, seq(s2, s3)); блок из одного оператора — сам оператор
func (p *Parser) parseBlock() Stmt {
	p.expect(TokLBrace, "")
	stmts := []Stmt{p.parseStmt()}
	for !p.is(TokRBrace, "") {
		stmts = append(stmts, p.parseStmt())
	}
	p.expect(TokRBrace, "")
	return seq(stmts)
}

// seq сворачивает список операторов в правовложенную цепочку Seq.
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

// "read" "(" IDENT ")" ";"?
func (p *Parser) parseRead() Stmt {
	p.next()
	p.expect(TokLParen, "")
	name := p.expect(TokIdent, "").Text
	p.expect(TokRParen, "")
	p.accept(TokSemi, "")
	return Read{Name: name}
}

// "write" "(" expr ")" ";"?
func (p *Parser) parseWrite() Stmt {
	p.next()
	p.expect(TokLParen, "")
	e := p.parseExpr()
	p.expect(TokRParen, "")
	p.accept(TokSemi, "")
	return Write{Expr: e}
}

// IDENT "=" expr ";"?  |  IDENT BINOP "=" expr ";"?
func (p *Parser) parseAssign() Stmt {
	name := p.next().Text
	op := ""
	if p.is(TokBinop, "") {
		op = p.next().Text // составное присваивание: x op= e
	}
	p.expect(TokAssign, "")
	e := p.parseExpr()
	p.accept(TokSemi, "")
	if op != "" {
		e = Binop{Op: op, Left: Var{Name: name}, Right: e} // x op= e  ==>  x = x op e
	}
	return Assign{Name: name, Expr: e}
}

// "while" "(" expr ")" stmt
func (p *Parser) parseWhile() Stmt {
	p.next()
	p.expect(TokLParen, "")
	cond := p.parseExpr()
	p.expect(TokRParen, "")
	body := p.parseStmt()
	return While{Cond: cond, Body: body}
}

// "do" stmt "while" "(" expr ")" ";"?
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

// "for" "(" stmt expr ";" stmt ")" stmt  ==>  seq(init, while (cond) seq(body, step))
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

// "if" "(" expr ")" stmt elsePart?
// elsePart: "else" stmt | "elif" "(" expr ")" stmt elsePart?
// "elif" разбирается этой же функцией и становится вложенным If в ветке Else.
func (p *Parser) parseIf() Stmt {
	p.next() // "if" или "elif"
	p.expect(TokLParen, "")
	cond := p.parseExpr()
	p.expect(TokRParen, "")
	then := p.parseStmt()
	var els Stmt = Skip{} // if без else — это if (c) s else skip
	if p.accept(TokKeyword, "else") {
		els = p.parseStmt()
	} else if p.is(TokKeyword, "elif") {
		els = p.parseIf()
	}
	return If{Cond: cond, Then: then, Else: els}
}

// Приоритеты операторов: чем больше число, тем сильнее оператор связывает операнды.
var precedence = map[string]int{
	"!!": 1,
	"&&": 2,
	"==": 3, "!=": 3, "<": 3, "<=": 3, ">": 3, ">=": 3,
	"+": 4, "-": 4,
	"*": 5, "/": 5, "%": 5,
}

// expr: IDENT | CONST | expr BINOP expr | "(" expr ")"
func (p *Parser) parseExpr() Expr {
	return p.parseBinary(1)
}

// parseBinary разбирает цепочку операторов с приоритетом не ниже minPrec
// (precedence climbing). Все операторы левоассоциативны.
func (p *Parser) parseBinary(minPrec int) Expr {
	left := p.parsePrimary()
	for p.is(TokBinop, "") && precedence[p.peek().Text] >= minPrec {
		op := p.next().Text
		right := p.parseBinary(precedence[op] + 1)
		left = Binop{Op: op, Left: left, Right: right}
	}
	return left
}

// IDENT | CONST | "(" expr ")"
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
