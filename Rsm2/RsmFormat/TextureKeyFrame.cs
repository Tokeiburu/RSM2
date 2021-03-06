using System;
using System.IO;
using GRF.FileFormats;
using GRF.Graphics;
using GRF.IO;

namespace Rsm2.RsmFormat {
	public struct TextureKeyFrame : IWriteableObject {
		public int Frame;
		public float Offset;

		/// <summary>
		/// Initializes a new instance of the <see cref="TextureKeyFrame"/> struct.
		/// </summary>
		/// <param name="tkf">The TKF.</param>
		public TextureKeyFrame(TextureKeyFrame tkf) {
			Frame = tkf.Frame;
			Offset = tkf.Offset;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TextureKeyFrame"/> struct.
		/// </summary>
		/// <param name="reader">The reader.</param>
		public TextureKeyFrame(IBinaryReader reader) {
			Frame = reader.Int32();
			Offset = reader.Float();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="TextureKeyFrame"/> struct.
		/// </summary>
		/// <param name="data">The data.</param>
		/// <param name="offset">The offset.</param>
		public TextureKeyFrame(byte[] data, int offset) {
			Frame = BitConverter.ToInt32(data, offset);
			Offset = BitConverter.ToSingle(data, offset + 4);
		}

		/// <summary>
		/// Writes the specified object to the stream.
		/// </summary>
		/// <param name="writer">The writer.</param>
		public void Write(BinaryWriter writer) {
			writer.Write(Frame);
			writer.Write(Offset);
		}
	}
}