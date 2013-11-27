//
// HelloRhinoActivity.cs
// HelloRhino.Droid
//
// Created by dan (dan@mcneel.com) on 9/19/2013
// Copyright 2013 Robert McNeel & Associates.  All rights reserved.
// OpenNURBS, Rhinoceros, and Rhino3D are registered trademarks of Robert
// McNeel & Associates.
//
// THIS SOFTWARE IS PROVIDED "AS IS" WITHOUT EXPRESS OR IMPLIED WARRANTY.
// ALL IMPLIED WARRANTIES OF FITNESS FOR ANY PARTICULAR PURPOSE AND OF
// MERCHANTABILITY ARE HEREBY DISCLAIMED.
//
using Android.App;
using Android.OS;
using Android.Content.PM;
using Android.Widget;
using Android.Views;

using RhinoMobile;
using RhinoMobile.Model;

namespace HelloRhino.Droid
{
	[Activity(Label = "HelloRhino", ConfigurationChanges=ConfigChanges.Orientation | ConfigChanges.KeyboardHidden, MainLauncher = true)]
	public class HelloRhinoActivity : Activity
	{ 
		#region methods
		/// <summary>
		/// OnCreate is called by Android when this activity is created
		/// </summary>
		protected override void OnCreate(Bundle savedInstanceState)
		{
			base.OnCreate(savedInstanceState);

			SetContentView (Resource.Layout.Main);

			FindViewById (Resource.Id.hellorhinoview);

			var intLayout = FindViewById<RelativeLayout> (Resource.Id.relLayout1);
			if (App.Manager.CurrentModel.IsReadyForRendering) {
				RunOnUiThread (() => intLayout.Visibility = ViewStates.Invisible);
			} else {
				RunOnUiThread (() => intLayout.Visibility = ViewStates.Visible);
			}

			var warningImage = FindViewById<ImageView> (Resource.Id.imageView1);
			RunOnUiThread (() => warningImage.Visibility = ViewStates.Invisible);

			var cancelButton = FindViewById<Button> (Resource.Id.cancelButton);
			cancelButton.Click += (sender, e) => App.Manager.CurrentModel.CancelModelPreparation ();

			// Tell the model to prepare itself for display...
			App.Manager.CurrentModel.MeshPrep += new MeshPreparationHandler (ObserveMeshPrep);
		}

		/// <summary>
		/// OnPause is called by Android just before this activity is about to be paused
		/// </summary>
		protected override void OnPause ()
		{
			// never forget to do this!
			base.OnPause ();
			var view = FindViewById<HelloRhinoView> (Resource.Id.hellorhinoview);
			view.Pause ();
		}

		/// <summary>
		/// OnResume is called by Android when thiu activity is brought to the "foreground"
		/// </summary>
		protected override void OnResume ()
		{
			// never forget to do this!
			base.OnResume ();
			var view = FindViewById<HelloRhinoView> (Resource.Id.hellorhinoview);
			view.Resume ();
		}

		protected override void OnStart ()
		{
			base.OnStart ();
		}
		#endregion

		#region Model Initialization Events
		private void ObserveMeshPrep (RMModel model, MeshPreparationProgress progress)
		{
			// Success
			if (progress.PreparationDidSucceed) {
				var initProgress = FindViewById<RelativeLayout> (Resource.Id.relLayout1);
				RunOnUiThread (() => initProgress.Visibility = ViewStates.Gone);
			}

			// Still working
			if (!progress.PreparationDidSucceed && progress.FailException == null) {
				// can't really update the progress bar on Android :(
			}

			// Failure or Cancellation
			if (progress.FailException != null) {
				var progressBar = FindViewById<ProgressBar> (Resource.Id.ProgressBar01);
				RunOnUiThread (() => progressBar.Visibility = ViewStates.Invisible);

				var warningImage = FindViewById<ImageView> (Resource.Id.imageView1);
				RunOnUiThread (() => warningImage.Visibility = ViewStates.Visible);

				var initLabel = FindViewById<TextView> (Resource.Id.txtText);
				RunOnUiThread (() => initLabel.Text = progress.FailException.Message);
			}
		}
		#endregion
	}
}