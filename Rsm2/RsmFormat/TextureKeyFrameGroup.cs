using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Rsm2.RsmFormat {
	public class TextureKeyFrameGroup {
		public Dictionary<int, Dictionary<int, List<TextureKeyFrame>>> _offsets = new Dictionary<int, Dictionary<int, List<TextureKeyFrame>>>();

		public int Count {
			get { return _offsets.Count; }
		}

		public IEnumerable<int> Types {
			get { return _offsets.Keys; }
		}

		public Dictionary<int, Dictionary<int, List<TextureKeyFrame>>> Offsets {
			get { return _offsets; }
		}

		/// <summary>
		/// Adds the texture key frame.
		/// </summary>
		/// <param name="textureId">The texture identifier.</param>
		/// <param name="type">The type.</param>
		/// <param name="frame">The frame.</param>
		public void AddTextureKeyFrame(int textureId, int type, TextureKeyFrame frame) {
			if (!_offsets.ContainsKey(type)) {
				_offsets[type] = new Dictionary<int, List<TextureKeyFrame>>();
			}

			var offsets = _offsets[type];

			if (!offsets.ContainsKey(textureId)) {
				offsets[textureId] = new List<TextureKeyFrame>();
			}

			offsets[textureId].Add(frame);
		}

		/// <summary>
		/// Determines whether [has texture animation] [the specified texture identifier].
		/// </summary>
		/// <param name="textureId">The texture identifier.</param>
		/// <param name="type">The type.</param>
		/// <returns><c>true</c> if [has texture animation] [the specified texture identifier]; otherwise, <c>false</c>.</returns>
		public bool HasTextureAnimation(int textureId, int type) {
			return GetTextureKeyFrames(textureId, type) != null;
		}

		/// <summary>
		/// Gets the texture key frames.
		/// </summary>
		/// <param name="textureId">The texture identifier.</param>
		/// <param name="type">The type.</param>
		/// <returns>List&lt;TextureKeyFrame&gt;.</returns>
		public List<TextureKeyFrame> GetTextureKeyFrames(int textureId, int type) {
			if (_offsets.ContainsKey(type)) {
				var offsets = _offsets[type];

				if (!offsets.ContainsKey(textureId))
					return null;

				return offsets[textureId];
			}

			return null;
		}
	}
}
