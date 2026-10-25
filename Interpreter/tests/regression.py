import argparse
import json
from pathlib import Path
import random
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
DOTNET = "dotnet"
DLL = None


def const(value):
    return {"const": value}


def var(name):
    return {"var": name}


def string(value):
    return {"string": value}


def array(*elements):
    return {"array": list(elements)}


def element(source, index):
    return {"elem": source, "index": const(index) if isinstance(index, int) else index}


def binary(operator, left, right):
    return {"binop": operator, "left": left, "right": right}


def assign(name, source):
    return {"assn": {"dst": name, "src": source}}


def set_element(destination, index, value):
    return {"set": {"dst": destination, "idx": const(index), "value": value}}


def write(value):
    return {"write": value}


def sequence(*statements):
    result = statements[-1]
    for statement in reversed(statements[:-1]):
        result = {"seq": {"left": statement, "right": result}}
    return result


def execute(program, input_text="", input_file=False):
    with tempfile.TemporaryDirectory(prefix="interpreter-test-") as temporary:
        directory = Path(temporary)
        path = directory / "program.json"
        path.write_text(json.dumps(program, ensure_ascii=False), encoding="utf-8")
        command = [DOTNET, str(DLL), str(path)]
        if input_file:
            input_path = directory / "input.txt"
            input_path.write_text(input_text, encoding="utf-8")
            command.append(str(input_path))
        return subprocess.run(command, input="" if input_file else input_text,
                              capture_output=True, text=True, encoding="utf-8", timeout=20)


class InterpreterTests(unittest.TestCase):
    def succeeds(self, program, expected, input_text="", input_file=False):
        result = execute(program, input_text, input_file)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stderr, "")
        self.assertEqual(result.stdout, expected)

    def fails(self, program, message, input_text="", category="Runtime error"):
        result = execute(program, input_text)
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertIn(category + ":", result.stderr)
        self.assertIn(message, result.stderr)
        self.assertNotIn("Unhandled exception", result.stderr)
        self.assertEqual(result.stdout, "")

    def test_existing_and_new_examples(self):
        for name in ("countdown", "features", "strings-arrays", "nested-sum"):
            with self.subTest(example=name):
                directory = ROOT / "examples/input" / name
                program = json.loads((directory / "program.json").read_text(encoding="utf-8-sig"))
                path = directory / "input.txt"
                input_text = path.read_text(encoding="utf-8-sig") if path.exists() else ""
                expected = (ROOT / "examples/output" / (name + ".txt")).read_text(encoding="utf-8-sig")
                self.succeeds(program, expected.rstrip("\n") + "\n", input_text, input_file=True)

    def test_saved_reference_programs(self):
        path = ROOT.parent / "Shared/tests/Language.Runtime.Tests/Fixtures/reference.json"
        fixtures = json.loads(path.read_text(encoding="utf-8"))["cases"]
        for fixture in fixtures:
            with self.subTest(reference=fixture["name"]):
                program = json.loads(fixture["ast"]["json"])
                if fixture["ast"]["error"]:
                    self.fails(program, "Runtime error", fixture["input"])
                else:
                    self.succeeds(program, fixture["ast"]["output"] + "\n", fixture["input"])

    def test_empty_input_array_and_list(self):
        self.succeeds(sequence({"read": "a"}, {"read": "b"}, write(var("a")), write(var("b"))),
                      "[], {}\n", "[], {}")

    def test_empty_output_is_empty_stdout(self):
        self.succeeds("skip", "")

    def test_excessive_input_depth_is_controlled(self):
        self.fails({"read": "a"}, "depth limit", "[" * 10000 + "1" + "]" * 10000)

    def test_error_discards_previous_writes(self):
        self.fails(sequence(write(const(1)), write(binary("/", const(1), const(0)))), "Division by zero")

    def test_integer_operations_and_overflow_are_preserved(self):
        cases = [("+", 2147483647, 1, -2147483648), ("-", -2147483648, 1, 2147483647),
                 ("*", 1073741824, 4, 0), ("/", -2147483648, -1, -2147483648),
                 ("%", -2147483648, -1, 0), ("/", -5, 2, -2), ("%", -5, 2, -1),
                 ("!!", 0, -3, 1), ("&&", 2, -3, 1)]
        self.succeeds(sequence(*(write(binary(op, const(a), const(b))) for op, a, b, _ in cases)),
                      ", ".join(str(expected) for _, _, _, expected in cases) + "\n")

    def test_logical_operators_still_evaluate_both_operands(self):
        for operator, left in (("!!", 1), ("&&", 0)):
            with self.subTest(operator=operator):
                self.fails(write(binary(operator, const(left), binary("/", const(1), const(0)))),
                           "Division by zero")
                self.fails(write(binary(operator, const(left), string("x"))), "integer")

    def test_mixed_arrays_nested_access_and_assignment(self):
        a = var("a")
        self.succeeds(sequence(assign("a", array(const(1), array(const(2), const(3)), string("x"))),
                                set_element(element(a, 1), 0, const(9)),
                                write(element(element(a, 1), 0)), write({"length": a}),
                                set_element(a, 0, string("changed")), write(a)),
                      '9, 3, ["changed", [9, 3], "x"]\n')

    def test_array_aliases_share_mutations(self):
        self.succeeds(sequence(assign("a", array(array(const(1), const(2)))),
                                assign("b", var("a")), assign("c", element(var("b"), 0)),
                                set_element(var("c"), 1, const(7)), write(var("a"))), "[[1, 7]]\n")

    def test_compound_element_assignment_uses_reference_ast(self):
        target = element(var("a"), 1)
        self.succeeds(sequence(assign("a", array(const(10), const(20))),
                                set_element(var("a"), 1, binary("+", target, const(3))),
                                write(target)), "23\n")

    def test_literals_are_evaluated_afresh_in_a_loop(self):
        body = sequence(assign("s", string("a")), assign("a", array(const(1))),
                        write(var("s")), write(element(var("a"), 0)),
                        set_element(var("s"), 0, const(90)), set_element(var("a"), 0, const(9)),
                        assign("i", binary("+", var("i"), const(1))))
        self.succeeds(sequence(assign("i", const(0)), {"while": {
            "cond": binary("<", var("i"), const(2)), "body": body}}), '"Z", 1, "Z", 1\n')

    def test_strings_are_mutable_utf8_bytes_and_share_aliases(self):
        self.succeeds(sequence(assign("s", string("abc")), assign("alias", var("s")),
                                write(element(var("s"), 0)),
                                set_element(var("s"), 0, const(90)), write(var("alias")),
                                assign("unicode", string("Я")), write({"length": var("unicode")}),
                                write(element(var("unicode"), 0)), write(element(var("unicode"), 1))),
                      '97, "Zbc", 2, 208, 175\n')

    def test_string_escapes_and_empty_string(self):
        self.succeeds(sequence(write(string('a"\\\nb')), write({"length": string("")}),
                                write(string(""))), '"a"\\\nb", 0, ""\n')

    def test_strings_do_not_support_concatenation(self):
        self.fails(write(binary("+", string("a"), string("b"))), "integer")

    def test_string_byte_validation(self):
        for invalid in (const(-1), const(256), string("Z"), array(const(90))):
            with self.subTest(value=invalid):
                self.fails(sequence(assign("s", string("abc")), set_element(var("s"), 0, invalid)),
                           "between 0 and 255" if "const" in invalid else "integer")

    def test_comparisons_for_strings_and_nested_arrays(self):
        cases = [("==", string("ab"), string("ab"), 1),
                 ("!=", string("ab"), string("ac"), 1),
                 ("<", string("ab"), string("ac"), 1),
                 ("<=", string("ab"), string("ab"), 1),
                 (">", string("b"), string("a"), 1),
                 (">=", string("a"), string("a"), 1),
                 ("==", array(const(1), array(const(2))), array(const(1), array(const(2))), 1),
                 ("<", array(const(1), const(2)), array(const(1), const(3)), 1),
                 ("<", array(const(9)), array(const(1), const(2)), 1),
                 ("<", array(const(1), const(9)), array(const(2)), 0)]
        self.succeeds(sequence(*(write(binary(op, a, b)) for op, a, b, _ in cases)),
                      ", ".join(str(expected) for _, _, _, expected in cases) + "\n")

    def test_comparison_of_different_types_is_an_error(self):
        for left, right in ((const(1), string("1")), (string("1"), const(1)),
                            (array(const(1)), const(1))):
            with self.subTest(left=left):
                self.fails(write(binary("==", left, right)), "Runtime error")

    def test_index_bounds_for_reads_and_writes(self):
        for source in (array(const(1), const(2)), string("ab")):
            for index in (-1, 2):
                with self.subTest(source=source, index=index):
                    self.fails(write(element(source, index)), "out of bounds")
                    self.fails(set_element(source, index, const(5)), "out of bounds")

    def test_invalid_index_types_and_targets(self):
        self.fails(write(element(array(const(1)), string("0"))), "integer")
        self.fails(write(element(const(1), 0)), "Indexing requires")
        self.fails(set_element(const(1), 0, const(2)), "Element assignment requires")
        self.fails(write({"length": const(1)}), "Length requires")

    def test_noninteger_conditions_are_rejected(self):
        for condition in (string("x"), array(const(1))):
            with self.subTest(condition=condition):
                self.fails({"if": {"cond": condition, "then": "skip", "else": "skip"}}, "integer")

    def test_input_preserves_whitespace_separated_signed_integers(self):
        self.succeeds(sequence({"read": "a"}, {"read": "b"}, {"read": "c"},
                                write(var("a")), write(var("b")), write(var("c"))),
                      "1, -2, 3\n", "1\n -2\t+3")

    def test_input_supports_strings_and_nested_arrays(self):
        self.succeeds(sequence({"read": "s"}, {"read": "a"}, write(var("s")), write(var("a"))),
                      '"a"b", [1, [-2, "x"]]\n', '"a\\"b", [1, [-2, "x"]]')

    def test_invalid_input_is_reported(self):
        for value in ("", "[1,]", "[1,,2]", "[1 2]", "[1", "2147483648",
                      "1foo", '"x"junk', '[1]junk', '"unterminated', '"bad\\q"', '"raw\nnewline"'):
            with self.subTest(value=value):
                self.fails({"read": "x"}, "Runtime error", value)

    def test_missing_variables_are_reported(self):
        self.fails(write(var("missing")), "not defined")

    def test_malformed_new_ast_nodes_are_rejected(self):
        expressions = [{"string": None}, {"string": 1}, {"string": "x", "extra": 1},
                       {"array": []}, {"array": 1}, {"array": [1]},
                       {"elem": var("a")}, {"elem": var("a"), "index": None},
                       {"length": None}, {"length": var("a"), "extra": 1}]
        for expression in expressions:
            with self.subTest(expression=expression):
                self.fails(write(expression), "Invalid AST", category="Invalid AST")
        for value in ({"dst": var("a"), "idx": const(0)},
                      {"dst": "a", "idx": const(0), "value": const(1)},
                      {"dst": var("a"), "idx": const(0), "value": const(1), "extra": 1}):
            with self.subTest(set=value):
                self.fails({"set": value}, "Invalid AST", category="Invalid AST")

    def test_shared_subarrays_are_not_mistaken_for_cycles(self):
        self.succeeds(sequence(assign("a", array(const(1))),
                                assign("b", array(var("a"), var("a"))), write(var("b")),
                                write(binary("==", var("b"), array(array(const(1)), array(const(1)))))),
                      "[[1], [1]], 1\n")

    def test_cyclic_output_does_not_crash_the_process(self):
        self.fails(sequence(assign("a", array(const(1))), set_element(var("a"), 0, var("a")),
                            write(var("a"))), "cyclic array")

    def test_sum_handles_thousand_nested_arrays(self):
        program = json.loads((ROOT / "examples/input/nested-sum/program.json").read_text())
        for depth in (0, 10, 1000):
            with self.subTest(depth=depth):
                input_text = "[[1," * depth + "[[0,7],[0,-2]]" + "]]" * depth
                self.succeeds(program, "5\n", input_text)

    def test_sum_matches_independent_generated_trees(self):
        program = json.loads((ROOT / "examples/input/nested-sum/program.json").read_text())
        rng = random.Random(142857)

        def tree(depth):
            values, total = [], 0
            for _ in range(rng.randint(1, 4)):
                if depth and rng.random() < 0.45:
                    children, subtotal = tree(depth - 1)
                    values.append([1, children])
                    total += subtotal
                else:
                    number = rng.randint(-100, 100)
                    values.append([0, number])
                    total += number
            return values, total

        for case in range(25):
            values, expected = tree(5)
            with self.subTest(case=case):
                self.succeeds(program, str(expected) + "\n", json.dumps(values))


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--no-build", action="store_true")
    parser.add_argument("--dll", type=Path, help="Path to a previously built Interpreter.Cli.dll")
    arguments = parser.parse_args()
    DOTNET = arguments.dotnet
    if not arguments.no_build:
        subprocess.run(
            [
                DOTNET,
                "build",
                str(ROOT.parent / "Interpreter.slnx"),
                "--nologo",
            ],
            check=True,
        )
    if arguments.dll:
        DLL = arguments.dll.resolve()
    else:
        result = subprocess.run(
            [DOTNET, "msbuild", str(ROOT / "src/Interpreter.Cli/Interpreter.Cli.csproj"),
             "-nologo", "-getProperty:TargetPath"],
            capture_output=True, text=True, encoding="utf-8", check=True,
        )
        DLL = Path(result.stdout.strip())
    if not DLL.is_file():
        parser.error(f"Application not found: {DLL}. Build first or supply --dll.")
    unittest.main(argv=[__file__], verbosity=2)
