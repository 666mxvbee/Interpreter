package main

import (
	"bytes"
	"fmt"
	"strconv"
	"strings"
	"unicode/utf8"
)

type Value interface{ isValue() }
type IntegerValue int32
type StringValue struct{ Bytes []byte }
type ArrayValue struct{ Elements []Value }
type SExpressionValue struct {
	Name      string
	Arguments []Value
}

func (IntegerValue) isValue()      {}
func (*StringValue) isValue()      {}
func (*ArrayValue) isValue()       {}
func (*SExpressionValue) isValue() {}

func validateValue(value Value) error {
	switch value := value.(type) {
	case IntegerValue:
		return nil
	case *StringValue:
		if value != nil {
			return nil
		}
	case *ArrayValue:
		if value != nil {
			return nil
		}
	case *SExpressionValue:
		if value != nil {
			return nil
		}
	}
	return fmt.Errorf("invalid runtime value")
}

func asInteger(value Value) (int32, error) {
	number, ok := value.(IntegerValue)
	if !ok {
		return 0, fmt.Errorf("expected an integer value")
	}
	return int32(number), nil
}

func valueLength(value Value) (int, error) {
	if err := validateValue(value); err != nil {
		return 0, err
	}
	switch value := value.(type) {
	case *StringValue:
		return len(value.Bytes), nil
	case *ArrayValue:
		return len(value.Elements), nil
	case *SExpressionValue:
		return len(value.Arguments), nil
	}
	return 0, fmt.Errorf("length requires an array, string, or S-expression")
}

func checkIndex(value Value, index int32) error {
	length, err := valueLength(value)
	if err != nil {
		return err
	}
	if index < 0 || int64(index) >= int64(length) {
		return fmt.Errorf("index %d is out of bounds for length %d", index, length)
	}
	return nil
}

func getElement(value Value, index int32) (Value, error) {
	if err := checkIndex(value, index); err != nil {
		return nil, err
	}
	switch value := value.(type) {
	case *StringValue:
		return IntegerValue(value.Bytes[index]), nil
	case *ArrayValue:
		return value.Elements[index], nil
	case *SExpressionValue:
		return value.Arguments[index], nil
	}
	return nil, fmt.Errorf("indexing requires an array, string, or S-expression")
}

func setElement(destination Value, index int32, value Value) error {
	if err := validateValue(value); err != nil {
		return err
	}
	if err := checkIndex(destination, index); err != nil {
		return err
	}
	switch destination := destination.(type) {
	case *StringValue:
		number, err := asInteger(value)
		if err != nil {
			return err
		}
		if number < 0 || number > 255 {
			return fmt.Errorf("string byte must be between 0 and 255")
		}
		destination.Bytes[index] = byte(number)
	case *ArrayValue:
		destination.Elements[index] = value
	case *SExpressionValue:
		destination.Arguments[index] = value
	default:
		return fmt.Errorf("element assignment requires an array, string, or S-expression")
	}
	return nil
}

func compareValues(left, right Value, active map[[2]*ArrayValue]bool) (int, error) {
	if err := validateValue(left); err != nil {
		return 0, err
	}
	if err := validateValue(right); err != nil {
		return 0, err
	}
	switch left := left.(type) {
	case IntegerValue:
		right, ok := right.(IntegerValue)
		if !ok {
			return 0, fmt.Errorf("comparison requires values of the same type")
		}
		if left < right {
			return -1, nil
		}
		if left > right {
			return 1, nil
		}
		return 0, nil
	case *StringValue:
		right, ok := right.(*StringValue)
		if !ok {
			return 0, fmt.Errorf("comparison requires values of the same type")
		}
		return bytes.Compare(left.Bytes, right.Bytes), nil
	case *ArrayValue:
		right, ok := right.(*ArrayValue)
		if !ok {
			return 0, fmt.Errorf("comparison requires values of the same type")
		}
		if len(left.Elements) < len(right.Elements) {
			return -1, nil
		}
		if len(left.Elements) > len(right.Elements) {
			return 1, nil
		}
		pair := [2]*ArrayValue{left, right}
		if active[pair] {
			return 0, fmt.Errorf("cannot compare cyclic arrays")
		}
		if len(active) >= maxNestingDepth {
			return 0, fmt.Errorf("comparison nesting exceeds the limit of %d", maxNestingDepth)
		}
		active[pair] = true
		defer delete(active, pair)
		for i, element := range left.Elements {
			comparison, err := compareValues(element, right.Elements[i], active)
			if err != nil || comparison != 0 {
				return comparison, err
			}
		}
		return 0, nil
	}
	return 0, fmt.Errorf("S-expressions cannot be compared")
}

func binaryValue(op string, left, right Value) (Value, error) {
	switch op {
	case "==", "!=", "<", "<=", ">", ">=":
		comparison, err := compareValues(left, right, make(map[[2]*ArrayValue]bool))
		if err != nil {
			return nil, err
		}
		var result bool
		switch op {
		case "==":
			result = comparison == 0
		case "!=":
			result = comparison != 0
		case "<":
			result = comparison < 0
		case "<=":
			result = comparison <= 0
		case ">":
			result = comparison > 0
		case ">=":
			result = comparison >= 0
		}
		return IntegerValue(boolToInt(result)), nil
	}
	a, err := asInteger(left)
	if err != nil {
		return nil, err
	}
	b, err := asInteger(right)
	if err != nil {
		return nil, err
	}
	result, err := binop(op, int(a), int(b))
	if err != nil {
		return nil, err
	}
	return IntegerValue(result), nil
}

func formatValue(value Value) (string, error) {
	return formatActiveValue(value, make(map[Value]bool), 0)
}

func formatActiveValue(value Value, active map[Value]bool, depth int) (string, error) {
	if err := validateValue(value); err != nil {
		return "", err
	}
	if depth >= maxNestingDepth {
		return "", fmt.Errorf("output nesting exceeds the limit of %d", maxNestingDepth)
	}
	switch scalar := value.(type) {
	case IntegerValue:
		return strconv.FormatInt(int64(scalar), 10), nil
	case *StringValue:
		return "\"" + decodeUTF8Bytes(scalar.Bytes) + "\"", nil
	}
	if active[value] {
		return "", fmt.Errorf("cannot write a cyclic value")
	}
	active[value] = true
	defer delete(active, value)
	switch value := value.(type) {
	case *ArrayValue:
		elements, err := formatElements(value.Elements, active, depth+1)
		if err != nil {
			return "", err
		}
		return "[" + elements + "]", nil
	case *SExpressionValue:
		if value.Name == "nil" && len(value.Arguments) == 0 {
			return "{}", nil
		}
		if value.Name == "cons" && len(value.Arguments) == 2 {
			return formatList(value, active, depth+1)
		}
		if len(value.Arguments) == 0 {
			return value.Name, nil
		}
		arguments, err := formatElements(value.Arguments, active, depth+1)
		if err != nil {
			return "", err
		}
		return value.Name + " (" + arguments + ")", nil
	}
	return "", fmt.Errorf("unknown runtime value")
}

func decodeUTF8Bytes(data []byte) string {
	var result strings.Builder
	for len(data) > 0 {
		character, size := utf8.DecodeRune(data)
		if character != utf8.RuneError || size > 1 {
			result.Write(data[:size])
			data = data[size:]
			continue
		}

		// Replace each maximal invalid subpart, including a truncated valid prefix.
		first := data[0]
		if first >= 0xC2 && first <= 0xF4 {
			expected := 2
			if first >= 0xE0 {
				expected = 3
			}
			if first >= 0xF0 {
				expected = 4
			}
			for size < expected && size < len(data) {
				lower, upper := byte(0x80), byte(0xBF)
				if size == 1 {
					switch first {
					case 0xE0:
						lower = 0xA0
					case 0xED:
						upper = 0x9F
					case 0xF0:
						lower = 0x90
					case 0xF4:
						upper = 0x8F
					}
				}
				if data[size] < lower || data[size] > upper {
					break
				}
				size++
			}
		}
		result.WriteRune(utf8.RuneError)
		data = data[size:]
	}
	return result.String()
}

func formatElements(values []Value, active map[Value]bool, depth int) (string, error) {
	parts := make([]string, len(values))
	for i, value := range values {
		text, err := formatActiveValue(value, active, depth)
		if err != nil {
			return "", err
		}
		parts[i] = text
	}
	return strings.Join(parts, ", "), nil
}

func formatList(cell *SExpressionValue, active map[Value]bool, depth int) (string, error) {
	var parts []string
	var tails []*SExpressionValue
	defer func() {
		for _, tail := range tails {
			delete(active, tail)
		}
	}()
	for {
		head, err := formatActiveValue(cell.Arguments[0], active, depth)
		if err != nil {
			return "", err
		}
		parts = append(parts, head)
		tail, ok := cell.Arguments[1].(*SExpressionValue)
		if !ok || tail == nil {
			return "", fmt.Errorf("list tail must be a list")
		}
		if tail.Name == "nil" && len(tail.Arguments) == 0 {
			return "{" + strings.Join(parts, ", ") + "}", nil
		}
		if tail.Name != "cons" || len(tail.Arguments) != 2 {
			return "", fmt.Errorf("list tail must be a list")
		}
		if active[tail] {
			return "", fmt.Errorf("cannot write a cyclic list")
		}
		active[tail] = true
		tails = append(tails, tail)
		cell = tail
	}
}

func FormatOutput(values []Value) (string, error) {
	parts := make([]string, len(values))
	for i, value := range values {
		text, err := formatValue(value)
		if err != nil {
			return "", fmt.Errorf("output value %d: %w", i, err)
		}
		parts[i] = text
	}
	if len(parts) == 0 {
		return "", nil
	}
	return strings.Join(parts, ", ") + "\n", nil
}
