package main

import "testing"

func TestInputJSONStrings(t *testing.T) {
	cases := []struct{ text, want string }{
		{`"a\nb"`, "\"a\nb\"\n"},
		{`"a\"b"`, "\"a\"b\"\n"},
		{`"a\\b"`, "\"a\\b\"\n"},
		{`"\u042f"`, "\"Я\"\n"},
		{`"\ud83d\ude00"`, "\"😀\"\n"},
		{`["a\tb", Cons("\u042f", Nil), []]`, "[\"a\tb\", Cons (\"Я\", Nil), []]\n"},
	}
	for _, c := range cases {
		t.Run(c.text, func(t *testing.T) {
			values, err := ParseInput(c.text)
			if err != nil {
				t.Fatal(err)
			}
			got, err := FormatOutput(values)
			if err != nil {
				t.Fatal(err)
			}
			if got != c.want {
				t.Errorf("got %q, want %q", got, c.want)
			}
		})
	}
}

func TestInvalidInputStringsAreRejected(t *testing.T) {
	for _, text := range []string{`"bad\q"`, `"unterminated`, `"a""b"`, "\"raw\nnewline\"", `"\uZZZZ"`} {
		t.Run(text, func(t *testing.T) {
			if _, err := ParseInput(text); err == nil {
				t.Errorf("accepted invalid input %q", text)
			}
		})
	}
}

func TestSourceStringsKeepReferenceSyntax(t *testing.T) {
	cases := []struct{ text, want string }{
		{`"a\nb"`, `a\nb`},
		{`"a""b"`, `a"b`},
	}
	for _, c := range cases {
		t.Run(c.text, func(t *testing.T) {
			tokens, err := Lex(c.text)
			if err != nil {
				t.Fatal(err)
			}
			if tokens[0].Kind != TokString || tokens[0].Text != c.want {
				t.Errorf("got %v, want string %q", tokens[0], c.want)
			}
		})
	}
}
