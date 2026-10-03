package main

import (
	"fmt"
	"strconv"
)

type Kind int

const (
	TokEOF Kind = iota
	TokIdent
	TokConst
	TokKeyword
	TokBinop
	TokAssign
	TokLParen
	TokRParen
	TokLBrace
	TokRBrace
	TokSemi
)

type Token struct {
	Kind Kind
	Text string
	Num  int
	Line int
	Col  int
}

func (t Token) String() string {
	if t.Kind == TokEOF {
		return "end of file"
	}
	return strconv.Quote(t.Text)
}

var keywords = map[string]bool{
	"read": true, "write": true, "while": true, "do": true, "for": true,
	"if": true, "elif": true, "else": true, "skip": true,
}

var binops = []string{"!!", "&&", "==", "!=", "<=", ">=", "<", ">", "+", "-", "*", "/", "%"}

var singles = map[rune]Kind{
	'(': TokLParen, ')': TokRParen, '{': TokLBrace, '}': TokRBrace, ';': TokSemi, '=': TokAssign,
}

type Lexer struct {
	src  []rune
	pos  int
	line int
	col  int
}

func Lex(src string) ([]Token, error) {
	lx := &Lexer{src: []rune(src), line: 1, col: 1}
	var toks []Token
	for {
		tok, err := lx.next()
		if err != nil {
			return nil, err
		}
		toks = append(toks, tok)
		if tok.Kind == TokEOF {
			return toks, nil
		}
	}
}

func (lx *Lexer) peek(off int) rune {
	if lx.pos+off < len(lx.src) {
		return lx.src[lx.pos+off]
	}
	return 0
}

func (lx *Lexer) advance() {
	if lx.src[lx.pos] == '\n' {
		lx.line++
		lx.col = 1
	} else {
		lx.col++
	}
	lx.pos++
}

func (lx *Lexer) hasPrefix(s string) bool {
	for i, r := range []rune(s) {
		if lx.peek(i) != r {
			return false
		}
	}
	return true
}

func (lx *Lexer) skipSpace() error {
	for lx.pos < len(lx.src) {
		switch {
		case isSpace(lx.peek(0)):
			lx.advance()
		case lx.hasPrefix("--"):
			for lx.pos < len(lx.src) && lx.peek(0) != '\n' {
				lx.advance()
			}
		case lx.hasPrefix("(*"):
			line, col := lx.line, lx.col
			lx.advance()
			lx.advance()
			for !lx.hasPrefix("*)") {
				if lx.pos >= len(lx.src) {
					return fmt.Errorf("%d:%d: unterminated comment", line, col)
				}
				lx.advance()
			}
			lx.advance()
			lx.advance()
		default:
			return nil
		}
	}
	return nil
}

func isSpace(c rune) bool { return c == ' ' || c == '\t' || c == '\r' || c == '\n' }
func isDigit(c rune) bool { return c >= '0' && c <= '9' }
func isLower(c rune) bool { return c >= 'a' && c <= 'z' }

func isIdentChar(c rune) bool {
	return isLower(c) || (c >= 'A' && c <= 'Z') || isDigit(c) || c == '_' || c == '\''
}

func (lx *Lexer) next() (Token, error) {
	if err := lx.skipSpace(); err != nil {
		return Token{}, err
	}
	tok := Token{Kind: TokEOF, Line: lx.line, Col: lx.col}
	if lx.pos >= len(lx.src) {
		return tok, nil
	}
	c := lx.peek(0)
	start := lx.pos
	switch {
	case isLower(c):
		for isIdentChar(lx.peek(0)) {
			lx.advance()
		}
		tok.Text = string(lx.src[start:lx.pos])
		tok.Kind = TokIdent
		if keywords[tok.Text] {
			tok.Kind = TokKeyword
		}
		return tok, nil
	case isDigit(c):
		for isDigit(lx.peek(0)) {
			lx.advance()
		}
		tok.Text = string(lx.src[start:lx.pos])
		n, err := strconv.Atoi(tok.Text)
		if err != nil {
			return tok, fmt.Errorf("%d:%d: constant %s is too large", tok.Line, tok.Col, tok.Text)
		}
		tok.Kind, tok.Num = TokConst, n
		return tok, nil
	}
	for _, op := range binops {
		if lx.hasPrefix(op) {
			for range op {
				lx.advance()
			}
			tok.Kind, tok.Text = TokBinop, op
			return tok, nil
		}
	}
	if kind, ok := singles[c]; ok {
		lx.advance()
		tok.Kind, tok.Text = kind, string(c)
		return tok, nil
	}
	return tok, fmt.Errorf("%d:%d: unexpected character %q", tok.Line, tok.Col, c)
}
