//
// HelloRhinoViewController.cs
// HelloRhino.Touch
//
// Created by dan (dan@mcneel.com) on 9/17/2013
// Copyright 2013 Robert McNeel & Associates.  All rights reserved.
// OpenNURBS, Rhinoceros, and Rhino3D are registered trademarks of Robert
// McNeel & Associates.
//
// THIS SOFTWARE IS PROVIDED "AS IS" WITHOUT EXPRESS OR IMPLIED WARRANTY.
// ALL IMPLIED WARRANTIES OF FITNESS FOR ANY PARTICULAR PURPOSE AND OF
// MERCHANTABILITY ARE HEREBY DISCLAIMED.
//
using System;

using MonoTouch.Foundation;
using MonoTouch.UIKit;

using RhinoMobile;
using RhinoMobile.Model;

namespace HelloRhino.Touch
{
	[Register ("HelloRhinoViewController")]
	public partial class HelloRhinoViewController : UIViewController, IDisposable
	{
		#region properties
		/// <value> The backing HelloRhinoView associated with this Controller. </value>
		new HelloRhinoView View 
		{ 
			get 
			{ 
				return (HelloRhinoView)base.View; 
			} 
		}

		/// <value> True is an iPhone or iPodTouch, False if it's an iPad. </value>
		static bool UserInterfaceIdiomIsPhone {
			get { return UIDevice.CurrentDevice.UserInterfaceIdiom == UIUserInterfaceIdiom.Phone; }
		}
		#endregion

		#region GestureRecognizerDelegate
		class GestureDelegate : UIGestureRecognizerDelegate
		{
			/// <summary>
			/// GestureDelegate is stubbed for custom delegate handling
			/// </summary>
			public GestureDelegate (HelloRhinoViewController controller)
			{
				//stubbed for delegate handling...for example:
				//m_controller = controller;
			}

			/// <summary>
			/// ShouldReceiveTouch is required for the UIGestureRecognizer protocol
			/// </summary>
			public override bool ShouldReceiveTouch(UIGestureRecognizer aRecogniser, UITouch aTouch)
			{
				return true;
			}

			/// <summary>
			/// Ensure that the pinch, pan and rotate gestures are all recognized simultaneously
			/// </summary>
			public override bool ShouldRecognizeSimultaneously (UIGestureRecognizer gestureRecognizer, UIGestureRecognizer otherGestureRecognizer)
			{
				// Orbit and Zoom 
				if ( (gestureRecognizer is UIPinchGestureRecognizer) && (otherGestureRecognizer is OrbitDollyGestureRecognizer) )
					return true;

				// Orbit and Double Tap
				if ((gestureRecognizer is OrbitDollyGestureRecognizer) && (otherGestureRecognizer is UITapGestureRecognizer))
					return true;

				return false;
			}
		}
		#endregion

		#region constructors and disposal
		public HelloRhinoViewController () : base (UserInterfaceIdiomIsPhone ? "HelloRhinoViewController_iPhone" : "HelloRhinoViewController_iPad", null)
		{

		}

		/// <summary>
		/// Passively reclaims unmanaged resources when the class user did not explicitly call Dispose().
		/// </summary>
		~ HelloRhinoViewController () { Dispose (false); }

		/// <summary>
		/// Actively reclaims unmanaged resources that this instance uses.
		/// </summary>
		public new void Dispose()
		{
			try {
				Dispose(true);
				GC.SuppressFinalize(this);
			}
			finally {
				base.Dispose (true);
				NSNotificationCenter.DefaultCenter.RemoveObserver (this);
			}
		}

		/// <summary>
		/// <para>This method is called with argument true when class user calls Dispose(), while with argument false when
		/// the Garbage Collector invokes the finalizer, or Finalize() method.</para>
		/// <para>You must reclaim all used unmanaged resources in both cases, and can use this chance to call Dispose on disposable fields if the argument is true.</para>
		/// </summary>
		/// <param name="disposing">true if the call comes from the Dispose() method; false if it comes from the Garbage Collector finalizer.</param>
		private new void Dispose (bool disposing)
		{
			// Free unmanaged resources...

			// Free managed resources...but only if called from Dispose
			// (If called from Finalize then the objects might not exist anymore)
			if (disposing) {

				ReleaseDesignerOutlets ();

			}	
		}
		#endregion

		#region methods
		/// <summary>
		/// ViewDidLoad is called by CocoaTouch when the view had loaded.
		/// </summary>
		public override void ViewDidLoad ()
		{
			base.ViewDidLoad ();

			NSNotificationCenter.DefaultCenter.AddObserver (UIApplication.WillResignActiveNotification, a => {
				if (IsViewLoaded && View.Window != null)
					View.StopAnimating ();
			}, this);
			NSNotificationCenter.DefaultCenter.AddObserver (UIApplication.DidBecomeActiveNotification, a => {
				if (IsViewLoaded && View.Window != null)
					View.StartAnimating ();
			}, this);
			NSNotificationCenter.DefaultCenter.AddObserver (UIApplication.WillTerminateNotification, a => {
				if (IsViewLoaded && View.Window != null)
					View.StopAnimating ();
			}, this);

			// setup the GestureRecognizer delegates (this is for handling simultaneous recognizers in shouldRecognize...)
			View.ZoomRecognizer.Delegate = new GestureDelegate (this);
			View.OrbitDollyRecognizer.Delegate = new GestureDelegate (this);

			// Show the initialization view...
			InitPrepView.Hidden = true;
			WarningSymbol.Hidden = true;
			ProgressBar.Progress = 0.0f;
			// subscribe to mesh prep events in the model...
			App.Manager.CurrentModel.MeshPrep += new MeshPreparationHandler (ObserveMeshPrep);
		}

		/// <summary>
		/// ViewWillAppear is called by CocoaTouch just before the view will appear.
		/// </summary>
		public override void ViewWillAppear (bool animated)
		{
			base.ViewWillAppear (animated);
			View.StartAnimating ();
		}

		/// <summary>
		/// ViewDidAppear is called by CocoaTouch just after the view did appear.
		/// </summary>
		public override void ViewDidAppear (bool animated)
		{
			base.ViewDidAppear (animated);

			if (App.Manager.CurrentModel != null) {
				View.ViewDidAppear ();
			}

			InitPrepView.Hidden = false;

			// Tell the model to prepare itself for display...
			App.Manager.CurrentModel.Prepare ();
		}

		/// <summary>
		/// ViewWillDisappear is called by CocoaTouch just before the view will disappear.
		/// </summary>
		public override void ViewWillDisappear (bool animated)
		{
			base.ViewWillDisappear (animated);
			View.StopAnimating ();
			App.Manager.CurrentModel.MeshPrep -= ObserveMeshPrep;
		}

		/// <summary>
		/// CaptureImage saves and image to the photo album.
		/// </summary>
		void CaptureImage()
		{
			UIImage image;

			UIGraphics.BeginImageContext (View.Frame.Size);

			//Use this to determine the OS version...
			//UIDevice.CurrentDevice.SystemName

			//pre-iOS 7 using layer to snapshot render an empty image when used with OpenGL
			//View.Layer.RenderInContext (UIGraphics.GetCurrentContext ());

			//new iOS 7 method to snapshot works with OpenGL
			View.DrawViewHierarchy (View.Frame, true);

			image = UIGraphics.GetImageFromCurrentImageContext ();
			UIGraphics.EndImageContext ();

			image.SaveToPhotosAlbum((img, err) => {
				if(err != null)
					Console.WriteLine("error saving image: {0}", err);
				else
					Console.WriteLine ("image saved to photo album");
			});
		}
		#endregion

		#region Device Orientation Changes
		/// <summary>
		/// This view should autorotate to all interface orientations.
		/// </summary>
		public override bool ShouldAutorotate ()
		{
			return true;
		}
		#endregion 

		#region Model Initialization Events
		private void ObserveMeshPrep (RMModel model, MeshPreparationProgress progress)
		{
			// Success
			if (progress.PreparationDidSucceed) {
				InitPrepView.InvokeOnMainThread (delegate {
					InitPrepView.Hidden = true;
					App.Manager.CurrentModel.MeshPrep -= new MeshPreparationHandler (ObserveMeshPrep);
				});
			}

			// Still working
			if (!progress.PreparationDidSucceed && progress.FailException == null) {
				ProgressBar.BeginInvokeOnMainThread (delegate {
					ProgressBar.SetProgress ((float)progress.MeshProgress, true);
				});
			}

			// Failure or Cancellation
			if (progress.FailException != null) {
				ProgressBar.BeginInvokeOnMainThread (delegate {
					ProgressBar.Hidden = true;
				});

				WarningSymbol.BeginInvokeOnMainThread (delegate {
					WarningSymbol.Hidden = false;
				});

				InitializingLabel.BeginInvokeOnMainThread (delegate {
					InitializingLabel.Text = progress.FailException.Message;
				});
			}
		}

		partial void CancelPrep (MonoTouch.Foundation.NSObject sender)
		{
			App.Manager.CurrentModel.CancelModelPreparation();
		}
		#endregion

	}
}