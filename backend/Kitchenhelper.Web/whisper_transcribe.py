#!/usr/bin/env python3
"""
Whisper ASR Script for KitchenHelper
Transcribes audio files using OpenAI Whisper locally.

Usage:
    python whisper_transcribe.py <audio_file> <output_file> [model]

Arguments:
    audio_file  - Path to input audio file (WAV, MP3, etc.)
    output_file - Path to output text file
    model       - Whisper model size: tiny, base, small, medium, large (default: base)

Requirements:
    pip install openai-whisper
    # or for faster-whisper:
    pip install faster-whisper
"""

import sys
import os

def transcribe_with_whisper(audio_path: str, output_path: str, model_name: str = "base") -> str:
    """Transcribe audio using standard OpenAI Whisper."""
    import whisper
    
    print(f"Loading Whisper model: {model_name}", file=sys.stderr)
    model = whisper.load_model(model_name)
    
    print(f"Transcribing: {audio_path}", file=sys.stderr)
    result = model.transcribe(audio_path, fp16=False)
    
    transcript = result["text"].strip()
    return transcript


def transcribe_with_faster_whisper(audio_path: str, output_path: str, model_name: str = "base") -> str:
    """Transcribe audio using faster-whisper (CTranslate2 optimized)."""
    from faster_whisper import WhisperModel
    
    print(f"Loading faster-whisper model: {model_name}", file=sys.stderr)
    # Use CPU with int8 quantization for better performance without GPU
    model = WhisperModel(model_name, device="cpu", compute_type="int8")
    
    print(f"Transcribing: {audio_path}", file=sys.stderr)
    segments, info = model.transcribe(audio_path, beam_size=5)
    
    # Combine all segments
    transcript = " ".join([segment.text.strip() for segment in segments])
    return transcript


def main():
    # Fix Windows console encoding for Unicode output
    import io
    if sys.platform == 'win32':
        sys.stdout = io.TextIOWrapper(sys.stdout.buffer, encoding='utf-8', errors='replace')
        sys.stderr = io.TextIOWrapper(sys.stderr.buffer, encoding='utf-8', errors='replace')
    
    if len(sys.argv) < 3:
        print("Usage: python whisper_transcribe.py <audio_file> <output_file> [model]", file=sys.stderr)
        sys.exit(1)
    
    audio_path = sys.argv[1]
    output_path = sys.argv[2]
    model_name = sys.argv[3] if len(sys.argv) > 3 else "base"
    
    if not os.path.exists(audio_path):
        print(f"Error: Audio file not found: {audio_path}", file=sys.stderr)
        sys.exit(1)
    
    # Try faster-whisper first (more efficient), fall back to standard whisper
    transcript = None
    try:
        transcript = transcribe_with_faster_whisper(audio_path, output_path, model_name)
        print("Used: faster-whisper", file=sys.stderr)
    except ImportError:
        print("faster-whisper not available, trying standard whisper...", file=sys.stderr)
        try:
            transcript = transcribe_with_whisper(audio_path, output_path, model_name)
            print("Used: openai-whisper", file=sys.stderr)
        except ImportError:
            print("Error: Neither faster-whisper nor openai-whisper is installed.", file=sys.stderr)
            print("Install with: pip install openai-whisper", file=sys.stderr)
            print("Or for better performance: pip install faster-whisper", file=sys.stderr)
            sys.exit(1)
    
    if transcript:
        # Write to output file first (this is the important part)
        with open(output_path, "w", encoding="utf-8") as f:
            f.write(transcript)
        
        # Print to stdout with error handling for encoding issues
        try:
            print(transcript)
        except UnicodeEncodeError:
            # If stdout fails, just print a success message
            print("[Transcript written to file - console encoding issue]")
        
        print(f"Transcription saved to: {output_path}", file=sys.stderr)
    else:
        print("Error: Empty transcription result", file=sys.stderr)
        sys.exit(1)


if __name__ == "__main__":
    main()
