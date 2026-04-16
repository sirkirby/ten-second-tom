/**
 * Shared audio conversion utilities used by transcription services.
 */

/**
 * Converts a Buffer of Int16 PCM samples to a Float32Array (range -1.0 to 1.0).
 * Both whisper.node's transcribeData and sherpa-onnx-node's acceptWaveform
 * expect Float32Array audio data.
 */
export function int16BufferToFloat32(buffer: Buffer): Float32Array {
  const sampleCount = Math.floor(buffer.byteLength / 2);
  const float32 = new Float32Array(sampleCount);
  for (let i = 0; i < sampleCount; i++) {
    // Read little-endian Int16
    const sample = buffer.readInt16LE(i * 2);
    float32[i] = sample / 32768.0;
  }
  return float32;
}
