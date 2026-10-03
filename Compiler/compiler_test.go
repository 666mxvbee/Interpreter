package main

import (
	"fmt"
	"os"
	"reflect"
	"strings"
	"testing"
)

func mustParse(t *testing.T, src string) Stmt {
	t.Helper()
	prog, err := Parse(src)
	if err != nil {
		t.Fatalf("parse %q: %v", src, err)
	}
	return prog
}

func compileAndRun(t *testing.T, src string, input ...int) []int {
	t.Helper()
	out, err := Run(Compile(mustParse(t, src)), input)
	if err != nil {
		t.Fatalf("run %q: %v", src, err)
	}
	return out
}

func check(t *testing.T, name string, got, want []int) {
	t.Helper()
	if !reflect.DeepEqual(got, want) {
		t.Errorf("%s: got %v, want %v", name, got, want)
	}
}

const exampleProgram = `{
    read(x);

    y = 10;
    y += 2;

    write(y);
    write(5 / 2);
    write(5 > 3);
    write(1 && 0);
    write(1 !! 0);

    if (x > 10) {
        write(100);
    } elif (x == 10) {
        write(200);
    } else {
        write(300);
    }

    i = 0;
    while (i < 2) {
        i += 1;
    }

    do {
        y -= 1;
    } while (y > 10);

    for (j = 0; j < 3; j += 1) {
        write(j);
    }

    skip;
}
`

func TestExample(t *testing.T) {
	cases := []struct{ x, branch int }{{5, 300}, {10, 200}, {15, 100}}
	for _, c := range cases {
		got := compileAndRun(t, exampleProgram, c.x)
		check(t, "example", got, []int{12, 2, 1, 0, 1, c.branch, 0, 1, 2})
	}
}

func TestExampleFile(t *testing.T) {
	src, err := os.ReadFile("examples/example.txt")
	if err != nil {
		t.Skip("examples/example.txt not found")
	}
	if string(src) != exampleProgram {
		t.Error("examples/example.txt differs from exampleProgram")
	}
}

func TestJSON(t *testing.T) {
	src := `{ read(n); s = 0; while (n > 0) { s = s + n; n = n - 1; } write(s); }`
	got := ToJSON(Compile(mustParse(t, src)))
	want := `[
  "READ",
  { "ST": "n" },
  { "CONST": 0 },
  { "ST": "s" },
  { "JMP": "L_while_cond_5" },
  { "LABEL": "L_while_cody_4" },
  { "LD": "s" },
  { "LD": "n" },
  { "BINOP": "+" },
  { "ST": "s" },
  { "LD": "n" },
  { "CONST": 1 },
  { "BINOP": "-" },
  { "ST": "n" },
  { "LABEL": "L_while_cond_5" },
  { "LD": "n" },
  { "CONST": 0 },
  { "BINOP": ">" },
  { "JNZ": "L_while_cody_4" },
  { "LD": "s" },
  "WRITE"
]
`
	if got != want {
		t.Errorf("json output mismatch:\n got:\n%s\nwant:\n%s", got, want)
	}
	check(t, "sum", compileAndRun(t, src, 4), []int{10})
}

func TestLabels(t *testing.T) {
	cases := []struct{ name, src, want string }{
		{"if in the middle", `{ if (x > 1) write(1) write(2) }`,
			"LD x; CONST 1; BINOP >; JZ L_else_2; CONST 1; WRITE; JMP L_seq_1; LABEL L_else_2; LABEL L_seq_1; CONST 2; WRITE"},
		{"if at the end", `{ read(x) if (x) write(1) else write(2) }`,
			"READ; ST x; LD x; JZ L_else_2; CONST 1; WRITE; JMP L_end_0; LABEL L_else_2; CONST 2; WRITE; LABEL L_end_0"},
		{"nested if", `{ if (a) { if (b) x = 1 } else y = 2 }`,
			"LD a; JZ L_else_1; LD b; JZ L_else_2; CONST 1; ST x; JMP L_end_0; LABEL L_else_2; JMP L_end_0; LABEL L_else_1; CONST 2; ST y; LABEL L_end_0"},
		{"for", `{ for (i = 0; i < 2; i += 1) write(i) }`,
			"CONST 0; ST i; JMP L_while_cond_3; LABEL L_while_cody_2; LD i; WRITE; LD i; CONST 1; BINOP +; ST i; LABEL L_while_cond_3; LD i; CONST 2; BINOP <; JNZ L_while_cody_2"},
		{"do-while", `{ do write(1) while (0) }`,
			"LABEL L_while_cody_1; CONST 1; WRITE; LABEL L_while_cond_2; CONST 0; JNZ L_while_cody_1"},
		{"if inside while", `{ while (n) if (n) n = 0 }`,
			"JMP L_while_cond_2; LABEL L_while_cody_1; LD n; JZ L_else_3; CONST 0; ST n; JMP L_while_cond_2; LABEL L_else_3; LABEL L_while_cond_2; LD n; JNZ L_while_cody_1"},
		{"two whiles", `{ while (a) skip x = 1 while (b) skip }`,
			"JMP L_while_cond_3; LABEL L_while_cody_2; LABEL L_while_cond_3; LD a; JNZ L_while_cody_2; CONST 1; ST x; JMP L_while_cond_6; LABEL L_while_cody_5; LABEL L_while_cond_6; LD b; JNZ L_while_cody_5"},
	}
	for _, c := range cases {
		var parts []string
		for _, in := range Compile(mustParse(t, c.src)) {
			if in.Arg == nil {
				parts = append(parts, in.Op)
			} else {
				parts = append(parts, fmt.Sprintf("%s %v", in.Op, in.Arg))
			}
		}
		if got := strings.Join(parts, "; "); got != c.want {
			t.Errorf("%s:\n got: %s\nwant: %s", c.name, got, c.want)
		}
	}
}

func TestExpressions(t *testing.T) {
	cases := []struct {
		expr string
		want int
	}{
		{"1 + 2 * 3", 7},
		{"10 - 2 - 3", 5},
		{"(1 + 2) * 3", 9},
		{"100 / 10 / 2", 5},
		{"7 / 2", 3},
		{"7 % 3", 1},
		{"2 + 3 == 5", 1},
		{"3 != 3", 0},
		{"5 >= 5", 1},
		{"5 <= 4", 0},
		{"1 < 2 && 2 < 3", 1},
		{"1 !! 0 && 0", 1},
		{"(1 !! 0) && 0", 0},
		{"x' + 1", 43},
	}
	for _, c := range cases {
		got := compileAndRun(t, "{ x' = 42 write("+c.expr+") }")
		check(t, c.expr, got, []int{c.want})
	}
}

func TestComments(t *testing.T) {
	src := `{ -- комментарий до конца строки
	x = 1; (* блочный
	комментарий *) x += 1
	write(x) -- ещё один
	}`
	check(t, "comments", compileAndRun(t, src), []int{2})
}

func TestCompoundAssign(t *testing.T) {
	src := `{ x = 10; x -= 3; x *= 2; x /= 4; x %= 2; write(x); }`
	check(t, "compound", compileAndRun(t, src), []int{1})
}

func TestElif(t *testing.T) {
	src := `{ read(x) if (x == 1) write(10) elif (x == 2) write(20) elif (x == 3) write(30) else write(40) }`
	for x, want := range map[int]int{1: 10, 2: 20, 3: 30, 9: 40} {
		check(t, "elif", compileAndRun(t, src, x), []int{want})
	}
	noElse := `{ read(x) if (x == 1) write(1) elif (x == 2) write(2) write(0) }`
	check(t, "elif no else", compileAndRun(t, noElse, 5), []int{0})
	check(t, "elif no else", compileAndRun(t, noElse, 2), []int{2, 0})
}

func TestLoops(t *testing.T) {
	check(t, "do-while runs once", compileAndRun(t, `{ i = 5 do { write(i) i += 1 } while (i < 3) }`), []int{5})
	check(t, "while zero iterations", compileAndRun(t, `{ i = 5 while (i < 3) { write(i) i += 1 } write(i) }`), []int{5})
	check(t, "for", compileAndRun(t, `{ s = 0 for (i = 1; i <= 4; i += 1) s += i write(s) }`), []int{10})
	check(t, "nested", compileAndRun(t, `{ for (i = 0; i < 2; i += 1) for (j = 0; j < 2; j += 1) write(i * 10 + j) }`), []int{0, 1, 10, 11})
}

func TestParseErrors(t *testing.T) {
	bad := []string{
		"{ }",
		"{ x = }",
		"{ x = 1 } y",
		"{ write(1 + ) }",
		"{ if (1) write(1) else }",
		"{ x = 1; (* незакрытый комментарий }",
		"{ X = 1 }",
		"{ else skip }",
		"{ elif (1) skip }",
		"x = 1",
		"{ x = 1",
		"{ x == 1 }",
		"{ do skip }",
	}
	for _, src := range bad {
		if _, err := Parse(src); err == nil {
			t.Errorf("%q: expected an error", src)
		}
	}
}

func TestRunErrors(t *testing.T) {
	bad := []string{
		"{ read(x) }",
		"{ write(x) }",
		"{ write(1 / 0) }",
	}
	for _, src := range bad {
		if _, err := Run(Compile(mustParse(t, src)), nil); err == nil {
			t.Errorf("%q: expected a runtime error", src)
		}
	}
}
