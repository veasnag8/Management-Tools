import json
import sys

def main():
    if len(sys.argv) > 1:
        with open("tts_debug.log", "a") as f:
            f.write("Args: " + str(sys.argv) + "\n")
            try:
                with open(sys.argv[1], "r") as jf:
                    f.write("Payload: " + jf.read() + "\n")
            except:
                pass

if __name__ == "__main__":
    main()
