using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using ErrorManager;
using GRF.Core;
using GRF.Graphics;
using GRF.Image;
using GRF.Image.Decoders;
using Rsm2.RsmFormat;
using TokeiLibrary;
using Utilities;
using Point = System.Windows.Point;

namespace Rsm2 {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {
		private ModelVisual3D _cubeModel;
		private float _distance = 0;
		private double _angleXRad = 0;
		private double _angleYDegree = 0;
		private Vector3D _lookAt = new Vector3D(0, 0, 0);

		private Point _oldPosition;
		private Vector3D _cameraPosition = new Vector3D(7, 5, 7);
		private int _animationFrame = 0;
		private Model3DGroup _mainModelGroup = new Model3DGroup();
		private bool _terminateThread;

		private readonly string[] _baseLoadData = { "200, 3.10855, 21, -6.18, 0, 6.15", "0", "++", @"data\goldberg_s_01.rsm2" };
		private string _animationStartState = "++";

		public MainWindow() {
			InitializeComponent();
			//_grfData = new GrfHolder(@"C:\Gravity\kRO Clean\data_l.grf");
			_grfData = new GrfHolder(@"data\data.grf");
			Test();
		}

		/// <summary>
		/// Draws the grid.
		/// </summary>
		public void DrawGrid() {
			Model3DGroup mainModel3dGroup = new Model3DGroup();
			MeshGeometry3D mesh = new MeshGeometry3D();

			Vector3D up = new Vector3D(0, 1, 0);
			AddSegment(mesh, new Point3D(-100, 0, 0), new Point3D(100, 0, 0), up);
			AddSegment(mesh, new Point3D(0, 0, -100), new Point3D(0, 0, 100), up);

			Vector3D right = new Vector3D(1, 0, 0);
			AddSegment(mesh, new Point3D(0, -100, 0), new Point3D(0, 100, 0), right);

			SolidColorBrush brush = Brushes.Red;
			DiffuseMaterial material = new DiffuseMaterial(brush);
			GeometryModel3D model = new GeometryModel3D(mesh, material);
			mainModel3dGroup.Children.Add(model);

			_modelGrid.Content = mainModel3dGroup;
		}

		public void Test() {
			_primaryGrid.MouseMove += new MouseEventHandler(_primaryGrid_MouseMove);
			_primaryGrid.MouseUp += new MouseButtonEventHandler(_primaryGrid_MouseUp);
			_primaryGrid.KeyDown += new KeyEventHandler(_primaryGrid_KeyDown);
			_primaryGrid.MouseWheel += new MouseWheelEventHandler(_primaryGrid_MouseWheel);

			_modelLight2.Content = new AmbientLight(Color.FromArgb(127, 127, 127, 127));
			_viewport3D1.Children.Add(new ModelVisual3D { Content = _mainModelGroup});

			if (_baseLoadData != null) {
				var data = _baseLoadData[0].Split(' ').Select(p => p.Trim(' ', ',')).ToList();

				_distance = Single.Parse(data[0]);
				_angleXRad = FormatConverters.DoubleConverter(data[1]);
				_angleYDegree = FormatConverters.DoubleConverter(data[2]);
				_lookAt = new Vector3D(FormatConverters.DoubleConverter(data[3]), FormatConverters.DoubleConverter(data[4]), FormatConverters.DoubleConverter(data[5]));

				_animationFrame = Int32.Parse(_baseLoadData[1]);
				_animationStartState = _baseLoadData[2];
			}

			UpdateCamera();
			new Thread(_animation).Start();
		}

		/// <summary>
		/// Thread to animate the model.
		/// </summary>
		private void _animation() {
			Rsm rsm = new Rsm(_baseLoadData[3]);

			this.Dispatch(delegate {
				_slider.Maximum = rsm.AnimationLength;
			});

			foreach (var mesh in rsm.Meshes) {
				Console.WriteLine(mesh.Name);
			}

			while (!_terminateThread) {
				_mainModelGroup.Dispatch(delegate {
					_mainModelGroup.Children.Clear();
					LoadModel(rsm, _mainModelGroup);
				});

				if (_animationStartState == "++") {
					_animationFrame++;
				}

				if (_animationFrame >= rsm.AnimationLength)
					_animationFrame = 0;

				int delay = (int)(1000 / rsm.FrameRatePerSecond);

				if (delay < 5) {
					delay = 33;
				}

				Thread.Sleep(delay);
			}
		}

		protected override void OnClosing(System.ComponentModel.CancelEventArgs e) {
			_terminateThread = true;
			base.OnClosing(e);
		}

		public class MeshRawData2 {
			public string Texture;
			public byte Alpha;
			public List<int> TriangleIndices = new List<int>();
			public List<Point> TextureCoordinates = new List<Point>();
			public List<Point3D> Positions = new List<Point3D>();
			public List<Vector3D> Normals = new List<Vector3D>();
			public Vertex Position = new Vertex(0, 0, 0);
			public BoundingBox BoundingBox = new BoundingBox();
			public Material Material;
			public Mesh Mesh;
			public bool MaterialNoTile;
		}

		private void LoadModel(Rsm rsm, Model3DGroup mainModel3DGroup) {
			rsm.MainMesh.Calc(_animationFrame);
			rsm.ClearBuffers();

			List<MeshRawData2> meshData = new List<MeshRawData2>();
			List<Vertex> position = new List<Vertex>();

			// Convert all data to WPF 3D, calculate normals, texture effects, etc
			foreach (var mesh in rsm.Meshes) {
				if (_baseLoadData.Length > 4) {
					if (!_baseLoadData[4].Contains(mesh.Name)) {
						continue;
					}
				}

				var vertices = _compile(mesh);

				Dictionary<string, MeshRawData2> allRawData = new Dictionary<string, MeshRawData2>();
				List<Vector3D> normals = new List<Vector3D>(vertices.Count);
				List<Vector3D> vertices3d = new List<Vector3D>(vertices.Count);
				List<Point3D> verticesPoints3D = new List<Point3D>(vertices.Count);
				List<Point> textures3D = new List<Point>(mesh.TextureVertices.Count);
					
				for (int i = 0; i < vertices.Count; i++) {
					normals.Add(new Vector3D(0, 0, 0));
					vertices3d.Add(new Vector3D(vertices[i].X, vertices[i].Y, vertices[i].Z));
					verticesPoints3D.Add(new Point3D(vertices[i].X, vertices[i].Y, vertices[i].Z));
				}
					
				for (int i = 0; i < mesh.TextureVertices.Count; i++) {
					textures3D.Add(new Point(mesh.TextureVertices[i].U, mesh.TextureVertices[i].V));
				}
				
				// Could use the smoothing groups, but... this is much simpler and good enough.
				for (int i = 0; i < mesh.Faces.Count; i++) {
					Vector3D p = Vector3D.CrossProduct(vertices3d[mesh.Faces[i].VertexIds[1]] - vertices3d[mesh.Faces[i].VertexIds[0]], vertices3d[mesh.Faces[i].VertexIds[2]] - vertices3d[mesh.Faces[i].VertexIds[0]]);
					normals[mesh.Faces[i].VertexIds[0]] += p;
					normals[mesh.Faces[i].VertexIds[1]] += p;
					normals[mesh.Faces[i].VertexIds[2]] += p;
				}
					
				for (int i = 0; i < normals.Count; i++) {
					normals[i].Normalize();
				}
					
				for (int i = 0; i < mesh.Faces.Count; i++) {
					var face = mesh.Faces[i];
					string texture;
					
					if (mesh.Textures.Count > 0) {
						texture = mesh.Textures[mesh.TextureIndexes[face.TextureId]];
					}
					else {
						texture = rsm.Textures[mesh.TextureIndexes[face.TextureId]];
					}
					
					if (!allRawData.ContainsKey(texture)) {
						allRawData[texture] = new MeshRawData2 { Texture = texture, Alpha = 0, Position = mesh.BoundingBox.Center, Mesh = mesh, BoundingBox = mesh.BoundingBox };
					}
					
					var rawData = allRawData[texture];
					rawData.Positions.Add(verticesPoints3D[face.VertexIds[0]]);
					rawData.Positions.Add(verticesPoints3D[face.VertexIds[1]]);
					rawData.Positions.Add(verticesPoints3D[face.VertexIds[2]]);
					
					rawData.Normals.Add(normals[face.VertexIds[0]]);
					rawData.Normals.Add(normals[face.VertexIds[1]]);
					rawData.Normals.Add(normals[face.VertexIds[2]]);
					
					Point v0 = textures3D[face.TextureVertexIds[0]];
					Point v1 = textures3D[face.TextureVertexIds[1]];
					Point v2 = textures3D[face.TextureVertexIds[2]];
					
					if (mesh.TextureKeyFrameGroup.Count > 0) {
						foreach (var type in mesh.TextureKeyFrameGroup.Types) {
							if (mesh.TextureKeyFrameGroup.HasTextureAnimation(face.TextureId, type)) {
								float offset = mesh.GetTexture(_animationFrame, face.TextureId, type);
								Matrix4 mat = Matrix4.Identity;
					
								switch (type) {
									case 0:
										v0.X += offset;
										v1.X += offset;
										v2.X += offset;
										break;
									case 1:
										v0.Y += offset;
										v1.Y += offset;
										v2.Y += offset;
										break;
									case 2:
										v0.X *= offset;
										v1.X *= offset;
										v2.X *= offset;
										break;
									case 3:
										v0.Y *= offset;
										v1.Y *= offset;
										v2.Y *= offset;
										break;
									case 4:
										mat = Matrix4.Rotate3(mat, new Vertex(0, 0, 1), offset);
					
										Vertex n0 = Matrix4.Multiply2(mat, new Vertex(v0.X, v0.Y, 0));
										Vertex n1 = Matrix4.Multiply2(mat, new Vertex(v1.X, v1.Y, 0));
										Vertex n2 = Matrix4.Multiply2(mat, new Vertex(v2.X, v2.Y, 0));
					
										v0.X = n0.X;
										v0.Y = n0.Y;
					
										v1.X = n1.X;
										v1.Y = n1.Y;
					
										v2.X = n2.X;
										v2.Y = n2.Y;
										rawData.MaterialNoTile = true;
										break;
								}
							}
						}
					}
					
					rawData.TextureCoordinates.Add(v0);
					rawData.TextureCoordinates.Add(v1);
					rawData.TextureCoordinates.Add(v2);
				}
					
				foreach (var meshRawData in allRawData) {
					meshData.Add(meshRawData.Value);
					position.Add(mesh.Position_);
				}
			}

			for (int i = 0; i < meshData.Count; i++) {
				meshData[i].Material = _generateMaterial(meshData[i].Texture, meshData[i].MaterialNoTile);
			}
				
			meshData.Sort(new TextureMeshComparer2(rsm, new Vertex(_cameraPosition.X, _cameraPosition.Y, _cameraPosition.Z)));
				
			foreach (var meshRawData in meshData) {
				MeshGeometry3D mesh = new MeshGeometry3D();
				
				mesh.Positions = new Point3DCollection(meshRawData.Positions);
				mesh.TextureCoordinates = new PointCollection(meshRawData.TextureCoordinates);
				mesh.Normals = new Vector3DCollection(meshRawData.Normals);
				
				var material = meshRawData.Material;
				GeometryModel3D model = new GeometryModel3D(mesh, material);
				model.BackMaterial = material;
				mainModel3DGroup.Children.Add(model);
			}

			DrawGrid();

			Matrix3D mat3d = Matrix3D.Identity;
			mat3d.Scale(new Vector3D(-1, 1, 1));
			MatrixTransform3D mt = new MatrixTransform3D(mat3d);
			mainModel3DGroup.Transform = mt;
			_modelGrid.Content.Transform = mt;
		}

		private readonly Dictionary<string, Material> _bufferedTextures = new Dictionary<string, Material>();
		private readonly Dictionary<string, Material> _bufferedTexturesNoTiles = new Dictionary<string, Material>();
		private GrfHolder _grfData;
		private bool _disableEvents;

		private Material _generateMaterial(string texture, bool meterialNoTile) {
			if (meterialNoTile) {
				if (_bufferedTexturesNoTiles.ContainsKey(texture)) {
					return _bufferedTexturesNoTiles[texture];
				}
			}
			else {	
				if (_bufferedTextures.ContainsKey(texture)) {
					return _bufferedTextures[texture];
				}
			}

			var material = new DiffuseMaterial();
			Brush materialBrush;
			FileEntry entry = _grfData.FileTable.TryGet(Rsm.RsmTexturePath + "\\" + texture);

			if (entry != null) {
				ImageBrush imageBrush = new ImageBrush();

				if (meterialNoTile) {
					imageBrush.TileMode = TileMode.None;
				}
				else {
					imageBrush.TileMode = TileMode.Tile;
				}

				materialBrush = imageBrush;

				byte[] fileData = entry.GetDecompressedData();

				try {
					GrfImage image = new GrfImage(fileData);

					if (image.GrfImageType == GrfImageType.Indexed8) {
						image.MakePinkTransparent();
						imageBrush.ImageSource = image.Cast<BitmapSource>();

						bool[] trans = new bool[256];

						for (int i = 0; i < image.Palette.Length; i += 4) {
							if (image.Palette[i + 3] == 0) {
								trans[i / 4] = true;
							}
						}
					}
					else if (image.GrfImageType == GrfImageType.Bgr24) {
						image.Convert(new Bgra32FormatConverter());
						image.MakePinkTransparent();
						imageBrush.ImageSource = image.Cast<BitmapSource>();
					}
					else {
						imageBrush.ImageSource = image.Cast<BitmapSource>();
					}
				}
				catch (Exception err) {
					ErrorHandler.HandleException(err);
				}

				imageBrush.ViewportUnits = BrushMappingMode.Absolute;
			}
			else {
				materialBrush = new SolidColorBrush(Color.FromArgb(255, 174, 0, 0));
			}

			material.Brush = materialBrush;

			if (meterialNoTile) {
				_bufferedTexturesNoTiles[texture] = material;
			}
			else {
				_bufferedTextures[texture] = material;
			}

			return material;
		}

		private void _applyMatrix(List<Vertex> vert, Matrix4 mat, Mesh mesh) {
			if (mesh != null) {
				mesh.BoundingBox = new BoundingBox();
			}

			for (int i = 0; i < vert.Count; i++) {
				vert[i] = Matrix4.Multiply2(mat, vert[i]);

				if (mesh != null) {
					for (int j = 0; j < 3; j++) {
						mesh.BoundingBox.Min[j] = Math.Min(vert[i][j], mesh.BoundingBox.Min[j]);
						mesh.BoundingBox.Max[j] = Math.Max(vert[i][j], mesh.BoundingBox.Max[j]);
					}
				}
			}

			if (mesh != null) {
				for (int i = 0; i < 3; i++) {
					mesh.BoundingBox.Offset[i] = (mesh.BoundingBox.Max[i] + mesh.BoundingBox.Min[i]) / 2.0f;
					mesh.BoundingBox.Range[i] = (mesh.BoundingBox.Max[i] - mesh.BoundingBox.Min[i]) / 2.0f;
					mesh.BoundingBox.Center[i] = mesh.BoundingBox.Min[i] + mesh.BoundingBox.Range[i];
				}
			}
		}

		private List<Vertex> _compile(Mesh mesh) {
			List<Vertex> vertices = mesh.Vertices.ToList();
			_applyMatrix(vertices, mesh.MeshMatrixSelf, mesh);
			return vertices;
		}

		private void _primaryGrid_MouseWheel(object sender, MouseWheelEventArgs e) {
			float delta = e.Delta / 20f;
			_distance -= delta;
			UpdateCamera();
		}

		private void _primaryGrid_MouseUp(object sender, MouseButtonEventArgs e) {
			_primaryGrid.ReleaseMouseCapture();
		}

		private void _primaryGrid_KeyDown(object sender, KeyEventArgs e) {
			if (e.Key == Key.Right) {
				_angleXRad += 0.04;
			}
			if (e.Key == Key.Left) {
				_angleXRad -= 0.04;
			}
			if (e.Key == Key.Up) {
				_angleYDegree += 0.04;
			}
			if (e.Key == Key.Down) {
				_angleYDegree -= 0.04;
			}

			UpdateCamera();
		}

		private void _primaryGrid_MouseMove(object sender, MouseEventArgs e) {
			Point newMousePosition = e.GetPosition(_viewport3D1);

			if (e.LeftButton == MouseButtonState.Pressed) {
				if (!_primaryGrid.IsMouseCaptured)
					_primaryGrid.CaptureMouse();

				double deltaX = ModelViewerHelper.ToRad(newMousePosition.X - _oldPosition.X);
				_angleXRad -= deltaX;

				double deltaY = newMousePosition.Y - _oldPosition.Y;
				_angleYDegree += deltaY;

				UpdateCamera();
			}
			else if (e.RightButton == MouseButtonState.Pressed) {
				if (!_primaryGrid.IsMouseCaptured)
					_primaryGrid.CaptureMouse();

				double deltaX = newMousePosition.X - _oldPosition.X;
				double deltaZ = newMousePosition.Y - _oldPosition.Y;

				double distX = _distance * 0.0013 * deltaX;
				double distZ = _distance * 0.0013 * deltaZ;

				_lookAt.X += -distX * Math.Cos(_angleXRad) - distZ * Math.Sin(_angleXRad);
				_lookAt.Z += distX * Math.Sin(_angleXRad) - distZ * Math.Cos(_angleXRad);

				UpdateCamera();
			}

			_oldPosition = newMousePosition;
		}

		public void UpdateCamera() {
			Dispatcher.Invoke(new Action(delegate {
				_angleYDegree = _angleYDegree > 89 ? 89 : _angleYDegree;
				_angleYDegree = _angleYDegree < -89 ? -89 : _angleYDegree;

				double subDistance = _distance * Math.Cos(ModelViewerHelper.ToRad(_angleYDegree));
				_cameraPosition.Y = _distance * Math.Sin(ModelViewerHelper.ToRad(_angleYDegree));

				_cameraPosition.X = subDistance * Math.Sin(_angleXRad);
				_cameraPosition.Z = subDistance * Math.Cos(_angleXRad);
				_cameraPosition += _lookAt;

				_primaryCamera.Position = new Point3D(_cameraPosition.X, _cameraPosition.Y, _cameraPosition.Z);
				_primaryCamera.LookDirection = _lookAt - _cameraPosition;

				if (_modelLight.Content is DirectionalLight) {
					((DirectionalLight)_modelLight.Content).Direction = _lookAt - _cameraPosition;
				}
			}));
		}

		/// <summary>
		/// Adds triangle.
		/// </summary>
		/// <param name="mesh">The mesh.</param>
		/// <param name="point1">The point1.</param>
		/// <param name="point2">The point2.</param>
		/// <param name="point3">The point3.</param>
		private void AddTriangle(MeshGeometry3D mesh, Point3D point1, Point3D point2, Point3D point3) {
			int index1 = mesh.Positions.Count;
			mesh.Positions.Add(point1);
			mesh.Positions.Add(point2);
			mesh.Positions.Add(point3);

			mesh.TriangleIndices.Add(index1++);
			mesh.TriangleIndices.Add(index1++);
			mesh.TriangleIndices.Add(index1);
		}

		/// <summary>
		/// Adds segment.
		/// </summary>
		/// <param name="mesh">The mesh.</param>
		/// <param name="point1">The point1.</param>
		/// <param name="point2">The point2.</param>
		/// <param name="up">Up.</param>
		/// <param name="thickness">The thickness.</param>
		private void AddSegment(MeshGeometry3D mesh, Point3D point1, Point3D point2, Vector3D up, double thickness = 0.01) {
			Vector3D v = point2 - point1;
			Vector3D n1 = ScaleVector(up, thickness / 2.0);
			Vector3D n2 = Vector3D.CrossProduct(v, n1);
			n2 = ScaleVector(n2, thickness / 2.0);

			Point3D p1pp = point1 + n1 + n2;
			Point3D p1mp = point1 - n1 + n2;
			Point3D p1pm = point1 + n1 - n2;
			Point3D p1mm = point1 - n1 - n2;
			Point3D p2pp = point2 + n1 + n2;
			Point3D p2mp = point2 - n1 + n2;
			Point3D p2pm = point2 + n1 - n2;
			Point3D p2mm = point2 - n1 - n2;

			AddTriangle(mesh, p1pp, p1mp, p2mp);
			AddTriangle(mesh, p1pp, p2mp, p2pp);

			AddTriangle(mesh, p1pp, p2pp, p2pm);
			AddTriangle(mesh, p1pp, p2pm, p1pm);

			AddTriangle(mesh, p1pm, p2pm, p2mm);
			AddTriangle(mesh, p1pm, p2mm, p1mm);

			AddTriangle(mesh, p1mm, p2mm, p2mp);
			AddTriangle(mesh, p1mm, p2mp, p1mp);

			AddTriangle(mesh, p1pp, p1pm, p1mm);
			AddTriangle(mesh, p1pp, p1mm, p1mp);

			AddTriangle(mesh, p2pp, p2mp, p2mm);
			AddTriangle(mesh, p2pp, p2mm, p2pm);
		}

		/// <summary>
		/// Scales the vector.
		/// </summary>
		/// <param name="vector">The vector.</param>
		/// <param name="length">The length.</param>
		/// <returns>Vector3D.</returns>
		private Vector3D ScaleVector(Vector3D vector, double length) {
			double scale = length / vector.Length;
			return new Vector3D(
				vector.X * scale,
				vector.Y * scale,
				vector.Z * scale);
		}

		#region Nested type: TextureMeshComparer

		public class TextureMeshComparer2 : IComparer<MeshRawData2> {
			private readonly Rsm _rsm;
			private readonly Vertex _origin;

			#region IComparer<MeshRawData> Members

			public TextureMeshComparer2(Rsm rsm, Vertex origin) {
				_rsm = rsm;
				_origin = origin;
			}

			public int Compare(MeshRawData2 x, MeshRawData2 y) {
				if (x.Texture.ToLower().EndsWith(".tga") && !y.Texture.ToLower().EndsWith(".tga")) {
					return 1;
				}

				if (y.Texture.ToLower().EndsWith(".tga") && !x.Texture.ToLower().EndsWith(".tga")) {
					return -1;
				}

				var lenghtX = (x.Position - _origin).Length + x.BoundingBox.Range.Length;
				var lenghtY = (y.Position - _origin).Length + y.BoundingBox.Range.Length;

				if (Math.Abs(lenghtX - lenghtY) < 0.00001) {
					if (x.Mesh.Parent != null && x.Mesh.Parent == y.Mesh) {
						return 1;
					}

					if (y.Mesh.Parent != null && y.Mesh.Parent == x.Mesh) {
						return -1;
					}
					
					return 0;
				}

				if (Math.Abs(lenghtX - lenghtY) < 5) {	// Both models are too close to tell, use model's hierarchy
					int i1 = _rsm.Meshes.IndexOf(x.Mesh);
					int i2 = _rsm.Meshes.IndexOf(y.Mesh);

					return i1 - i2 < 0 ? -1 : 1;
				}

				return lenghtX > lenghtY ? -1 : 1;
			}

			#endregion
		}

		#endregion

		private void _slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			if (_disableEvents)
				return;
			_animationStartState = "";
			_animationFrame = (int)_slider.Value;
			_disableEvents = true;
			_slider.Value = _animationFrame;
			_sliderPosition.Content = "Frame: " + _animationFrame;
			_disableEvents = false;
		}
	}
}
