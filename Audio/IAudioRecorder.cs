using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace DTMediaCapture.Internal {
	public interface IAudioRecorder {
		void OnAudioFilterRead(float[] data, int channels);
	}
}