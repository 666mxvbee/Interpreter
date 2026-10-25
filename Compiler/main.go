package main

import (
	"fmt"
	"io"
	"os"
	"path/filepath"
	"strings"
)

type CLIOptions struct {
	SourcePath  string
	InputPath   string
	OutputPath  string
	InputValues []string
	CompileOnly bool
	Help        bool
}

func main() {
	os.Exit(runCLI(os.Args[1:], os.Stdin, os.Stdout, os.Stderr))
}

func parseArguments(arguments []string) (CLIOptions, error) {
	var options CLIOptions
	positionalOnly := false
	for i := 0; i < len(arguments); i++ {
		argument := arguments[i]
		if !positionalOnly {
			switch argument {
			case "--":
				positionalOnly = true
				continue
			case "--help", "-h":
				options.Help = true
				return options, nil
			case "--compile-only":
				options.CompileOnly = true
				continue
			case "--input", "--output":
				if i+1 >= len(arguments) || arguments[i+1] == "" || strings.HasPrefix(arguments[i+1], "--") {
					return options, fmt.Errorf("%s requires a file path", argument)
				}
				i++
				if argument == "--input" {
					if options.InputPath != "" {
						return options, fmt.Errorf("--input may be specified only once")
					}
					options.InputPath = arguments[i]
				} else {
					if options.OutputPath != "" {
						return options, fmt.Errorf("--output may be specified only once")
					}
					options.OutputPath = arguments[i]
				}
				continue
			}
			if strings.HasPrefix(argument, "-") && !(options.SourcePath != "" && len(argument) > 1 && isDigit(rune(argument[1]))) {
				return options, fmt.Errorf("unknown option %q", argument)
			}
		}
		if options.SourcePath == "" {
			if argument == "" {
				return options, fmt.Errorf("source file path must not be empty")
			}
			options.SourcePath = argument
		} else {
			options.InputValues = append(options.InputValues, argument)
		}
	}
	if options.SourcePath == "" {
		return options, fmt.Errorf("a source file is required")
	}
	if options.InputPath != "" && len(options.InputValues) != 0 {
		return options, fmt.Errorf("use either --input or positional input values")
	}
	if options.OutputPath == "" {
		base := strings.TrimSuffix(filepath.Base(options.SourcePath), filepath.Ext(options.SourcePath))
		options.OutputPath = filepath.Join("output", base+".json")
	}
	if sameFile(options.SourcePath, options.OutputPath) {
		return options, fmt.Errorf("output path must differ from the source file")
	}
	if options.InputPath != "" && options.InputPath != "-" && sameFile(options.InputPath, options.OutputPath) {
		return options, fmt.Errorf("output path must differ from the input file")
	}
	return options, nil
}

func sameFile(left, right string) bool {
	leftPath, leftErr := filepath.Abs(left)
	rightPath, rightErr := filepath.Abs(right)
	if leftErr == nil && rightErr == nil && leftPath == rightPath {
		return true
	}
	leftInfo, leftErr := os.Stat(left)
	rightInfo, rightErr := os.Stat(right)
	return leftErr == nil && rightErr == nil && os.SameFile(leftInfo, rightInfo)
}

func compileSource(path string) ([]Instr, error) {
	source, err := os.ReadFile(path)
	if err != nil {
		return nil, err
	}
	program, err := Parse(string(source))
	if err != nil {
		return nil, fmt.Errorf("%s: %w", path, err)
	}
	code, err := CompileChecked(program)
	if err != nil {
		return nil, fmt.Errorf("%s: %w", path, err)
	}
	return code, nil
}

func writeProgram(code []Instr, path string) error {
	text, err := EncodeProgram(code)
	if err != nil {
		return err
	}
	if err := os.MkdirAll(filepath.Dir(path), 0o755); err != nil {
		return err
	}
	return os.WriteFile(path, []byte(text), 0o644)
}

func readInput(options CLIOptions, stdin io.Reader) ([]Value, error) {
	text := strings.Join(options.InputValues, ", ")
	if options.InputPath != "" {
		var data []byte
		var err error
		if options.InputPath == "-" {
			data, err = io.ReadAll(stdin)
		} else {
			data, err = os.ReadFile(options.InputPath)
		}
		if err != nil {
			return nil, err
		}
		text = string(data)
	}
	return ParseInput(text)
}

func executeProgram(code []Instr, options CLIOptions, stdin io.Reader) (string, error) {
	input, err := readInput(options, stdin)
	if err != nil {
		return "", err
	}
	output, err := RunValues(code, input)
	if err != nil {
		return "", err
	}
	return FormatOutput(output)
}

func runCLI(arguments []string, stdin io.Reader, stdout, stderr io.Writer) int {
	options, err := parseArguments(arguments)
	if err != nil {
		fmt.Fprintln(stderr, "error:", err)
		usage(stderr)
		return 2
	}
	if options.Help {
		usage(stderr)
		return 0
	}
	code, err := compileSource(options.SourcePath)
	if err != nil {
		fmt.Fprintln(stderr, "error:", err)
		return 1
	}
	if err := writeProgram(code, options.OutputPath); err != nil {
		fmt.Fprintln(stderr, "error:", err)
		return 1
	}
	fmt.Fprintln(stderr, "compiled to", options.OutputPath)
	if options.CompileOnly {
		return 0
	}
	result, err := executeProgram(code, options, stdin)
	if err != nil {
		fmt.Fprintln(stderr, "error:", err)
		return 1
	}
	if result != "" {
		if _, err := io.WriteString(stdout, result); err != nil {
			fmt.Fprintln(stderr, "error:", err)
			return 1
		}
	}
	return 0
}

func usage(writer io.Writer) {
	fmt.Fprintln(writer, "usage: smcompiler [--compile-only] [--input file|-] [--output program.json] <source-file> [input values...]")
}
