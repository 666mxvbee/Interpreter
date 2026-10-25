package main

import "testing"

func TestStringOutputReplacesInvalidUTF8Subparts(t *testing.T) {
	cases := []struct {
		name string
		data []byte
		want string
	}{
		{"valid", []byte("Я😀�"), "Я😀�"},
		{"adjacent invalid bytes", []byte{0xFF, 0xFF, 'c'}, "��c"},
		{"truncated prefix", []byte{0xE2, 0x82}, "�"},
		{"prefix before ASCII", []byte{0xE2, 0x82, 'a'}, "�a"},
		{"overlong sequence", []byte{0xE0, 0x80, 0x80}, "���"},
		{"surrogate sequence", []byte{0xED, 0xA0, 0x80}, "���"},
		{"out of range", []byte{0xF4, 0x90, 0x80, 0x80}, "����"},
		{"truncated four bytes", []byte{0xF0, 0x90, 0x80}, "�"},
	}
	for _, c := range cases {
		t.Run(c.name, func(t *testing.T) {
			got, err := FormatOutput([]Value{&StringValue{Bytes: c.data}})
			if err != nil {
				t.Fatal(err)
			}
			if want := "\"" + c.want + "\"\n"; got != want {
				t.Errorf("got %q, want %q", got, want)
			}
		})
	}
}
