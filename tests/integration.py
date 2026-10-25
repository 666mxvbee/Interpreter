"""Offline compatibility checks for the Go compiler and the C# executors."""

import argparse
import json
from pathlib import Path
import subprocess
import tempfile
import unittest


ROOT = Path(__file__).resolve().parents[1]
DOTNET = "dotnet"
COMPILER = None
AST_DLL = None
MACHINE_DLL = None


def run(command):
    return subprocess.run([str(argument) for argument in command], input="",
                          capture_output=True, text=True, encoding="utf-8", timeout=30)


def target_path(project):
    result = run([DOTNET, "msbuild", ROOT / project, "-nologo", "-getProperty:TargetPath"])
    result.check_returncode()
    path = Path(result.stdout.strip())
    if not path.is_file():
        raise FileNotFoundError(f"Application not found: {path}. Build the root solution first.")
    return path


class IntegrationTests(unittest.TestCase):
    def assert_execution(self, result, expected, fails=False):
        self.assertEqual(result.returncode, 1 if fails else 0, result.stderr)
        self.assertEqual(result.stdout, "" if fails else expected)
        self.assertNotIn("panic:", result.stderr)
        self.assertNotIn("Unhandled exception", result.stderr)
        self.assertNotIn("Stack overflow", result.stderr)
        if fails:
            self.assertTrue(result.stderr.strip())

    def check_program(self, source, input_text="", expected="", fails=False, ast=None):
        with tempfile.TemporaryDirectory(prefix="interpreter-program-") as temporary:
            directory = Path(temporary)
            source_path = directory / "program.src"
            input_path = directory / "input.txt"
            program_path = directory / "program.json"
            source_path.write_text(source, encoding="utf-8")
            input_path.write_text(input_text, encoding="utf-8")

            compiled = run([COMPILER, "--compile-only", "--output", program_path, source_path])
            self.assertEqual(compiled.returncode, 0, compiled.stderr)
            self.assertEqual(compiled.stdout, "")
            self.assertIsInstance(json.loads(program_path.read_text(encoding="utf-8")), list)

            machine = run([DOTNET, MACHINE_DLL, program_path, input_path])
            built_in = run([COMPILER, "--input", input_path, "--output", program_path, source_path])
            self.assert_execution(machine, expected, fails)
            self.assert_execution(built_in, expected, fails)
            self.assertEqual(machine.stdout, built_in.stdout)

            if ast is not None:
                ast_path = directory / "ast.json"
                ast_path.write_text(ast["json"], encoding="utf-8")
                result = run([DOTNET, AST_DLL, ast_path, input_path])
                output = ast["output"] + "\n" if ast["output"] else ""
                self.assert_execution(result, output, bool(ast["error"]))

    def test_saved_reference_programs(self):
        fixture_path = ROOT / "Shared/tests/Language.Runtime.Tests/Fixtures/reference.json"
        fixtures = json.loads(fixture_path.read_text(encoding="utf-8"))["cases"]
        for fixture in fixtures:
            with self.subTest(reference=fixture["name"]):
                machine = fixture["sm"]
                expected = machine["output"] + "\n" if machine["output"] else ""
                self.check_program(fixture["source"], fixture["input"], expected,
                                   bool(machine["error"]), fixture["ast"])

    def test_existing_interpreter_sources(self):
        for directory in sorted((ROOT / "Interpreter/examples/input").iterdir()):
            with self.subTest(example=directory.name):
                source_path = directory / "program.src"
                if not source_path.is_file():
                    continue
                input_path = directory / "input.txt"
                input_text = input_path.read_text(encoding="utf-8-sig") if input_path.is_file() else ""
                expected_path = ROOT / "Interpreter/examples/output" / (directory.name + ".txt")
                expected = expected_path.read_text(encoding="utf-8-sig")
                ast = {"json": (directory / "program.json").read_text(encoding="utf-8-sig"),
                       "output": expected.removesuffix("\n"), "error": ""}
                self.check_program(source_path.read_text(encoding="utf-8-sig"), input_text, expected, ast=ast)

    def test_compiler_composite_example(self):
        self.check_program((ROOT / "Compiler/examples/composite.txt").read_text(encoding="utf-8"),
                           (ROOT / "Compiler/examples/composite.input").read_text(encoding="utf-8"),
                           '[], "Zbc", [1, [7, 3], "Zbc"], Cons (1, Nil), {1, 2}, 2, -2147483648\n')

    def test_nested_loops(self):
        self.check_program("{ s = 0; for (i = 0; i < 2; i += 1) "
                           "{ for (j = 0; j < 3; j += 1) { s += i + j; } } write(s); }", expected="9\n")

    def test_compiled_nested_sum_on_deep_input(self):
        source = (ROOT / "Interpreter/nested-sum.src").read_text(encoding="utf-8-sig")
        for depth in (0, 25, 100):
            with self.subTest(depth=depth):
                input_text = "[[1," * depth + "[[0,7],[0,-2]]" + "]]" * depth
                self.check_program(source, input_text, "5\n")

    def test_empty_output(self):
        self.check_program("{ skip; }")

    def test_compile_only_does_not_read_input(self):
        with tempfile.TemporaryDirectory(prefix="interpreter-compile-only-") as temporary:
            directory = Path(temporary)
            source = directory / "read.src"
            source.write_text("{ read(x); write(x); }", encoding="utf-8")
            output = directory / "program.json"
            missing_input = directory / "does-not-exist.txt"
            result = run([COMPILER, "--compile-only", "--input", missing_input,
                          "--output", output, source])
            self.assert_execution(result, "")
            self.assertTrue(output.is_file())

    def test_structured_input_uses_json_string_escapes(self):
        cases = [
            ('"a\\nb"', '"a\nb"\n'),
            ('"a\\"b"', '"a"b"\n'),
            ('"a\\\\b"', '"a\\b"\n'),
            ('"\\u042f"', '"Я"\n'),
            ('Nil()', 'Nil\n'),
            ('["a\\tb", Cons("\\u042f", Nil), []]', '["a\tb", Cons ("Я", Nil), []]\n'),
        ]
        ast = {"json": '{"seq":{"left":{"read":"a"},"right":{"write":{"var":"a"}}}}',
               "error": ""}
        for input_text, expected in cases:
            with self.subTest(input=input_text):
                self.check_program("{ read(a); write(a); }", input_text, expected,
                                   ast={**ast, "output": expected.removesuffix("\n")})

    def test_int32_division_boundaries(self):
        self.check_program("{ read(a); read(b); write(a / b); write(a % b); }",
                           "-2147483648, -1", "-2147483648, 0\n")

    def test_invalid_input_discards_output(self):
        for input_text in ("2147483648", "-2147483649", "1,", "[1,]", '"bad\\q"'):
            with self.subTest(input=input_text):
                self.check_program("{ write(1); read(a); write(a); }", input_text, fails=True)

    def test_out_of_range_source_is_rejected(self):
        with tempfile.TemporaryDirectory(prefix="interpreter-invalid-source-") as temporary:
            directory = Path(temporary)
            for literal in ("2147483648", "-2147483649"):
                with self.subTest(literal=literal):
                    source = directory / "invalid.src"
                    source.write_text("{ write(" + literal + "); }", encoding="utf-8")
                    output = directory / "program.json"
                    result = run([COMPILER, "--compile-only", "--output", output, source])
                    self.assert_execution(result, "", fails=True)
                    self.assertFalse(output.exists())


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--dotnet", default="dotnet")
    parser.add_argument("--go", default="go")
    parser.add_argument("--no-build", action="store_true", help="Use existing C# build outputs")
    parser.add_argument("--compiler", type=Path, help="Use an existing Go compiler executable")
    arguments = parser.parse_args()
    DOTNET = arguments.dotnet
    if not arguments.no_build:
        subprocess.run([DOTNET, "build", str(ROOT / "Interpreter.slnx"), "--nologo"], check=True)
    AST_DLL = target_path("Interpreter/src/Interpreter.Cli/Interpreter.Cli.csproj")
    MACHINE_DLL = target_path("StackMachine/src/StackMachine.Cli/StackMachine.Cli.csproj")
    with tempfile.TemporaryDirectory(prefix="interpreter-compiler-") as temporary:
        if arguments.compiler:
            COMPILER = arguments.compiler.resolve()
        else:
            COMPILER = Path(temporary) / "smcompiler.exe"
            subprocess.run([arguments.go, "build", "-o", str(COMPILER), "."],
                           cwd=ROOT / "Compiler", check=True)
        if not COMPILER.is_file():
            parser.error(f"Compiler not found: {COMPILER}")
        tests = unittest.main(argv=[__file__], verbosity=2, exit=False)
        successful = tests.result.wasSuccessful()
    raise SystemExit(0 if successful else 1)
