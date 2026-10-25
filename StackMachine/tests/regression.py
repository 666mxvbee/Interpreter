import argparse
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

ROOT = Path(__file__).resolve().parents[1]
DOTNET = "dotnet"
DLL = None


def execute(program, input_text="", input_file=False):
    with tempfile.TemporaryDirectory(prefix="stack-machine-test-") as temporary:
        directory = Path(temporary)
        program_path = directory / "program.json"
        text = program if isinstance(program, str) else json.dumps(program, ensure_ascii=False)
        program_path.write_text(text, encoding="utf-8")
        command = [DOTNET, str(DLL), str(program_path)]
        if input_file:
            input_path = directory / "input.txt"
            input_path.write_text(input_text, encoding="utf-8")
            command.append(str(input_path))
        return subprocess.run(command, input="" if input_file else input_text,
                              capture_output=True, text=True, encoding="utf-8", timeout=20)


class MachineTests(unittest.TestCase):
    def succeeds(self, program, expected, input_text="", input_file=False):
        result = execute(program, input_text, input_file)
        self.assertEqual(result.returncode, 0, result.stderr)
        self.assertEqual(result.stderr, "")
        self.assertEqual(result.stdout, expected)

    def fails(self, program, message, input_text=""):
        result = execute(program, input_text)
        self.assertEqual(result.returncode, 1, result.stderr)
        self.assertEqual(result.stdout, "")
        self.assertIn(message, result.stderr)
        self.assertNotIn("Unhandled exception", result.stderr)
        self.assertNotIn("Stack overflow", result.stderr)

    def test_examples(self):
        for program_path in sorted((ROOT / "examples/input").glob("*.json")):
            with self.subTest(example=program_path.stem):
                input_path = program_path.with_suffix(".txt")
                output_path = ROOT / "examples/output" / (program_path.stem + ".txt")
                input_text = input_path.read_text(encoding="utf-8-sig")
                expected = output_path.read_text(encoding="utf-8-sig").rstrip("\n") + "\n"
                self.succeeds(program_path.read_text(encoding="utf-8-sig"), expected, input_text, True)

    def test_saved_reference_programs(self):
        path = ROOT.parent / "Shared/tests/Language.Runtime.Tests/Fixtures/reference.json"
        fixtures = json.loads(path.read_text(encoding="utf-8"))["cases"]
        for fixture in fixtures:
            with self.subTest(reference=fixture["name"]):
                reference = fixture["sm"]
                if reference["error"]:
                    self.fails(reference["json"], "instruction", fixture["input"])
                else:
                    self.succeeds(reference["json"], reference["output"] + "\n", fixture["input"])

    def test_empty_program_and_zero_arity(self):
        self.succeeds([], "")
        self.succeeds([{"ARRAY": 0}, "WRITE", {"SEXP": ["nil", 0]}, "WRITE"], "[], {}\n")

    def test_structured_input_from_file(self):
        self.succeeds(["READ", "WRITE", "READ", "WRITE"],
                      '"abc", [1, []]\n', '"abc", [1, []]', True)

    def test_stack_underflow(self):
        for program in (["WRITE"], ["ELEM"], ["SET"], [{"ARRAY": 1}],
                        [{"SEXP": ["Foo", 1]}], [{"CONST": 1}, {"BINOP": "+"}]):
            with self.subTest(program=program):
                self.fails(program, "Exhausted stack")

    def test_invalid_json_and_instruction_shapes(self):
        for program in ("[", {}, ["UNKNOWN"], [{"ARRAY": -1}], [{"ARRAY": 1.5}],
                        [{"STRING": 1}], [{"SEXP": ["Foo"]}], [{"SEXP": ["Foo", -1]}],
                        [{"CONST": 2147483648}], [{"LD": "a", "ST": "b"}]):
            with self.subTest(program=program):
                self.fails(program, "Invalid")

    def test_input_errors(self):
        for input_text in ("1,,2", "1,", "[1,]", "[1 2]", "2147483648", '"unterminated'):
            with self.subTest(input=input_text):
                self.fails(["READ"], "Invalid input", input_text)
        self.fails(["READ"], "instruction 0")

    def test_invalid_remaining_input_is_not_ignored(self):
        self.fails([], "Invalid input", "1,")

    def test_labels_and_variables(self):
        self.fails([{"LD": "missing"}], "instruction 0")
        self.fails([{"JMP": "missing"}], "instruction 0")
        self.fails(["WRITE", {"LABEL": "L"}, {"LABEL": "L"}], "Duplicate label")
        self.succeeds([{"CONST": 0}, {"JNZ": "missing"}, {"CONST": 42}, "WRITE"], "42\n")
        self.succeeds([{"CONST": 1}, {"JZ": "missing"}, {"CONST": 42}, "WRITE"], "42\n")

    def test_error_discards_previous_writes(self):
        self.fails([{"CONST": 1}, "WRITE", {"CONST": 1}, {"CONST": 0}, {"BINOP": "/"}], "instruction 4")

    def test_cyclic_output(self):
        self.fails([{"CONST": 0}, {"ARRAY": 1}, {"ST": "a"}, {"LD": "a"},
                    {"CONST": 0}, {"LD": "a"}, "SET", {"LD": "a"}, "WRITE"], "cyclic")

    def test_deep_values(self):
        depth = 3000
        text = "[" * depth + "1" + "]" * depth
        self.succeeds(["READ", "WRITE"], text + "\n", text)
        text = "[" * 10000 + "1" + "]" * 10000
        self.fails(["READ"], "depth limit", text)

    def test_argument_exit_codes(self):
        for arguments in ([], ["--help"], ["a", "b", "c"]):
            with self.subTest(arguments=arguments):
                result = subprocess.run([DOTNET, str(DLL), *arguments],
                                        capture_output=True, text=True, encoding="utf-8", timeout=20)
                self.assertEqual(result.returncode, 2)
                self.assertEqual(result.stdout, "")
                self.assertIn("input.txt", result.stderr)

    def test_missing_file_is_an_io_error(self):
        with tempfile.TemporaryDirectory(prefix="stack-machine-missing-") as temporary:
            path = Path(temporary) / "missing.json"
            result = subprocess.run([DOTNET, str(DLL), str(path)],
                                    capture_output=True, text=True, encoding="utf-8", timeout=20)
            self.assertEqual(result.returncode, 1)
            self.assertEqual(result.stdout, "")
            self.assertIn("I/O error", result.stderr)


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description="StackMachine CLI regression tests")
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--no-build", action="store_true")
    parser.add_argument("--dll", type=Path, help="Path to a previously built StackMachine.Cli.dll")
    arguments = parser.parse_args()
    DOTNET = arguments.dotnet
    if not arguments.no_build:
        subprocess.run([DOTNET, "build", str(ROOT.parent / "Interpreter.slnx"), "--nologo"], check=True)
    if arguments.dll:
        DLL = arguments.dll.resolve()
    else:
        result = subprocess.run(
            [DOTNET, "msbuild", str(ROOT / "src/StackMachine.Cli/StackMachine.Cli.csproj"),
             "-nologo", "-getProperty:TargetPath"],
            capture_output=True, text=True, encoding="utf-8", check=True,
        )
        DLL = Path(result.stdout.strip())
    if not DLL.is_file():
        parser.error(f"Application not found: {DLL}. Build first or supply --dll.")
    unittest.main(argv=[__file__], verbosity=2)
