using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using GRF.FileFormats;
using GRF.Graphics;
using GRF.IO;
using Utilities;
using Utilities.Extension;

namespace Rsm2.RsmFormat {
	public class Mesh : IWriteableFile {
		private readonly List<Vertex> _vertices = new List<Vertex>();
		private readonly List<int> _textureIndexes = new List<int>();
		private readonly List<TextureVertex> _tvertices = new List<TextureVertex>();
		private readonly List<Face> _faces = new List<Face>();
		public BoundingBox BoundingBox = new BoundingBox();
		public Mesh Parent;
		public HashSet<Mesh> Children = new HashSet<Mesh>();
		public Vertex Position;
		public Vertex Position_;

		public float RotAngle;
		public Vertex RotAxis;
		public Vertex Scale;
		private Matrix3 _transformationMatrix = new Matrix3();

		public List<string> Textures = new List<string>();
		internal Matrix4 MeshMatrixSelf;
		internal Matrix4 MeshMatrix;

		private readonly List<ScaleKeyFrame> _scaleKeyFrames = new List<ScaleKeyFrame>();
		private readonly List<RotKeyFrame> _rotFrames = new List<RotKeyFrame>();
		private readonly List<PosKeyFrame> _posKeyFrames = new List<PosKeyFrame>();
		private readonly TextureKeyFrameGroup _textureKeyFrameGroup = new TextureKeyFrameGroup();
		private TkQuaternion? _bufferedRot;
		private Vertex? _bufferedScale;
		private Vertex? _bufferedPos;
		private readonly Dictionary<int, float> _bufferedTextureOffset = new Dictionary<int, float>();

		public List<int> TextureIndexes {
			get { return _textureIndexes; }
		}

		internal Rsm Model { get; private set; }
		public string Name { get; set; }
		public string ParentName { get; set; }

		/// <summary>
		/// Gets or sets the loaded file path of this object.
		/// </summary>
		public string LoadedPath { get; set; }

		/// <summary>
		/// Gets or sets the transformation matrix.
		/// </summary>
		/// <value>The transformation matrix.</value>
		public Matrix3 TransformationMatrix {
			get { return _transformationMatrix; }
			set { _transformationMatrix = value; }
		}

		/// <summary>
		/// Gets the vertices.
		/// </summary>
		/// <value>The vertices.</value>
		public List<Vertex> Vertices {
			get { return _vertices; }
		}

		/// <summary>
		/// Gets the texture vertices.
		/// </summary>
		/// <value>The texture vertices.</value>
		public List<TextureVertex> TextureVertices {
			get { return _tvertices; }
		}

		/// <summary>
		/// Gets the faces.
		/// </summary>
		/// <value>The faces.</value>
		public List<Face> Faces {
			get { return _faces; }
		}

		/// <summary>
		/// Gets the scale key frames.
		/// </summary>
		/// <value>The scale key frames.</value>
		public List<ScaleKeyFrame> ScaleKeyFrames {
			get { return _scaleKeyFrames; }
		}

		/// <summary>
		/// Gets the rotation key frames.
		/// </summary>
		/// <value>The rotation key frames.</value>
		public List<RotKeyFrame> RotationKeyFrames {
			get { return _rotFrames; }
		}

		/// <summary>
		/// Gets the position key frames.
		/// </summary>
		/// <value>The position key frames.</value>
		public List<PosKeyFrame> PosKeyFrames {
			get { return _posKeyFrames; }
		}

		/// <summary>
		/// Gets the texture key frame group.
		/// </summary>
		/// <value>The texture key frame group.</value>
		public TextureKeyFrameGroup TextureKeyFrameGroup {
			get { return _textureKeyFrameGroup; }
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Mesh"/> class.
		/// </summary>
		public Mesh() {
			_transformationMatrix = new Matrix3();
			_transformationMatrix[0] = _transformationMatrix[4] = _transformationMatrix[8] = 1f;
			Position = new Vertex();
			Position_ = new Vertex();
			RotAngle = 0;
			RotAxis = new Vertex(0, 0, 0);
			Scale = new Vertex(1, 1, 1);
			ParentName = "";
			Name = "";
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Mesh"/> class.
		/// </summary>
		/// <param name="mesh">The mesh.</param>
		public Mesh(Mesh mesh) {
			_transformationMatrix = new Matrix3(mesh._transformationMatrix);

			foreach (var skf in mesh._scaleKeyFrames) {
				_scaleKeyFrames.Add(new ScaleKeyFrame(skf));
			}

			foreach (var rf in mesh._rotFrames) {
				_rotFrames.Add(new RotKeyFrame(rf));
			}

			foreach (var psk in mesh._posKeyFrames) {
				_posKeyFrames.Add(new PosKeyFrame(psk));
			}

			foreach (var texture in mesh.Textures) {
				Textures.Add(texture);
			}

			_textureIndexes = mesh._textureIndexes.ToList();
			_tvertices = mesh._tvertices.ToList();
			_vertices = mesh._vertices.ToList();

			foreach (var child in mesh.Children) {
				Children.Add(new Mesh(child));
			}

			Parent = mesh.Parent;
			Position = mesh.Position;
			Position_ = mesh.Position_;
			RotAngle = mesh.RotAngle;
			RotAxis = mesh.RotAxis;
			Scale = mesh.Scale;

			foreach (var f in mesh._faces) {
				_faces.Add(new Face(f));
			}

			Model = mesh.Model;
			Name = mesh.Name;
			ParentName = mesh.ParentName;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Mesh"/> class.
		/// </summary>
		/// <param name="rsm">The RSM.</param>
		/// <param name="reader">The reader.</param>
		/// <param name="version">The version.</param>
		public Mesh(Rsm rsm, IBinaryReader reader, double version) {
			int count;

			Model = rsm;

			if (version >= 2.2) {
				Name = reader.String(reader.Int32(), '\0');
				ParentName = reader.String(reader.Int32(), '\0');
			}
			else {
				Name = reader.String(40, '\0');
				ParentName = reader.String(40, '\0');
			}

			if (version >= 2.3) {
				count = reader.Int32();

				for (int i = 0; i < count; i++) {
					Textures.Add(reader.String(reader.Int32(), '\0'));
				}

				_textureIndexes.Capacity = count;

				for (int i = 0; i < count; i++) {
					_textureIndexes.Add(i);
				}
			}
			else {
				_textureIndexes.Capacity = count = reader.Int32();

				for (int i = 0; i < count; i++) {
					_textureIndexes.Add(reader.Int32());
				}
			}

			for (int i = 0; i < 9; i++) {
				_transformationMatrix[i] = reader.Float();
			}

			Position_ = new Vertex(reader);

			if (version >= 2.2) {
				Position = new Vertex(0, 0, 0);
				RotAngle = 0;
				RotAxis = new Vertex(0, 0, 0);
				Scale = new Vertex(1, 1, 1);
			}
			else {
				Position = new Vertex(reader);
				RotAngle = reader.Float();
				RotAxis = new Vertex(reader);
				Scale = new Vertex(reader);
			}

			_vertices.Capacity = count = reader.Int32();

			for (int i = 0; i < count; i++) {
				_vertices.Add(new Vertex(reader));
			}

			_tvertices.Capacity = count = reader.Int32();

			for (int i = 0; i < count; i++) {
				_tvertices.Add(new TextureVertex {
					Color = version >= 1.2 ? reader.UInt32() : 0xFFFFFFFF,
					U = reader.Float(),
					V = reader.Float()
				});
			}

			_faces.Capacity = count = reader.Int32();

			for (int i = 0; i < count; i++) {
				Face face = new Face();
				int len = -1;

				if (version >= 2.2) {
					len = reader.Int32();
				}

				face.VertexIds = reader.ArrayUInt16(3);
				face.TextureVertexIds = reader.ArrayUInt16(3);
				face.TextureId = reader.UInt16();
				face.Padding = reader.UInt16();
				face.TwoSide = reader.Int32();

				if (version >= 1.2) {
					face.SmoothGroup[0] = face.SmoothGroup[1] = face.SmoothGroup[2] = reader.Int32();

					if (len > 24) {
						face.SmoothGroup[1] = reader.Int32();
					}

					if (len > 28) {
						face.SmoothGroup[2] = reader.Int32();
					}
				}

				_faces.Add(face);
			}

			if (version >= 1.6) {
				_scaleKeyFrames.Capacity = count = reader.Int32();

				for (int i = 0; i < count; i++) {
					_scaleKeyFrames.Add(new ScaleKeyFrame {
						Frame = reader.Int32(),
						Sx = reader.Float(),
						Sy = reader.Float(),
						Sz = reader.Float(),
						Data = reader.Float()
					});
				}
			}

			_rotFrames.Capacity = count = reader.Int32();

			for (int i = 0; i < count; i++) {
				_rotFrames.Add(new RotKeyFrame {
					Frame = reader.Int32(),
					Quaternion = new TkQuaternion(reader.Float(), reader.Float(), reader.Float(), reader.Float())
				});
			}

			if (version >= 2.2) {
				_posKeyFrames.Capacity = count = reader.Int32();

				for (int i = 0; i < count; i++) {
					_posKeyFrames.Add(new PosKeyFrame {
						Frame = reader.Int32(),
						X = reader.Float(),
						Y = reader.Float(),
						Z = reader.Float(),
						Data = reader.Int32()
					});
				}
			}

			if (version >= 2.3) {
				count = reader.Int32();

				for (int i = 0; i < count; i++) {
					int textureId = reader.Int32();
					int amountTextureAnimations = reader.Int32();

					for (int j = 0; j < amountTextureAnimations; j++) {
						int type = reader.Int32();
						int amountFrames = reader.Int32();

						for (int k = 0; k < amountFrames; k++) {
							_textureKeyFrameGroup.AddTextureKeyFrame(textureId, type, new TextureKeyFrame {
								Frame = reader.Int32(),
								Offset = reader.Float()
							});
						}
					}
				}
			}

			_uniqueTextures();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Mesh"/> class.
		/// </summary>
		/// <param name="rsm">The RSM.</param>
		/// <param name="file">The file.</param>
		public Mesh(Rsm rsm, string file)
			: this(rsm, new ByteReader(file, 6), 1.4) {
			LoadedPath = file;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="Mesh"/> class.
		/// </summary>
		/// <param name="rsm">The RSM.</param>
		/// <param name="reader">The reader.</param>
		public Mesh(Rsm rsm, IBinaryReader reader)
			: this(rsm, reader, rsm.Version) {
		}

		#region IWriteableFile Members

		public void Save() {
			Save(LoadedPath);
		}

		public void Save(string file) {
			using (var stream = File.Create(file)) {
				Save(stream);
			}
		}

		public void Save(Stream stream) {
			Save(new BinaryWriter(stream));
		}

		#endregion

		private void _uniqueTextures() {
			var rsm = Model;
			HashSet<string> textures = new HashSet<string>();

			for (int i = 0; i < rsm.Textures.Count; i++) {
				if (!textures.Add(rsm.Textures[i])) {
					var newTextureIndex = -1;

					for (int j = 0; j < i; j++) {
						//if (textures.Contains(rsm.Textures[i])) {
						if (String.CompareOrdinal(rsm.Textures[j], rsm.Textures[i]) == 0) {
							// Make sure the texture is never used
							newTextureIndex = j;
							break;
						}
					}

					if (newTextureIndex < 0) { // Should never happen
						newTextureIndex = 0;
					}

					if (_textureIndexes.Contains(newTextureIndex)) {
						newTextureIndex = _textureIndexes.IndexOf(newTextureIndex);
					}
					else {
						_textureIndexes.Add(newTextureIndex);
						newTextureIndex = _textureIndexes.Count - 1;
					}

					for (int k = 0; k < Faces.Count; k++) {
						if (GetAbsoluteTextureId(Faces[k].TextureId) == i)
							Faces[k].TextureId = (ushort)newTextureIndex;
					}
				}
			}
		}

		/// <summary>
		/// Calculates the MeshMatrix and MeshMatrixSelf for the specified animation frame.
		/// </summary>
		/// <param name="animationFrame">The animation frame.</param>
		public void Calc(int animationFrame) {
			MeshMatrixSelf = Matrix4.Identity;
			MeshMatrix = Matrix4.Identity;

			// Calculate Matrix applied on the mesh itself
			if (ScaleKeyFrames.Count > 0) {
				MeshMatrix = Matrix4.Scale(MeshMatrix, GetScale(animationFrame));
			}

			if (RotationKeyFrames.Count > 0) {
				MeshMatrix = Matrix4.Rotate(MeshMatrix, GetRotationQuaternion(animationFrame));
			}
			else {
				MeshMatrix = Matrix4.Multiply2(MeshMatrix, new Matrix4(TransformationMatrix));

				if (Parent != null) {
					MeshMatrix = Matrix4.Multiply2(MeshMatrix, new Matrix4(Parent.TransformationMatrix).Invert());
				}
			}

			MeshMatrixSelf = new Matrix4(MeshMatrix);
			
			Vertex position;

			// Calculate the position of the mesh from its parent
			if (PosKeyFrames.Count > 0) {
				position = GetPosition(animationFrame);
			}
			else {
				if (Parent != null) {
					position = Position_ - Parent.Position_;
					position = Matrix4.Multiply2(new Matrix4(Parent.TransformationMatrix).Invert(), position);
				}
				else {
					position = Position_;
				}
			}

			MeshMatrixSelf.Offset = position;

			// Apply parent transformations
			Mesh mesh = this;

			while (mesh.Parent != null) {
				mesh = mesh.Parent;
				MeshMatrixSelf = Matrix4.Multiply2(MeshMatrixSelf, mesh.MeshMatrix);
			}

			// Set the final position relative to the parent's position
			if (Parent != null) {
				MeshMatrixSelf.Offset += Parent.MeshMatrixSelf.Offset;
			}

			// Calculate children
			foreach (var child in Children) {
				child.Calc(animationFrame);
			}
		}

		public int GetAbsoluteTextureId(int relativeId) {
			return _textureIndexes[relativeId];
		}

		#region Transformation key frames

		/// <summary>
		/// Clears the animation buffered key frames.
		/// </summary>
		public void ClearBuffer() {
			_bufferedScale = null;
			_bufferedRot = null;
			_bufferedPos = null;
			_bufferedTextureOffset.Clear();
		}

		/// <summary>
		/// Gets the rotation quaternion.
		/// </summary>
		/// <param name="animationFrame">The animation frame.</param>
		/// <returns>TkQuaternion.</returns>
		public TkQuaternion GetRotationQuaternion(int animationFrame) {
			if (_bufferedRot == null) {
				for (int i = 0; i < _rotFrames.Count - 1; i++) {
					if (animationFrame >= _rotFrames[i].Frame && _rotFrames[i + 1].Frame < animationFrame)
						continue;

					if (_rotFrames[i].Frame == animationFrame) {
						_bufferedRot = _rotFrames[i].Quaternion;
						return _bufferedRot.Value;
					}

					if (_rotFrames[i + 1].Frame == animationFrame) {
						_bufferedRot = _rotFrames[i + 1].Quaternion;
						return _bufferedRot.Value;
					}

					int dist = _rotFrames[i + 1].Frame - _rotFrames[i].Frame;
					animationFrame = animationFrame - _rotFrames[i].Frame;
					float mult = (animationFrame / (float)dist);

					var curFrame = _rotFrames[i];
					var nexFrame = _rotFrames[i + 1];

					_bufferedRot = TkQuaternion.Slerp(curFrame.Quaternion, nexFrame.Quaternion, mult);
					return _bufferedRot.Value;
				}

				if (animationFrame >= _rotFrames[_rotFrames.Count - 1].Frame)
					return _rotFrames[_rotFrames.Count - 1].Quaternion;

				return _rotFrames[0].Quaternion;
			}

			return _bufferedRot.Value;
		}

		/// <summary>
		/// Gets the scale.
		/// </summary>
		/// <param name="animationFrame">The animation frame.</param>
		/// <returns>Vertex.</returns>
		public Vertex GetScale(int animationFrame) {
			if (_bufferedScale == null) {
				for (int i = 0; i < _scaleKeyFrames.Count - 1; i++) {
					if (animationFrame >= _scaleKeyFrames[i].Frame && _scaleKeyFrames[i + 1].Frame < animationFrame)
						continue;

					if (_scaleKeyFrames[i].Frame == animationFrame) {
						_bufferedScale = _scaleKeyFrames[i].Scale;
						return _bufferedScale.Value;
					}

					if (_scaleKeyFrames[i + 1].Frame == animationFrame) {
						_bufferedScale = _scaleKeyFrames[i + 1].Scale;
						return _bufferedScale.Value;
					}

					int dist = _scaleKeyFrames[i + 1].Frame - _scaleKeyFrames[i].Frame;
					animationFrame = animationFrame - _scaleKeyFrames[i].Frame;
					float mult = (animationFrame / (float)dist);

					var curFrame = _scaleKeyFrames[i];
					var nexFrame = _scaleKeyFrames[i + 1];


					_bufferedScale = mult * (nexFrame.Scale - curFrame.Scale) + curFrame.Scale;
					return _bufferedScale.Value;
				}

				if (animationFrame >= _scaleKeyFrames[_scaleKeyFrames.Count - 1].Frame)
					return _scaleKeyFrames[_scaleKeyFrames.Count - 1].Scale;

				return _scaleKeyFrames[0].Scale;
			}

			return _bufferedScale.Value;
		}

		/// <summary>
		/// Gets the position.
		/// </summary>
		/// <param name="animationFrame">The animation frame.</param>
		/// <returns>Vertex.</returns>
		public Vertex GetPosition(int animationFrame) {
			if (_bufferedPos == null) {
				for (int i = 0; i < _posKeyFrames.Count - 1; i++) {
					if (animationFrame >= _posKeyFrames[i].Frame && _posKeyFrames[i + 1].Frame < animationFrame)
						continue;

					if (_posKeyFrames[i].Frame == animationFrame) {
						_bufferedPos = _posKeyFrames[i].Position;
						return _bufferedPos.Value;
					}

					if (_posKeyFrames[i + 1].Frame == animationFrame) {
						_bufferedPos = _posKeyFrames[i + 1].Position;
						return _bufferedPos.Value;
					}

					int dist = _posKeyFrames[i + 1].Frame - _posKeyFrames[i].Frame;
					animationFrame = animationFrame - _posKeyFrames[i].Frame;
					float mult = (animationFrame / (float)dist);

					var curFrame = _posKeyFrames[i];
					var nexFrame = _posKeyFrames[i + 1];

					_bufferedPos = mult * (nexFrame.Position - curFrame.Position) + curFrame.Position;
					return _bufferedPos.Value;
				}

				if (animationFrame >= _posKeyFrames[_posKeyFrames.Count - 1].Frame)
					return _posKeyFrames[_posKeyFrames.Count - 1].Position;

				return _posKeyFrames[0].Position;
			}

			return _bufferedPos.Value;
		}

		/// <summary>
		/// Gets the texture.
		/// </summary>
		/// <param name="animationFrame">The animation frame.</param>
		/// <param name="textureId">The texture identifier.</param>
		/// <param name="type">The type.</param>
		/// <returns>System.Single.</returns>
		public float GetTexture(int animationFrame, int textureId, int type) {
			var frames = _textureKeyFrameGroup.GetTextureKeyFrames(textureId, type);

			if (frames == null || frames.Count == 0)
				return 0;

			int uid = 100 * (textureId + 1) + type;

			if (_bufferedTextureOffset.ContainsKey(uid))
				return _bufferedTextureOffset[uid];

			for (int i = 0; i < frames.Count - 1; i++) {
				if (animationFrame >= frames[i].Frame && frames[i + 1].Frame < animationFrame)
					continue;

				if (frames[i].Frame == animationFrame) {
					_bufferedTextureOffset[uid] = frames[i].Offset;
					return frames[i].Offset;
				}

				if (frames[i + 1].Frame == animationFrame) {
					_bufferedTextureOffset[uid] = frames[i + 1].Offset;
					return frames[i + 1].Offset;
				}

				int dist = frames[i + 1].Frame - frames[i].Frame;
				animationFrame = animationFrame - frames[i].Frame;
				float mult = (animationFrame / (float)dist);

				var curFrame = frames[i];
				var nexFrame = frames[i + 1];

				float res = mult * (nexFrame.Offset - curFrame.Offset) + curFrame.Offset;
				_bufferedTextureOffset[uid] = res;
				return res;
			}

			if (animationFrame >= frames[frames.Count - 1].Frame)
				return frames[frames.Count - 1].Offset;

			return frames[0].Offset;
		}

		#endregion

		public override string ToString() {
			return "Name = " + Name;
		}

		public void Write(BinaryWriter writer) {
			if (Model.Version >= 2.2) {
				writer.Write(Name.Length);
				writer.WriteANSI(Name, Name.Length);
				writer.Write(ParentName.Length);
				writer.WriteANSI(ParentName, ParentName.Length);
			}
			else {
				writer.WriteANSI(Name, 40);
				writer.WriteANSI(ParentName, 40);
			}

			HashSet<string> textures = new HashSet<string>();

			foreach (var texture in Model.Textures) {
				textures.Add(texture);
			}

			if (Model.Version >= 2.3) {
				writer.Write(_textureIndexes.Count);

				for (int i = 0; i < _textureIndexes.Count; i++) {
					int index = i;

					writer.Write(Textures[index].Length);
					writer.WriteANSI(Textures[index], Textures[index].Length);
				}
			}
			else {
				writer.Write(_textureIndexes.Count);

				foreach (int index in _textureIndexes) {
					writer.Write(index);
				}
			}

			for (int i = 0; i < 9; i++) {
				writer.Write(_transformationMatrix[i]);
			}

			Position_.Write(writer);

			if (Model.Version >= 2.2) {
				// Skip
			}
			else {
				Position.Write(writer);
				writer.Write(RotAngle);
				RotAxis.Write(writer);
				Scale.Write(writer);
			}

			writer.Write(_vertices.Count);

			for (int i = 0; i < _vertices.Count; i++) {
				_vertices[i].Write(writer);
			}

			writer.Write(_tvertices.Count);

			for (int i = 0; i < _tvertices.Count; i++) {
				if (Model.Header.MajorVersion > 1 || (Model.Header.MajorVersion == 1 && Model.Header.MinorVersion >= 2)) {
					_tvertices[i].Write(writer, true);
				}
				else {
					_tvertices[i].Write(writer, false);
				}
			}

			writer.Write(_faces.Count);

			for (int i = 0; i < _faces.Count; i++) {
				_faces[i].Write(Model, writer);
			}

			if (Model.Header.MajorVersion > 1 || (Model.Header.MajorVersion == 1 && Model.Header.MinorVersion >= 6)) {
				writer.Write(_scaleKeyFrames.Count);

				for (int i = 0; i < _scaleKeyFrames.Count; i++) {
					_scaleKeyFrames[i].Write(writer);
				}
			}

			writer.Write(_rotFrames.Count);

			for (int i = 0; i < _rotFrames.Count; i++) {
				_rotFrames[i].Write(writer);
			}

			if (Model.Version >= 2.3) {
				writer.Write(_posKeyFrames.Count);
			
				for (int i = 0; i < _posKeyFrames.Count; i++) {
					_posKeyFrames[i].Write(writer);
				}

				writer.Write(_textureKeyFrameGroup.Count);

				foreach (var entry in _textureKeyFrameGroup.Offsets) {
					int textureId = entry.Key;

					writer.Write(textureId);
					writer.Write(entry.Value.Count);

					foreach (var textAnim in entry.Value) {
						writer.Write(textAnim.Key);
						writer.Write(textAnim.Value.Count);

						foreach (var frame in textAnim.Value) {
							frame.Write(writer);
						}
					}
				}
			}
			
		}

		internal void Save(BinaryWriter writer) {
			writer.WriteANSI("MESH", 4);
			writer.Write(Model.Header.MajorVersion);
			writer.Write(Model.Header.MinorVersion);
			writer.WriteANSI(Name, 40);
			writer.WriteANSI(ParentName, 40);

			writer.Write(_textureIndexes.Count);

			foreach (int index in _textureIndexes) {
				writer.Write(index);
			}

			for (int i = 0; i < 9; i++) {
				writer.Write(_transformationMatrix[i]);
			}

			Position_.Write(writer);
			Position.Write(writer);
			writer.Write(RotAngle);
			RotAxis.Write(writer);
			Scale.Write(writer);

			writer.Write(_vertices.Count);

			for (int i = 0; i < _vertices.Count; i++) {
				_vertices[i].Write(writer);
			}

			writer.Write(_tvertices.Count);

			for (int i = 0; i < _tvertices.Count; i++) {
				if (Model.Header.MajorVersion > 1 || (Model.Header.MajorVersion == 1 && Model.Header.MinorVersion >= 2)) {
					_tvertices[i].Write(writer, true);
				}
				else {
					_tvertices[i].Write(writer, false);
				}
			}

			writer.Write(_faces.Count);

			for (int i = 0; i < _faces.Count; i++) {
				_faces[i].Write(Model, writer);
			}

			if (Model.Header.MajorVersion > 1 || (Model.Header.MajorVersion == 1 && Model.Header.MinorVersion >= 6)) {
				writer.Write(_scaleKeyFrames.Count);

				for (int i = 0; i < _scaleKeyFrames.Count; i++) {
					_scaleKeyFrames[i].Write(writer);
				}
			}

			writer.Write(_rotFrames.Count);

			for (int i = 0; i < _rotFrames.Count; i++) {
				_rotFrames[i].Write(writer);
			}
		}
	}
}