using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DTMediaCapture.Internal {
	public class AudioRecorderProxy : MonoBehaviour, IDisposable {
		// PRAGMA MARK - IDisposable Implementation
		public void Dispose() {
			this.enabled = false;
		}


		// PRAGMA MARK - Public Interface
		public float[] AudioBuffer {
			get { return audioBuffer_; }
		}

		public int BufferLength {
			get { return bufferLength_; }
		}

		public int Channels {
			get { return channels_; }
		}

		public void FlushBuffer() {
			bufferLength_ = 0;
		}

		public void Init() {
			this.enabled = true;

			int numBuffers, bufferLength;
			AudioSettings.GetDSPBufferSize(out bufferLength, out numBuffers);
			audioBuffer_ = new float[bufferLength * 256];
		}


		// PRAGMA MARK - Internal
		private float[] audioBuffer_;
		private int bufferLength_ = 0;

		private int channels_ = 2;

		private void OnAudioFilterRead(float[] data, int channels) {
			channels_ = channels;

			int length = Mathf.Min(data.Length, audioBuffer_.Length - bufferLength_);

			for (int i = 0; i < length; i++) {
				audioBuffer_[i + bufferLength_] = data[i];
			}
			bufferLength_ += length;
		}
	}
}