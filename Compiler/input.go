package main

import "fmt"

func ParseInput(text string) (values []Value, err error) {
	tokens, err := lex(text, true)
	if err != nil {
		return nil, err
	}
	defer func() {
		if r := recover(); r != nil {
			pe, ok := r.(parseError)
			if !ok {
				panic(r)
			}
			values, err = nil, pe
		}
	}()
	p := &Parser{toks: tokens, allowEmptyArrays: true}
	for !p.is(TokEOF, "") {
		start := p.peek()
		expression := p.parsePrimary()
		value, err := literalValue(expression)
		if err != nil {
			return nil, fmt.Errorf("%d:%d: %w", start.Line, start.Col, err)
		}
		values = append(values, value)
		if p.accept(TokComma, "") {
			if p.is(TokEOF, "") {
				p.fail(p.peek(), "expected an input value after comma")
			}
		} else if !p.is(TokEOF, "") && p.toks[p.pos-1].End == p.peek().Start {
			p.fail(p.peek(), "expected a separator after input value")
		}
	}
	return values, nil
}

func literalValue(expression Expr) (Value, error) {
	return literalValueAtDepth(expression, 0)
}

func literalValueAtDepth(expression Expr, depth int) (Value, error) {
	if depth >= maxNestingDepth {
		return nil, fmt.Errorf("input nesting exceeds the limit of %d", maxNestingDepth)
	}
	switch expression := expression.(type) {
	case Const:
		if err := validateInteger(expression.Value); err != nil {
			return nil, err
		}
		return IntegerValue(expression.Value), nil
	case String:
		return &StringValue{Bytes: []byte(expression.Value)}, nil
	case Array:
		elements, err := literalElements(expression.Elements, depth+1)
		if err != nil {
			return nil, err
		}
		return &ArrayValue{Elements: elements}, nil
	case SExpression:
		arguments, err := literalElements(expression.Arguments, depth+1)
		if err != nil {
			return nil, err
		}
		return &SExpressionValue{Name: expression.Name, Arguments: arguments}, nil
	}
	return nil, fmt.Errorf("input must contain literal values")
}

func literalElements(expressions []Expr, depth int) ([]Value, error) {
	values := make([]Value, len(expressions))
	for i, expression := range expressions {
		value, err := literalValueAtDepth(expression, depth)
		if err != nil {
			return nil, err
		}
		values[i] = value
	}
	return values, nil
}
