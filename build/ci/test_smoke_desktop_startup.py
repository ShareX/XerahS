"""Exercise the smoke gate with real child processes, without a desktop session."""
import os
from pathlib import Path
import tempfile
import unittest

from smoke_desktop_startup import check_startup


class StartupSmokeTests(unittest.TestCase):
    def setUp(self):
        self.directory = tempfile.TemporaryDirectory()
        self.addCleanup(self.directory.cleanup)
        self.root = Path(self.directory.name)
        self.executable = self.root / "app"
        self.output = self.root / "startup.log"

    def app(self, behavior):
        self.executable.write_text("#!/usr/bin/env python3\nimport os,time\nfrom pathlib import Path\n" + behavior)
        self.executable.chmod(0o755)

    def run_smoke(self, **kwargs):
        check_startup(self.executable, self.output, timeout=2, settle=0.1, **kwargs)

    def test_accepts_two_live_startups_with_an_isolated_profile(self):
        self.app("Path(os.environ['XERAHS_LOGS_DIR'],'app.log').write_text('Tray menu rebuilt with 9 items.\\n' * 2 + 'Plugins: 12 loaded\\n')\ntime.sleep(10)\n")
        self.run_smoke()
        self.assertEqual(self.output.read_text().count("process stayed alive"), 2)

    def test_rejects_early_exit_even_with_zero_exit_code(self):
        self.app("raise SystemExit(0)\n")
        with self.assertRaisesRegex(RuntimeError, "exited during startup"):
            self.run_smoke()

    def test_rejects_the_reported_native_menu_error_and_preserves_diagnostics(self):
        self.app("Path(os.environ['XERAHS_LOGS_DIR'],'app.log').write_text('Critical application startup failure: The menu being updated does not match')\ntime.sleep(10)\n")
        with self.assertRaisesRegex(RuntimeError, "failure was logged"):
            self.run_smoke()
        self.assertIn("The menu being updated does not match", self.output.read_text())

    def test_does_not_accept_a_running_process_without_tray_and_plugins(self):
        self.app("time.sleep(10)\n")
        with self.assertRaisesRegex(RuntimeError, "timed out"):
            check_startup(self.executable, self.output, timeout=0.2, settle=0.1)

    def test_does_not_accept_a_process_that_exits_immediately_after_readiness(self):
        self.app("Path(os.environ['XERAHS_LOGS_DIR'],'app.log').write_text('Tray menu rebuilt with 9 items.\\n' * 2 + 'Plugins: 12 loaded\\n')\n")
        with self.assertRaisesRegex(RuntimeError, "exited during startup"):
            self.run_smoke()


if __name__ == "__main__":
    unittest.main()
