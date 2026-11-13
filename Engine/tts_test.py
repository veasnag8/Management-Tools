import asyncio
import edge_tts

async def test_khmer():
    text = "សួស្តីបងប្អូនទាំងអស់គ្នា សូមស្វាគមន៍មកកាន់កម្មវិធី SNA-Dubber"
    voice = "km-KH-PisethNeural"
    output_file = "test_piseth.mp3"
    communicate = edge_tts.Communicate(text, voice)
    await communicate.save(output_file)
    print("Saved test audio successfully:", output_file)

if __name__ == "__main__":
    asyncio.run(test_khmer())
