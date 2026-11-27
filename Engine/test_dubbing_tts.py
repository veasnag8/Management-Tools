import os
import tempfile
import unittest
from unittest.mock import AsyncMock, patch

from Engine import dubbing_tts


class FakeCommunicate:
    def __init__(self, fail_with_partial_output=False):
        self.fail_with_partial_output = fail_with_partial_output

    async def save(self, output_path):
        if self.fail_with_partial_output:
            with open(output_path, "wb") as audio_file:
                audio_file.write(b"partial audio")
            raise OSError("temporary connection failure")
        with open(output_path, "wb") as audio_file:
            audio_file.write(b"valid audio" * 20)


class SynthesizeTierTests(unittest.IsolatedAsyncioTestCase):
    async def test_retries_transient_failure_and_replaces_partial_audio(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            output_path = os.path.join(temp_dir, "line.mp3")
            communicate = [
                FakeCommunicate(fail_with_partial_output=True),
                FakeCommunicate(),
            ]
            with patch.object(
                dubbing_tts.edge_tts,
                "Communicate",
                side_effect=communicate,
            ) as create_communicate, patch.object(
                dubbing_tts.asyncio,
                "sleep",
                new_callable=AsyncMock,
            ):
                succeeded = await dubbing_tts._synthesize_tier(
                    "hello",
                    output_path,
                    "km-KH-PisethNeural",
                    {},
                    "test",
                )

            self.assertTrue(succeeded)
            self.assertEqual(create_communicate.call_count, 2)
            with open(output_path, "rb") as audio_file:
                self.assertEqual(audio_file.read(), b"valid audio" * 20)

    async def test_failed_attempts_remove_partial_output(self):
        with tempfile.TemporaryDirectory() as temp_dir:
            output_path = os.path.join(temp_dir, "line.mp3")
            with patch.object(
                dubbing_tts.edge_tts,
                "Communicate",
                side_effect=OSError("service unavailable"),
            ), patch.object(
                dubbing_tts.asyncio,
                "sleep",
                new_callable=AsyncMock,
            ):
                succeeded = await dubbing_tts._synthesize_tier(
                    "hello",
                    output_path,
                    "km-KH-PisethNeural",
                    {},
                    "test",
                )

            self.assertFalse(succeeded)
            self.assertFalse(os.path.exists(output_path))


if __name__ == "__main__":
    unittest.main()
