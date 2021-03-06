using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Windows;
using ErrorManager;
using GRF.Image;
using GrfToWpfBridge.Application;
using TokeiLibrary;

namespace Rsm2 {
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application {
		public App() {
			ErrorHandler.SetErrorHandler(new DefaultErrorHandler());
		}

		protected override void OnStartup(StartupEventArgs e) {
			ApplicationManager.CrashReportEnabled = true;
			ImageConverterManager.AddConverter(new DefaultImageConverter());

			Configuration.SetImageRendering(Resources);

			base.OnStartup(e);
		}
	}
}
