using System.IO;
using GRF.Graphics;
using GRF.IO;

namespace Rsm2.RsmFormat {
	public class VolumeBox {
		/// <summary>
		/// Initializes a new instance of the <see cref="VolumeBox" /> class.
		/// </summary>
		/// <param name="reader">The reader.</param>
		/// <param name="noFlag">if set to <c>true</c> [no flag].</param>
		public VolumeBox(IBinaryReader reader, bool noFlag = false) {
			Size = new Vertex(reader);
			Position = new Vertex(reader);
			Rotation = new Vertex(reader);
			Flag = noFlag ? 0 : reader.Int32();
		}

		public Vertex Size { get; private set; }
		public Vertex Position { get; private set; }
		public Vertex Rotation { get; private set; }
		public int Flag { get; private set; }

		public void Write(BinaryWriter writer, bool noFlag = false) {
			Size.Write(writer);
			Position.Write(writer);
			Rotation.Write(writer);

			if (!noFlag)
				writer.Write(Flag);
		}
	}
}