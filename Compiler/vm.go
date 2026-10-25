package main

import "fmt"

func Run(code []Instr, input []int) ([]int, error) {
	values := make([]Value, len(input))
	for i, number := range input {
		if err := validateInteger(number); err != nil {
			return nil, fmt.Errorf("input value %d: %w", i, err)
		}
		values[i] = IntegerValue(number)
	}
	result, err := RunValues(code, values)
	if err != nil {
		return nil, err
	}
	var output []int
	for _, value := range result {
		number, conversionError := asInteger(value)
		if conversionError != nil {
			return nil, conversionError
		}
		output = append(output, int(number))
	}
	return output, nil
}

func RunValues(code []Instr, input []Value) (output []Value, err error) {
	labels := map[string]int{}
	for i, instruction := range code {
		if instruction.Op == "LABEL" {
			if err := validateInstruction(instruction); err != nil {
				return nil, fmt.Errorf("instruction %d: %w", i, err)
			}
			name, ok := instruction.Arg.(string)
			if !ok {
				return nil, fmt.Errorf("instruction %d: LABEL requires a name", i)
			}
			if _, exists := labels[name]; exists {
				return nil, fmt.Errorf("instruction %d: duplicate label %s", i, name)
			}
			labels[name] = i
		}
	}
	variables := map[string]Value{}
	var stack []Value
	pc := 0
	defer func() {
		if err != nil {
			output = nil
			err = fmt.Errorf("instruction %d: %w", pc, err)
		}
	}()
	pop := func() (Value, error) {
		if len(stack) == 0 {
			return nil, fmt.Errorf("stack is empty")
		}
		value := stack[len(stack)-1]
		stack = stack[:len(stack)-1]
		return value, nil
	}
	popInteger := func() (int32, error) {
		value, err := pop()
		if err != nil {
			return 0, err
		}
		return asInteger(value)
	}
	popElements := func(count int) ([]Value, error) {
		if count < 0 || count > len(stack) {
			return nil, fmt.Errorf("invalid element count or exhausted stack")
		}
		start := len(stack) - count
		elements := append([]Value(nil), stack[start:]...)
		stack = stack[:start]
		return elements, nil
	}
	jump := func(argument any) (int, error) {
		label, ok := argument.(string)
		if !ok {
			return 0, fmt.Errorf("jump requires a label")
		}
		target, ok := labels[label]
		if !ok {
			return 0, fmt.Errorf("unknown label %s", label)
		}
		return target, nil
	}
	for pc = 0; pc < len(code); pc++ {
		instruction := code[pc]
		if err := validateInstruction(instruction); err != nil {
			return nil, err
		}
		switch instruction.Op {
		case "READ":
			if len(input) == 0 {
				return output, fmt.Errorf("READ: not enough input values")
			}
			if err := validateValue(input[0]); err != nil {
				return nil, err
			}
			stack = append(stack, input[0])
			input = input[1:]
		case "WRITE":
			value, err := pop()
			if err != nil {
				return output, err
			}
			output = append(output, value)
		case "LD":
			name, ok := instruction.Arg.(string)
			if !ok {
				return output, fmt.Errorf("LD requires a variable name")
			}
			value, ok := variables[name]
			if !ok {
				return output, fmt.Errorf("LD: variable %s is not defined", name)
			}
			stack = append(stack, value)
		case "ST":
			name, ok := instruction.Arg.(string)
			if !ok {
				return output, fmt.Errorf("ST requires a variable name")
			}
			value, err := pop()
			if err != nil {
				return output, err
			}
			variables[name] = value
		case "CONST":
			number, ok := instruction.Arg.(int)
			if !ok {
				return output, fmt.Errorf("CONST requires an integer")
			}
			stack = append(stack, IntegerValue(number))
		case "STRING":
			text, ok := instruction.Arg.(string)
			if !ok {
				return output, fmt.Errorf("STRING requires a string")
			}
			stack = append(stack, &StringValue{Bytes: []byte(text)})
		case "ARRAY":
			count, ok := instruction.Arg.(int)
			if !ok {
				return output, fmt.Errorf("ARRAY requires an element count")
			}
			elements, err := popElements(count)
			if err != nil {
				return output, err
			}
			stack = append(stack, &ArrayValue{Elements: elements})
		case "SEXP":
			args, ok := instruction.Arg.([]any)
			if !ok || len(args) != 2 {
				return output, fmt.Errorf("SEXP requires a name and an argument count")
			}
			name, nameOK := args[0].(string)
			count, countOK := args[1].(int)
			if !nameOK || !countOK {
				return output, fmt.Errorf("invalid SEXP arguments")
			}
			arguments, err := popElements(count)
			if err != nil {
				return output, err
			}
			stack = append(stack, &SExpressionValue{Name: name, Arguments: arguments})
		case "LENGTH":
			value, err := pop()
			if err != nil {
				return output, err
			}
			length, err := valueLength(value)
			if err != nil {
				return output, err
			}
			if err := validateInteger(length); err != nil {
				return nil, err
			}
			stack = append(stack, IntegerValue(length))
		case "ELEM":
			index, err := popInteger()
			if err != nil {
				return output, err
			}
			source, err := pop()
			if err != nil {
				return output, err
			}
			value, err := getElement(source, index)
			if err != nil {
				return output, err
			}
			stack = append(stack, value)
		case "SET":
			value, err := pop()
			if err != nil {
				return output, err
			}
			index, err := popInteger()
			if err != nil {
				return output, err
			}
			destination, err := pop()
			if err != nil {
				return output, err
			}
			if err := setElement(destination, index, value); err != nil {
				return output, err
			}
		case "BINOP":
			op, ok := instruction.Arg.(string)
			if !ok {
				return output, fmt.Errorf("BINOP requires an operator")
			}
			right, err := pop()
			if err != nil {
				return output, err
			}
			left, err := pop()
			if err != nil {
				return output, err
			}
			value, err := binaryValue(op, left, right)
			if err != nil {
				return output, err
			}
			stack = append(stack, value)
		case "LABEL":
		case "JMP":
			target, err := jump(instruction.Arg)
			if err != nil {
				return output, err
			}
			pc = target
		case "JZ", "JNZ":
			condition, err := popInteger()
			if err != nil {
				return output, err
			}
			if (instruction.Op == "JZ" && condition == 0) || (instruction.Op == "JNZ" && condition != 0) {
				target, err := jump(instruction.Arg)
				if err != nil {
					return output, err
				}
				pc = target
			}
		default:
			return output, fmt.Errorf("unknown instruction %s", instruction.Op)
		}
	}
	return output, nil
}

func binop(op string, a, b int) (int, error) {
	if err := validateInteger(a); err != nil {
		return 0, err
	}
	if err := validateInteger(b); err != nil {
		return 0, err
	}
	switch op {
	case "+":
		return int(int32(a) + int32(b)), nil
	case "-":
		return int(int32(a) - int32(b)), nil
	case "*":
		return int(int32(a) * int32(b)), nil
	case "/":
		if b == 0 {
			return 0, fmt.Errorf("division by zero")
		}
		return int(int32(a) / int32(b)), nil
	case "%":
		if b == 0 {
			return 0, fmt.Errorf("division by zero")
		}
		return int(int32(a) % int32(b)), nil
	case "==":
		return boolToInt(a == b), nil
	case "!=":
		return boolToInt(a != b), nil
	case "<":
		return boolToInt(a < b), nil
	case "<=":
		return boolToInt(a <= b), nil
	case ">":
		return boolToInt(a > b), nil
	case ">=":
		return boolToInt(a >= b), nil
	case "&&":
		return boolToInt(a != 0 && b != 0), nil
	case "!!":
		return boolToInt(a != 0 || b != 0), nil
	}
	return 0, fmt.Errorf("unknown operator %s", op)
}

func boolToInt(b bool) int {
	if b {
		return 1
	}
	return 0
}
