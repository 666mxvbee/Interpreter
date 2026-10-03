package main

import (
	"fmt"
	"os"
	"path/filepath"
	"strconv"
	"strings"
)

func main() {
	if len(os.Args) < 2 {
		fmt.Fprintln(os.Stderr, "usage: smcompiler <source-file> [input values...]")
		os.Exit(2)
	}
	srcPath := os.Args[1]

	src, err := os.ReadFile(srcPath)
	die(err)
	prog, err := Parse(string(src))
	die(err)
	code := Compile(prog)

	base := strings.TrimSuffix(filepath.Base(srcPath), filepath.Ext(srcPath))
	outPath := filepath.Join("output", base+".json")
	die(os.MkdirAll("output", 0o755))
	die(os.WriteFile(outPath, []byte(ToJSON(code)), 0o644))
	fmt.Println("compiled to", outPath)

	var input []int
	for _, a := range os.Args[2:] {
		n, err := strconv.Atoi(a)
		if err != nil {
			die(fmt.Errorf("bad input value %q", a))
		}
		input = append(input, n)
	}
	out, err := Run(code, input)
	for _, v := range out {
		fmt.Println(v)
	}
	die(err)
}

func die(err error) {
	if err != nil {
		fmt.Fprintln(os.Stderr, "error:", err)
		os.Exit(1)
	}
}
