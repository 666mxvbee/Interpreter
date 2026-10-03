package main

import "fmt"

func Run(code []Instr, input []int) ([]int, error) {
	labels := map[string]int{}
	for i, in := range code {
		if in.Op == "LABEL" {
			labels[in.Arg.(string)] = i
		}
	}
	vars := map[string]int{}
	var stack, output []int

	pop := func() (int, error) {
		if len(stack) == 0 {
			return 0, fmt.Errorf("stack is empty")
		}
		v := stack[len(stack)-1]
		stack = stack[:len(stack)-1]
		return v, nil
	}
	jump := func(label string) (int, error) {
		pc, ok := labels[label]
		if !ok {
			return 0, fmt.Errorf("unknown label %s", label)
		}
		return pc, nil
	}

	for pc := 0; pc < len(code); pc++ {
		in := code[pc]
		switch in.Op {
		case "READ":
			if len(input) == 0 {
				return output, fmt.Errorf("READ: not enough input values")
			}
			stack = append(stack, input[0])
			input = input[1:]
		case "WRITE":
			v, err := pop()
			if err != nil {
				return output, err
			}
			output = append(output, v)
		case "LD":
			name := in.Arg.(string)
			v, ok := vars[name]
			if !ok {
				return output, fmt.Errorf("LD: variable %s is not defined", name)
			}
			stack = append(stack, v)
		case "ST":
			v, err := pop()
			if err != nil {
				return output, err
			}
			vars[in.Arg.(string)] = v
		case "CONST":
			stack = append(stack, in.Arg.(int))
		case "BINOP":
			b, err := pop()
			if err != nil {
				return output, err
			}
			a, err := pop()
			if err != nil {
				return output, err
			}
			v, err := binop(in.Arg.(string), a, b)
			if err != nil {
				return output, err
			}
			stack = append(stack, v)
		case "LABEL":
		case "JMP":
			target, err := jump(in.Arg.(string))
			if err != nil {
				return output, err
			}
			pc = target
		case "JZ", "JNZ":
			v, err := pop()
			if err != nil {
				return output, err
			}
			jz := in.Op == "JZ"
			if (jz && v == 0) || (!jz && v != 0) {
				target, err := jump(in.Arg.(string))
				if err != nil {
					return output, err
				}
				pc = target
			}
		default:
			return output, fmt.Errorf("unknown instruction %s", in.Op)
		}
	}
	return output, nil
}

func binop(op string, a, b int) (int, error) {
	switch op {
	case "+":
		return a + b, nil
	case "-":
		return a - b, nil
	case "*":
		return a * b, nil
	case "/":
		if b == 0 {
			return 0, fmt.Errorf("division by zero")
		}
		return a / b, nil
	case "%":
		if b == 0 {
			return 0, fmt.Errorf("division by zero")
		}
		return a % b, nil
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
