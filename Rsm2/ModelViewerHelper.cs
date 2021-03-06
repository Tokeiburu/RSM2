using System;

namespace Rsm2 {
	public static class ModelViewerHelper {
		public static double ToRad(double angle) {
			return angle * (Math.PI / 180f);
		}

		public static float ToRad(float angle) {
			return (float) (angle * (Math.PI / 180f));
		}
	}
}