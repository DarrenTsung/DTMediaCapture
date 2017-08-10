using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

using DTMediaCapture.Internal;

namespace DTMediaCapture {
	// Based off keijiro's ImageSequenceOut (https://github.com/keijiro/ImageSequenceOut)
	public class Recorder : MonoBehaviour {
		// PRAGMA MARK - Internal
		// constants for the wave file header
		private const int HEADER_SIZE = 44;
		private const short BITS_PER_SAMPLE = 16;
		private const int SAMPLE_RATE = 44100;

		[Header("Properties")]
		[SerializeField]
		private string recordingPath_ = "${DESKTOP}/Recordings";
		[SerializeField]
		private string recordingNameFormat_ = "Recording__${DATE}__${INDEX}";

		[Space]
		#pragma warning disable 0414 // not used because used inside !UNITY_EDITOR
		[SerializeField]
		private string nonEditorFfmpegPath_ = "${DESKTOP}/Ffmpeg/ffmpeg";
		#pragma warning restore 0414

		[Space]
		[SerializeField, Range(1, 60)]
		private int frameRate_ = 30;

		[Header("Key-Bindings")]
		[SerializeField]
		private bool useKeyBindings_ = true;
		[Space]
		[SerializeField]
		private KeyCode toggleRecordingKey_ = KeyCode.K;

		[Header("Record Set Length")]
		[SerializeField]
		private float recordSetLengthInSeconds_ = 10.0f;
		[SerializeField]
		private bool recordSetLengthOnStart_ = false;

		[Header("Read-Only")]
		[SerializeField]
		private float recordingTime_ = 0.0f;

		private string populatedRecordingPath_;
		private string currentRecordingName_;

		private bool recording_ = false;
		private int frameCount_ = -1;

		private AudioRecorderProxy audioProxy_;
		private MemoryStream audioOutputStream_;
		private BinaryWriter audioOutputWriter_;

		#if DT_DEBUG_MENU
		private DTDebugMenu.IDynamicGroup dynamicGroup_;
		#endif

		private void Awake() {
			bool debug = Debug.isDebugBuild;
			#if DEBUG
			debug = true;
			#endif

			if (!debug) {
				this.enabled = false;
				return;
			}

			// populate recording path
			populatedRecordingPath_ = recordingPath_;
			populatedRecordingPath_ = SavePathUtil.PopulateDesktopVariable(populatedRecordingPath_);

			#if DT_DEBUG_MENU
			var inspector = DTDebugMenu.GenericInspectorRegistry.Get("DTMediaCapture");
			inspector.BeginDynamic();
			inspector.RegisterHeader("Recorder");
			inspector.RegisterToggle("Set Recording: " + (useKeyBindings_ ? string.Format("({0})", toggleRecordingKey_) : ""), (b) => { if (b) { StartRecording(); } else { StopRecording(); }}, () => recording_);
			dynamicGroup_ = inspector.EndDynamic();
			#endif
		}

		private void OnEnable() {
			#if DT_DEBUG_MENU
			dynamicGroup_.Enabled = true;
			#endif
		}

		private void OnDisable() {
			#if DT_DEBUG_MENU
			dynamicGroup_.Enabled = false;
			#endif
		}

		private void Start() {
			if (recordSetLengthOnStart_) {
				StartRecording();
			}
		}

		private void StartRecording() {
			if (recording_) {
				Debug.LogWarning("Can't start recording when already recording!");
				return;
			}

			recording_ = true;
			frameCount_ = -1;
			Time.captureFramerate = frameRate_;
			RefreshSequencePath();
			AttachAudioRecorderProxy();
			Debug.Log("Starting Recording!");
		}

		private void StopRecording() {
			if (!recording_) {
				Debug.LogWarning("Can't stop recording when not recording!");
				return;
			}

			recording_ = false;
			Time.captureFramerate = 0;
			SaveAudioOutput();
			CreateVideoFromCurrentSequence();

			if (audioProxy_ != null) {
				audioProxy_.Dispose();
				audioProxy_ = null;
			}

			var currentRecordingPath = Path.Combine(populatedRecordingPath_, currentRecordingName_);
			Debug.Log("Finished Recording! Saved video at: " + currentRecordingPath + "!");
			currentRecordingName_ = null;
		}

		private void Update() {
			if (useKeyBindings_) {
				if (Input.GetKeyDown(toggleRecordingKey_)) {
					if (recording_) {
						StopRecording();
					} else {
						StartRecording();
					}
				}
			}

			UpdateRecording();
		}

		private void UpdateRecording() {
			if (!recording_) {
				return;
			}

			if (string.IsNullOrEmpty(currentRecordingName_)) {
				return;
			}

			if (frameCount_ > 0) {
				var currentRecordingPath = Path.Combine(populatedRecordingPath_, currentRecordingName_);
				var screenshotPath = Path.Combine(currentRecordingPath, "Frame" + frameCount_.ToString("000000") + ".png");
				Application.CaptureScreenshot(screenshotPath);

				// write audio for frame
				for (int i = 0; i < audioProxy_.BufferLength; i++) {
					audioOutputWriter_.Write((short)(audioProxy_.AudioBuffer[i] * (float)Int16.MaxValue));
				}
				audioProxy_.FlushBuffer();
			}
			frameCount_++;

			recordingTime_ = (float)frameCount_ / (float)frameRate_;
			if (recordSetLengthOnStart_ && recordingTime_ > recordSetLengthInSeconds_) {
				StopRecording();
			}
		}

		private void RefreshSequencePath() {
			string recordingNameFormat = recordingNameFormat_;
			recordingNameFormat = SavePathUtil.PopulateDateVariable(recordingNameFormat);

			if (!recordingNameFormat.Contains("${INDEX}")) {
				Debug.LogWarning("RecordingNameFormat is missing ${INDEX} - adding _${INDEX} to the end!");
				recordingNameFormat = recordingNameFormat + "_${INDEX}";
			}

			string finalRecordingName = null;
			int index = 0;
			while (true) {
				string currentRecordingName  = recordingNameFormat.Replace("${INDEX}", index.ToString());
				string currentRecordingPath = Path.Combine(populatedRecordingPath_, currentRecordingName) + ".mp4";

				if (!File.Exists(currentRecordingPath)) {
					finalRecordingName = currentRecordingName;
					break;
				}
				index++;
			}

			Directory.CreateDirectory(Path.Combine(populatedRecordingPath_, finalRecordingName));
			currentRecordingName_ = finalRecordingName;
		}

		private void CreateVideoFromCurrentSequence() {
			if (string.IsNullOrEmpty(currentRecordingName_)) {
				Debug.LogWarning("Cannot create video because no current recording name!");
				return;
			}

			string ffmpegPath = "";
#if UNITY_EDITOR
			string binPath = ScriptableObjectEditorUtil.PathForScriptableObjectType<BinMarker>();
			string pathToProject = Application.dataPath.Replace("Assets", "");
			string binFullPath = Path.Combine(pathToProject, binPath);
			ffmpegPath = Path.Combine(binFullPath, "ffmpeg/ffmpeg");
#else
			ffmpegPath = SavePathUtil.PopulateDesktopVariable(nonEditorFfmpegPath_);
#endif

			string arguments = string.Format("-f image2 -r {0} -i ./{1}/Frame%06d.png -c:v libx264 -r {0} -b:v 30M -pix_fmt yuv420p {1}.mp4 -loglevel debug", frameRate_, currentRecordingName_);

			var process = new System.Diagnostics.Process();
			process.StartInfo.FileName = ffmpegPath;
			process.StartInfo.Arguments = arguments;
			process.StartInfo.WorkingDirectory = populatedRecordingPath_;
			process.Start();

			process.WaitForExit(5 * 60 * 1000); // 5 minutes max

			Directory.Delete(Path.Combine(populatedRecordingPath_, currentRecordingName_), recursive: true);
		}

		private void AttachAudioRecorderProxy() {
			var audioListeners = UnityEngine.Object.FindObjectsOfType<AudioListener>();
			if (audioListeners.Length == 0) {
				return;
			}

			audioProxy_ = audioListeners.First().GetOrAddComponent<AudioRecorderProxy>();
			audioProxy_.Init();

			audioOutputStream_ = new MemoryStream();
			audioOutputWriter_ = new BinaryWriter(audioOutputStream_);
		}

		private void SaveAudioOutput() {
			string audioFilename = Path.Combine(populatedRecordingPath_, currentRecordingName_) + ".wav";
			if (audioOutputStream_.Length > 0) {
				// add a header to the file so we can send it to the SoundPlayer
				AddHeader();

				// Save to a file. Print a warning if overwriting a file.
				if (File.Exists(audioFilename)) {
					Debug.LogWarning("Overwriting " + audioFilename + "...");
				}

				// reset the stream pointer to the beginning of the stream
				audioOutputStream_.Position = 0;

				// write the stream to a file
				FileStream fs = File.OpenWrite(audioFilename);
				audioOutputStream_.WriteTo(fs);
				fs.Close();

				// for debugging only
				Debug.Log("Finished saving audio to " + audioFilename + ".");
			} else {
				Debug.LogWarning("There is no audio data to save!");
			}
		}

		/// Taken from Evan Merz's blog (http://evanxmerz.com/?p=212)
		/// This generates a simple header for a canonical wave file,
		/// which is the simplest practical audio file format. It
		/// writes the header and the audio file to a new stream, then
		/// moves the reference to that stream.
		///
		/// See this page for details on canonical wave files:
		/// http://www.lightlink.com/tjweber/StripWav/Canon.html
		private void AddHeader() {
			int channels = audioProxy_.Channels;
			Debug.LogError("channels: " + channels);

			// reset the output stream
			audioOutputStream_.Position = 0;

			// calculate the number of samples in the data chunk
			long numberOfSamples = audioOutputStream_.Length / (BITS_PER_SAMPLE / 8);

			// create a new MemoryStream that will have both the audio data AND the header
			MemoryStream newOutputStream = new MemoryStream();
			BinaryWriter writer = new BinaryWriter(newOutputStream);

			writer.Write(0x46464952); // "RIFF" in ASCII

			// write the number of bytes in the entire file
			writer.Write((int)(HEADER_SIZE + (numberOfSamples * BITS_PER_SAMPLE * channels / 8)) - 8);

			writer.Write(0x45564157); // "WAVE" in ASCII
			writer.Write(0x20746d66); // "fmt " in ASCII
			writer.Write(16);

			// write the format tag. 1 = PCM
			writer.Write((short)1);

			// write the number of channels.
			writer.Write((short)channels);

			// write the sample rate. 44100 in this case. The number of audio samples per second
			writer.Write(SAMPLE_RATE);

			writer.Write(SAMPLE_RATE * channels * (BITS_PER_SAMPLE / 8));
			writer.Write((short)(channels * (BITS_PER_SAMPLE / 8)));

			// 16 bits per sample
			writer.Write(BITS_PER_SAMPLE);

			// "data" in ASCII. Start the data chunk.
			writer.Write(0x61746164);

			// write the number of bytes in the data portion
			writer.Write((int)(numberOfSamples * BITS_PER_SAMPLE * channels / 8));

			// copy over the actual audio data
			audioOutputStream_.WriteTo(newOutputStream);

			// move the reference to the new stream
			audioOutputStream_ = newOutputStream;
		}
	}
}